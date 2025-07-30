namespace MeshQTT.Entities
{
    public record Config
    {
        /// <summary>
        ///     Gets or sets the port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets whether TLS is enabled.
        /// </summary>
        public bool TlsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the TLS port (default 8883).
        /// </summary>
        public int TlsPort { get; set; }

        /// <summary>
        /// Gets or sets the path to the certificate file (.crt or .pem).
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the private key file (.key or .pem).
        /// </summary>
        public string? PrivateKeyPath { get; set; }

        /// <summary>
        /// Gets or sets the certificate password (if needed).
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        ///     Gets or sets the list of valid users.
        /// </summary>
        public List<User> Users { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of user groups.
        /// </summary>
        public List<Group> Groups { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of user roles.
        /// </summary>
        public List<Role> Roles { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of encryption keys.
        /// </summary>
        public List<string> EncryptionKeys { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of banned node IDs.
        /// </summary>
        public List<string> Banlist { get; set; } = [];

        public int PositionAppTimeoutMinutes { get; set; }

        /// <summary>
        /// Gets or sets the alerting configuration.
        /// </summary>
        public AlertConfig Alerting { get; set; } = new();

        private FileSystemWatcher? _fileWatcher = null;
        private Timer? _pollingTimer = null;
        private DateTime _lastWriteTime;
        private readonly string? _filePath;
        private readonly object _reloadLock = new();

        public Config()
        {
            // Default values
            Port = 1883;
            TlsEnabled = false;
            TlsPort = 8883;
            Users = new List<User>();
            Groups = new List<Group>();
            Roles = new List<Role>();
            EncryptionKeys = new List<string>();
            Banlist = new List<string>();
            PositionAppTimeoutMinutes = 30; // Default timeout in minutes
            Alerting = new AlertConfig();
        }

        public Config(string filePath)
        {
            if (File.Exists(filePath))
            {
                using var r = new StreamReader(filePath);
                var json = r.ReadToEnd();
                var config = System.Text.Json.JsonSerializer.Deserialize<Config>(json);
                if (config != null)
                {
                    Port = config.Port;
                    TlsEnabled = config.TlsEnabled;
                    TlsPort = config.TlsPort;
                    CertificatePath = config.CertificatePath;
                    PrivateKeyPath = config.PrivateKeyPath;
                    CertificatePassword = config.CertificatePassword;
                    Users = config.Users;
                    Groups = config.Groups ?? new List<Group>();
                    Roles = config.Roles ?? new List<Role>();
                    EncryptionKeys = config.EncryptionKeys;
                    Banlist = config.Banlist;
                    PositionAppTimeoutMinutes = config.PositionAppTimeoutMinutes;
                    Alerting = config.Alerting ?? new AlertConfig();

                    // FileSystemWatcher (may not work in Docker Desktop)
                    var configPath = Path.Combine(AppContext.BaseDirectory, "config");
                    _fileWatcher = new FileSystemWatcher($"{configPath}", "*.json");
                    _fileWatcher.Changed += (sender, e) =>
                    {
                        if (e.FullPath == filePath)
                        {
                            ReloadConfigWithRetry(filePath);
                        }
                    };
                    _fileWatcher.EnableRaisingEvents = true;

                    // Polling fallback for Docker Desktop
                    _filePath = filePath;
                    _lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                    _pollingTimer = new Timer(_ => PollConfigFile(), null, 1000, 1000); // every 1s
                }
            }
            else
            {
                throw new FileNotFoundException("Configuration file not found.", filePath);
            }
        }

        private void PollConfigFile()
        {
            if (_filePath == null)
                return;
            var currentWriteTime = File.GetLastWriteTimeUtc(_filePath);
            if (currentWriteTime != _lastWriteTime)
            {
                _lastWriteTime = currentWriteTime;
                ReloadConfigWithRetry(_filePath);
            }
        }

        private void ReloadConfigWithRetry(string filePath)
        {
            lock (_reloadLock)
            {
                const int maxRetries = 5;
                const int delayMs = 100;
                int retries = 0;
                while (retries < maxRetries)
                {
                    try
                    {
                        using var reader = new StreamReader(filePath);
                        var updatedJson = reader.ReadToEnd();
                        var updatedConfig = System.Text.Json.JsonSerializer.Deserialize<Config>(
                            updatedJson
                        );
                        if (updatedConfig != null)
                        {
                            Port = updatedConfig.Port;
                            TlsEnabled = updatedConfig.TlsEnabled;
                            TlsPort = updatedConfig.TlsPort;
                            CertificatePath = updatedConfig.CertificatePath;
                            PrivateKeyPath = updatedConfig.PrivateKeyPath;
                            CertificatePassword = updatedConfig.CertificatePassword;
                            Users = updatedConfig.Users;
                            Groups = updatedConfig.Groups ?? new List<Group>();
                            Roles = updatedConfig.Roles ?? new List<Role>();
                            EncryptionKeys = updatedConfig.EncryptionKeys;
                            Banlist = updatedConfig.Banlist;
                            PositionAppTimeoutMinutes = updatedConfig.PositionAppTimeoutMinutes;
                            Alerting = updatedConfig.Alerting ?? new AlertConfig();
                        }
                        Console.WriteLine("Reloaded config (polling)");
                        break;
                    }
                    catch (IOException)
                    {
                        retries++;
                        Thread.Sleep(delayMs);
                    }
                }
            }
        }
    }
}
