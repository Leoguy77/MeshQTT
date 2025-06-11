using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet.Server;
using Meshtastic;
using Meshtastic.Protobufs;
using Meshtastic.Crypto;
using Org.BouncyCastle.Asn1.Cms;
using MQTTnet.Protocol;
using System.Text.Json.Serialization;

namespace MeshQTT;

class Program
{
    private static Config? config = new Config();
    static async Task Main(string[] args)
    {
        try
        {
            config = ReadConfiguration(AppContext.BaseDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read configuration: {ex.Message}");
            return;
        }
        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Any)
            .WithDefaultEndpointPort(config?.Port ?? 1883)
            .Build();

        using var mqttServer = new MqttServerFactory().CreateMqttServer(
            mqttServerOptions
        );

        mqttServer.InterceptingPublishAsync += async context =>
        {
            try
            {
                var payload = context.ApplicationMessage.Payload;
                if (payload.IsEmpty || payload.Length == 0)
                {
                    // Log the empty payload and skip processing
                    Console.WriteLine("Received MQTT message with empty payload from " +
                                      $"{context.ClientId} on topic {context.ApplicationMessage.Topic} - skipping processing.");
                    context.ProcessPublish = false; // Skip processing
                    return;
                }
                // Process the payload
                ServiceEnvelope? envelope = ProcessMeshtasticPayload(payload);

                if (envelope == null)
                {
                    Console.WriteLine("Received invalid ServiceEnvelope, skipping further processing.");
                    context.ProcessPublish = false; // Skip processing
                    return;
                }
                Data? data = DecryptEnvelope(envelope);
                if (data == null)
                {
                    Console.WriteLine("Decrypted payload is null or invalid, skipping further processing.");
                    context.ProcessPublish = false; // Skip processing
                    return;
                }



                Console.WriteLine($"Received MQTT message on topic {context.ApplicationMessage.Topic}, payload length: {payload.Length}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process MQTT message: {ex.Message}");
            }
            context.ProcessPublish = true;
            await Task.CompletedTask;
        };

        mqttServer.ClientConnectedAsync += async context =>
        {
            Console.WriteLine($"Client connected: {context.ClientId} with user {context.AuthenticationData} from {context.RemoteEndPoint}");
            await Task.CompletedTask;
        };
        mqttServer.ClientDisconnectedAsync += async context =>
        {
            Console.WriteLine($"Client disconnected: {context.ClientId} from {context.RemoteEndPoint}");
            await Task.CompletedTask;
        };

        mqttServer.ValidatingConnectionAsync += ValidateConnection;


        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Shutdown requested (Ctrl+C pressed)...");
            eventArgs.Cancel = true; // Prevent immediate process termination
            cts.Cancel();
        };

        Console.WriteLine("About to start MQTT broker...");
        try
        {
            await mqttServer.StartAsync();
            Console.WriteLine($"MQTT broker started on port {config?.Port ?? 1883}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start MQTT broker: {ex.Message}");
            throw;
        }
        Console.WriteLine("Press Ctrl+C to exit gracefully...");
        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (TaskCanceledException) { }
        Console.WriteLine("Stopping MQTT broker...");
        await mqttServer.StopAsync();
        Console.WriteLine("MQTT broker stopped. Goodbye!");
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
                Console.WriteLine($"User {args.UserName} not found in configuration.");
                return Task.CompletedTask;
            }

            if (args.UserName != currentUser.UserName)
            {
                args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                Console.WriteLine($"User {args.UserName} is not authorized.");
                return Task.CompletedTask;
            }

            if (args.Password != currentUser.Password)
            {
                args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                Console.WriteLine($"Invalid password for user {args.UserName}.");
                return Task.CompletedTask;
            }

            if (!currentUser.ValidateClientId)
            {
                args.ReasonCode = MqttConnectReasonCode.Success;
                args.SessionItems.Add(args.ClientId, currentUser);
                Console.WriteLine($"User {args.UserName} connected with client id {args.ClientId}.");
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(currentUser.ClientIdPrefix))
            {
                if (args.ClientId != currentUser.ClientId)
                {
                    args.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                    Console.WriteLine($"Client id {args.ClientId} is not valid.");
                    return Task.CompletedTask;
                }

                args.SessionItems.Add(currentUser.ClientId, currentUser);
            }


            args.ReasonCode = MqttConnectReasonCode.Success;
            Console.WriteLine($"User {args.UserName} connected with client id {args.ClientId}.");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex}");
            return Task.FromException(ex);
        }
    }

    static ServiceEnvelope? ProcessMeshtasticPayload(System.Buffers.ReadOnlySequence<byte> payload)
    {
        // Implement your Meshtastic payload processing logic here
        ServiceEnvelope envelope = ServiceEnvelope.Parser.ParseFrom(payload);
        if (!IsValidEnvelope(envelope))
        {
            Console.WriteLine("Received invalid ServiceEnvelope.");
            return null;
        }
        return envelope;

    }

    static bool IsValidEnvelope(ServiceEnvelope envelope)
    {
        // Implement your validation logic here
        // For example, check if the envelope has a valid type and data
        return !(String.IsNullOrEmpty(envelope.GatewayId) ||
         String.IsNullOrEmpty(envelope.ChannelId) ||
        envelope.Packet == null ||
        envelope.Packet.Id < 0 ||
        envelope.Packet.Decoded != null);
    }

    static Data? DecryptEnvelope(ServiceEnvelope envelope)
    {
        var nonce = new NonceGenerator(envelope.Packet.From, envelope.Packet.Id).Create();
        var decrypted = PacketEncryption.TransformPacket(envelope.Packet.Encrypted.ToByteArray(), nonce, Resources.DEFAULT_PSK);
        var payload = Data.Parser.ParseFrom(decrypted);
        if (payload.Portnum > PortNum.UnknownApp && payload.Payload.Length > 0)
            return payload;

        return null;
    }

    private static Config? ReadConfiguration(string currentPath)
    {
        var filePath = $"{currentPath}\\config.json";

        if (File.Exists(filePath))
        {
            Config config;
            using (var r = new StreamReader(filePath))
            {
                var json = r.ReadToEnd();
                config = System.Text.Json.JsonSerializer.Deserialize<Config>(json) ?? new();
            }



            return config;
        }
        throw new FileNotFoundException("Configuration file not found.", filePath);


    }

}


