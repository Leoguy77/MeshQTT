namespace MeshQTT.Entities
{
    /// <summary>
    /// Permission types for fine-grained access control
    /// </summary>
    public enum PermissionType
    {
        /// <summary>
        /// No access
        /// </summary>
        None = 0,

        /// <summary>
        /// Read/Subscribe access only
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write/Publish access only
        /// </summary>
        Write = 2,

        /// <summary>
        /// Both read and write access
        /// </summary>
        ReadWrite = Read | Write,

        /// <summary>
        /// Administrative access (includes read/write plus ability to manage permissions)
        /// </summary>
        Admin = ReadWrite | 4
    }

    /// <summary>
    /// Represents a fine-grained permission entry for a specific topic pattern
    /// </summary>
    public record TopicPermission
    {
        /// <summary>
        /// Gets or sets the topic pattern (supports wildcards + and #)
        /// </summary>
        public string TopicPattern { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permission type for this topic
        /// </summary>
        public PermissionType Permission { get; set; } = PermissionType.None;

        /// <summary>
        /// Gets or sets the priority of this permission (higher number = higher priority)
        /// Used to resolve conflicts when multiple patterns match the same topic
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets optional time-based restrictions
        /// </summary>
        public TimeRestriction? TimeRestriction { get; set; }
    }

    /// <summary>
    /// Represents time-based access restrictions
    /// </summary>
    public record TimeRestriction
    {
        /// <summary>
        /// Gets or sets the start time (UTC) when access is allowed
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time (UTC) when access expires
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets allowed days of the week (0 = Sunday, 6 = Saturday)
        /// </summary>
        public List<int> AllowedDaysOfWeek { get; set; } = new();

        /// <summary>
        /// Gets or sets allowed hour ranges (24-hour format)
        /// </summary>
        public List<TimeRange> AllowedHours { get; set; } = new();
    }

    /// <summary>
    /// Represents a time range within a day
    /// </summary>
    public record TimeRange
    {
        /// <summary>
        /// Gets or sets the start hour (0-23)
        /// </summary>
        public int StartHour { get; set; }

        /// <summary>
        /// Gets or sets the end hour (0-23)
        /// </summary>
        public int EndHour { get; set; }
    }

    /// <summary>
    /// Enhanced role system with fine-grained permissions
    /// </summary>
    public record Role
    {
        /// <summary>
        /// Gets or sets the unique role name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the role description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of fine-grained topic permissions
        /// </summary>
        public List<TopicPermission> TopicPermissions { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of parent role names for hierarchical inheritance
        /// </summary>
        public List<string> InheritsFrom { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether this role is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets default throttling settings for this role
        /// </summary>
        public bool ThrottleUser { get; set; }

        /// <summary>
        /// Gets or sets default monthly byte limit for this role
        /// </summary>
        public long? MonthlyByteLimit { get; set; }
    }
}