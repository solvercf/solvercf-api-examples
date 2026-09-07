# Java Examples for SolverCF

Official Java integration examples for **Cloudflare Turnstile** and **Google reCAPTCHA v3** using standard `java.net.http.HttpClient` (Java 11+, zero external dependencies).

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

### Compile & Run

1. **Compile**:
   ```bash
   javac TurnstileDemo.java RecaptchaV3Demo.java
   ```

2. **Run Cloudflare Turnstile**:
   ```bash
   java TurnstileDemo
   ```

3. **Run Google reCAPTCHA v3**:
   ```bash
   java RecaptchaV3Demo
   ```

Both classes expose static `createTask()` and `getTaskResult()` methods that you can easily integrate into your Java scraper, bot, or backend service.
