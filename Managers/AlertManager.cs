using System.Collections.Concurrent;
using MeshQTT.Entities;
using MeshQTT.Managers.Notifications;

namespace MeshQTT.Managers
{
    public class AlertManager
    {
        private readonly Config _config;
        private readonly Dictionary<string, INotificationProvider> _providers;
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
        private readonly ConcurrentDictionary<string, int> _eventCounts = new();
        private readonly Timer _cleanupTimer;

        // Security event tracking
        private readonly ConcurrentDictionary<string, List<DateTime>> _failedLogins = new();
        private readonly ConcurrentDictionary<string, int> _nodeJoins = new();
        private readonly ConcurrentDictionary<string, int> _nodeLeaves = new();
        private int _messageCount = 0;
        private DateTime _lastMessageReset = DateTime.UtcNow;
        private int _errorCount = 0;
        private DateTime _lastErrorReset = DateTime.UtcNow;

        public AlertManager(Config config)
        {
            _config = config;
            _providers = new Dictionary<string, INotificationProvider>
            {
                { "email", new EmailNotificationProvider() },
                { "discord", new DiscordNotificationProvider() },
                { "slack", new SlackNotificationProvider() },
                { "telegram", new TelegramNotificationProvider() }
            };

            // Cleanup timer to reset counters periodically
            _cleanupTimer = new Timer(CleanupCounters, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            
            Logger.Log($"AlertManager initialized with {_providers.Count} notification providers (Email, Discord, Slack, Telegram)");
        }

        public async Task TriggerFailedLoginAlert(string remoteEndpoint, string username, string reason)
        {
            if (!_config.Alerting.Enabled) return;

            var ip = ExtractIpFromEndpoint(remoteEndpoint);
            var now = DateTime.UtcNow;
            
            // Track failed logins per IP
            if (!_failedLogins.ContainsKey(ip))
                _failedLogins[ip] = new List<DateTime>();
            
            _failedLogins[ip].Add(now);
            
            // Remove old entries (older than 1 hour)
            _failedLogins[ip] = _failedLogins[ip].Where(t => now - t < TimeSpan.FromHours(1)).ToList();
            
            // Check if threshold exceeded
            if (_failedLogins[ip].Count >= _config.Alerting.Security.FailedLoginThreshold)
            {
                var alertEvent = new AlertEvent
                {
                    Type = "security.failed_login_threshold",
                    Title = "Failed Login Threshold Exceeded",
                    Message = $"IP {ip} has exceeded the failed login threshold with {_failedLogins[ip].Count} failed attempts in the last hour.",
                    Severity = AlertSeverity.High,
                    Metadata = new Dictionary<string, object>
                    {
                        { "IP", ip },
                        { "Username", username },
                        { "Reason", reason },
                        { "FailedAttempts", _failedLogins[ip].Count },
                        { "Threshold", _config.Alerting.Security.FailedLoginThreshold }
                    }
                };

                await SendAlert(alertEvent);
            }
        }

        public async Task TriggerNodeBanAlert(string nodeId, string reason)
        {
            if (!_config.Alerting.Enabled || !_config.Alerting.Security.AlertOnNodeBan) return;

            var alertEvent = new AlertEvent
            {
                Type = "security.node_ban",
                Title = "Node Banned",
                Message = $"Node {nodeId} has been banned. Reason: {reason}",
                Severity = AlertSeverity.Medium,
                Metadata = new Dictionary<string, object>
                {
                    { "NodeId", nodeId },
                    { "Reason", reason }
                }
            };

            await SendAlert(alertEvent);
        }

        public async Task TriggerNodeJoinAlert(string nodeId)
        {
            if (!_config.Alerting.Enabled) return;

            var hour = DateTime.UtcNow.ToString("yyyy-MM-dd-HH");
            _nodeJoins[hour] = _nodeJoins.GetValueOrDefault(hour, 0) + 1;

            if (_nodeJoins[hour] >= _config.Alerting.Security.RapidNodeJoinsThreshold)
            {
                var alertEvent = new AlertEvent
                {
                    Type = "security.rapid_node_joins",
                    Title = "Rapid Node Joins Detected",
                    Message = $"Detected {_nodeJoins[hour]} node joins in the current hour, exceeding threshold of {_config.Alerting.Security.RapidNodeJoinsThreshold}.",
                    Severity = AlertSeverity.Medium,
                    Metadata = new Dictionary<string, object>
                    {
                        { "JoinsThisHour", _nodeJoins[hour] },
                        { "Threshold", _config.Alerting.Security.RapidNodeJoinsThreshold },
                        { "LatestNodeId", nodeId }
                    }
                };

                await SendAlert(alertEvent);
            }
        }

        public async Task TriggerNodeLeaveAlert(string nodeId)
        {
            if (!_config.Alerting.Enabled) return;

            var hour = DateTime.UtcNow.ToString("yyyy-MM-dd-HH");
            _nodeLeaves[hour] = _nodeLeaves.GetValueOrDefault(hour, 0) + 1;

            if (_nodeLeaves[hour] >= _config.Alerting.Security.RapidNodeLeavesThreshold)
            {
                var alertEvent = new AlertEvent
                {
                    Type = "security.rapid_node_leaves",
                    Title = "Rapid Node Leaves Detected",
                    Message = $"Detected {_nodeLeaves[hour]} node leaves in the current hour, exceeding threshold of {_config.Alerting.Security.RapidNodeLeavesThreshold}.",
                    Severity = AlertSeverity.Medium,
                    Metadata = new Dictionary<string, object>
                    {
                        { "LeavesThisHour", _nodeLeaves[hour] },
                        { "Threshold", _config.Alerting.Security.RapidNodeLeavesThreshold },
                        { "LatestNodeId", nodeId }
                    }
                };

                await SendAlert(alertEvent);
            }
        }

        public async Task TriggerMessageRateAlert()
        {
            if (!_config.Alerting.Enabled) return;

            _messageCount++;
            var now = DateTime.UtcNow;
            
            // Reset counter every minute
            if (now - _lastMessageReset > TimeSpan.FromMinutes(1))
            {
                if (_messageCount >= _config.Alerting.System.MessageRateThreshold)
                {
                    var alertEvent = new AlertEvent
                    {
                        Type = "system.high_message_rate",
                        Title = "High Message Rate Detected",
                        Message = $"Message rate of {_messageCount} messages per minute exceeds threshold of {_config.Alerting.System.MessageRateThreshold}.",
                        Severity = AlertSeverity.Medium,
                        Metadata = new Dictionary<string, object>
                        {
                            { "MessagesPerMinute", _messageCount },
                            { "Threshold", _config.Alerting.System.MessageRateThreshold }
                        }
                    };

                    await SendAlert(alertEvent);
                }
                
                _messageCount = 0;
                _lastMessageReset = now;
            }
        }

        public async Task TriggerSystemErrorAlert(string errorMessage, Exception? exception = null)
        {
            if (!_config.Alerting.Enabled || !_config.Alerting.System.AlertOnSystemErrors) return;

            _errorCount++;
            var now = DateTime.UtcNow;
            
            // Check if we need to alert on error rate
            if (now - _lastErrorReset > TimeSpan.FromHours(1))
            {
                if (_errorCount >= _config.Alerting.System.ErrorRateThreshold)
                {
                    var alertEvent = new AlertEvent
                    {
                        Type = "system.high_error_rate",
                        Title = "High Error Rate Detected",
                        Message = $"Error rate of {_errorCount} errors per hour exceeds threshold of {_config.Alerting.System.ErrorRateThreshold}.",
                        Severity = AlertSeverity.High,
                        Metadata = new Dictionary<string, object>
                        {
                            { "ErrorsPerHour", _errorCount },
                            { "Threshold", _config.Alerting.System.ErrorRateThreshold },
                            { "LatestError", errorMessage }
                        }
                    };

                    await SendAlert(alertEvent);
                }
                
                _errorCount = 0;
                _lastErrorReset = now;
            }
            
            // Also send individual error alert for critical errors
            var individualAlert = new AlertEvent
            {
                Type = "system.error",
                Title = "System Error Occurred",
                Message = errorMessage,
                Severity = AlertSeverity.Low,
                Metadata = new Dictionary<string, object>
                {
                    { "Error", errorMessage },
                    { "Exception", exception?.ToString() ?? "None" }
                }
            };

            await SendAlert(individualAlert);
        }

        public async Task TriggerServiceRestartAlert(string reason)
        {
            if (!_config.Alerting.Enabled || !_config.Alerting.System.AlertOnServiceRestart) return;

            var alertEvent = new AlertEvent
            {
                Type = "system.service_restart",
                Title = "Service Restart",
                Message = $"MeshQTT service has been restarted. Reason: {reason}",
                Severity = AlertSeverity.Medium,
                Metadata = new Dictionary<string, object>
                {
                    { "Reason", reason },
                    { "RestartTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
                }
            };

            await SendAlert(alertEvent);
        }

        private async Task SendAlert(AlertEvent alertEvent)
        {
            // Rate limiting - don't send same alert type more than once per 5 minutes
            var rateLimitKey = $"{alertEvent.Type}";
            if (_lastAlertTimes.ContainsKey(rateLimitKey) && 
                DateTime.UtcNow - _lastAlertTimes[rateLimitKey] < TimeSpan.FromMinutes(5))
            {
                return;
            }
            
            _lastAlertTimes[rateLimitKey] = DateTime.UtcNow;

            Logger.Log($"Triggering alert: {alertEvent.Type} - {alertEvent.Title}");

            var tasks = new List<Task>();
            
            foreach (var providerConfig in _config.Alerting.Providers.Where(p => p.Enabled))
            {
                if (_providers.TryGetValue(providerConfig.Type.ToLower(), out var provider))
                {
                    if (provider.ValidateConfig(providerConfig.Config))
                    {
                        tasks.Add(provider.SendNotificationAsync(alertEvent, providerConfig.Config));
                    }
                    else
                    {
                        Logger.Log($"Invalid configuration for {providerConfig.Type} notification provider");
                    }
                }
                else
                {
                    Logger.Log($"Unknown notification provider type: {providerConfig.Type}");
                }
            }

            await Task.WhenAll(tasks);
        }

        private string ExtractIpFromEndpoint(string endpoint)
        {
            // Extract IP from endpoint like "192.168.1.1:12345"
            var parts = endpoint.Split(':');
            return parts.Length > 0 ? parts[0] : endpoint;
        }

        private void CleanupCounters(object? state)
        {
            var now = DateTime.UtcNow;
            var currentHour = now.ToString("yyyy-MM-dd-HH");
            
            // Clean up old failed login entries
            foreach (var kvp in _failedLogins.ToList())
            {
                var recentLogins = kvp.Value.Where(t => now - t < TimeSpan.FromHours(1)).ToList();
                if (recentLogins.Any())
                    _failedLogins[kvp.Key] = recentLogins;
                else
                    _failedLogins.TryRemove(kvp.Key, out _);
            }
            
            // Clean up old node join/leave counters (keep current and previous hour)
            var previousHour = now.AddHours(-1).ToString("yyyy-MM-dd-HH");
            foreach (var key in _nodeJoins.Keys.ToList())
            {
                if (key != currentHour && key != previousHour)
                    _nodeJoins.TryRemove(key, out _);
            }
            
            foreach (var key in _nodeLeaves.Keys.ToList())
            {
                if (key != currentHour && key != previousHour)
                    _nodeLeaves.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}
