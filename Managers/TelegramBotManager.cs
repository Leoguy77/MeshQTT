using System.Text;
using MeshQTT.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MeshQTT.Managers
{
    public class TelegramBotManager
    {
        private readonly Config _config;
        private readonly List<Node> _nodes;
        private readonly AlertManager? _alertManager;
        private TelegramBotClient? _botClient;
        private CancellationTokenSource? _cancellationTokenSource;

        public TelegramBotManager(Config config, List<Node> nodes, AlertManager? alertManager = null)
        {
            _config = config;
            _nodes = nodes;
            _alertManager = alertManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!_config.TelegramBot.Enabled || string.IsNullOrEmpty(_config.TelegramBot.BotToken))
            {
                Logger.Log("Telegram bot is disabled or not configured.");
                return;
            }

            _botClient = new TelegramBotClient(_config.TelegramBot.BotToken);
            _cancellationTokenSource = new CancellationTokenSource();

            // Create a combined cancellation token
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);

            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };

            try
            {
                // Test the bot token
                var me = await _botClient.GetMe(combinedTokenSource.Token);
                Logger.Log($"Telegram bot started successfully: @{me.Username}");

                // Start receiving updates
                _botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: combinedTokenSource.Token
                );

                Logger.Log("Telegram bot is receiving updates...");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start Telegram bot: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_cancellationTokenSource != null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_botClient != null)
            {
                Logger.Log("Telegram bot stopped.");
                _botClient = null;
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Only handle message updates
            if (update.Message is not { } message)
                return;

            // Only handle text messages
            if (message.Text is not { } messageText)
                return;

            var chatId = message.Chat.Id;
            var userId = message.From?.Id ?? 0;
            var username = message.From?.Username ?? "Unknown";

            Logger.Log($"Received message from {username} ({userId}) in chat {chatId}: {messageText}");

            // Check authentication
            if (!IsAuthorized(userId, chatId))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ You are not authorized to use this bot.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Handle commands
            if (messageText.StartsWith(_config.TelegramBot.CommandPrefix))
            {
                await HandleCommandAsync(botClient, message, cancellationToken);
            }
        }

        private async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text ?? "";
            var commandParts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = commandParts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "/start":
                    case "/help":
                        await SendHelpMessage(botClient, chatId, cancellationToken);
                        break;

                    case "/status":
                        await SendStatusMessage(botClient, chatId, cancellationToken);
                        break;

                    case "/ban":
                        await HandleBanCommand(botClient, chatId, commandParts, cancellationToken);
                        break;

                    case "/unban":
                        await HandleUnbanCommand(botClient, chatId, commandParts, cancellationToken);
                        break;

                    case "/banlist":
                        await SendBanlistMessage(botClient, chatId, cancellationToken);
                        break;

                    case "/nodes":
                        await SendNodesMessage(botClient, chatId, cancellationToken);
                        break;

                    default:
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"❓ Unknown command: {command}\nUse /help to see available commands.",
                            cancellationToken: cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling command {command}: {ex.Message}");
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ An error occurred while processing your command.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SendHelpMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var helpText = new StringBuilder();
            helpText.AppendLine("🤖 <b>MeshQTT Bot Commands</b>");
            helpText.AppendLine();
            helpText.AppendLine("📊 <b>Information:</b>");
            helpText.AppendLine("/status - Show server status");
            helpText.AppendLine("/nodes - List connected nodes");
            helpText.AppendLine("/banlist - Show banned nodes");
            helpText.AppendLine();
            helpText.AppendLine("🔨 <b>Node Management:</b>");
            helpText.AppendLine("/ban &lt;nodeId&gt; [reason] - Ban a node");
            helpText.AppendLine("/unban &lt;nodeId&gt; - Unban a node");
            helpText.AppendLine();
            helpText.AppendLine("ℹ️ <b>General:</b>");
            helpText.AppendLine("/help - Show this help message");
            helpText.AppendLine();
            helpText.AppendLine("<i>Note: Node IDs should be in hexadecimal format (e.g., !12345678)</i>");

            await botClient.SendMessage(
                chatId: chatId,
                text: helpText.ToString(),
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private async Task SendStatusMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var status = new StringBuilder();
            status.AppendLine("📊 <b>MeshQTT Server Status</b>");
            status.AppendLine();
            status.AppendLine($"🟢 Server: Running");
            status.AppendLine($"📡 Connected Nodes: {_nodes.Count}");
            status.AppendLine($"🚫 Banned Nodes: {_config.Banlist.Count}");
            status.AppendLine($"⏰ Uptime: {DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime:hh\\:mm\\:ss}");
            status.AppendLine();
            status.AppendLine($"📋 MQTT Port: {_config.Port}");
            if (_config.TlsEnabled)
            {
                status.AppendLine($"🔒 TLS Port: {_config.TlsPort}");
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: status.ToString(),
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private async Task HandleBanCommand(ITelegramBotClient botClient, long chatId, string[] commandParts, CancellationToken cancellationToken)
        {
            if (commandParts.Length < 2)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Usage: /ban &lt;nodeId&gt; [reason]",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                return;
            }

            var nodeId = commandParts[1];
            var reason = commandParts.Length > 2 ? string.Join(" ", commandParts.Skip(2)) : "Banned via Telegram bot";

            // Validate nodeId format (optional - you may want to add specific validation)
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Invalid node ID provided.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Check if already banned
            if (_config.Banlist.Contains(nodeId))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"⚠️ Node {nodeId} is already banned.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Add to banlist
            _config.Banlist.Add(nodeId);

            // Save configuration (trigger file watcher to reload)
            await SaveConfigurationAsync();

            // Send alert if available
            if (_alertManager != null)
            {
                await _alertManager.TriggerNodeBanAlert(nodeId, reason);
            }

            Logger.Log($"Node {nodeId} banned via Telegram bot. Reason: {reason}");

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Node <code>{nodeId}</code> has been banned.\nReason: {reason}",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private async Task HandleUnbanCommand(ITelegramBotClient botClient, long chatId, string[] commandParts, CancellationToken cancellationToken)
        {
            if (commandParts.Length < 2)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Usage: /unban &lt;nodeId&gt;",
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
                return;
            }

            var nodeId = commandParts[1];

            // Check if node is banned
            if (!_config.Banlist.Contains(nodeId))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"⚠️ Node {nodeId} is not in the banlist.",
                    cancellationToken: cancellationToken);
                return;
            }

            // Remove from banlist
            _config.Banlist.Remove(nodeId);

            // Save configuration
            await SaveConfigurationAsync();

            Logger.Log($"Node {nodeId} unbanned via Telegram bot.");

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Node <code>{nodeId}</code> has been unbanned.",
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private async Task SendBanlistMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var banlist = new StringBuilder();
            banlist.AppendLine("🚫 <b>Banned Nodes</b>");
            banlist.AppendLine();

            if (_config.Banlist.Count == 0)
            {
                banlist.AppendLine("No nodes are currently banned.");
            }
            else
            {
                for (int i = 0; i < _config.Banlist.Count; i++)
                {
                    banlist.AppendLine($"{i + 1}. <code>{_config.Banlist[i]}</code>");
                }
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: banlist.ToString(),
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private async Task SendNodesMessage(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var nodes = new StringBuilder();
            nodes.AppendLine("📡 <b>Connected Nodes</b>");
            nodes.AppendLine();

            if (_nodes.Count == 0)
            {
                nodes.AppendLine("No nodes are currently connected.");
            }
            else
            {
                var activeNodes = _nodes.Where(n => n.LastUpdate > DateTime.Now.AddMinutes(-30)).ToList();
                var totalNodes = _nodes.Count;

                nodes.AppendLine($"Total: {totalNodes} | Active (last 30m): {activeNodes.Count}");
                nodes.AppendLine();

                // Show up to 20 most recent nodes to avoid message length limits
                var recentNodes = _nodes.OrderByDescending(n => n.LastUpdate).Take(20);
                foreach (var node in recentNodes)
                {
                    var status = node.LastUpdate > DateTime.Now.AddMinutes(-30) ? "🟢" : "🔴";
                    var lastSeen = node.LastUpdate.ToString("HH:mm:ss");
                    nodes.AppendLine($"{status} <code>{node.NodeID}</code> - {lastSeen}");
                }

                if (totalNodes > 20)
                {
                    nodes.AppendLine();
                    nodes.AppendLine($"<i>... and {totalNodes - 20} more nodes</i>");
                }
            }

            await botClient.SendMessage(
                chatId: chatId,
                text: nodes.ToString(),
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }

        private bool IsAuthorized(long userId, long chatId)
        {
            if (!_config.TelegramBot.RequireAuthentication)
                return true;

            // Check if user is authorized
            if (_config.TelegramBot.AuthorizedUsers.Contains(userId))
                return true;

            // Check if chat is authorized
            if (_config.TelegramBot.AuthorizedChats.Contains(chatId))
                return true;

            return false;
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "config", "config.json");
                var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await System.IO.File.WriteAllTextAsync(configPath, json);
                Logger.Log("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save configuration: {ex.Message}");
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Logger.Log($"Telegram bot polling error: {errorMessage}");
            return Task.CompletedTask;
        }
    }
}