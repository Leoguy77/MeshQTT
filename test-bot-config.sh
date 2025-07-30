#!/bin/bash

# Simple test script to validate MeshQTT Telegram Bot configuration

echo "=== MeshQTT Telegram Bot Configuration Test ==="
echo

# Check if config.json exists
if [ ! -f "config/config.json" ]; then
    echo "❌ config/config.json not found. Please create it from config.json.example"
    exit 1
fi

echo "✅ Configuration file found"

# Check if TelegramBot section exists in config
if grep -q '"TelegramBot"' config/config.json; then
    echo "✅ TelegramBot configuration section found"
    
    # Check if bot is enabled
    if grep -q '"Enabled": true' config/config.json; then
        echo "✅ Telegram Bot is enabled"
        
        # Check if bot token is configured
        if grep -q '"BotToken": ""' config/config.json || ! grep -q '"BotToken":' config/config.json; then
            echo "⚠️  Bot token is not configured. Please add your bot token from @BotFather"
        else
            echo "✅ Bot token appears to be configured"
        fi
        
        # Check if authorized users are configured
        if grep -q '"AuthorizedUsers": \[\]' config/config.json; then
            echo "⚠️  No authorized users configured. Add user IDs to AuthorizedUsers array"
        else
            echo "✅ Authorized users are configured"
        fi
        
    else
        echo "⚠️  Telegram Bot is disabled. Set 'Enabled': true to activate"
    fi
    
else
    echo "❌ TelegramBot configuration section not found"
    echo "Please add the TelegramBot section to your config.json:"
    echo '{
  "TelegramBot": {
    "Enabled": true,
    "BotToken": "your-bot-token-here",
    "AuthorizedUsers": [your-user-id],
    "AuthorizedChats": [],
    "RequireAuthentication": true,
    "CommandPrefix": "/"
  }
}'
fi

echo
echo "=== Test Commands ==="
echo "1. Build the project: dotnet build"
echo "2. Run the server: dotnet run"
echo "3. Test bot commands in Telegram:"
echo "   - /help (show available commands)"
echo "   - /status (server status)"
echo "   - /nodes (list nodes)"
echo "   - /banlist (show banned nodes)"
echo "   - /ban <nodeId> [reason] (ban a node)"
echo "   - /unban <nodeId> (unban a node)"
echo

echo "=== Getting Your Telegram IDs ==="
echo "1. Send a message to your bot"
echo "2. Visit: https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates"
echo "3. Find your user ID in the 'from' field"
echo "4. For groups, find the chat ID in the 'chat' field (negative number)"