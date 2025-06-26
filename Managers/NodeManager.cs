using MeshQTT.Entities;

namespace MeshQTT.Managers
{
    public class NodeManager
    {
        private readonly List<Node> nodes;
        private readonly MeshQTT.Entities.Config? config;
        private readonly AlertManager? alertManager;

        public NodeManager(
            List<Node> nodes,
            MeshQTT.Entities.Config? config,
            AlertManager? alertManager = null
        )
        {
            this.nodes = nodes;
            this.config = config;
            this.alertManager = alertManager;
        }

        public async Task<Node> GetOrCreateNodeAsync(string nodeId)
        {
            var node = nodes.FirstOrDefault(n => n.NodeID == nodeId);
            if (node == null)
            {
                node = new Node(nodeId);
                nodes.Add(node);

                // Trigger node join alert
                if (alertManager != null)
                {
                    await alertManager.TriggerNodeJoinAlert(nodeId);
                }
            }
            return node;
        }

        public Node GetOrCreateNode(string nodeId)
        {
            var node = nodes.FirstOrDefault(n => n.NodeID == nodeId);
            if (node == null)
            {
                node = new Node(nodeId);
                nodes.Add(node);
            }
            return node;
        }

        public bool ShouldUpdateNode(Node node, double latitude, double longitude)
        {
            return node.LastUpdate.AddMinutes(config?.PositionAppTimeoutMinutes ?? 720)
                    < DateTime.Now
                || node.GetDistanceTo(latitude, longitude) > 100;
        }
    }
}
