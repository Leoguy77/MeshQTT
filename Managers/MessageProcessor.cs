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

        public MessageProcessor(List<Node> nodes, MeshQTT.Entities.Config? config)
        {
            this.nodes = nodes;
            this.config = config;
        }

        public async Task InterceptingPublishAsync(InterceptingPublishEventArgs context)
        {
            try
            {
                MetricsManager.MessagesReceived.Inc();
                var payload = context.ApplicationMessage.Payload;
                Dictionary<string, object>? PacketJson;
                try
                {
                    PacketJson = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        context.ApplicationMessage.Payload.ToArray()
                    );
                }
                catch
                {
                    PacketJson = null;
                }
                if (PacketJson is not null)
                {
                    if (
                        PacketJson.TryGetValue("type", out var value)
                        && value as string == "position"
                    )
                    {
                        var payloadObj = value as JsonElement?;
                        if (payloadObj.HasValue)
                        {
                            double Latitude =
                                payloadObj.Value.GetProperty("latitude_i").GetInt32() * 1e-7;
                            double Longitude =
                                payloadObj.Value.GetProperty("longitude_i").GetInt32() * 1e-7;

                            // Update or create node
                            Node? node = nodes.FirstOrDefault(n =>
                                n.NodeID == payloadObj.Value.GetProperty("sender").GetString()
                            );
                            if (node == null)
                            {
                                var senderId = payloadObj.Value.GetProperty("sender").GetString();
                                if (string.IsNullOrEmpty(senderId))
                                {
                                    Logger.Log(
                                        "Sender ID is null or empty in position payload, skipping node creation."
                                    );
                                    MetricsManager.MessagesFiltered.Inc();
                                    context.ProcessPublish = false;
                                    return;
                                }
                                node = new Node(senderId);
                                nodes.Add(node);
                            }
                            else
                            {
                                if (
                                    !(
                                        node.LastUpdate.AddMinutes(
                                            config?.PositionAppTimeoutMinutes ?? 720
                                        ) < DateTime.Now
                                        || node.GetDistanceTo(Latitude, Longitude) > 100
                                    )
                                )
                                // Update only if the last update was more than 12 hours ago or if the position has changed significantly
                                {
                                    Logger.Log(
                                        $"Node {payloadObj.Value.GetProperty("sender").GetString()} position update ignored due to insufficient change or recent update. Time since last update: {DateTime.Now - node.LastUpdate}, Position change: {node.GetDistanceTo(Latitude, Longitude)} m"
                                    );
                                    MetricsManager.MessagesFiltered.Inc();
                                    context.ProcessPublish = false;
                                    return;
                                }
                            }
                        }
                    }
                }
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
                // Process the payload
                ServiceEnvelope? envelope = ProcessMeshtasticPayload(payload);

                if (envelope == null)
                {
                    Logger.Log("Received invalid ServiceEnvelope, skipping further processing.");
                    MetricsManager.MessagesFiltered.Inc();
                    context.ProcessPublish = false;
                    return;
                }
                MeshPacket? data = DecryptEnvelope(envelope);
                // Last part of the topic is the NodeID
                string nodeID = context.ApplicationMessage.Topic.Split('/').Last();
                if (data?.Decoded.Portnum == PortNum.TextMessageApp)
                {
                    Logger.Log(
                        $"Received text message from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {data.Decoded.Payload.ToStringUtf8()}"
                    );
                }
                else if (data?.Decoded.Portnum == PortNum.PositionApp)
                {
                    Position position = Position.Parser.ParseFrom(data.Decoded.Payload);
                    double latitude = position.LatitudeI * 1e-7;
                    double longitude = position.LongitudeI * 1e-7;

                    // Update or create node
                    Node? node = nodes.FirstOrDefault(n => n.NodeID == nodeID);
                    if (node == null)
                    {
                        node = new Node(nodeID);
                        nodes.Add(node);
                    }
                    else
                    {
                        if (
                            !(
                                node.LastUpdate.AddMinutes(config?.PositionAppTimeoutMinutes ?? 720)
                                    < DateTime.Now
                                || node.GetDistanceTo(latitude, longitude) > 100
                            )
                        )
                        // Update only if the last update was more than 12 hours ago or if the position has changed significantly
                        {
                            Logger.Log(
                                $"Node {nodeID} position update ignored due to insufficient change or recent update. Time since last update: {DateTime.Now - node.LastUpdate}, Position change: {node.GetDistanceTo(latitude, longitude)} m"
                            );
                            MetricsManager.MessagesFiltered.Inc();
                            context.ProcessPublish = false;
                            return;
                        }
                        node.LastLatitude = latitude;
                        node.LastLongitude = longitude;
                        node.LastUpdate = DateTime.Now;
                    }

                    Logger.Log(
                        $"Received location data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {latitude}, {longitude} (accuracy: {position.VDOP} m)"
                    );
                }
                else if (data?.Decoded.Portnum == PortNum.NodeinfoApp)
                {
                    NodeInfo nodeInfo = NodeInfo.Parser.ParseFrom(data.Decoded.Payload);
                    Logger.Log(
                        $"Received node info from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {nodeInfo.User} (Channel: {nodeInfo.Channel}, version: {nodeInfo.Num})"
                    );
                }
                else if (data?.Decoded.Portnum == PortNum.TelemetryApp)
                {
                    Telemetry telemetry = Telemetry.Parser.ParseFrom(data.Decoded.Payload);
                    Logger.Log(
                        $"Received telemetry data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: AirUtilTx: {telemetry.DeviceMetrics.AirUtilTx}, ChannelUtilization: {telemetry.DeviceMetrics.ChannelUtilization}"
                    );
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
