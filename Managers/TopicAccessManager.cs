using MeshQTT.Entities;

namespace MeshQTT.Managers
{
    public static class TopicAccessManager
    {
        /// <summary>
        /// Validates if a user has permission to publish to a specific topic
        /// </summary>
        public static bool CanPublish(User user, string topic)
        {
            return HasTopicAccess(user.PublishTopicLists, topic);
        }

        /// <summary>
        /// Validates if a user has permission to subscribe to a specific topic
        /// </summary>
        public static bool CanSubscribe(User user, string topic)
        {
            return HasTopicAccess(user.SubscriptionTopicLists, topic);
        }

        /// <summary>
        /// Checks if a topic matches the access rules defined in TopicTuple
        /// </summary>
        private static bool HasTopicAccess(TopicTuple topicLists, string topic)
        {
            // If whitelist is defined and not empty, topic must match at least one whitelist pattern
            if (topicLists.WhitelistTopics.Count > 0)
            {
                bool whitelistMatch = topicLists.WhitelistTopics.Any(pattern => MatchesTopic(pattern, topic));
                if (!whitelistMatch)
                {
                    return false;
                }
            }

            // If blacklist is defined and not empty, topic must not match any blacklist pattern
            if (topicLists.BlacklistTopics.Count > 0)
            {
                bool blacklistMatch = topicLists.BlacklistTopics.Any(pattern => MatchesTopic(pattern, topic));
                if (blacklistMatch)
                {
                    return false;
                }
            }

            // If no whitelist defined or whitelist matches, and no blacklist matches, allow access
            return true;
        }

        /// <summary>
        /// Checks if a topic matches a pattern supporting MQTT wildcards (+ and #)
        /// </summary>
        /// <param name="pattern">The pattern that may contain wildcards</param>
        /// <param name="topic">The actual topic to match</param>
        /// <returns>True if the topic matches the pattern</returns>
        public static bool MatchesTopic(string pattern, string topic)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(topic))
            {
                return false;
            }

            // Exact match
            if (pattern == topic)
            {
                return true;
            }

            // Split by '/'
            var patternParts = pattern.Split('/');
            var topicParts = topic.Split('/');

            return MatchesTopicParts(patternParts, topicParts, 0, 0);
        }

        private static bool MatchesTopicParts(string[] patternParts, string[] topicParts, int patternIndex, int topicIndex)
        {
            // Check if we've reached the end of both pattern and topic
            if (patternIndex >= patternParts.Length && topicIndex >= topicParts.Length)
            {
                return true;
            }

            // If we've reached the end of pattern but not topic
            if (patternIndex >= patternParts.Length)
            {
                return false;
            }

            // If current pattern part is '#' (multi-level wildcard)
            if (patternParts[patternIndex] == "#")
            {
                // '#' must be the last part of the pattern
                if (patternIndex != patternParts.Length - 1)
                {
                    return false;
                }
                // '#' matches any remaining topic parts, but requires at least one more level
                // For MQTT standard: "sensor/#" does NOT match "sensor", only "sensor/..." 
                return topicIndex < topicParts.Length;
            }

            // If we've reached the end of topic but not pattern
            if (topicIndex >= topicParts.Length)
            {
                return false;
            }

            // If current pattern part is '+' (single-level wildcard)
            if (patternParts[patternIndex] == "+")
            {
                // '+' matches exactly one topic level
                return MatchesTopicParts(patternParts, topicParts, patternIndex + 1, topicIndex + 1);
            }

            // Exact match for current level
            if (patternParts[patternIndex] == topicParts[topicIndex])
            {
                return MatchesTopicParts(patternParts, topicParts, patternIndex + 1, topicIndex + 1);
            }

            // No match
            return false;
        }
    }
}