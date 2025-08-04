using MeshQTT.Entities;
using Meshtastic.Protobufs;

namespace MeshQTT.Managers
{
    public class PayloadHandler
    {
        private readonly NodeManager nodeManager;
        private readonly MeshQTT.Entities.Config? config;
        private readonly AlertManager? alertManager;

        public PayloadHandler(NodeManager nodeManager, MeshQTT.Entities.Config? config, AlertManager? alertManager = null)
        {
            this.nodeManager = nodeManager;
            this.config = config;
            this.alertManager = alertManager;
        }

        public void HandleTextMessage(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            try
            {
                if (data?.Decoded?.Payload == null || envelope == null)
                {
                    Logger.Log($"Invalid text message data from {nodeID} - null payload or envelope");
                    return;
                }

                Logger.Log(
                    $"Received text message from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {data.Decoded.Payload.ToStringUtf8()}"
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to handle text message from {nodeID}: {ex.Message}");
            }
        }

        public void HandlePosition(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            try
            {
                if (data?.Decoded?.Payload == null || envelope == null)
                {
                    Logger.Log($"Invalid position data from {nodeID} - null payload or envelope");
                    return;
                }

                Position position = Position.Parser.ParseFrom(data.Decoded.Payload);
                if (position == null)
                {
                    Logger.Log($"Failed to parse position data from {nodeID}");
                    return;
                }

                double latitude = position.LatitudeI * 1e-7;
                double longitude = position.LongitudeI * 1e-7;
                var node = nodeManager.GetOrCreateNode(nodeID);
                if (!nodeManager.ShouldUpdateNode(node, latitude, longitude))
                {
                    Logger.Log(
                        $"Node {nodeID} position update ignored due to insufficient change or recent update. Time since last update: {DateTime.Now - node.LastUpdate}, Position change: {node.GetDistanceTo(latitude, longitude)} m"
                    );
                    MetricsManager.MessagesFiltered.Inc();
                    return;
                }
                node.LastLatitude = latitude;
                node.LastLongitude = longitude;
                node.LastUpdate = DateTime.Now;
                Logger.Log(
                    $"Received location data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {latitude}, {longitude} (accuracy: {position.VDOP} m)"
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to handle position data from {nodeID}: {ex.Message}");
            }
        }

        public void HandleNodeInfo(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            try
            {
                if (data?.Decoded?.Payload == null || envelope == null)
                {
                    Logger.Log($"Invalid node info data from {nodeID} - null payload or envelope");
                    return;
                }

                NodeInfo nodeInfo = NodeInfo.Parser.ParseFrom(data.Decoded.Payload);
                if (nodeInfo == null)
                {
                    Logger.Log($"Failed to parse node info data from {nodeID}");
                    return;
                }

                Logger.Log(
                    $"Received node info from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {nodeInfo.User} (Channel: {nodeInfo.Channel}, version: {nodeInfo.Num})"
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to handle node info from {nodeID}: {ex.Message}");
            }
        }

        public void HandleTelemetry(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            try
            {
                if (data?.Decoded?.Payload == null || envelope == null)
                {
                    Logger.Log($"Invalid telemetry data from {nodeID} - null payload or envelope");
                    return;
                }

                Telemetry telemetry = Telemetry.Parser.ParseFrom(data.Decoded.Payload);
                if (telemetry?.DeviceMetrics == null)
                {
                    Logger.Log($"Failed to parse telemetry data from {nodeID} or no device metrics available");
                    return;
                }

                Logger.Log(
                    $"Received telemetry data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: AirUtilTx: {telemetry.DeviceMetrics.AirUtilTx}, ChannelUtilization: {telemetry.DeviceMetrics.ChannelUtilization}"
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to handle telemetry data from {nodeID}: {ex.Message}");
            }
        }
    }
}
