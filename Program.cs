using System.Buffers;
using MeshQTT.Entities;
using MeshQTT.Managers;

namespace MeshQTT
{
    public class Program
    {
        private static Config? config = new(
            Path.Combine(AppContext.BaseDirectory, "config", "config.json")
        );
        private static readonly List<Node> nodes = new();

        static async Task Main(string[] args)
        {
            bool isDebug = false;
#if DEBUG
            isDebug = true;
#endif
            MetricsManager.StartPrometheusMetricsServer(isDebug);
            var messageProcessor = new MessageProcessor(nodes, config);
            var mqttServerManager = new MqttServerManager(config, messageProcessor, nodes);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Logger.Log("Shutdown requested (Ctrl+C pressed)...");
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            await mqttServerManager.StartAsync(cts.Token);
        }
    }
}
