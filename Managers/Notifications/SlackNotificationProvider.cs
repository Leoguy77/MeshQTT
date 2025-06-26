using System.Text;
using System.Text.Json;
using MeshQTT.Entities;

namespace MeshQTT.Managers.Notifications
{
    public class SlackNotificationProvider : INotificationProvider
    {
        private static readonly HttpClient HttpClient = new();
        
        public string ProviderType => "slack";

        public async Task<bool> SendNotificationAsync(AlertEvent alertEvent, Dictionary<string, string> config)
        {
            try
            {
                var webhookUrl = config.GetValueOrDefault("WebhookUrl", "");
                var channel = config.GetValueOrDefault("Channel", "#alerts");
                var username = config.GetValueOrDefault("Username", "MeshQTT");

                var attachment = new
                {
                    color = GetColorForSeverity(alertEvent.Severity),
                    title = $"{alertEvent.Severity} Alert - {alertEvent.Title}",
                    text = alertEvent.Message,
                    fields = new[]
                    {
                        new { title = "Event Type", value = alertEvent.Type, @short = true },
                        new { title = "Severity", value = alertEvent.Severity.ToString(), @short = true },
                        new { title = "Time", value = alertEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                        new { title = "Event ID", value = alertEvent.Id, @short = true }
                    }.Concat(alertEvent.Metadata.Select(kvp => new { title = kvp.Key, value = kvp.Value?.ToString() ?? "", @short = true })).ToArray(),
                    footer = "MeshQTT Alert System",
                    ts = ((DateTimeOffset)alertEvent.Timestamp).ToUnixTimeSeconds()
                };

                var payload = new
                {
                    channel = channel,
                    username = username,
                    attachments = new[] { attachment }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(webhookUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"Slack alert sent successfully for event: {alertEvent.Id}");
                    return true;
                }
                else
                {
                    Logger.Log($"Failed to send Slack alert. Status: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send Slack alert: {ex.Message}");
                return false;
            }
        }

        public bool ValidateConfig(Dictionary<string, string> config)
        {
            return config.ContainsKey("WebhookUrl") && !string.IsNullOrEmpty(config["WebhookUrl"]);
        }

        private string GetColorForSeverity(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Low => "good",
                AlertSeverity.Medium => "warning",
                AlertSeverity.High => "warning",
                AlertSeverity.Critical => "danger",
                _ => "#808080"
            };
        }
    }
}
