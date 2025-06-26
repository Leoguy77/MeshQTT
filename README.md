# MeshQTT

MeshQTT is a MQTT broker specifically designed for meshtastic networks. It handels encrypted Meshtastic packets and allows for more more specific control compared to normal MQTT brokers like Mosquitto.
It supports advanced filtering and processing of messages, making it ideal for applications that require fine-grained control over the data being transmitted in a meshtastic network.

# Features

- Handles encrypted Meshtastic packets
- Allows for more specific control compared to normal MQTT brokers
- Supports advanced filtering and processing of messages
- **TLS/SSL Support** - Secure MQTT connections with automatic certificate generation
- **Automated Alerting** - Comprehensive security and system monitoring with multi-channel notifications

## Automated Alerting System

MeshQTT includes a comprehensive alerting system that can notify administrators of security threats and system issues through multiple channels including email, Discord, Slack, and Telegram.

### Alert Types

#### Security Alerts
- **Failed Login Attempts**: Detects and alerts on repeated failed login attempts from the same IP
- **Node Bans**: Immediate notifications when nodes are banned from the network
- **Rapid Node Activity**: Alerts on unusual node join/leave patterns that might indicate network issues or attacks

#### System Alerts  
- **High Message Rates**: Monitors message throughput and alerts on abnormal traffic
- **System Errors**: Automatic notification of application errors and exceptions
- **Service Restarts**: Notifications when the service starts, stops, or restarts
- **High Error Rates**: Alerts when error frequency exceeds normal thresholds

### Notification Channels

#### Email Notifications
- Full HTML email alerts with detailed event information
- Support for SMTP with TLS/SSL
- Configurable sender and recipient addresses

#### Discord Integration
- Rich embed messages with color-coded severity levels
- Webhook-based integration (no bot required)
- Customizable bot username and appearance

#### Slack Integration  
- Native Slack attachments with proper formatting
- Channel-specific notifications
- Color-coded severity indicators

#### Telegram Integration
- Rich HTML-formatted messages with emojis
- Bot-based integration with flexible chat targeting
- Support for both private chats and groups
- Optional silent notifications

### Configuration

Enable alerting by adding the `Alerting` section to your `config.json`:

```json
{
  "Alerting": {
    "Enabled": true,
    "Providers": [
      {
        "Type": "email",
        "Enabled": true,
        "Config": {
          "SmtpHost": "smtp.gmail.com",
          "SmtpPort": "587", 
          "Username": "alerts@yourdomain.com",
          "Password": "your-app-password",
          "FromEmail": "alerts@yourdomain.com",
          "ToEmail": "admin@yourdomain.com",
          "EnableSsl": "true"
        }
      },
      {
        "Type": "discord",
        "Enabled": true,
        "Config": {
          "WebhookUrl": "https://discord.com/api/webhooks/123456789/abcdefghijk",
          "Username": "MeshQTT Security"
        }
      },
      {
        "Type": "telegram",
        "Enabled": true,
        "Config": {
          "BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
          "ChatId": "-123456789",
          "DisableNotification": "false"
        }
      }
    ],
    "Security": {
      "FailedLoginThreshold": 5,
      "RapidNodeJoinsThreshold": 50, 
      "RapidNodeLeavesThreshold": 50,
      "AlertOnNodeBan": true
    },
    "System": {
      "MessageRateThreshold": 1000,
      "AlertOnServiceRestart": true,
      "AlertOnSystemErrors": true,
      "ErrorRateThreshold": 10
    }
  }
}
```

### Alert Thresholds

#### Security Thresholds
- `FailedLoginThreshold`: Maximum failed login attempts per IP per hour (default: 5)
- `RapidNodeJoinsThreshold`: Maximum node joins per hour before alerting (default: 50)  
- `RapidNodeLeavesThreshold`: Maximum node leaves per hour before alerting (default: 50)
- `AlertOnNodeBan`: Whether to send immediate alerts for node bans (default: true)

#### System Thresholds
- `MessageRateThreshold`: Maximum messages per minute before alerting (default: 1000)
- `AlertOnServiceRestart`: Alert when service starts/restarts (default: true)
- `AlertOnSystemErrors`: Alert on individual system errors (default: true)  
- `ErrorRateThreshold`: Maximum errors per hour before rate alerting (default: 10)

### Setting Up Notification Providers

#### Email Setup (Gmail Example)
1. Enable 2-factor authentication on your Gmail account
2. Generate an App Password (not your regular password)
3. Configure the email provider with your SMTP settings

#### Discord Setup
1. Create a Discord webhook in your server settings
2. Copy the webhook URL to the configuration
3. Optionally customize the bot username

#### Slack Setup  
1. Create a Slack webhook in your workspace
2. Choose the channel for notifications
3. Copy the webhook URL to the configuration

#### Telegram Setup
1. Create a Telegram bot by messaging @BotFather
2. Get your bot token from BotFather
3. Add the bot to your chat/group and get the Chat ID
4. Configure the bot token and chat ID in the settings

##### Getting Your Telegram Chat ID
To find your Chat ID:
- **For private chats**: Send a message to your bot, then visit `https://api.telegram.org/bot<YourBotToken>/getUpdates` to see the chat ID
- **For groups**: Add your bot to the group, send a message mentioning the bot, then check the same URL for the group's chat ID (will be negative)
- **Using @userinfobot**: Forward a message from your chat/group to @userinfobot to get the ID

##### Telegram Configuration Options
- `BotToken`: Your bot token from @BotFather (required)
- `ChatId`: The chat ID where alerts should be sent (required)
- `DisableNotification`: Set to `true` for silent notifications (optional, default: false)

### Rate Limiting

The alerting system includes built-in rate limiting to prevent notification spam:
- Same alert types are limited to once per 5 minutes
- Failed login tracking uses sliding windows
- Counters are automatically cleaned up to prevent memory leaks

### Testing Alerts

To test your alerting configuration:
1. Enable alerting with your desired providers (email, Discord, Slack, or Telegram)
2. Restart the service (should trigger a service restart alert)  
3. Try failed logins to trigger security alerts
4. Monitor logs for alert delivery confirmation

## TLS Configuration

MeshQTT supports TLS/SSL encryption for secure MQTT connections. The TLS configuration is handled through the `config.json` file.

### Configuration Options

```json
{
  "TlsEnabled": true,
  "TlsPort": 8883,
  "CertificatePath": "certs/server.crt",
  "PrivateKeyPath": "certs/server.key",
  "CertificatePassword": ""
}
```

- `TlsEnabled`: Set to `true` to enable TLS support
- `TlsPort`: Port for TLS connections (default: 8883)
- `CertificatePath`: Path to your certificate file (.crt or .pem)
- `PrivateKeyPath`: Path to your private key file (.key or .pem)
- `CertificatePassword`: Password for the certificate (if required)

### Certificate Support

MeshQTT supports the following certificate formats:

- **PEM files** (.pem) - Both certificate and key in PEM format
- **CRT files** (.crt) - Certificate with separate .key file
- **PFX/P12 files** (.pfx, .p12) - PKCS#12 format with embedded private key

### Automatic Self-Signed Certificates

If no valid certificate files are found at the specified paths, MeshQTT will automatically generate a self-signed certificate for development and testing purposes. The generated certificate will be saved to the `certs/` directory:

- `certs/server.crt` - The certificate file
- `certs/server.key` - The private key file

### Production Deployment

For production environments, it's recommended to use certificates from a trusted Certificate Authority (CA) or use Let's Encrypt for automatic certificate management.

#### Using Let's Encrypt certificates:

```json
{
  "TlsEnabled": true,
  "TlsPort": 8883,
  "CertificatePath": "/etc/letsencrypt/live/yourdomain.com/fullchain.pem",
  "PrivateKeyPath": "/etc/letsencrypt/live/yourdomain.com/privkey.pem"
}
```

#### Using custom certificates:

```json
{
  "TlsEnabled": true,
  "TlsPort": 8883,
  "CertificatePath": "/path/to/your/certificate.crt",
  "PrivateKeyPath": "/path/to/your/private.key",
  "CertificatePassword": "your-cert-password"
}
```

### Testing TLS Connection

To test the TLS connection, you can use mosquitto client tools:

```bash
# Test with self-signed certificate (using server certificate as CA)
mosquitto_pub -h localhost -p 8883 --cafile certs/server.crt -u username -P password -i "test-client" -t test -m "hello TLS"

# Test with insecure flag (skip certificate validation - only for testing)
mosquitto_pub -h localhost -p 8883 --insecure -u username -P password -i "test-client" -t test -m "hello TLS"
```

**Note**: When using self-signed certificates, clients need to either:

1. Use the `--cafile` option pointing to the server certificate
2. Use the `--insecure` flag to skip certificate validation (not recommended for production)
3. Import the certificate into the system's certificate store

## Docker Deployment with TLS

### Docker Compose Setup

The provided `compose.yaml` is already configured for TLS support:

```yaml
services:
  meshqtt:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "1883:1883" # MQTT port
      - "8883:8883" # MQTT TLS port
      - "9000:9000" # Metrics port
    volumes:
      - ./config:/App/config:rw
      - ./logs:/App/logs:rw
      - ./certs:/App/certs:rw # TLS certificates directory
```

### Using TLS with Docker

1. **Enable TLS in config.json**:

   ```json
   {
     "TlsEnabled": true,
     "TlsPort": 8883,
     "CertificatePath": "certs/server.crt",
     "PrivateKeyPath": "certs/server.key"
   }
   ```

2. **Start with Docker Compose**:

   ```bash
   docker-compose up -d
   ```

3. **Using custom certificates**: Place your certificate files in the `./certs` directory:

   ```
   certs/
   ├── server.crt    # Your certificate
   └── server.key    # Your private key
   ```

4. **Test TLS connection**:

   ```bash
   # If using self-signed certificates generated by the app
   mosquitto_pub -h localhost -p 8883 --cafile certs/server.crt -u username -P password -i "test-client" -t test -m "hello docker TLS"

   # Or skip certificate validation for testing
   mosquitto_pub -h localhost -p 8883 --insecure -u username -P password -i "test-client" -t test -m "hello docker TLS"
   ```

### Certificate Persistence

When using Docker, certificates generated by the application will be stored in the `./certs` directory on the host, ensuring they persist across container restarts.
