using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private const string ApiBaseUrl = "https://solvercf.com/token/extension";

    public static async Task<JsonDocument> CreateTaskAsync(HttpClient client, string clientKey, object taskPayload)
    {
        var payload = new
        {
            clientKey,
            task = taskPayload
        };
        var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/createTask", payload);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    public static async Task<JsonDocument> GetTaskResultAsync(HttpClient client, string clientKey, string taskId)
    {
        var payload = new
        {
            clientKey,
            taskId
        };
        var response = await client.PostAsJsonAsync($"{ApiBaseUrl}/getTaskResult", payload);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    static async Task Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

        string configPath = File.Exists("config.json") ? "config.json"
            : File.Exists(Path.Combine("..", "config.json")) ? Path.Combine("..", "config.json")
            : "config.json";

        if (!File.Exists(configPath))
        {
            Console.WriteLine("[!] Error: config.json not found!");
            return;
        }

        using var configDoc = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var root = configDoc.RootElement;

        string? envKey = Environment.GetEnvironmentVariable("SOLVERCF_CLIENT_KEY") ?? Environment.GetEnvironmentVariable("SOLVERCF_API_KEY");
        string clientKey = envKey ?? (root.TryGetProperty("clientKey", out var ck) ? ck.GetString()! : root.GetProperty("apiKey").GetString()!);

        if (string.IsNullOrWhiteSpace(clientKey) || clientKey == "YOUR_API_KEY_HERE")
        {
            Console.WriteLine("[!] Error: Please configure clientKey in config.json or export SOLVERCF_API_KEY.");
            return;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (mode == "turnstile" || mode == "all")
        {
            await RunTurnstileDemoAsync(client, clientKey, root);
        }

        if (mode == "all")
        {
            Console.WriteLine();
        }

        if (mode == "recaptcha" || mode == "recaptchav3" || mode == "all")
        {
            await RunRecaptchaV3DemoAsync(client, clientKey, root);
        }
    }

    private static async Task RunTurnstileDemoAsync(HttpClient client, string clientKey, JsonElement root)
    {
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("  🚀 SolverCF API - Cloudflare Turnstile Demo (C#)");
        Console.WriteLine(new string('=', 60));

        var turnstile = root.GetProperty("turnstile");
        string websiteUrl = turnstile.GetProperty("websiteUrl").GetString()!;
        string websiteKey = turnstile.GetProperty("websiteKey").GetString()!;
        string verifyUrl = turnstile.GetProperty("verifyUrl").GetString()!;

        Console.WriteLine("\n[1/3] 📝 Creating Turnstile task...");
        var createPayload = new
        {
            type = "TurnstileTask",
            websiteUrl,
            websiteKey
        };

        JsonDocument createDoc;
        try
        {
            createDoc = await CreateTaskAsync(client, clientKey, createPayload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [x] Network error creating task: {ex.Message}");
            return;
        }

        using (createDoc)
        {
            var createRoot = createDoc.RootElement;
            if (createRoot.GetProperty("errorId").GetInt32() != 0)
            {
                string errCode = createRoot.TryGetProperty("errorCode", out var ec) ? ec.GetString()! : "UNKNOWN";
                string errDesc = createRoot.TryGetProperty("errorDescription", out var ed) ? ed.GetString()! : createRoot.ToString();
                Console.WriteLine($"      [x] Create task failed (Code: {errCode}): {errDesc}");
                return;
            }

            string taskId = createRoot.GetProperty("taskId").GetString()!;
            Console.WriteLine($"      [✓] Task ID: {taskId}");

            Console.WriteLine("\n[2/3] ⏳ Polling result every 1.5s...");
            string? token = null;
            string? userAgent = null;
            var start = DateTime.UtcNow;
            int attempt = 0;

            while ((DateTime.UtcNow - start).TotalSeconds < 90)
            {
                await Task.Delay(1500);
                attempt++;
                double elapsed = Math.Round((DateTime.UtcNow - start).TotalSeconds, 1);

                JsonDocument getDoc;
                try
                {
                    getDoc = await GetTaskResultAsync(client, clientKey, taskId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      ➜ [#{attempt}] Polling warning: {ex.Message}");
                    continue;
                }

                using (getDoc)
                {
                    var getRoot = getDoc.RootElement;
                    if (getRoot.GetProperty("errorId").GetInt32() != 0)
                    {
                        string errCode = getRoot.TryGetProperty("errorCode", out var ec) ? ec.GetString()! : "UNKNOWN";
                        string errDesc = getRoot.TryGetProperty("errorDescription", out var ed) ? ed.GetString()! : getRoot.ToString();
                        Console.WriteLine($"      [x] Polling error (Code: {errCode}): {errDesc}");
                        return;
                    }

                    string status = getRoot.GetProperty("status").GetString()!;

                    if (status == "ready")
                    {
                        var solution = getRoot.GetProperty("solution");
                        token = solution.GetProperty("token").GetString();
                        if (solution.TryGetProperty("userAgent", out var ua))
                        {
                            userAgent = ua.GetString();
                        }
                        double totalTime = Math.Round((DateTime.UtcNow - start).TotalSeconds, 2);
                        double cost = getRoot.TryGetProperty("cost", out var costEl) ? costEl.GetDouble() : 0.0;
                        Console.WriteLine($"      [✓] Solved in {totalTime}s | Cost: ${cost}");
                        break;
                    }
                    else if (status == "failed" || status == "expired")
                    {
                        Console.WriteLine($"      [x] Task ended with status: {status}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"      ➜ [#{attempt}] Status: {status} ({elapsed}s elapsed)...");
                    }
                }
            }

            if (token == null)
            {
                Console.WriteLine("      [x] Timeout waiting for token.");
                return;
            }

            string tokenPreview = token.Length > 50 ? $"{token[..40]}...{token[^10..]}" : token;
            Console.WriteLine($"      [✓] Token: {tokenPreview}");

            Console.WriteLine("\n[3/3] 📡 Submitting token to verification endpoint...");
            var verifyMsg = new HttpRequestMessage(HttpMethod.Post, verifyUrl)
            {
                Content = JsonContent.Create(new { token })
            };
            verifyMsg.Headers.Referrer = new Uri(websiteUrl);
            if (!string.IsNullOrEmpty(userAgent))
            {
                verifyMsg.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            HttpResponseMessage verifyRes;
            string verifyText;
            try
            {
                verifyRes = await client.SendAsync(verifyMsg);
                verifyText = await verifyRes.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      [x] Network error during verification: {ex.Message}");
                return;
            }

        Console.WriteLine("\nVerify Response:");
        PrintFormattedJson(verifyText);

        Console.WriteLine(new string('-', 60));
        if (verifyRes.IsSuccessStatusCode && (verifyText.Contains("\"success\":true") || verifyText.Contains("\"success\": true")))
        {
            Console.WriteLine("[🎉 SUCCESS] Cloudflare Turnstile verified successfully!");
        }
        else
        {
            Console.WriteLine("[x] Verification failed.");
        }
        Console.WriteLine(new string('=', 60));
        }
    }

    private static async Task RunRecaptchaV3DemoAsync(HttpClient client, string clientKey, JsonElement root)
    {
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("  🚀 SolverCF API - Google reCAPTCHA v3 Demo (C#)");
        Console.WriteLine(new string('=', 60));

        var recaptcha = root.GetProperty("recaptchaV3");
        string websiteUrl = recaptcha.GetProperty("websiteUrl").GetString()!;
        string websiteKey = recaptcha.GetProperty("websiteKey").GetString()!;
        string pageAction = recaptcha.TryGetProperty("pageAction", out var pa) ? pa.GetString()! : "demo_page";
        string verifyUrl = recaptcha.GetProperty("verifyUrl").GetString()!;

        Console.WriteLine("\n[1/3] 📝 Creating reCAPTCHA v3 task...");
        var createPayload = new
        {
            type = "RecaptchaV3TaskProxyless",
            websiteUrl,
            websiteKey,
            pageAction
        };

        JsonDocument createDoc;
        try
        {
            createDoc = await CreateTaskAsync(client, clientKey, createPayload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [x] Network error creating task: {ex.Message}");
            return;
        }

        using (createDoc)
        {
            var createRoot = createDoc.RootElement;
            if (createRoot.GetProperty("errorId").GetInt32() != 0)
            {
                string errCode = createRoot.TryGetProperty("errorCode", out var ec) ? ec.GetString()! : "UNKNOWN";
                string errDesc = createRoot.TryGetProperty("errorDescription", out var ed) ? ed.GetString()! : createRoot.ToString();
                Console.WriteLine($"      [x] Create task failed (Code: {errCode}): {errDesc}");
                return;
            }

            string taskId = createRoot.GetProperty("taskId").GetString()!;
            Console.WriteLine($"      [✓] Task ID: {taskId}");

            Console.WriteLine("\n[2/3] ⏳ Polling result every 1.5s...");
            string? token = null;
            string? userAgent = null;
            var start = DateTime.UtcNow;
            int attempt = 0;

            while ((DateTime.UtcNow - start).TotalSeconds < 90)
            {
                await Task.Delay(1500);
                attempt++;
                double elapsed = Math.Round((DateTime.UtcNow - start).TotalSeconds, 1);

                JsonDocument getDoc;
                try
                {
                    getDoc = await GetTaskResultAsync(client, clientKey, taskId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"      ➜ [#{attempt}] Polling warning: {ex.Message}");
                    continue;
                }

                using (getDoc)
                {
                    var getRoot = getDoc.RootElement;
                    if (getRoot.GetProperty("errorId").GetInt32() != 0)
                    {
                        string errCode = getRoot.TryGetProperty("errorCode", out var ec) ? ec.GetString()! : "UNKNOWN";
                        string errDesc = getRoot.TryGetProperty("errorDescription", out var ed) ? ed.GetString()! : getRoot.ToString();
                        Console.WriteLine($"      [x] Polling error (Code: {errCode}): {errDesc}");
                        return;
                    }

                    string status = getRoot.GetProperty("status").GetString()!;

                    if (status == "ready")
                    {
                        var solution = getRoot.GetProperty("solution");
                        token = solution.GetProperty("token").GetString();
                        if (solution.TryGetProperty("userAgent", out var ua))
                        {
                            userAgent = ua.GetString();
                        }
                        double totalTime = Math.Round((DateTime.UtcNow - start).TotalSeconds, 2);
                        double cost = getRoot.TryGetProperty("cost", out var costEl) ? costEl.GetDouble() : 0.0;
                        Console.WriteLine($"      [✓] Solved in {totalTime}s | Cost: ${cost}");
                        break;
                    }
                    else if (status == "failed" || status == "expired")
                    {
                        Console.WriteLine($"      [x] Task ended with status: {status}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"      ➜ [#{attempt}] Status: {status} ({elapsed}s elapsed)...");
                    }
                }
            }

            if (token == null)
            {
                Console.WriteLine("      [x] Timeout waiting for token.");
                return;
            }

            string tokenPreview = token.Length > 50 ? $"{token[..40]}...{token[^10..]}" : token;
            Console.WriteLine($"      [✓] Token: {tokenPreview}");

            Console.WriteLine("\n[3/3] 📡 Submitting token to verification endpoint...");
            var verifyMsg = new HttpRequestMessage(HttpMethod.Post, verifyUrl)
            {
                Content = JsonContent.Create(new { token })
            };
            verifyMsg.Headers.Referrer = new Uri(websiteUrl);
            if (!string.IsNullOrEmpty(userAgent))
            {
                verifyMsg.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            HttpResponseMessage verifyRes;
            string verifyText;
            try
            {
                verifyRes = await client.SendAsync(verifyMsg);
                verifyText = await verifyRes.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      [x] Network error during verification: {ex.Message}");
                return;
            }

        Console.WriteLine("\nVerify Response:");
        PrintFormattedJson(verifyText);

        double? score = null;
        string? host = null;
        try
        {
            using var doc = JsonDocument.Parse(verifyText);
            if (doc.RootElement.TryGetProperty("rawJson", out var rawEl) && rawEl.ValueKind == JsonValueKind.String)
            {
                using var rawDoc = JsonDocument.Parse(rawEl.GetString()!);
                if (rawDoc.RootElement.TryGetProperty("score", out var s)) score = s.GetDouble();
                if (rawDoc.RootElement.TryGetProperty("hostname", out var h)) host = h.GetString();
            }
            else if (doc.RootElement.TryGetProperty("rawJson", out var rawObjEl) && rawObjEl.ValueKind == JsonValueKind.Object)
            {
                if (rawObjEl.TryGetProperty("score", out var s)) score = s.GetDouble();
                if (rawObjEl.TryGetProperty("hostname", out var h)) host = h.GetString();
            }
            if (!score.HasValue && doc.RootElement.TryGetProperty("score", out var directScore)) score = directScore.GetDouble();
            if (string.IsNullOrEmpty(host) && doc.RootElement.TryGetProperty("hostname", out var directHost)) host = directHost.GetString();
        }
        catch { }

        Console.WriteLine(new string('-', 60));
        if (verifyRes.IsSuccessStatusCode && (verifyText.Contains("\"success\":true") || verifyText.Contains("\"success\": true")))
        {
            string scoreText = score.HasValue ? $" (Score: {score})" : "";
            Console.WriteLine($"[🎉 SUCCESS] reCAPTCHA v3 verified successfully!{scoreText}");
            if (score.HasValue || !string.IsNullOrEmpty(host))
            {
                Console.WriteLine($"[📊 Result] Score: {score} | Action: {pageAction} | Host: {host}");
            }
        }
        else
        {
            Console.WriteLine("[x] Verification failed.");
        }
        Console.WriteLine(new string('=', 60));
        }
    }

    private static void PrintFormattedJson(string jsonString)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            // If rawJson property is a string, un-nest it
            var root = doc.RootElement;
            if (root.TryGetProperty("rawJson", out var rawJsonEl) && rawJsonEl.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var innerDoc = JsonDocument.Parse(rawJsonEl.GetString()!);
                    var combined = new
                    {
                        success = root.TryGetProperty("success", out var s) && s.GetBoolean(),
                        rawJson = innerDoc.RootElement
                    };
                    Console.WriteLine(JsonSerializer.Serialize(combined, new JsonSerializerOptions { WriteIndented = true }));
                    return;
                }
                catch { }
            }
            Console.WriteLine(JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            Console.WriteLine(jsonString);
        }
    }
}
