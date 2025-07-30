using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MeshQTT.Entities;
using MQTTnet.Server;

namespace MeshQTT.Managers
{
    public class MqttServerManager
    {
        private readonly Config? config;
        private readonly MessageProcessor messageProcessor;
        private readonly List<Node> nodes;
        private readonly AlertManager? alertManager;
        private MqttServer? mqttServer;

        public MqttServerManager(
            Config? config,
            MessageProcessor messageProcessor,
            List<Node> nodes,
            AlertManager? alertManager = null
        )
        {
            this.config = config;
            this.messageProcessor = messageProcessor;
            this.nodes = nodes;
            this.alertManager = alertManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var mqttServerOptionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Any)
                .WithDefaultEndpointPort(config?.Port ?? 1883); // Add TLS endpoint if enabled
            if (config?.TlsEnabled == true)
            {
                var certificate = CertificateManager.GetOrCreateCertificate(
                    config.CertificatePath,
                    config.PrivateKeyPath,
                    config.CertificatePassword
                );

                Logger.Log(
                    $"Certificate loaded: Subject={certificate.Subject}, HasPrivateKey={certificate.HasPrivateKey}"
                );
                Logger.Log(
                    $"Certificate valid from {certificate.NotBefore} to {certificate.NotAfter}"
                );

                // Convert to PFX format for MQTTnet
                var pfxBytes = certificate.Export(X509ContentType.Pfx);
                var pfxCertificate = new X509Certificate2(pfxBytes);

                mqttServerOptionsBuilder
                    .WithEncryptedEndpoint()
                    .WithEncryptedEndpointPort(config.TlsPort)
                    .WithEncryptedEndpointBoundIPAddress(IPAddress.Any)
                    .WithEncryptionCertificate(pfxCertificate)
                    .WithEncryptionSslProtocol(SslProtocols.Tls12 | SslProtocols.Tls13);

                Logger.Log($"TLS enabled on port {config.TlsPort}");
            }

            var mqttServerOptions = mqttServerOptionsBuilder.Build();

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
                var portInfo = $"port {config?.Port ?? 1883}";
                if (config?.TlsEnabled == true)
                {
                    portInfo += $" (TLS on port {config.TlsPort})";
                }
                Logger.Log($"MQTT broker started on {portInfo}.");
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

        private async Task ValidateConnection(ValidatingConnectionEventArgs args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args.UserName))
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log($"Failed login from {args.RemoteEndPoint} (reason: empty username)");

                    if (alertManager != null)
                    {
                        await alertManager.TriggerFailedLoginAlert(
                            args.RemoteEndPoint?.ToString() ?? "unknown",
                            args.UserName ?? "empty",
                            "empty username"
                        );
                    }
                    return;
                }

                var currentUser = config?.Users.FirstOrDefault(u => u.UserName == args.UserName);

                if (currentUser is null)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: user {args.UserName} not found)"
                    );

                    if (alertManager != null)
                    {
                        await alertManager.TriggerFailedLoginAlert(
                            args.RemoteEndPoint?.ToString() ?? "unknown",
                            args.UserName,
                            "user not found"
                        );
                    }
                    return;
                }

                if (args.UserName != currentUser.UserName)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: user {args.UserName} not authorized)"
                    );

                    if (alertManager != null)
                    {
                        await alertManager.TriggerFailedLoginAlert(
                            args.RemoteEndPoint?.ToString() ?? "unknown",
                            args.UserName,
                            "user not authorized"
                        );
                    }
                    return;
                }

                if (args.Password != currentUser.Password)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.BadUserNameOrPassword;
                    Logger.Log(
                        $"Failed login from {args.RemoteEndPoint} (reason: invalid password for user {args.UserName})"
                    );

                    if (alertManager != null)
                    {
                        await alertManager.TriggerFailedLoginAlert(
                            args.RemoteEndPoint?.ToString() ?? "unknown",
                            args.UserName,
                            "invalid password"
                        );
                    }
                    return;
                }

                if (!currentUser.ValidateClientId)
                {
                    args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
                    args.SessionItems.Add(args.ClientId, currentUser);
                    Logger.Log($"User {args.UserName} connected with client id {args.ClientId}.");
                    return;
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
                        return;
                    }

                    args.SessionItems.Add(currentUser.ClientId, currentUser);
                }

                args.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;
                Logger.Log($"User {args.UserName} connected with client id {args.ClientId}.");
            }
            catch (Exception ex)
            {
                Logger.Log($"An error occurred: {ex}");

                if (alertManager != null)
                {
                    await alertManager.TriggerSystemErrorAlert(
                        $"Connection validation error: {ex.Message}",
                        ex
                    );
                }

                throw;
            }
        }
    }
}
