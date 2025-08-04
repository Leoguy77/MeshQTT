using MeshQTT.Entities;

namespace MeshQTT.Managers
{
    public static class TopicAccessManager
    {
        /// <summary>
        /// Validates if a user has permission to publish to a specific topic
        /// This method considers user-specific permissions, group permissions, and role permissions
        /// </summary>
        public static bool CanPublish(User user, string topic, Config? config = null)
        {
            // If the user has new-style permissions (roles, groups with TopicPermissions, or user TopicPermissions), use the new system
            if (HasNewStylePermissions(user, config))
            {
                return HasPermission(user, topic, PermissionType.Write, config);
            }
            
            // Fall back to legacy system
            return HasTopicAccess(user.PublishTopicLists, topic);
        }

        /// <summary>
        /// Validates if a user has permission to subscribe to a specific topic
        /// This method considers user-specific permissions, group permissions, and role permissions
        /// </summary>
        public static bool CanSubscribe(User user, string topic, Config? config = null)
        {
            // If the user has new-style permissions (roles, groups with TopicPermissions, or user TopicPermissions), use the new system
            if (HasNewStylePermissions(user, config))
            {
                return HasPermission(user, topic, PermissionType.Read, config);
            }
            
            // Fall back to legacy system
            return HasTopicAccess(user.SubscriptionTopicLists, topic);
        }

        /// <summary>
        /// Determines if a user should use the new permission system
        /// </summary>
        private static bool HasNewStylePermissions(User user, Config? config)
        {
            // User has roles or groups assigned
            if (user.Roles.Count > 0 || user.Groups.Count > 0)
                return true;

            // User has direct TopicPermissions
            if (user.TopicPermissions.Count > 0)
                return true;

            // Check if any assigned groups have TopicPermissions in roles
            if (config != null)
            {
                foreach (var groupName in user.Groups)
                {
                    var group = config.Groups.FirstOrDefault(g => g.Name == groupName && g.Enabled);
                    if (group != null)
                        return true; // Groups are part of new system
                }

                foreach (var roleName in user.Roles)
                {
                    var role = config.Roles.FirstOrDefault(r => r.Name == roleName && r.Enabled);
                    if (role != null)
                        return true; // Roles are part of new system
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a user has a specific permission type for a topic using the new fine-grained system
        /// </summary>
        public static bool HasPermission(User user, string topic, PermissionType requiredPermission, Config? config = null)
        {
            var allPermissions = GetEffectivePermissions(user, config);
            var applicablePermissions = GetApplicablePermissions(allPermissions, topic);
            
            if (applicablePermissions.Count == 0)
            {
                return false;
            }

            // Get the highest priority permission
            var effectivePermission = applicablePermissions
                .OrderByDescending(p => p.Priority)
                .ThenByDescending(p => GetSpecificity(p.TopicPattern))
                .First();

            // Check time restrictions
            if (effectivePermission.TimeRestriction != null && !IsTimeAllowed(effectivePermission.TimeRestriction))
            {
                return false;
            }

            return effectivePermission.Permission.HasFlag(requiredPermission);
        }

        /// <summary>
        /// Gets all effective permissions for a user including inherited permissions from groups and roles
        /// </summary>
        private static List<TopicPermission> GetEffectivePermissions(User user, Config? config)
        {
            var permissions = new List<TopicPermission>();

            if (config == null)
            {
                // Fall back to user-specific permissions only
                permissions.AddRange(user.TopicPermissions);
                return permissions;
            }

            // Add role permissions (lowest priority)
            foreach (var roleName in user.Roles)
            {
                var role = config.Roles.FirstOrDefault(r => r.Name == roleName && r.Enabled);
                if (role != null)
                {
                    permissions.AddRange(GetRolePermissions(role, config, new HashSet<string>()));
                }
            }

            // Add group permissions (medium priority)
            foreach (var groupName in user.Groups)
            {
                var group = config.Groups.FirstOrDefault(g => g.Name == groupName && g.Enabled);
                if (group != null)
                {
                    permissions.AddRange(GetGroupPermissions(group, config, new HashSet<string>()));
                }
            }

            // Add user-specific permissions (highest priority)
            permissions.AddRange(user.TopicPermissions.Select(p => p with { Priority = p.Priority + 1000 }));

            return permissions;
        }

        /// <summary>
        /// Gets permissions for a role including inherited permissions
        /// </summary>
        private static List<TopicPermission> GetRolePermissions(Role role, Config config, HashSet<string> visited)
        {
            var permissions = new List<TopicPermission>();

            if (visited.Contains(role.Name))
            {
                return permissions; // Prevent circular inheritance
            }

            visited.Add(role.Name);

            // Add inherited permissions first (lower priority)
            foreach (var parentRoleName in role.InheritsFrom)
            {
                var parentRole = config.Roles.FirstOrDefault(r => r.Name == parentRoleName && r.Enabled);
                if (parentRole != null)
                {
                    permissions.AddRange(GetRolePermissions(parentRole, config, visited));
                }
            }

            // Add this role's permissions
            permissions.AddRange(role.TopicPermissions);

            visited.Remove(role.Name);
            return permissions;
        }

        /// <summary>
        /// Gets permissions for a group including inherited permissions
        /// </summary>
        private static List<TopicPermission> GetGroupPermissions(Group group, Config config, HashSet<string> visited)
        {
            var permissions = new List<TopicPermission>();

            if (visited.Contains(group.Name))
            {
                return permissions; // Prevent circular inheritance
            }

            visited.Add(group.Name);

            // Add inherited permissions first (lower priority)
            foreach (var parentGroupName in group.InheritsFrom)
            {
                var parentGroup = config.Groups.FirstOrDefault(g => g.Name == parentGroupName && g.Enabled);
                if (parentGroup != null)
                {
                    permissions.AddRange(GetGroupPermissions(parentGroup, config, visited));
                }
            }

            // Convert legacy group permissions to new format
            foreach (var topic in group.SubscriptionTopicLists.WhitelistTopics)
            {
                permissions.Add(new TopicPermission
                {
                    TopicPattern = topic,
                    Permission = PermissionType.Read,
                    Priority = 100
                });
            }

            foreach (var topic in group.PublishTopicLists.WhitelistTopics)
            {
                permissions.Add(new TopicPermission
                {
                    TopicPattern = topic,
                    Permission = PermissionType.Write,
                    Priority = 100
                });
            }

            // Blacklist topics (deny permissions)
            foreach (var topic in group.SubscriptionTopicLists.BlacklistTopics)
            {
                permissions.Add(new TopicPermission
                {
                    TopicPattern = topic,
                    Permission = PermissionType.None,
                    Priority = 200 // Higher priority than whitelist
                });
            }

            foreach (var topic in group.PublishTopicLists.BlacklistTopics)
            {
                permissions.Add(new TopicPermission
                {
                    TopicPattern = topic,
                    Permission = PermissionType.None,
                    Priority = 200 // Higher priority than whitelist
                });
            }

            visited.Remove(group.Name);
            return permissions;
        }

        /// <summary>
        /// Gets all permissions that apply to a specific topic
        /// </summary>
        private static List<TopicPermission> GetApplicablePermissions(List<TopicPermission> permissions, string topic)
        {
            return permissions.Where(p => MatchesTopic(p.TopicPattern, topic)).ToList();
        }

        /// <summary>
        /// Calculates the specificity of a topic pattern (more specific patterns get higher priority)
        /// </summary>
        private static int GetSpecificity(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return 0;

            var parts = pattern.Split('/');
            int specificity = parts.Length * 100;

            // Reduce specificity for wildcards
            specificity -= parts.Count(p => p == "+") * 10;  // Single-level wildcards
            specificity -= parts.Count(p => p == "#") * 50;  // Multi-level wildcards

            return specificity;
        }

        /// <summary>
        /// Checks if the current time is allowed according to time restrictions
        /// </summary>
        private static bool IsTimeAllowed(TimeRestriction restriction)
        {
            var now = DateTime.UtcNow;

            // Check date range
            if (restriction.StartTime.HasValue && now < restriction.StartTime.Value)
                return false;

            if (restriction.EndTime.HasValue && now > restriction.EndTime.Value)
                return false;

            // Check day of week
            if (restriction.AllowedDaysOfWeek.Count > 0)
            {
                var dayOfWeek = (int)now.DayOfWeek;
                if (!restriction.AllowedDaysOfWeek.Contains(dayOfWeek))
                    return false;
            }

            // Check hour ranges
            if (restriction.AllowedHours.Count > 0)
            {
                var currentHour = now.Hour;
                bool hourAllowed = restriction.AllowedHours.Any(range =>
                    currentHour >= range.StartHour && currentHour <= range.EndHour);
                
                if (!hourAllowed)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Legacy method: Checks if a topic matches the access rules defined in TopicTuple
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