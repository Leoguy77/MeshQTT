using MeshQTT.Entities;
using Meshtastic.Protobufs;

namespace MeshQTT.Managers
{
    public class PayloadHandler
    {
        private readonly NodeManager nodeManager;
        private readonly MeshQTT.Entities.Config? config;

        public PayloadHandler(NodeManager nodeManager, MeshQTT.Entities.Config? config)
        {
            this.nodeManager = nodeManager;
            this.config = config;
        }

        public void HandleTextMessage(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            Logger.Log(
                $"Received text message from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {data.Decoded.Payload.ToStringUtf8()}"
            );
        }

        public void HandlePosition(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            Position position = Position.Parser.ParseFrom(data.Decoded.Payload);
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

        public void HandleNodeInfo(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            NodeInfo nodeInfo = NodeInfo.Parser.ParseFrom(data.Decoded.Payload);
            Logger.Log(
                $"Received node info from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: {nodeInfo.User} (Channel: {nodeInfo.Channel}, version: {nodeInfo.Num})"
            );
        }

        public void HandleTelemetry(string nodeID, ServiceEnvelope envelope, MeshPacket data)
        {
            Telemetry telemetry = Telemetry.Parser.ParseFrom(data.Decoded.Payload);
            Logger.Log(
                $"Received telemetry data from {nodeID} heard by {envelope.GatewayId} on channel {envelope.ChannelId}: AirUtilTx: {telemetry.DeviceMetrics.AirUtilTx}, ChannelUtilization: {telemetry.DeviceMetrics.ChannelUtilization}"
            );
        }
    }
}
