# Golang Examples for SolverCF

High-performance Go integration examples for **Cloudflare Turnstile** and **Google reCAPTCHA v3** using standard library `net/http` (zero external dependencies).

### Configuration
1. Open `config.json` in the root folder and set your API key:
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

1. **Run All Demos**:
   ```bash
   go run .
   ```

2. **Run Cloudflare Turnstile only**:
   ```bash
   go run . turnstile
   ```

3. **Run Google reCAPTCHA v3 only**:
   ```bash
   go run . recaptcha
   ```

Both `CreateTask` and `GetTaskResult` are modular functions that can be imported or adapted into your own crawlers.
