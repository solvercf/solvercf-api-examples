#!/usr/bin/env bash
# ============================================================
#   SolverCF API - Google reCAPTCHA v3 Demo (cURL)
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/../config.json"
if [ ! -f "$CONFIG_FILE" ]; then
  CONFIG_FILE="$SCRIPT_DIR/../config.example.json"
fi

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

WEBSITE_URL=$(read_config "recaptchaV3" "websiteUrl" "https://solvercf.com/demo/recaptchav3")
WEBSITE_KEY=$(read_config "recaptchaV3" "websiteKey" "6LeSYmUtAAAAADy78hcvyfvGXqWKqN4aaFs2vyG8")
PAGE_ACTION=$(read_config "recaptchaV3" "pageAction" "demo_page")
VERIFY_URL=$(read_config "recaptchaV3" "verifyUrl" "https://solvercf.com/token/demo/verify-recaptcha")

echo "============================================================"
echo "  🚀 SolverCF API - Google reCAPTCHA v3 Demo (cURL)"
echo "============================================================"

if [ -z "$API_KEY" ] || [ "$API_KEY" == "YOUR_API_KEY_HERE" ]; then
  echo "[!] Error: Please configure clientKey in config.json or export SOLVERCF_API_KEY."
  exit 1
fi

echo ""
echo "[1/3] 📝 Creating reCAPTCHA v3 task..."
CREATE_RES=$(curl -s --max-time 30 -X POST https://solvercf.com/token/extension/createTask \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{
    "clientKey": "'"$API_KEY"'",
    "task": {
      "type": "RecaptchaV3TaskProxyless",
      "websiteUrl": "'"$WEBSITE_URL"'",
      "websiteKey": "'"$WEBSITE_KEY"'",
      "pageAction": "'"$PAGE_ACTION"'"
    }
  }')

TASK_ID=$(echo "$CREATE_RES" | grep -o '"taskId":"[^"]*' | cut -d'"' -f4)

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

  STATUS=$(echo "$RESULT_RES" | grep -o '"status":"[^"]*' | cut -d'"' -f4)

  if [ "$STATUS" == "ready" ]; then
    TOKEN=$(echo "$RESULT_RES" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
    USER_AGENT=$(echo "$RESULT_RES" | grep -o '"userAgent":"[^"]*' | cut -d'"' -f4)
    COST=$(echo "$RESULT_RES" | grep -o '"cost":[0-9.]*' | cut -d':' -f2)
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

SCORE=$(echo "$VERIFY_RES" | grep -o '"score": *[0-9.]*' | cut -d':' -f2 | tr -d ' ')

echo "------------------------------------------------------------"
if echo "$VERIFY_RES" | grep -q '"success":true'; then
  echo "[🎉 SUCCESS] reCAPTCHA v3 verified successfully! (Score: $SCORE)"
  echo "[📊 Result] Score: $SCORE | Action: $PAGE_ACTION"
else
  echo "[x] Verification failed."
fi
echo "============================================================"
