# cURL & Bash Examples for SolverCF

Fast, pure bash/cURL scripts to solve and verify **Cloudflare Turnstile** and **Google reCAPTCHA v3**.

### Configuration
1. Open `config.json` in the root folder and set your API key:
   ```json
   {
     "clientKey": "YOUR_ACTUAL_API_KEY"
   }
   ```
2. Or export `SOLVERCF_API_KEY` as an environment variable:
   ```bash
   export SOLVERCF_API_KEY="YOUR_ACTUAL_API_KEY"
   ```

### Running Examples

1. **Cloudflare Turnstile Full Flow**:
   ```bash
   bash full_flow_turnstile.sh
   ```

2. **Google reCAPTCHA v3 Full Flow**:
   ```bash
   bash full_flow_recaptchav3.sh
   ```

All target URLs, site keys, and actions are dynamically extracted from `config.json`.
