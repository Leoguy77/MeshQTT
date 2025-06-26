using System.Text;
using System.Text.Json;
using MeshQTT.Entities;

namespace MeshQTT.Managers.Notifications
{
    public class DiscordNotificationProvider : INotificationProvider
    {
        private static readonly HttpClient HttpClient = new();
        
        public string ProviderType => "discord";

        public async Task<bool> SendNotificationAsync(AlertEvent alertEvent, Dictionary<string, string> config)
        {
            try
            {
                var webhookUrl = config.GetValueOrDefault("WebhookUrl", "");
                var username = config.GetValueOrDefault("Username", "MeshQTT");

                var embed = new
                {
                    title = $"{alertEvent.Severity} Alert - {alertEvent.Title}",
                    description = alertEvent.Message,
                    color = GetColorForSeverity(alertEvent.Severity),
                    timestamp = alertEvent.Timestamp.ToString("o"),
                    fields = new[]
                    {
                        new { name = "Event Type", value = alertEvent.Type, inline = true },
                        new { name = "Severity", value = alertEvent.Severity.ToString(), inline = true },
                        new { name = "Event ID", value = alertEvent.Id, inline = true }
                    }.Concat(alertEvent.Metadata.Select(kvp => new { name = kvp.Key, value = kvp.Value?.ToString() ?? "", inline = true })).ToArray(),
                    footer = new { text = "MeshQTT Alert System" }
                };

                var payload = new
                {
                    username = username,
                    embeds = new[] { embed }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(webhookUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"Discord alert sent successfully for event: {alertEvent.Id}");
                    return true;
                }
                else
                {
                    Logger.Log($"Failed to send Discord alert. Status: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send Discord alert: {ex.Message}");
                return false;
            }
        }

        public bool ValidateConfig(Dictionary<string, string> config)
        {
            return config.ContainsKey("WebhookUrl") && !string.IsNullOrEmpty(config["WebhookUrl"]);
        }

        private int GetColorForSeverity(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Low => 0x00FF00,      // Green
                AlertSeverity.Medium => 0xFFFF00,   // Yellow
                AlertSeverity.High => 0xFF8000,     // Orange
                AlertSeverity.Critical => 0xFF0000, // Red
                _ => 0x808080                       // Gray
            };
        }
    }
}
