using System.Net;
using MeshQTT.Entities;
using MQTTnet.Server;

namespace MeshQTT.Managers
{
    public class MqttServerManager
    {
        private readonly Config? config;
        private readonly MessageProcessor messageProcessor;
        private readonly List<Node> nodes;
        private MqttServer? mqttServer;

        public MqttServerManager(
            Config? config,
            MessageProcessor messageProcessor,
            List<Node> nodes
        )
        {
            this.config = config;
            this.messageProcessor = messageProcessor;
            this.nodes = nodes;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
                .WithDefaultEndpointPort(config?.Port ?? 1883)
                .Build();

            mqttServer = new MqttServerFactory().CreateMqttServer(mqttServerOptions);

            mqttServer.ClientConnectedAsync += async context =>
            {
                MetricsManager.ClientsConnected.Inc();
                Logger.Log(
                    $"Client connected: {context.ClientId} with user {context.AuthenticationData} from {context.RemoteEndPoint}"
                );
                await Task.CompletedTask;
            };
            mqttServer.ClientDisconnectedAsync += async context =>
            {
                MetricsManager.ClientsConnected.Dec();
                Logger.Log(
                    $"Client disconnected: {context.ClientId} from {context.RemoteEndPoint}"
                );
                await Task.CompletedTask;
            };
            mqttServer.InterceptingPublishAsync += messageProcessor.InterceptingPublishAsync;
            mqttServer.ValidatingConnectionAsync += ValidateConnection;

            Logger.Log("About to start MQTT broker...");
            try
            {
                await mqttServer.StartAsync();
                Logger.Log($"MQTT broker started on port {config?.Port ?? 1883}.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start MQTT broker: {ex.Message}");
                throw;
            }
            Logger.Log("Press Ctrl+C to exit gracefully...");
            try
            {
                await Task.Delay(-1, cancellationToken);
            }
            catch (TaskCanceledException) { }
            Logger.Log("Stopping MQTT broker...");
            await mqttServer.StopAsync();
            Logger.Log("MQTT broker stopped. Goodbye!");
        }

        private Task ValidateConnection(ValidatingConnectionEventArgs args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args.UserName))
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log($"Failed login from {args.RemoteEndPoint} (reason: empty username)");
                    return Task.CompletedTask;
                }

                var currentUser = config?.Users.FirstOrDefault(u => u.UserName == args.UserName);

                if (currentUser is null)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: user {args.UserName} not found)"
                    );
                    return Task.CompletedTask;
                }

                if (args.UserName != currentUser.UserName)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: user {args.UserName} not authorized)"
                    );
                    return Task.CompletedTask;
                }

                if (args.Password != currentUser.Password)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: invalid password for user {args.UserName})"
                    );
                    return Task.CompletedTask;
                }

                if (!currentUser.ValidateClientId)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
                    args.SessionItems.Add(args.ClientId, currentUser);
                    Logger.Log($"User {args.UserName} connected with client id {args.ClientId}.");
                    return Task.CompletedTask;
                }

                if (string.IsNullOrWhiteSpace(currentUser.ClientIdPrefix))
                {
                    if (args.ClientId != currentUser.ClientId)
                    {
                        args.ReasonCode = MQTTnet
                            .Protocol
                            .MqttConnectReasonCode
                            .ClientIdentifierNotValid;
                        Logger.Log($"Client id {args.ClientId} is not valid.");
                        return Task.CompletedTask;
                    }

                    args.SessionItems.Add(currentUser.ClientId, currentUser);
                }

                args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
                Logger.Log($"User {args.UserName} connected with client id {args.ClientId}.");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Log($"An error occurred: {ex}");
                return Task.FromException(ex);
            }
        }
    }
}
