#!/bin/bash

echo "🔍 Testing trace correlation between Collector and Pushers"
echo "=================================================="

# Test different skill IDs to verify filtering
echo "📤 Sending test data for Events (skill 1-3)..."

# Skill 1 - Events
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101,
    "idSkill": 1,
    "timestamp": "2024-01-15T10:30:00Z"
  }'

echo -e "\n"

# Skill 2 - Events  
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101,
    "idSkill": 2,
    "timestamp": "2024-01-15T10:31:00Z"
  }'

echo -e "\n"

# Skill 3 - Events
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101,
    "idSkill": 3,
    "timestamp": "2024-01-15T10:32:00Z"
  }'

echo -e "\n"

echo "🎯 Sending test data for Shots (skill 4)..."

# Skill 4 - Shots
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101,
    "idSkill": 4,
    "timestamp": "2024-01-15T10:33:00Z"
  }'

echo -e "\n"

echo "✅ Test requests sent!"
echo ""
echo "🔍 To verify trace correlation:"
echo "1. Open Jaeger UI: http://localhost:16686"
echo "2. Look for services: collector, events-pusher, shots-pusher"
echo "3. Find traces that span multiple services"
echo "4. Verify the trace flow: HTTP Request → ServiceBus Publish → Message Processing → Webhook"
echo ""
echo "💡 Expected trace structure:"
echo "  └─ HTTP POST /api/collect (collector)"
echo "     └─ ServiceBus.PublishChampionshipData (collector)"
echo "        ├─ ServiceBus.ReceiveEvents (events-pusher) [skills 1-3]"
echo "        │  ├─ Process.ProcessEvents"
echo "        │  └─ Webhook.PushEventsWebhook"
echo "        └─ ServiceBus.ReceiveShots (shots-pusher) [skill 4]"
echo "           ├─ Process.ProcessShots"
echo "           └─ Webhook.PushShotsWebhook"