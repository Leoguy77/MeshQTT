using System.Text;
using System.Text.Json;
using MeshQTT.Entities;

namespace MeshQTT.Managers.Notifications
{
    public class TelegramNotificationProvider : INotificationProvider
    {
        private static readonly HttpClient HttpClient = new();

        public string ProviderType => "telegram";

        public async Task<bool> SendNotificationAsync(
            AlertEvent alertEvent,
            Dictionary<string, string> config
        )
        {
            try
            {
                var botToken = config.GetValueOrDefault("BotToken", "");
                var chatId = config.GetValueOrDefault("ChatId", "");
                var disableNotification = bool.Parse(
                    config.GetValueOrDefault("DisableNotification", "false")
                );

                var message = BuildTelegramMessage(alertEvent);

                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML",
                    disable_notification = disableNotification,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
                var response = await HttpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    Logger.Log($"Telegram alert sent successfully for event: {alertEvent.Id}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.Log(
                        $"Failed to send Telegram alert. Status: {response.StatusCode}, Error: {errorContent}"
                    );
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send Telegram alert: {ex.Message}");
                return false;
            }
        }

        public bool ValidateConfig(Dictionary<string, string> config)
        {
            return config.ContainsKey("BotToken")
                && !string.IsNullOrEmpty(config["BotToken"])
                && config.ContainsKey("ChatId")
                && !string.IsNullOrEmpty(config["ChatId"]);
        }

        private string BuildTelegramMessage(AlertEvent alertEvent)
        {
            var sb = new StringBuilder();

            // Add severity emoji
            var severityEmoji = alertEvent.Severity switch
            {
                AlertSeverity.Low => "🟢",
                AlertSeverity.Medium => "🟡",
                AlertSeverity.High => "🟠",
                AlertSeverity.Critical => "🔴",
                _ => "⚪",
            };

            // Add alert type emoji
            var typeEmoji = alertEvent.Type switch
            {
                var t when t.StartsWith("security.") => "🔒",
                var t when t.StartsWith("system.") => "⚙️",
                _ => "📢",
            };

            sb.AppendLine($"{severityEmoji} {typeEmoji} <b>MeshQTT Alert</b>");
            sb.AppendLine();
            sb.AppendLine($"<b>Severity:</b> {alertEvent.Severity}");
            sb.AppendLine($"<b>Type:</b> {alertEvent.Type}");
            sb.AppendLine($"<b>Title:</b> {alertEvent.Title}");
            sb.AppendLine();
            sb.AppendLine($"<b>Message:</b>");
            sb.AppendLine(alertEvent.Message);
            sb.AppendLine();
            sb.AppendLine($"<b>Time:</b> {alertEvent.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");

            if (alertEvent.Metadata.Any())
            {
                sb.AppendLine();
                sb.AppendLine("<b>Additional Details:</b>");
                foreach (var item in alertEvent.Metadata)
                {
                    sb.AppendLine($"• <b>{item.Key}:</b> {item.Value}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"<i>Event ID: {alertEvent.Id}</i>");

            return sb.ToString();
        }
    }
}
