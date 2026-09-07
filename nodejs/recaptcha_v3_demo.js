import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Load configuration from config.json
const configPath = path.join(__dirname, '..', 'config.json');
const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));

// Priority: Environment variable -> config.json
export const CLIENT_KEY = process.env.SOLVERCF_CLIENT_KEY || process.env.SOLVERCF_API_KEY || config.clientKey || config.apiKey;
const rc = config.recaptchaV3;
export const WEBSITE_URL = rc.websiteUrl;
export const WEBSITE_KEY = rc.websiteKey;
export const PAGE_ACTION = rc.pageAction || "demo_page";
export const VERIFY_URL = rc.verifyUrl;
export const API_BASE_URL = "https://solvercf.com/token/extension";

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Submit a reCAPTCHA v3 solving task to SolverCF API
 */
export async function createTask(clientKey, websiteUrl, websiteKey, pageAction = "demo_page") {
  const res = await fetch(`${API_BASE_URL}/createTask`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      clientKey,
      task: {
        type: "RecaptchaV3TaskProxyless",
        websiteUrl,
        websiteKey,
        pageAction
      }
    }),
    signal: AbortSignal.timeout(30000)
  });
  return await res.json();
}

/**
 * Query task status and solution from SolverCF API
 */
export async function getTaskResult(clientKey, taskId) {
  const res = await fetch(`${API_BASE_URL}/getTaskResult`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ clientKey, taskId }),
    signal: AbortSignal.timeout(15000)
  });
  return await res.json();
}

async function main() {
  console.log("=".repeat(60));
  console.log("  🚀 SolverCF API - Google reCAPTCHA v3 Demo (Node.js)");
  console.log("=".repeat(60));

  if (!CLIENT_KEY || CLIENT_KEY === "YOUR_API_KEY_HERE") {
    console.log("[!] Error: Please set your clientKey in config.json or export SOLVERCF_API_KEY.");
    return;
  }

  console.log("\n[1/3] 📝 Creating reCAPTCHA v3 task...");
  let createData;
  try {
    createData = await createTask(CLIENT_KEY, WEBSITE_URL, WEBSITE_KEY, PAGE_ACTION);
  } catch (err) {
    console.log(`      [x] Network error creating task: ${err.message}`);
    return;
  }

  if (createData.errorId !== 0) {
    console.log(`      [x] Create task failed (Code: ${createData.errorCode}): ${createData.errorDescription || JSON.stringify(createData)}`);
    return;
  }

  const taskId = createData.taskId;
  console.log(`      [✓] Task ID: ${taskId}`);

  console.log("\n[2/3] ⏳ Polling result every 1.5s...");
  let solution = null;
  const start = Date.now();
  let attempt = 0;

  while (Date.now() - start < 90000) {
    await sleep(1500);
    attempt++;
    const elapsed = ((Date.now() - start) / 1000).toFixed(1);

    let resultData;
    try {
      resultData = await getTaskResult(CLIENT_KEY, taskId);
    } catch (err) {
      console.log(`      ➜ [#${attempt}] Polling warning: ${err.message}`);
      continue;
    }

    if (resultData.errorId !== 0) {
      console.log(`      [x] Polling error (Code: ${resultData.errorCode}): ${resultData.errorDescription || JSON.stringify(resultData)}`);
      return;
    }

    if (resultData.status === "ready") {
      solution = resultData.solution || {};
      const totalTime = ((Date.now() - start) / 1000).toFixed(2);
      const cost = resultData.cost || 0;
      console.log(`      [✓] Solved in ${totalTime}s | Cost: $${cost}`);
      break;
    } else if (resultData.status === "failed" || resultData.status === "expired") {
      console.log(`      [x] Task ended with status: ${resultData.status} (${JSON.stringify(resultData)})`);
      return;
    } else {
      console.log(`      ➜ [#${attempt}] Status: ${resultData.status} (${elapsed}s elapsed)...`);
    }
  }

  if (!solution) {
    console.log("      [x] Timeout waiting for token.");
    return;
  }

  const token = solution.token || "";
  const userAgent = solution.userAgent || "";
  const tokenPreview = token.length > 50 ? `${token.slice(0, 40)}...${token.slice(-10)}` : token;
  console.log(`      [✓] Token: ${tokenPreview}`);

  console.log("\n[3/3] 📡 Submitting token to verification endpoint...");
  const headers = {
    "Content-Type": "application/json",
    "Referer": WEBSITE_URL,
    ...(userAgent ? { "User-Agent": userAgent } : {})
  };

  let verifyRes;
  try {
    const res = await fetch(VERIFY_URL, {
      method: "POST",
      headers,
      body: JSON.stringify({ token }),
      signal: AbortSignal.timeout(30000)
    });
    verifyRes = await res.json();
  } catch (err) {
    console.log(`      [x] Network error during verification: ${err.message}`);
    return;
  }

  // Parse nested rawJson for clean display
  const formattedRes = { ...verifyRes };
  let rawDetails = {};
  if (typeof formattedRes.rawJson === 'string') {
    try {
      rawDetails = JSON.parse(formattedRes.rawJson);
      formattedRes.rawJson = rawDetails;
    } catch {
      // Keep as string
    }
  } else if (typeof formattedRes.rawJson === 'object' && formattedRes.rawJson !== null) {
    rawDetails = formattedRes.rawJson;
  }

  console.log("\nVerify Response:");
  console.log(JSON.stringify(formattedRes, null, 2));

  const score = rawDetails.score ?? verifyRes.score;
  const hostname = rawDetails.hostname ?? verifyRes.hostname;
  const action = rawDetails.action ?? verifyRes.action;

  console.log("-".repeat(60));
  if (verifyRes.success) {
    const scoreText = score !== undefined ? ` (Score: ${score})` : "";
    console.log(`[🎉 SUCCESS] reCAPTCHA v3 verified successfully!${scoreText}`);
    if (hostname || action) {
      console.log(`[📊 Result] Score: ${score} | Action: ${action} | Host: ${hostname}`);
    }
  } else {
    console.log("[x] Verification failed.");
  }
  console.log("=".repeat(60));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main();
}
