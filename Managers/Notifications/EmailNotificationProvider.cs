using System.Net;
using System.Net.Mail;
using System.Text;
using MeshQTT.Entities;

namespace MeshQTT.Managers.Notifications
{
    public class EmailNotificationProvider : INotificationProvider
    {
        public string ProviderType => "email";

        public async Task<bool> SendNotificationAsync(AlertEvent alertEvent, Dictionary<string, string> config)
        {
            try
            {
                var smtpHost = config.GetValueOrDefault("SmtpHost", "");
                var smtpPort = int.Parse(config.GetValueOrDefault("SmtpPort", "587"));
                var username = config.GetValueOrDefault("Username", "");
                var password = config.GetValueOrDefault("Password", "");
                var fromEmail = config.GetValueOrDefault("FromEmail", "");
                var toEmail = config.GetValueOrDefault("ToEmail", "");
                var enableSsl = bool.Parse(config.GetValueOrDefault("EnableSsl", "true"));

                using var client = new SmtpClient(smtpHost, smtpPort);
                client.EnableSsl = enableSsl;
                client.Credentials = new NetworkCredential(username, password);

                var subject = $"[MeshQTT Alert] {alertEvent.Severity} - {alertEvent.Title}";
                var body = BuildEmailBody(alertEvent);

                var mailMessage = new MailMessage(fromEmail, toEmail, subject, body);
                mailMessage.IsBodyHtml = true;

                await client.SendMailAsync(mailMessage);
                Logger.Log($"Email alert sent successfully for event: {alertEvent.Id}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send email alert: {ex.Message}");
                return false;
            }
        }

        public bool ValidateConfig(Dictionary<string, string> config)
        {
            var requiredKeys = new[] { "SmtpHost", "SmtpPort", "Username", "Password", "FromEmail", "ToEmail" };
            return requiredKeys.All(key => config.ContainsKey(key) && !string.IsNullOrEmpty(config[key]));
        }

        private string BuildEmailBody(AlertEvent alertEvent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><body>");
            sb.AppendLine($"<h2>MeshQTT Alert - {alertEvent.Severity}</h2>");
            sb.AppendLine($"<p><strong>Time:</strong> {alertEvent.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine($"<p><strong>Event Type:</strong> {alertEvent.Type}</p>");
            sb.AppendLine($"<p><strong>Title:</strong> {alertEvent.Title}</p>");
            sb.AppendLine($"<p><strong>Message:</strong> {alertEvent.Message}</p>");
            
            if (alertEvent.Metadata.Any())
            {
                sb.AppendLine("<h3>Additional Details:</h3>");
                sb.AppendLine("<ul>");
                foreach (var item in alertEvent.Metadata)
                {
                    sb.AppendLine($"<li><strong>{item.Key}:</strong> {item.Value}</li>");
                }
                sb.AppendLine("</ul>");
            }
            
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
