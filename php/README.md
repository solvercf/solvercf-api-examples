# PHP Examples for SolverCF

Official PHP cURL integration examples for **Cloudflare Turnstile** and **Google reCAPTCHA v3**.

### Configuration
1. Copy `config.example.json` in the root folder to `config.json` and set your API key:
   ```json
   {
     "clientKey": "YOUR_ACTUAL_API_KEY"
   }
   ```
2. Or export `SOLVERCF_API_KEY` as an environment variable:
   ```bash
   export SOLVERCF_API_KEY="YOUR_ACTUAL_API_KEY"       # Linux / macOS
   $env:SOLVERCF_API_KEY="YOUR_ACTUAL_API_KEY"        # Windows PowerShell
   ```

### Running Examples

1. **Cloudflare Turnstile Full Flow**:
   ```bash
   php turnstile_demo.php
   ```

2. **Google reCAPTCHA v3 Full Flow**:
   ```bash
   php recaptcha_v3_demo.php
   ```

Both scripts provide reusable `createTask()` and `getTaskResult()` functions.
