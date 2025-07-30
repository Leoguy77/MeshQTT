namespace MeshQTT.Api.Models
{
    /// <summary>
    /// Represents a node in the API response
    /// </summary>
    public class NodeResponse
    {
        public string NodeId { get; set; } = string.Empty;
        public double LastLatitude { get; set; }
        public double LastLongitude { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool IsBanned { get; set; }
    }

    /// <summary>
    /// Request model for banning/unbanning a node
    /// </summary>
    public class NodeBanRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration response model (read-only view)
    /// </summary>
    public class ConfigResponse
    {
        public int Port { get; set; }
        public bool TlsEnabled { get; set; }
        public int TlsPort { get; set; }
        public List<string> Banlist { get; set; } = new();
        public int PositionAppTimeoutMinutes { get; set; }
        public int UserCount { get; set; }
        public int EncryptionKeyCount { get; set; }
        public bool AlertingEnabled { get; set; }
    }

    /// <summary>
    /// Request model for updating configuration
    /// </summary>
    public class ConfigUpdateRequest
    {
        public int? PositionAppTimeoutMinutes { get; set; }
        public List<string>? Banlist { get; set; }
    }

    /// <summary>
    /// Standard API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    /// <summary>
    /// Standard API response without data
    /// </summary>
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}