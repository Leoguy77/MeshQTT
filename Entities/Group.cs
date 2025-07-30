namespace MeshQTT.Entities
{
    /// <summary>
    /// Represents a user group that can contain multiple users and inherit permissions
    /// </summary>
    public record Group
    {
        /// <summary>
        /// Gets or sets the unique group name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the group description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subscription topic lists for this group
        /// </summary>
        public TopicTuple SubscriptionTopicLists { get; set; } = new();

        /// <summary>
        /// Gets or sets the publish topic lists for this group
        /// </summary>
        public TopicTuple PublishTopicLists { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of parent group names for hierarchical inheritance
        /// </summary>
        public List<string> InheritsFrom { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether this group is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets user throttling configuration for this group
        /// </summary>
        public bool ThrottleUser { get; set; }

        /// <summary>
        /// Gets or sets monthly byte limit for users in this group
        /// </summary>
        public long? MonthlyByteLimit { get; set; }
    }
}