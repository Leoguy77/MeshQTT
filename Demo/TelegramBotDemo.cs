using System;
using System.Threading.Tasks;
using MeshQTT.Entities;
using MeshQTT.Managers;

namespace MeshQTT.Demo
{
    /// <summary>
    /// Demo class to showcase Telegram Bot Manager functionality
    /// This shows how the bot would handle various commands
    /// </summary>
    public class TelegramBotDemo
    {
        public static void ShowBotCommands()
        {
            Console.WriteLine("=== MeshQTT Telegram Bot Commands Demo ===");
            Console.WriteLine();
            
            Console.WriteLine("📱 Available Commands:");
            Console.WriteLine();
            
            Console.WriteLine("ℹ️ Information Commands:");
            Console.WriteLine("  /help      - Show this help message");
            Console.WriteLine("  /status    - Display server status and statistics");
            Console.WriteLine("  /nodes     - List connected nodes (active/inactive)");
            Console.WriteLine("  /banlist   - Show currently banned nodes");
            Console.WriteLine();
            
            Console.WriteLine("🔨 Node Management Commands:");
            Console.WriteLine("  /ban <nodeId> [reason]    - Ban a node from the network");
            Console.WriteLine("  /unban <nodeId>           - Remove a node from banlist");
            Console.WriteLine();
            
            Console.WriteLine("📋 Example Usage:");
            Console.WriteLine();
            
            ShowExampleInteraction();
        }
        
        private static void ShowExampleInteraction()
        {
            Console.WriteLine("User: /status");
            Console.WriteLine("Bot: 📊 MeshQTT Server Status");
            Console.WriteLine("     🟢 Server: Running");
            Console.WriteLine("     📡 Connected Nodes: 15");
            Console.WriteLine("     🚫 Banned Nodes: 1");
            Console.WriteLine("     ⏰ Uptime: 01:23:45");
            Console.WriteLine("     📋 MQTT Port: 1883");
            Console.WriteLine();
            
            Console.WriteLine("User: /nodes");
            Console.WriteLine("Bot: 📡 Connected Nodes");
            Console.WriteLine("     Total: 15 | Active (last 30m): 12");
            Console.WriteLine("     🟢 !87654321 - 14:32:15");
            Console.WriteLine("     🟢 !11111111 - 14:31:45");
            Console.WriteLine("     🔴 !22222222 - 13:15:30");
            Console.WriteLine("     ...");
            Console.WriteLine();
            
            Console.WriteLine("User: /ban !deadbeef Suspicious activity");
            Console.WriteLine("Bot: ✅ Node !deadbeef has been banned.");
            Console.WriteLine("     Reason: Suspicious activity");
            Console.WriteLine();
            
            Console.WriteLine("User: /banlist");
            Console.WriteLine("Bot: 🚫 Banned Nodes");
            Console.WriteLine("     1. !deadbeef");
            Console.WriteLine("     2. !badactor");
            Console.WriteLine();
            
            Console.WriteLine("User: /unban !deadbeef");
            Console.WriteLine("Bot: ✅ Node !deadbeef has been unbanned.");
            Console.WriteLine();
        }
        
        public static void ShowConfiguration()
        {
            Console.WriteLine("=== Configuration Example ===");
            Console.WriteLine();
            Console.WriteLine("Add this to your config.json:");
            Console.WriteLine();
            Console.WriteLine(@"{
  ""TelegramBot"": {
    ""Enabled"": true,
    ""BotToken"": ""123456789:ABCdefGHIjklMNOpqrsTUVwxyz"",
    ""AuthorizedUsers"": [123456789, 987654321],
    ""AuthorizedChats"": [-100123456789],
    ""RequireAuthentication"": true,
    ""CommandPrefix"": ""/""
  }
}");
            Console.WriteLine();
            Console.WriteLine("📝 Setup Instructions:");
            Console.WriteLine("1. Message @BotFather on Telegram to create a bot");
            Console.WriteLine("2. Get your bot token and add it to BotToken field");
            Console.WriteLine("3. Get your user ID and add to AuthorizedUsers");
            Console.WriteLine("4. For groups, get chat ID and add to AuthorizedChats");
            Console.WriteLine("5. Set Enabled to true and restart MeshQTT");
        }
    }
}