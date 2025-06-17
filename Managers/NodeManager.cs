using MeshQTT.Entities;

namespace MeshQTT.Managers
{
    public class NodeManager
    {
        private readonly List<Node> nodes;
        private readonly MeshQTT.Entities.Config? config;

        public NodeManager(List<Node> nodes, MeshQTT.Entities.Config? config)
        {
            this.nodes = nodes;
            this.config = config;
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
