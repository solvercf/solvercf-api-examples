# Python Examples for SolverCF

This directory provides complete, lightweight Python examples using `requests` to solve and verify **Cloudflare Turnstile** and **Google reCAPTCHA v3**.

### Setup
```bash
pip install -r requirements.txt
```

### Configuration
You can provide your API key in either of two ways:
1. Open `config.json` in the root folder and set `"clientKey"`:
   ```json
   {
     "clientKey": "YOUR_ACTUAL_API_KEY"
   }
   ```
2. Or set the `SOLVERCF_API_KEY` environment variable:
   ```bash
   export SOLVERCF_API_KEY="YOUR_ACTUAL_API_KEY"       # Linux / macOS
   $env:SOLVERCF_API_KEY="YOUR_ACTUAL_API_KEY"        # Windows PowerShell
   ```

### Running Examples

1. **Full Flow - Cloudflare Turnstile** (Create task ➔ Poll ➔ Verify token):
   ```bash
   python turnstile_demo.py
   ```
   *Note: `turnstile_demo.py` also exposes modular `create_task()` and `get_task_result()` helper functions.*

2. **Full Flow - Google reCAPTCHA v3** (Create task ➔ Poll ➔ Verify token & score):
   ```bash
   python recaptcha_v3_demo.py
   ```
