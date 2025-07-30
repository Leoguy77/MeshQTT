namespace MeshQTT.Entities
{
    public class AlertConfig
    {
        /// <summary>
        /// Gets or sets whether alerting is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the list of notification providers.
        /// </summary>
        public List<NotificationProvider> Providers { get; set; } = new();

        /// <summary>
        /// Gets or sets the security alert thresholds.
        /// </summary>
        public SecurityThresholds Security { get; set; } = new();

        /// <summary>
        /// Gets or sets the system alert thresholds.
        /// </summary>
        public SystemThresholds System { get; set; } = new();
    }

    public class NotificationProvider
    {
        /// <summary>
        /// Gets or sets the provider type (email, discord, slack).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this provider is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the provider configuration.
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new();
    }

    public class SecurityThresholds
    {
        /// <summary>
        /// Maximum failed login attempts before alerting (per IP per hour).
        /// </summary>
        public int FailedLoginThreshold { get; set; } = 5;

        /// <summary>
        /// Maximum node joins per hour before alerting.
        /// </summary>
        public int RapidNodeJoinsThreshold { get; set; } = 50;

        /// <summary>
        /// Maximum node leaves per hour before alerting.
        /// </summary>
        public int RapidNodeLeavesThreshold { get; set; } = 50;

        /// <summary>
        /// Alert on any node ban events.
        /// </summary>
        public bool AlertOnNodeBan { get; set; } = true;
    }

    public class SystemThresholds
    {
        /// <summary>
        /// Maximum messages per minute before alerting.
        /// </summary>
        public int MessageRateThreshold { get; set; } = 1000;

        /// <summary>
        /// Maximum messages per minute per node before alerting.
        /// </summary>
        public int NodeMessageRateThreshold { get; set; } = 100;

        /// <summary>
        /// Alert on service restarts.
        /// </summary>
        public bool AlertOnServiceRestart { get; set; } = true;

        /// <summary>
        /// Alert on system errors.
        /// </summary>
        public bool AlertOnSystemErrors { get; set; } = true;

        /// <summary>
        /// Maximum error rate per hour before alerting.
        /// </summary>
        public int ErrorRateThreshold { get; set; } = 10;
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical,
    }

    public class AlertEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class TelegramBotConfig
    {
        /// <summary>
        /// Gets or sets whether the Telegram bot is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the bot token from @BotFather.
        /// </summary>
        public string BotToken { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of authorized user IDs who can control the bot.
        /// </summary>
        public List<long> AuthorizedUsers { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of authorized chat IDs where the bot can operate.
        /// </summary>
        public List<long> AuthorizedChats { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to require user authentication for commands.
        /// </summary>
        public bool RequireAuthentication { get; set; } = true;

        /// <summary>
        /// Gets or sets the command prefix (default is '/').
        /// </summary>
        public string CommandPrefix { get; set; } = "/";
    }
}
