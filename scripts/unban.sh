#!/usr/bin/env bash
# Usage: ./unban.sh <IP_ADDRESS>
# Unbans the given IP from the meshqtt jail in the fail2ban container

if [ -z "$1" ]; then
  echo "Usage: $0 <IP_ADDRESS>"
  exit 1
fi

docker exec fail2ban fail2ban-client set meshqtt unbanip "$1"
