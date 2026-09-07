import os
import time
import json
import requests

# Load configuration from config.json
CONFIG_PATH = os.path.join(os.path.dirname(__file__), "..", "config.json")
with open(CONFIG_PATH, "r", encoding="utf-8") as f:
    config = json.load(f)

# Priority: Environment variable -> config.json
CLIENT_KEY = os.environ.get("SOLVERCF_CLIENT_KEY") or os.environ.get("SOLVERCF_API_KEY") or config.get("clientKey") or config.get("apiKey")
WEBSITE_URL = config["turnstile"]["websiteUrl"]
WEBSITE_KEY = config["turnstile"]["websiteKey"]
VERIFY_URL = config["turnstile"]["verifyUrl"]
API_BASE_URL = "https://solvercf.com/token/extension"

def create_task(client_key, website_url, website_key, session=None):
    """Submit a Turnstile solving task to SolverCF API."""
    caller = session or requests
    payload = {
        "clientKey": client_key,
        "task": {
            "type": "TurnstileTask",
            "websiteUrl": website_url,
            "websiteKey": website_key
        }
    }
    res = caller.post(f"{API_BASE_URL}/createTask", json=payload, timeout=30)
    return res.json()

def get_task_result(client_key, task_id, session=None):
    """Query task status and solution from SolverCF API."""
    caller = session or requests
    payload = {
        "clientKey": client_key,
        "taskId": task_id
    }
    res = caller.post(f"{API_BASE_URL}/getTaskResult", json=payload, timeout=15)
    return res.json()

# Aliases matching API endpoint casing
createTask = create_task
getTaskResult = get_task_result

def main():
    print("=" * 60)
    print("  🚀 SolverCF API - Cloudflare Turnstile Demo")
    print("=" * 60)

    if not CLIENT_KEY or CLIENT_KEY == "YOUR_API_KEY_HERE":
        print("[!] Error: Please set your clientKey in config.json or export SOLVERCF_API_KEY.")
        return

    session = requests.Session()
    session.headers.update({"Content-Type": "application/json; charset=utf-8"})

    print("\n[1/3] 📝 Creating Turnstile task...")
    try:
        res = create_task(CLIENT_KEY, WEBSITE_URL, WEBSITE_KEY, session=session)
    except requests.RequestException as e:
        print(f"      [x] Network error creating task: {e}")
        return
    except ValueError:
        print("      [x] Invalid response format from createTask endpoint.")
        return

    if res.get("errorId", 0) != 0:
        print(f"      [x] Create task failed (Code: {res.get('errorCode')}): {res.get('errorDescription', res)}")
        return

    task_id = res.get("taskId")
    print(f"      [✓] Task ID: {task_id}")

    print("\n[2/3] ⏳ Polling result every 1.5s...")
    solution = None
    start = time.time()
    attempt = 0

    while time.time() - start < 90:
        time.sleep(1.5)
        attempt += 1
        elapsed = round(time.time() - start, 1)

        try:
            res = get_task_result(CLIENT_KEY, task_id, session=session)
        except requests.RequestException as e:
            print(f"      ➜ [#{attempt}] Polling warning: {e}")
            continue
        except ValueError:
            print(f"      ➜ [#{attempt}] Polling warning: Invalid response from server.")
            continue

        if res.get("errorId", 0) != 0:
            print(f"      [x] Polling error (Code: {res.get('errorCode')}): {res.get('errorDescription', res)}")
            return

        status = res.get("status")
        if status == "ready":
            solution = res.get("solution", {})
            total_time = round(time.time() - start, 2)
            cost = res.get("cost", 0)
            print(f"      [✓] Solved in {total_time}s | Cost: ${cost}")
            break
        elif status in ("failed", "expired"):
            print(f"      [x] Task ended with status: {status} ({res})")
            return
        else:
            print(f"      ➜ [#{attempt}] Status: {status} ({elapsed}s elapsed)...")

    if not solution:
        print("      [x] Timeout waiting for token.")
        return

    token = solution.get("token", "")
    user_agent = solution.get("userAgent", "")
    token_preview = f"{token[:40]}...{token[-10:]}" if len(token) > 50 else token
    print(f"      [✓] Token: {token_preview}")

    print("\n[3/3] 📡 Submitting token to verification endpoint...")
    headers = {
        "Content-Type": "application/json; charset=utf-8",
        "Referer": WEBSITE_URL
    }
    if user_agent:
        headers["User-Agent"] = user_agent

    try:
        verify_res = requests.post(VERIFY_URL, headers=headers, json={"token": token}, timeout=30).json()
    except requests.RequestException as e:
        print(f"      [x] Network error during verification: {e}")
        return
    except ValueError:
        print("      [x] Invalid response from verify endpoint.")
        return

    # Parse and format rawJson for clean display
    formatted_res = dict(verify_res)
    raw_json_str = formatted_res.get("rawJson")
    if isinstance(raw_json_str, str):
        try:
            formatted_res["rawJson"] = json.loads(raw_json_str)
        except ValueError:
            pass

    print("\nVerify Response:")
    print(json.dumps(formatted_res, indent=2))

    print("-" * 60)
    if verify_res.get("success"):
        print("[🎉 SUCCESS] Cloudflare Turnstile verified successfully!")
    else:
        print("[x] Verification failed.")
    print("=" * 60)

if __name__ == "__main__":
    main()
