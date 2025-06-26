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

        public MessageProcessor(List<Node> nodes, MeshQTT.Entities.Config? config, AlertManager? alertManager = null)
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
                MetricsManager.MessagesReceived.Inc();
                
                // Trigger message rate alert
                if (alertManager != null)
                {
                    await alertManager.TriggerMessageRateAlert();
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
                ServiceEnvelope? envelope = ProcessMeshtasticPayload(payload);
                if (envelope == null)
                {
                    Logger.Log("Received invalid ServiceEnvelope, skipping further processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }
                MeshPacket? data = DecryptEnvelope(envelope);
                string nodeID = context.ApplicationMessage.Topic.Split('/').Last();
                if (config != null && config.Banlist.Contains(nodeID))
                {
                    Logger.Log($"Blocked message from banned node {nodeID}.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }
                switch (data?.Decoded.Portnum)
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
            ServiceEnvelope envelope = ServiceEnvelope.Parser.ParseFrom(payload);
            if (!IsValidEnvelope(envelope))
            {
                Logger.Log("Received invalid ServiceEnvelope.");
                return null;
            }
            return envelope;
        }

        public bool IsValidEnvelope(ServiceEnvelope envelope)
        {
            return !(
                string.IsNullOrEmpty(envelope.GatewayId)
                || string.IsNullOrEmpty(envelope.ChannelId)
                || envelope.Packet == null
                || envelope.Packet.Id < 0
                || envelope.Packet.Decoded != null
            );
        }

        public MeshPacket? DecryptEnvelope(ServiceEnvelope envelope)
        {
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
                        && payload.Payload.Length > 0
                    )
                        return meshPacket;
                }
                catch (Exception)
                {
                    //Logger.Log($"Failed to decrypt with key {key}: {ex.Message}");
                }
            }
            return null;
        }
    }
}
