#!/usr/bin/env bash
# ============================================================
#   SolverCF API - Cloudflare Turnstile Demo (cURL)
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/../config.json"

# Read configuration from environment or config file
API_KEY="${SOLVERCF_CLIENT_KEY:-${SOLVERCF_API_KEY:-}}"
if [ -z "$API_KEY" ] && [ -f "$CONFIG_FILE" ]; then
  API_KEY=$(grep -o '"clientKey": *"[^"]*"' "$CONFIG_FILE" | head -1 | cut -d'"' -f4)
  if [ -z "$API_KEY" ]; then
    API_KEY=$(grep -o '"apiKey": *"[^"]*"' "$CONFIG_FILE" | head -1 | cut -d'"' -f4)
  fi
fi

# Helper to parse nested config values
read_config() {
  local section="$1"
  local key="$2"
  local fallback="$3"
  local val=""

  if command -v python3 >/dev/null 2>&1; then
    val=$(python3 -c "import json; c=json.load(open('$CONFIG_FILE')); print(c.get('$section', {}).get('$key', ''))" 2>/dev/null)
  elif command -v python >/dev/null 2>&1; then
    val=$(python -c "import json; c=json.load(open('$CONFIG_FILE')); print(c.get('$section', {}).get('$key', ''))" 2>/dev/null)
  elif command -v jq >/dev/null 2>&1; then
    val=$(jq -r ".$section.$key // empty" "$CONFIG_FILE" 2>/dev/null)
  fi

  if [ -z "$val" ] && [ -f "$CONFIG_FILE" ]; then
    val=$(awk -v s="$section" -v k="$key" '
      $0 ~ "\""s"\"" { in_sec=1; next }
      in_sec && /}/ { in_sec=0 }
      in_sec && $0 ~ "\""k"\"" {
        split($0, a, "\"")
        print a[4]
      }
    ' "$CONFIG_FILE")
  fi

  echo "${val:-$fallback}"
}

WEBSITE_URL=$(read_config "turnstile" "websiteUrl" "https://solvercf.com/demo/cloudflare-turnstile")
WEBSITE_KEY=$(read_config "turnstile" "websiteKey" "0x4AAAAAAB__YGWiObopXheP")
VERIFY_URL=$(read_config "turnstile" "verifyUrl" "https://solvercf.com/token/demo/verify-turnstile")

echo "============================================================"
echo "  🚀 SolverCF API - Cloudflare Turnstile Demo (cURL)"
echo "============================================================"

if [ -z "$API_KEY" ] || [ "$API_KEY" == "YOUR_API_KEY_HERE" ]; then
  echo "[!] Error: Please configure clientKey in config.json or export SOLVERCF_API_KEY."
  exit 1
fi

echo ""
echo "[1/3] 📝 Creating Turnstile task..."
CREATE_RES=$(curl -s --max-time 30 -X POST https://solvercf.com/token/extension/createTask \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{
    "clientKey": "'"$API_KEY"'",
    "task": {
      "type": "TurnstileTask",
      "websiteUrl": "'"$WEBSITE_URL"'",
      "websiteKey": "'"$WEBSITE_KEY"'"
    }
  }')

TASK_ID=$(echo "$CREATE_RES" | grep -o '"taskId"[ :]*"[^"]*"' | head -1 | cut -d'"' -f4)

if [ -z "$TASK_ID" ]; then
  echo "      [x] Failed to create task: $CREATE_RES"
  exit 1
fi

echo "      [✓] Task ID: $TASK_ID"
echo ""
echo "[2/3] ⏳ Polling result every 1.5s..."

TOKEN=""
USER_AGENT=""

for attempt in {1..60}; do
  sleep 1.5
  RESULT_RES=$(curl -s --max-time 15 -X POST https://solvercf.com/token/extension/getTaskResult \
    -H "Content-Type: application/json; charset=utf-8" \
    -d '{
      "clientKey": "'"$API_KEY"'",
      "taskId": "'"$TASK_ID"'"
    }')

  STATUS=$(echo "$RESULT_RES" | grep -o '"status"[ :]*"[^"]*"' | head -1 | cut -d'"' -f4)

  if [ "$STATUS" == "ready" ]; then
    TOKEN=$(echo "$RESULT_RES" | grep -o '"token"[ :]*"[^"]*"' | head -1 | cut -d'"' -f4)
    USER_AGENT=$(echo "$RESULT_RES" | grep -o '"userAgent"[ :]*"[^"]*"' | head -1 | cut -d'"' -f4)
    COST=$(echo "$RESULT_RES" | grep -o '"cost"[ :]*[0-9.]*' | head -1 | cut -d':' -f2 | tr -d ' ')
    echo "      [✓] Solved successfully! | Cost: \$$COST"
    break
  elif [ "$STATUS" == "failed" ] || [ "$STATUS" == "expired" ]; then
    echo "      [x] Task ended with status: $STATUS"
    exit 1
  else
    echo "      ➜ [#$attempt] Status: $STATUS..."
  fi
done

if [ -z "$TOKEN" ]; then
  echo "      [x] Timeout or failed to get token."
  exit 1
fi

TOKEN_PREVIEW="${TOKEN:0:40}...${TOKEN: -10}"
echo "      [✓] Token: $TOKEN_PREVIEW"

echo ""
echo "[3/3] 📡 Submitting token to verification endpoint..."
HEADERS=(
  -H "Content-Type: application/json; charset=utf-8"
  -H "Referer: $WEBSITE_URL"
)
if [ -n "$USER_AGENT" ]; then
  HEADERS+=(-H "User-Agent: $USER_AGENT")
fi

VERIFY_RES=$(curl -s --max-time 30 -X POST "$VERIFY_URL" \
  "${HEADERS[@]}" \
  -d '{"token": "'"$TOKEN"'"}')

echo ""
echo "Verify Response:"
echo "$VERIFY_RES"

echo "------------------------------------------------------------"
if echo "$VERIFY_RES" | grep -Eq '"success"[ :]*true'; then
  echo "[🎉 SUCCESS] Cloudflare Turnstile verified successfully!"
else
  echo "[x] Verification failed."
fi
echo "============================================================"
