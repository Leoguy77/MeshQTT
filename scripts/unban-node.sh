#!/bin/bash

if [ -z "$1" ]; then
  echo "Usage: $0 <NodeId>"
  exit 1
fi
NODEID="$1"
CONFIG="$(dirname "$0")/../config/config.json"

if ! command -v jq &> /dev/null; then
  echo "jq is required but not installed."
  exit 1
fi

if jq -e --arg id "$NODEID" '.Banlist | index($id)' "$CONFIG" > /dev/null; then
  tmp=$(mktemp)
  jq --arg id "$NODEID" '.Banlist |= map(select(. != $id))' "$CONFIG" > "$tmp" && mv "$tmp" "$CONFIG"
  echo "Node '$NODEID' removed from banlist."
else
  echo "Node '$NODEID' is not in the banlist."
fi
