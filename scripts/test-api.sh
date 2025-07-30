#!/bin/bash

# Simple test script for MeshQTT Management API
# This script tests the basic functionality of the API endpoints

BASE_URL="http://localhost:8080/api"

echo "Testing MeshQTT Management API..."

# Test health endpoint
echo "1. Testing health endpoint..."
HEALTH_RESPONSE=$(curl -s "$BASE_URL/health")
if echo "$HEALTH_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Health check passed"
else
    echo "✗ Health check failed"
    exit 1
fi

# Test nodes endpoint
echo "2. Testing nodes endpoint..."
NODES_RESPONSE=$(curl -s "$BASE_URL/nodes")
if echo "$NODES_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Nodes endpoint working"
else
    echo "✗ Nodes endpoint failed"
    exit 1
fi

# Test config endpoint
echo "3. Testing config endpoint..."
CONFIG_RESPONSE=$(curl -s "$BASE_URL/config")
if echo "$CONFIG_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Config endpoint working"
else
    echo "✗ Config endpoint failed"
    exit 1
fi

# Test stats endpoint
echo "4. Testing stats endpoint..."
STATS_RESPONSE=$(curl -s "$BASE_URL/stats")
if echo "$STATS_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Stats endpoint working"
else
    echo "✗ Stats endpoint failed"
    exit 1
fi

# Test ban functionality
echo "5. Testing ban functionality..."
BAN_RESPONSE=$(curl -s -X POST "$BASE_URL/nodes/testnode/ban" -H "Content-Type: application/json" -d '{"reason": "API test"}')
if echo "$BAN_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Ban functionality working"
else
    echo "✗ Ban functionality failed"
    exit 1
fi

# Test unban functionality
echo "6. Testing unban functionality..."
UNBAN_RESPONSE=$(curl -s -X DELETE "$BASE_URL/nodes/testnode/ban")
if echo "$UNBAN_RESPONSE" | grep -q '"success":true'; then
    echo "✓ Unban functionality working"
else
    echo "✗ Unban functionality failed"
    exit 1
fi

echo "All API tests passed! ✓"