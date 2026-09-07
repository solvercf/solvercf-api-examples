# Node.js Examples for SolverCF

Lightweight, modern Node.js examples using native `fetch` (Node.js 18+ required, zero external dependencies).

### Configuration
1. Open `config.json` in the root folder and enter your API key:
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
   node turnstile_demo.js
   # or
   npm run turnstile
   ```

2. **Google reCAPTCHA v3 Full Flow**:
   ```bash
   node recaptcha_v3_demo.js
   # or
   npm run recaptcha
   ```

Both scripts export modular `createTask()` and `getTaskResult()` functions that you can import into your own project:
```javascript
import { createTask, getTaskResult } from './turnstile_demo.js';
```
