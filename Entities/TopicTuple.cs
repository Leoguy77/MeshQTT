namespace MeshQTT.Entities
{
    public class TopicTuple
    {
        /// <summary>
        ///     Gets or sets the whitelist topics.
        /// </summary>
        public List<string> WhitelistTopics { get; set; } = [];

        /// <summary>
        ///     Gets or sets the blacklist topics.
        /// </summary>
        public List<string> BlacklistTopics { get; set; } = [];
    }
}