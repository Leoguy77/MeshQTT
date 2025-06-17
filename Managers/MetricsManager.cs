using Prometheus;

namespace MeshQTT.Managers
{
    public static class MetricsManager
    {
        public static readonly Counter MessagesReceived = Metrics.CreateCounter(
            "mqtt_messages_received_total",
            "Total number of MQTT messages received."
        );
        public static readonly Counter MessagesFiltered = Metrics.CreateCounter(
            "mqtt_messages_filtered_total",
            "Total number of MQTT messages filtered due to empty payload or invalid data."
        );
        public static readonly Gauge ClientsConnected = Metrics.CreateGauge(
            "mqtt_clients_connected_total",
            "Total number of MQTT clients connected."
        );

        private static MetricServer? metricServer;

        public static void StartPrometheusMetricsServer(bool isDebug)
        {
            if (metricServer != null)
                return;
#if DEBUG
            metricServer = new MetricServer(port: 9000, hostname: "localhost");
#else
            metricServer = new MetricServer(port: 9000);
#endif
            metricServer.Start();
            if (isDebug)
                Logger.Log("Prometheus metrics server started on http://localhost:9000/metrics");
            else
                Logger.Log("Prometheus metrics server started on http://0.0.0.0:9000/metrics");
        }
    }
}
