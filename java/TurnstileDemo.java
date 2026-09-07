import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class TurnstileDemo {
    private static final String API_BASE_URL = "https://solvercf.com/token/extension";
    private static final HttpClient client = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(15))
            .build();

    public static String createTask(String clientKey, String websiteUrl, String websiteKey) throws IOException, InterruptedException {
        String payload = String.format("{\"clientKey\":\"%s\",\"task\":{\"type\":\"TurnstileTask\",\"websiteUrl\":\"%s\",\"websiteKey\":\"%s\"}}",
                escapeJson(clientKey), escapeJson(websiteUrl), escapeJson(websiteKey));

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(API_BASE_URL + "/createTask"))
                .header("Content-Type", "application/json; charset=utf-8")
                .timeout(Duration.ofSeconds(30))
                .POST(HttpRequest.BodyPublishers.ofString(payload))
                .build();

        HttpResponse<String> response = client.send(request, HttpResponse.BodyHandlers.ofString());
        return response.body();
    }

    public static String getTaskResult(String clientKey, String taskId) throws IOException, InterruptedException {
        String payload = String.format("{\"clientKey\":\"%s\",\"taskId\":\"%s\"}", escapeJson(clientKey), escapeJson(taskId));

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(API_BASE_URL + "/getTaskResult"))
                .header("Content-Type", "application/json; charset=utf-8")
                .timeout(Duration.ofSeconds(15))
                .POST(HttpRequest.BodyPublishers.ofString(payload))
                .build();

        HttpResponse<String> response = client.send(request, HttpResponse.BodyHandlers.ofString());
        return response.body();
    }

    public static void main(String[] args) throws Exception {
        System.out.println("=".repeat(60));
        System.out.println("  🚀 SolverCF API - Cloudflare Turnstile Demo (Java)");
        System.out.println("=".repeat(60));

        String configPath = new File("config.json").exists() ? "config.json"
                : new File("../config.json").exists() ? "../config.json"
                : "config.json";

        String configContent = Files.exists(Path.of(configPath)) ? Files.readString(Path.of(configPath)) : "";

        String envKey = System.getenv("SOLVERCF_CLIENT_KEY");
        if (envKey == null) envKey = System.getenv("SOLVERCF_API_KEY");
        String clientKey = envKey != null ? envKey : extractString(configContent, "clientKey");
        if (clientKey == null) clientKey = extractString(configContent, "apiKey");

        String websiteUrl = extractNestedString(configContent, "turnstile", "websiteUrl");
        if (websiteUrl == null) websiteUrl = "https://solvercf.com/demo/cloudflare-turnstile";
        String websiteKey = extractNestedString(configContent, "turnstile", "websiteKey");
        if (websiteKey == null) websiteKey = "0x4AAAAAAB__YGWiObopXheP";
        String verifyUrl = extractNestedString(configContent, "turnstile", "verifyUrl");
        if (verifyUrl == null) verifyUrl = "https://solvercf.com/token/demo/verify-turnstile";

        if (clientKey == null || clientKey.isBlank() || clientKey.equals("YOUR_API_KEY_HERE")) {
            System.out.println("[!] Error: Please set your clientKey in config.json or export SOLVERCF_API_KEY.");
            return;
        }

        System.out.println("\n[1/3] 📝 Creating Turnstile task...");
        String createRes;
        try {
            createRes = createTask(clientKey, websiteUrl, websiteKey);
        } catch (Exception e) {
            System.out.printf("      [x] Network error calling createTask: %s\n", e.getMessage());
            return;
        }

        Integer errorId = extractInt(createRes, "errorId");
        if (errorId != null && errorId != 0) {
            String errorCode = extractString(createRes, "errorCode");
            String errorDesc = extractString(createRes, "errorDescription");
            System.out.printf("      [x] Create task failed (Code: %s): %s\n", errorCode, errorDesc);
            return;
        }

        String taskId = extractString(createRes, "taskId");
        System.out.printf("      [✓] Task ID: %s\n", taskId);

        System.out.println("\n[2/3] ⏳ Polling result every 1.5s...");
        String token = null;
        String userAgent = null;
        long startTime = System.currentTimeMillis();
        int attempt = 0;

        while ((System.currentTimeMillis() - startTime) < 90000) {
            Thread.sleep(1500);
            attempt++;
            double elapsed = Math.round((System.currentTimeMillis() - startTime) / 100.0) / 10.0;

            String resultRes;
            try {
                resultRes = getTaskResult(clientKey, taskId);
            } catch (Exception e) {
                System.out.printf("      ➜ [#%d] Polling warning: %s\n", attempt, e.getMessage());
                continue;
            }

            Integer pollErrId = extractInt(resultRes, "errorId");
            if (pollErrId != null && pollErrId != 0) {
                System.out.printf("      [x] Polling error: %s\n", resultRes);
                return;
            }

            String status = extractString(resultRes, "status");
            if ("ready".equals(status)) {
                token = extractString(resultRes, "token");
                userAgent = extractString(resultRes, "userAgent");
                double totalTime = Math.round((System.currentTimeMillis() - startTime) / 10.0) / 100.0;
                Double cost = extractDouble(resultRes, "cost");
                System.out.printf("      [✓] Solved in %.2fs | Cost: $%s\n", totalTime, cost != null ? cost : 0.0);
                break;
            } else if ("failed".equals(status) || "expired".equals(status)) {
                System.out.printf("      [x] Task ended with status: %s\n", status);
                return;
            } else {
                System.out.printf("      ➜ [#%d] Status: %s (%.1fs elapsed)...\n", attempt, status, elapsed);
            }
        }

        if (token == null) {
            System.out.println("      [x] Timeout waiting for token.");
            return;
        }

        String tokenPreview = token.length() > 50 ? token.substring(0, 40) + "..." + token.substring(token.length() - 10) : token;
        System.out.printf("      [✓] Token: %s\n", tokenPreview);

        System.out.println("\n[3/3] 📡 Submitting token to verification endpoint...");
        HttpRequest.Builder verifyBuilder = HttpRequest.newBuilder()
                .uri(URI.create(verifyUrl))
                .header("Content-Type", "application/json; charset=utf-8")
                .header("Referer", websiteUrl)
                .timeout(Duration.ofSeconds(30))
                .POST(HttpRequest.BodyPublishers.ofString(String.format("{\"token\":\"%s\"}", escapeJson(token))));

        if (userAgent != null && !userAgent.isBlank()) {
            verifyBuilder.header("User-Agent", userAgent);
        }

        HttpResponse<String> verifyResponse;
        try {
            verifyResponse = client.send(verifyBuilder.build(), HttpResponse.BodyHandlers.ofString());
        } catch (Exception e) {
            System.out.printf("      [x] Network error during verification: %s\n", e.getMessage());
            return;
        }

        String verifyBody = verifyResponse.body();

        System.out.println("\nVerify Response:");
        System.out.println(verifyBody);

        String unescapedBody = verifyBody.replace("\\\"", "\"").replace("\\n", "\n").replace("\\\\", "\\");
        System.out.println("-".repeat(60));
        if (verifyBody.contains("\"success\":true") || verifyBody.contains("\"success\": true") ||
            unescapedBody.contains("\"success\":true") || unescapedBody.contains("\"success\": true")) {
            System.out.println("[🎉 SUCCESS] Cloudflare Turnstile verified successfully!");
        } else {
            System.out.println("[x] Verification failed.");
        }
        System.out.println("=".repeat(60));
    }

    private static String escapeJson(String s) {
        return s == null ? "" : s.replace("\\", "\\\\").replace("\"", "\\\"");
    }

    private static String extractString(String json, String key) {
        if (json == null) return null;
        Pattern p = Pattern.compile("\"" + Pattern.quote(key) + "\"\\s*:\\s*\"([^\"]+)\"");
        Matcher m = p.matcher(json);
        return m.find() ? m.group(1) : null;
    }

    private static Integer extractInt(String json, String key) {
        if (json == null) return null;
        Pattern p = Pattern.compile("\"" + Pattern.quote(key) + "\"\\s*:\\s*([0-9]+)");
        Matcher m = p.matcher(json);
        return m.find() ? Integer.parseInt(m.group(1)) : null;
    }

    private static Double extractDouble(String json, String key) {
        if (json == null) return null;
        Pattern p = Pattern.compile("\"" + Pattern.quote(key) + "\"\\s*:\\s*([0-9.]+)");
        Matcher m = p.matcher(json);
        return m.find() ? Double.parseDouble(m.group(1)) : null;
    }

    private static String extractNestedString(String json, String parentKey, String childKey) {
        if (json == null) return null;
        Pattern parentPattern = Pattern.compile("\"" + Pattern.quote(parentKey) + "\"\\s*:\\s*\\{([^\\}]+)\\}");
        Matcher parentMatcher = parentPattern.matcher(json);
        if (parentMatcher.find()) {
            return extractString(parentMatcher.group(1), childKey);
        }
        return null;
    }
}
