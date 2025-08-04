using System.Buffers;
using System.Text.Json;
using MeshQTT.Entities;
using Meshtastic.Protobufs;
using MQTTnet.Server;

namespace MeshQTT.Managers
{
    public class MessageProcessor
    {
        private readonly List<Node> nodes;
        private readonly MeshQTT.Entities.Config? config;
        private readonly NodeManager nodeManager;
        private readonly PayloadHandler payloadHandler;
        private readonly AlertManager? alertManager;

        public MessageProcessor(
            List<Node> nodes,
            MeshQTT.Entities.Config? config,
            AlertManager? alertManager = null
        )
        {
            this.nodes = nodes;
            this.config = config;
            this.alertManager = alertManager;
            this.nodeManager = new NodeManager(nodes, config, alertManager);
            this.payloadHandler = new PayloadHandler(nodeManager, config, alertManager);
        }

        public async Task InterceptingPublishAsync(InterceptingPublishEventArgs context)
        {
            try
            {
                // Ensure context is valid
                if (context?.ApplicationMessage == null)
                {
                    Logger.Log("Received null context or application message - skipping processing.");
                    return;
                }

                MetricsManager.MessagesReceived.Inc();

                // First check topic-level publish permissions
                var user = GetUserFromSession(context.SessionItems, context.ClientId);
                if (user != null && !TopicAccessManager.CanPublish(user, context.ApplicationMessage.Topic, config))
                {
                    Logger.Log($"Publish denied for user {user.UserName} to topic {context.ApplicationMessage.Topic}");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    
                    if (alertManager != null)
                    {
                        await alertManager.TriggerSystemErrorAlert(
                            $"Unauthorized publish attempt by user {user.UserName} to topic {context.ApplicationMessage.Topic}",
                            null
                        );
                    }
                    return;
                }

                var payload = context.ApplicationMessage.Payload;
                if (payload.IsEmpty || payload.Length == 0)
                {
                    Logger.Log(
                        "Received MQTT message with empty payload from "
                            + $"{context.ClientId} on topic {context.ApplicationMessage.Topic} - skipping processing."
                    );
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }
                
                string? topic = context.ApplicationMessage?.Topic;
                if (string.IsNullOrEmpty(topic))
                {
                    Logger.Log("Received MQTT message with empty or null topic - skipping processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }

                string[] topicParts = topic.Split('/');
                if (topicParts.Length == 0)
                {
                    Logger.Log($"Received MQTT message with invalid topic format: {topic} - skipping processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }

                string nodeID = topicParts.Last();
                if (string.IsNullOrEmpty(nodeID))
                {
                    Logger.Log($"Could not extract nodeID from topic: {topic} - skipping processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }

                // Extract gateway IP from the context (if available)
                string? gatewayIp = null;
                try
                {
                    var remoteEndpoint = context.SessionItems["RemoteEndpoint"];
                    if (remoteEndpoint != null)
                    {
                        gatewayIp = ExtractIpFromEndpoint(remoteEndpoint.ToString() ?? "");
                    }
                }
                catch
                {
                    // RemoteEndpoint not available, continue without IP
                }

                // Trigger message rate alert with node ID, gateway client ID, and gateway IP
                if (alertManager != null && !string.IsNullOrEmpty(context.ClientId))
                {
                    await alertManager.TriggerMessageRateAlert(nodeID, context.ClientId, gatewayIp);
                }
                ServiceEnvelope? envelope = ProcessMeshtasticPayload(payload);
                if (envelope == null)
                {
                    Logger.Log("Received invalid ServiceEnvelope, skipping further processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }
                MeshPacket? data = DecryptEnvelope(envelope);

                if (config != null && config.Banlist != null && config.Banlist.Contains(nodeID))
                {
                    Logger.Log($"Blocked message from banned node {nodeID}.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }

                // Only process if we have valid decrypted data
                if (data?.Decoded == null)
                {
                    Logger.Log("Failed to decrypt message or no valid decoded data, skipping further processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }

                switch (data.Decoded.Portnum)
                {
                    case PortNum.TextMessageApp:
                        payloadHandler.HandleTextMessage(nodeID, envelope, data);
                        break;
                    case PortNum.PositionApp:
                        payloadHandler.HandlePosition(nodeID, envelope, data);
                        break;
                    case PortNum.NodeinfoApp:
                        payloadHandler.HandleNodeInfo(nodeID, envelope, data);
                        break;
                    case PortNum.TelemetryApp:
                        payloadHandler.HandleTelemetry(nodeID, envelope, data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to process MQTT message: {ex.Message}");
            }
            context.ProcessPublish = true;
            await Task.CompletedTask;
        }

        public ServiceEnvelope? ProcessMeshtasticPayload(ReadOnlySequence<byte> payload)
        {
            try
            {
                ServiceEnvelope envelope = ServiceEnvelope.Parser.ParseFrom(payload);
                if (!IsValidEnvelope(envelope))
                {
                    Logger.Log("Received invalid ServiceEnvelope.");
                    return null;
                }
                return envelope;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to parse ServiceEnvelope: {ex.Message}");
                return null;
            }
        }

        public bool IsValidEnvelope(ServiceEnvelope envelope)
        {
            return envelope != null && !(
                string.IsNullOrEmpty(envelope.GatewayId)
                || string.IsNullOrEmpty(envelope.ChannelId)
                || envelope.Packet == null
                || envelope.Packet.Id < 0
                || envelope.Packet.Decoded != null
            );
        }

        public MeshPacket? DecryptEnvelope(ServiceEnvelope envelope)
        {
            // Ensure we have a valid envelope and packet with encrypted data
            if (envelope?.Packet?.Encrypted == null || envelope.Packet.Encrypted.IsEmpty)
            {
                Logger.Log("No encrypted data found in packet.");
                return null;
            }

            foreach (var key in config?.EncryptionKeys ?? new List<string>())
            {
                try
                {
                    var nonce = new Meshtastic.Crypto.NonceGenerator(
                        envelope.Packet.From,
                        envelope.Packet.Id
                    ).Create();
                    var keyBytes = System.Convert.FromBase64String(key);
                    var decrypted = Meshtastic.Crypto.PacketEncryption.TransformPacket(
                        envelope.Packet.Encrypted.ToByteArray(),
                        nonce,
                        keyBytes
                    );
                    Data payload = Data.Parser.ParseFrom(decrypted);
                    MeshPacket meshPacket = new()
                    {
                        From = envelope.Packet.From,
                        To = envelope.Packet.To,
                        Id = envelope.Packet.Id,
                        Decoded = payload,
                    };
                    if (
                        payload != null
                        && payload.Portnum > PortNum.UnknownApp
                        && payload.Payload != null
                        && payload.Payload.Length > 0
                    )
                        return meshPacket;
                }
                catch (Exception ex)
                {
                    // Only log if the key is valid to avoid exposing sensitive information
                    if (!string.IsNullOrEmpty(key) && key.Length >= 4)
                    {
                        Logger.Log($"Failed to decrypt with key ending in {key.Substring(Math.Max(0, key.Length - 4))}: {ex.Message}");
                    }
                    else
                    {
                        Logger.Log($"Failed to decrypt with key: {ex.Message}");
                    }
                }
            }
            return null;
        }

        private string ExtractIpFromEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                return "Unknown";

            // Handle IPv6 addresses like [::1]:61642
            if (endpoint.StartsWith("["))
            {
                var closingBracket = endpoint.IndexOf(']');
                if (closingBracket > 0)
                {
                    return endpoint.Substring(1, closingBracket - 1);
                }
            }

            // Handle IPv4 addresses like "192.168.1.1:12345"
            var lastColon = endpoint.LastIndexOf(':');
            return lastColon > 0 ? endpoint.Substring(0, lastColon) : endpoint;
        }

        private MeshQTT.Entities.User? GetUserFromSession(System.Collections.IDictionary sessionItems, string clientId)
        {
            // Try to find user by client ID
            if (sessionItems.Contains(clientId) && sessionItems[clientId] is MeshQTT.Entities.User user)
            {
                return user;
            }

            // If not found by client ID, try to find by any key that contains a User object
            foreach (var item in sessionItems.Values)
            {
                if (item is MeshQTT.Entities.User sessionUser)
                {
                    return sessionUser;
                }
            }

            return null;
        }
    }
}
