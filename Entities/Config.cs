namespace MeshQTT.Entities
{
    public record Config
    {
        /// <summary>
        ///     Gets or sets the port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        ///     Gets or sets the list of valid users.
        /// </summary>
        public List<User> Users { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of encryption keys.
        /// </summary>
        public List<string> EncryptionKeys { get; set; } = [];

        public int PositionAppTimeoutMinutes { get; set; }

        private FileSystemWatcher? _fileWatcher = null;

        public Config()
        {
            // Default values
            Port = 1883;
            Users = new List<User>();
            EncryptionKeys = new List<string>();
            PositionAppTimeoutMinutes = 30; // Default timeout in minutes
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
                    Users = config.Users;
                    EncryptionKeys = config.EncryptionKeys;
                    PositionAppTimeoutMinutes = config.PositionAppTimeoutMinutes;

                    var configPath = Path.Combine(AppContext.BaseDirectory, "config");
                    _fileWatcher = new FileSystemWatcher($"{configPath}", "*.json");
                    _fileWatcher.Changed += (sender, e) =>
                    {
                        if (e.FullPath == filePath)
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
                                    var updatedConfig =
                                        System.Text.Json.JsonSerializer.Deserialize<Config>(
                                            updatedJson
                                        );
                                    if (updatedConfig != null)
                                    {
                                        Port = updatedConfig.Port;
                                        Users = updatedConfig.Users;
                                        EncryptionKeys = updatedConfig.EncryptionKeys;
                                        PositionAppTimeoutMinutes =
                                            updatedConfig.PositionAppTimeoutMinutes;
                                    }
                                    Console.WriteLine("Reloaded config");
                                    break;
                                }
                                catch (IOException)
                                {
                                    retries++;
                                    Thread.Sleep(delayMs);
                                }
                            }
                        }
                    };
                    _fileWatcher.EnableRaisingEvents = true;
                }
            }
            else
            {
                throw new FileNotFoundException("Configuration file not found.", filePath);
            }
        }
    }
}
