# 🚀 SolverCF API Integration Examples

[![Website](https://img.shields.io/badge/Website-solvercf.com-orange.svg)](https://solvercf.com/)
[![Documentation](https://img.shields.io/badge/Docs-API%20Reference-blue.svg)](https://solvercf.com/docs/overview)
[![Telegram](https://img.shields.io/badge/Telegram-Community-0088cc.svg)](https://t.me/solvercf_group)
[![YouTube](https://img.shields.io/badge/YouTube-@solvercf-red.svg?logo=youtube)](https://www.youtube.com/@solvercf)
[![X (Twitter)](https://img.shields.io/badge/X-@solvercf-black.svg?logo=x)](https://x.com/solvercf)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Official multi-language examples and quickstart guides for **[SolverCF](https://solvercf.com)** — the AI-powered Cloudflare Captcha Bypass and Anti-Bot resolution service.

Solve **Cloudflare Turnstile**, **Cloudflare 5s / Managed Challenges**, and **Google reCAPTCHA v3** with a 99.9% success rate and ~2.3s average response time across 10M+ daily requests.

---

## ⚡ Quick Start

### 1. Configure `config.json`
Open `config.json` in the root directory and replace `YOUR_API_KEY_HERE` with your API Key (or export `SOLVERCF_API_KEY="YOUR_KEY"` as an environment variable):

```json
{
  "clientKey": "YOUR_API_KEY_HERE",
  "turnstile": {
    "websiteUrl": "https://solvercf.com/demo/cloudflare-turnstile",
    "websiteKey": "0x4AAAAAAB__YGWiObopXheP",
    "verifyUrl": "https://solvercf.com/token/demo/verify-turnstile"
  }
}
```
*(Get **1,000 free requests** without a credit card at [https://solvercf.com](https://solvercf.com))*

### 2. Choose Your Language & Run
- **Python**: `cd python && pip install -r requirements.txt && python turnstile_demo.py`
- **Node.js**: `cd nodejs && node turnstile_demo.js`
- **Java**: `cd java && javac TurnstileDemo.java && java TurnstileDemo`
- **C#**: `cd csharp && dotnet run -- turnstile`
- **Golang**: `cd golang && go run . turnstile`
- **PHP**: `cd php && php turnstile_demo.php`
- **cURL**: `cd curl && bash full_flow_turnstile.sh`

---

## 📂 Repository Structure

| Language | Folder | Description |
| :--- | :--- | :--- |
| **Configuration** | [`config.json`](./config.json) | Centralized configuration for API keys, URLs, and site keys |
| **Python** | [`python/`](./python) | Complete samples for Cloudflare Turnstile & reCAPTCHA v3 using `requests` |
| **Node.js** | [`nodejs/`](./nodejs) | Modern native `fetch` samples for Turnstile & reCAPTCHA v3 |
| **Java** | [`java/`](./java) | Standard `java.net.http.HttpClient` integration (Java 11+) |
| **C# (.NET)** | [`csharp/`](./csharp) | `HttpClient` integration for Windows desktop, bot, and backend applications |
| **Golang** | [`golang/`](./golang) | High-throughput concurrent crawler examples |
| **PHP** | [`php/`](./php) | cURL-based implementation for standard backend systems |
| **cURL / Shell** | [`curl/`](./curl) | Command-line scripts to test Turnstile & reCAPTCHA v3 solving and verify |

---

## 🔄 Task-Based Solving Flow

All SolverCF captcha solving follows a simple 3-step lifecycle:

```
1. createTask  ───> Returns taskId immediately
       │
2. getTaskResult ──> Poll every 1.5s until status is "ready"
       │
3. Submit Token ───> Submit token + userAgent to target website / verify endpoint
```

---

## 🔗 Official Resources
- **Website**: [https://solvercf.com](https://solvercf.com)
- **API Documentation**: [https://solvercf.com/docs/overview](https://solvercf.com/docs/overview)
- **YouTube**: [@solvercf](https://www.youtube.com/@solvercf)
- **X (Twitter)**: [@solvercf](https://x.com/solvercf)
- **Telegram Group**: [@solvercf_group](https://t.me/solvercf_group)
- **Telegram Support**: [@solvercf](https://t.me/solvercf)
