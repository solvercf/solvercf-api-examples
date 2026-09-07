# C# (.NET) Examples for SolverCF

Integration examples for **Cloudflare Turnstile** and **Google reCAPTCHA v3** using modern `HttpClient` (.NET 8+).

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

1. **Run All Demos**:
   ```bash
   dotnet run
   ```

2. **Run Cloudflare Turnstile only**:
   ```bash
   dotnet run -- turnstile
   ```

3. **Run Google reCAPTCHA v3 only**:
   ```bash
   dotnet run -- recaptcha
   ```

### Reusable Methods
The `Program` class provides modular helper methods:
- `CreateTaskAsync(HttpClient client, string clientKey, object taskPayload)`
- `GetTaskResultAsync(HttpClient client, string clientKey, string taskId)`
