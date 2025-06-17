using System.Buffers;
using System.Text.Json;
using MeshQTT.Entities;
using Meshtastic.Crypto;
using Meshtastic.Protobufs;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Prometheus;

namespace MeshQTT
{
    public class Program
    {
        private static Entities.Config? config = new(
            Path.Combine(AppContext.BaseDirectory, "config", "config.json")
        );
        private static readonly List<Node> nodes = [];

        // Prometheus metrics
        private static readonly Counter MessagesReceived = Metrics.CreateCounter(
            "mqtt_messages_received_total",
            "Total number of MQTT messages received."
        );
        private static readonly Counter MessagesFiltered = Metrics.CreateCounter(
            "mqtt_messages_filtered_total",
            "Total number of MQTT messages filtered due to empty payload or invalid data."
        );
        private static readonly Gauge ClientsConnected = Metrics.CreateGauge(
            "mqtt_clients_connected_total",
            "Total number of MQTT clients connected."
        );

        static async Task Main(string[] args)
        {
            StartPrometheusMetricsServer();

            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Any)
                .WithDefaultEndpointPort(config?.Port ?? 1883)
                .Build();

            using var mqttServer = new MqttServerFactory().CreateMqttServer(mqttServerOptions);

            mqttServer.ClientConnectedAsync += async context =>
            {
                ClientsConnected.Inc();
                Log(
                    $"Client connected: {context.ClientId} with user {context.AuthenticationData} from {context.RemoteEndPoint}"
                );
                await Task.CompletedTask;
            };
            mqttServer.ClientDisconnectedAsync += async context =>
            {
                ClientsConnected.Dec();
                Log($"Client disconnected: {context.ClientId} from {context.RemoteEndPoint}");
                await Task.CompletedTask;
            };
            mqttServer.InterceptingPublishAsync += InterceptingPublishAsync;
            mqttServer.ValidatingConnectionAsync += ValidateConnection;

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Log("Shutdown requested (Ctrl+C pressed)...");
                eventArgs.Cancel = true; // Prevent immediate process termination
                cts.Cancel();
            };

            Log("About to start MQTT broker...");
            try
            {
                await mqttServer.StartAsync();
                Log($"MQTT broker started on port {config?.Port ?? 1883}.");
            }
            catch (Exception ex)
            {
                Log($"Failed to start MQTT broker: {ex.Message}");
                throw;
            }
            Log("Press Ctrl+C to exit gracefully...");
            try
            {
                await Task.Delay(-1, cts.Token);
            }
            catch (TaskCanceledException) { }
            Log("Stopping MQTT broker...");
            await mqttServer.StopAsync();
            Log("MQTT broker stopped. Goodbye!");
        }

        private static void StartPrometheusMetricsServer()
        {
            // if debug prometheus need hostname: "localhost"
#if DEBUG
            var metricServer = new MetricServer(port: 9000, hostname: "localhost");
            Log("Prometheus metrics server started on http://localhost:9000/metrics");
#else
            var metricServer = new MetricServer(port: 9000);
            Log("Prometheus metrics server started on http://0.0.0.0:9000/metrics");
#endif
            metricServer.Start();
        }

        static async Task InterceptingPublishAsync(InterceptingPublishEventArgs context)
        {
            try
            {
                MessagesReceived.Inc();
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
                                    Log(
                                        "Sender ID is null or empty in position payload, skipping node creation."
                                    );
                                    MessagesFiltered.Inc();
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
                                    Log(
                                        $"Node {payloadObj.Value.GetProperty("sender").GetString()} position update ignored due to insufficient change or recent update. Time since last update: {DateTime.Now - node.LastUpdate}, Position change: {node.GetDistanceTo(Latitude, Longitude)} m"
                                    );
                                    MessagesFiltered.Inc();
                                    context.ProcessPublish = false;
                                    return;
                                }
                            }
                        }
                    }
                }
                if (payload.IsEmpty || payload.Length == 0)
                {
                    // Log the empty payload and skip processing
                    Log(
                        "Received MQTT message with empty payload from "
                            + $"{context.ClientId} on topic {context.ApplicationMessage.Topic} - skipping processing."
                    );
                    MessagesFiltered.Inc();
                    context.ProcessPublish = false; // Skip processing
                    return;
                }
                // Process the payload
                ServiceEnvelope? envelope = ProcessMeshtasticPayload(payload);

                if (envelope == null)
                {
                    Log("Received invalid ServiceEnvelope, skipping further processing.");
                    MessagesFiltered.Inc();
                    context.ProcessPublish = false; // Skip processing
                    return;
                }
                MeshPacket? data = DecryptEnvelope(envelope);
                // Last part of the topic is the NodeID
                string nodeID = context.ApplicationMessage.Topic.Split('/').Last();
                if (data?.Decoded.Portnum == PortNum.TextMessageApp)
                {
                    Log(
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
                            Log(
                                $"Node {nodeID} position update ignored due to insufficient change or recent update. Time since last update: {DateTime.Now - node.LastUpdate}, Position change: {node.GetDistanceTo(latitude, longitude)} m"
                            );
                            MessagesFiltered.Inc();
                            context.ProcessPublish = false;
                            return;
                        }
                        node.LastLatitude = latitude;
                        node.LastLongitude = longitude;
                        node.LastUpdate = DateTime.Now;
                    }

                    Log(
                        $"Received location data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {latitude}, {longitude} (accuracy: {position.VDOP} m)"
                    );
                }
                else if (data?.Decoded.Portnum == PortNum.NodeinfoApp)
                {
                    NodeInfo nodeInfo = NodeInfo.Parser.ParseFrom(data.Decoded.Payload);
                    Log(
                        $"Received node info from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {nodeInfo.User} (Channel: {nodeInfo.Channel}, version: {nodeInfo.Num})"
                    );
                }
                else if (data?.Decoded.Portnum == PortNum.TelemetryApp)
                {
                    Telemetry telemetry = Telemetry.Parser.ParseFrom(data.Decoded.Payload);
                    Log(
                        $"Received telemetry data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: AirUtilTx: {telemetry.DeviceMetrics.AirUtilTx}, ChannelUtilization: {telemetry.DeviceMetrics.ChannelUtilization}"
                    );
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to process MQTT message: {ex.Message}");
            }
            context.ProcessPublish = true;
            await Task.CompletedTask;
        }

        static Task ValidateConnection(ValidatingConnectionEventArgs args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args.UserName))
                {
                    args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    return Task.CompletedTask;
                }

                var currentUser = config?.Users.FirstOrDefault(u => u.UserName == args.UserName);

                if (currentUser is null)
                {
                    args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    Log($"User {args.UserName} not found in configuration.");
                    return Task.CompletedTask;
                }

                if (args.UserName != currentUser.UserName)
                {
                    args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    Log($"User {args.UserName} is not authorized.");
                    return Task.CompletedTask;
                }

                if (args.Password != currentUser.Password)
                {
                    args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                    Log($"Invalid password for user {args.UserName}.");
                    return Task.CompletedTask;
                }

                if (!currentUser.ValidateClientId)
                {
                    args.ReasonCode = MqttConnectReasonCode.Success;
                    args.SessionItems.Add(args.ClientId, currentUser);
                    Log($"User {args.UserName} connected with client id {args.ClientId}.");
                    return Task.CompletedTask;
                }

                if (string.IsNullOrWhiteSpace(currentUser.ClientIdPrefix))
                {
                    if (args.ClientId != currentUser.ClientId)
                    {
                        args.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                        Log($"Client id {args.ClientId} is not valid.");
                        return Task.CompletedTask;
                    }

                    args.SessionItems.Add(currentUser.ClientId, currentUser);
                }

                args.ReasonCode = MqttConnectReasonCode.Success;
                Log($"User {args.UserName} connected with client id {args.ClientId}.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log($"An error occurred: {ex}");
                return Task.FromException(ex);
            }
        }

        static ServiceEnvelope? ProcessMeshtasticPayload(
            System.Buffers.ReadOnlySequence<byte> payload
        )
        {
            // Implement your Meshtastic payload processing logic here
            ServiceEnvelope envelope = ServiceEnvelope.Parser.ParseFrom(payload);
            if (!IsValidEnvelope(envelope))
            {
                Log("Received invalid ServiceEnvelope.");
                return null;
            }
            return envelope;
        }

        static bool IsValidEnvelope(ServiceEnvelope envelope)
        {
            // Implement your validation logic here
            // For example, check if the envelope has a valid type and data
            return !(
                String.IsNullOrEmpty(envelope.GatewayId)
                || String.IsNullOrEmpty(envelope.ChannelId)
                || envelope.Packet == null
                || envelope.Packet.Id < 0
                || envelope.Packet.Decoded != null
            );
        }

        static MeshPacket? DecryptEnvelope(ServiceEnvelope envelope)
        {
            // Try to decrypt with all available keys
            foreach (var key in config?.EncryptionKeys ?? [])
            {
                try
                {
                    var nonce = new NonceGenerator(
                        envelope.Packet.From,
                        envelope.Packet.Id
                    ).Create();
                    var keyBytes = System.Convert.FromBase64String(key);
                    var decrypted = PacketEncryption.TransformPacket(
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
                    //Log($"Failed to decrypt with key {key}: {ex.Message}");
                }
            }

            return null;
        }

        public static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
