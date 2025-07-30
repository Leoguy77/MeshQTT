using MeshQTT.Api.Models;
using MeshQTT.Entities;
using MeshQTT.Managers;

namespace MeshQTT.Api.Services
{
    /// <summary>
    /// Service for managing MeshQTT through API calls
    /// </summary>
    public class ApiService
    {
        private readonly List<Node> nodes;
        private readonly Config? config;
        private readonly AlertManager? alertManager;

        public ApiService(List<Node> nodes, Config? config, AlertManager? alertManager = null)
        {
            this.nodes = nodes;
            this.config = config;
            this.alertManager = alertManager;
        }

        /// <summary>
        /// Get all connected nodes
        /// </summary>
        public List<NodeResponse> GetAllNodes()
        {
            return nodes.Select(n => new NodeResponse
            {
                NodeId = n.NodeID,
                LastLatitude = n.LastLatitude,
                LastLongitude = n.LastLongitude,
                LastUpdate = n.LastUpdate,
                IsBanned = config?.Banlist.Contains(n.NodeID) ?? false
            }).ToList();
        }

        /// <summary>
        /// Get a specific node by ID
        /// </summary>
        public NodeResponse? GetNode(string nodeId)
        {
            var node = nodes.FirstOrDefault(n => n.NodeID == nodeId);
            if (node == null) return null;

            return new NodeResponse
            {
                NodeId = node.NodeID,
                LastLatitude = node.LastLatitude,
                LastLongitude = node.LastLongitude,
                LastUpdate = node.LastUpdate,
                IsBanned = config?.Banlist.Contains(node.NodeID) ?? false
            };
        }

        /// <summary>
        /// Ban a node
        /// </summary>
        public async Task<bool> BanNodeAsync(string nodeId, string reason = "")
        {
            if (config == null) return false;

            if (!config.Banlist.Contains(nodeId))
            {
                config.Banlist.Add(nodeId);
                
                // Trigger ban alert
                if (alertManager != null)
                {
                    await alertManager.TriggerNodeBanAlert(nodeId, reason);
                }

                Logger.Log($"Node {nodeId} has been banned. Reason: {reason}");
                return true;
            }

            return false; // Already banned
        }

        /// <summary>
        /// Unban a node
        /// </summary>
        public bool UnbanNode(string nodeId)
        {
            if (config == null) return false;

            if (config.Banlist.Remove(nodeId))
            {
                Logger.Log($"Node {nodeId} has been unbanned.");
                return true;
            }

            return false; // Was not banned
        }

        /// <summary>
        /// Get current configuration (read-only view)
        /// </summary>
        public ConfigResponse? GetConfig()
        {
            if (config == null) return null;

            return new ConfigResponse
            {
                Port = config.Port,
                TlsEnabled = config.TlsEnabled,
                TlsPort = config.TlsPort,
                Banlist = new List<string>(config.Banlist),
                PositionAppTimeoutMinutes = config.PositionAppTimeoutMinutes,
                UserCount = config.Users.Count,
                EncryptionKeyCount = config.EncryptionKeys.Count,
                AlertingEnabled = config.Alerting.Enabled
            };
        }

        /// <summary>
        /// Update configuration settings
        /// </summary>
        public bool UpdateConfig(ConfigUpdateRequest request)
        {
            if (config == null) return false;

            bool changed = false;

            if (request.PositionAppTimeoutMinutes.HasValue)
            {
                config.PositionAppTimeoutMinutes = request.PositionAppTimeoutMinutes.Value;
                changed = true;
            }

            if (request.Banlist != null)
            {
                config.Banlist.Clear();
                config.Banlist.AddRange(request.Banlist);
                changed = true;
            }

            if (changed)
            {
                Logger.Log("Configuration updated via API");
            }

            return changed;
        }

        /// <summary>
        /// Get system statistics
        /// </summary>
        public object GetSystemStats()
        {
            return new
            {
                TotalNodes = nodes.Count,
                BannedNodes = config?.Banlist.Count ?? 0,
                ActiveNodes = nodes.Count(n => n.LastUpdate > DateTime.Now.AddHours(-1)),
                ConfiguredUsers = config?.Users.Count ?? 0,
                EncryptionKeys = config?.EncryptionKeys.Count ?? 0,
                AlertingEnabled = config?.Alerting.Enabled ?? false,
                LastConfigUpdate = DateTime.Now // This could be enhanced to track actual config updates
            };
        }
    }
}