<?php
$configPath = file_exists(__DIR__ . '/../config.json') ? __DIR__ . '/../config.json' : __DIR__ . '/../config.example.json';
$config = file_exists($configPath) ? json_decode(file_get_contents($configPath), true) : [];

$clientKey = getenv('SOLVERCF_CLIENT_KEY') ?: getenv('SOLVERCF_API_KEY') ?: ($config['clientKey'] ?? $config['apiKey'] ?? '');
$websiteUrl = $config['turnstile']['websiteUrl'] ?? 'https://solvercf.com/demo/cloudflare-turnstile';
$websiteKey = $config['turnstile']['websiteKey'] ?? '0x4AAAAAAB__YGWiObopXheP';
$verifyUrl = $config['turnstile']['verifyUrl'] ?? 'https://solvercf.com/token/demo/verify-turnstile';
$apiBaseUrl = 'https://solvercf.com/token/extension';

function createTask($clientKey, $websiteUrl, $websiteKey) {
    global $apiBaseUrl;
    $ch = curl_init("$apiBaseUrl/createTask");
    curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode([
        "clientKey" => $clientKey,
        "task" => [
            "type" => "TurnstileTask",
            "websiteUrl" => $websiteUrl,
            "websiteKey" => $websiteKey
        ]
    ]));
    curl_setopt($ch, CURLOPT_HTTPHEADER, ['Content-Type: application/json; charset=utf-8']);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);
    $res = curl_exec($ch);
    curl_close($ch);
    return json_decode($res, true) ?: [];
}

function getTaskResult($clientKey, $taskId) {
    global $apiBaseUrl;
    $ch = curl_init("$apiBaseUrl/getTaskResult");
    curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode([
        "clientKey" => $clientKey,
        "taskId" => $taskId
    ]));
    curl_setopt($ch, CURLOPT_HTTPHEADER, ['Content-Type: application/json; charset=utf-8']);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_TIMEOUT, 15);
    $res = curl_exec($ch);
    curl_close($ch);
    return json_decode($res, true) ?: [];
}

echo str_repeat("=", 60) . "\n";
echo "  🚀 SolverCF API - Cloudflare Turnstile Demo (PHP)\n";
echo str_repeat("=", 60) . "\n";

if (empty($clientKey) || $clientKey === 'YOUR_API_KEY_HERE') {
    die("[!] Error: Please set clientKey in config.json or export SOLVERCF_API_KEY.\n");
}

echo "\n[1/3] 📝 Creating Turnstile task...\n";
$createRes = createTask($clientKey, $websiteUrl, $websiteKey);

if (($createRes['errorId'] ?? 0) !== 0) {
    $errCode = $createRes['errorCode'] ?? 'UNKNOWN';
    $errDesc = $createRes['errorDescription'] ?? json_encode($createRes);
    die("      [x] Create task failed (Code: $errCode): $errDesc\n");
}

$taskId = $createRes['taskId'] ?? null;
echo "      [✓] Task ID: $taskId\n";

echo "\n[2/3] ⏳ Polling result every 1.5s...\n";
$solution = null;
$start = microtime(true);
$attempt = 0;

while ((microtime(true) - $start) < 90) {
    usleep(1500000); // 1.5s
    $attempt++;
    $elapsed = round(microtime(true) - $start, 1);

    $resultRes = getTaskResult($clientKey, $taskId);

    if (($resultRes['errorId'] ?? 0) !== 0) {
        $errCode = $resultRes['errorCode'] ?? 'UNKNOWN';
        $errDesc = $resultRes['errorDescription'] ?? json_encode($resultRes);
        die("      [x] Polling error (Code: $errCode): $errDesc\n");
    }

    $status = $resultRes['status'] ?? '';
    if ($status === 'ready') {
        $solution = $resultRes['solution'] ?? [];
        $totalTime = round(microtime(true) - $start, 2);
        $cost = $resultRes['cost'] ?? 0;
        echo "      [✓] Solved in {$totalTime}s | Cost: \${$cost}\n";
        break;
    } elseif ($status === 'failed' || $status === 'expired') {
        die("      [x] Task ended with status: $status\n");
    } else {
        echo "      ➜ [#{$attempt}] Status: $status ({$elapsed}s elapsed)...\n";
    }
}

if (!$solution) {
    die("      [x] Timeout waiting for token.\n");
}

$token = $solution['token'] ?? '';
$userAgent = $solution['userAgent'] ?? '';
$tokenPreview = strlen($token) > 50 ? substr($token, 0, 40) . '...' . substr($token, -10) : $token;
echo "      [✓] Token: $tokenPreview\n";

echo "\n[3/3] 📡 Submitting token to verification endpoint...\n";
$ch = curl_init($verifyUrl);
curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode(["token" => $token]));
$headers = [
    'Content-Type: application/json; charset=utf-8',
    "Referer: $websiteUrl"
];
if (!empty($userAgent)) {
    $headers[] = "User-Agent: $userAgent";
}
curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_TIMEOUT, 30);
$verifyRaw = curl_exec($ch);
curl_close($ch);

$verifyRes = json_decode($verifyRaw, true) ?: [];
if (isset($verifyRes['rawJson']) && is_string($verifyRes['rawJson'])) {
    $parsed = json_decode($verifyRes['rawJson'], true);
    if ($parsed) $verifyRes['rawJson'] = $parsed;
}

echo "\nVerify Response:\n";
echo json_encode($verifyRes, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES) . "\n";

echo str_repeat("-", 60) . "\n";
if (!empty($verifyRes['success'])) {
    echo "[🎉 SUCCESS] Cloudflare Turnstile verified successfully!\n";
} else {
    echo "[x] Verification failed.\n";
}
echo str_repeat("=", 60) . "\n";
?>
