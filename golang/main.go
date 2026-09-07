package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

const apiBaseUrl = "https://solvercf.com/token/extension"

var httpClient = &http.Client{Timeout: 30 * time.Second}

type Config struct {
	ClientKey string `json:"clientKey"`
	ApiKey    string `json:"apiKey"`
	Turnstile struct {
		WebsiteUrl string `json:"websiteUrl"`
		WebsiteKey string `json:"websiteKey"`
		VerifyUrl  string `json:"verifyUrl"`
	} `json:"turnstile"`
	RecaptchaV3 struct {
		WebsiteUrl string `json:"websiteUrl"`
		WebsiteKey string `json:"websiteKey"`
		PageAction string `json:"pageAction"`
		VerifyUrl  string `json:"verifyUrl"`
	} `json:"recaptchaV3"`
}

func loadConfig() (*Config, error) {
	paths := []string{
		"config.json",
		filepath.Join("..", "config.json"),
		filepath.Join("..", "config.example.json"),
	}

	var data []byte
	var err error
	for _, p := range paths {
		if data, err = os.ReadFile(p); err == nil {
			break
		}
	}
	if len(data) == 0 {
		return nil, fmt.Errorf("neither config.json nor config.example.json found")
	}

	var cfg Config
	if err := json.Unmarshal(data, &cfg); err != nil {
		return nil, err
	}
	return &cfg, nil
}

func CreateTask(clientKey, websiteUrl, websiteKey, taskType, pageAction string) (string, error) {
	taskPayload := map[string]interface{}{
		"type":       taskType,
		"websiteUrl": websiteUrl,
		"websiteKey": websiteKey,
	}
	if pageAction != "" {
		taskPayload["pageAction"] = pageAction
	}

	payload := map[string]interface{}{
		"clientKey": clientKey,
		"task":      taskPayload,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return "", err
	}

	resp, err := httpClient.Post(apiBaseUrl+"/createTask", "application/json; charset=utf-8", bytes.NewBuffer(body))
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	var result map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return "", err
	}

	if errId, ok := result["errorId"].(float64); ok && errId != 0 {
		return "", fmt.Errorf("code %v: %v", result["errorCode"], result["errorDescription"])
	}

	if tid, ok := result["taskId"].(string); ok {
		return tid, nil
	}
	return "", fmt.Errorf("unexpected response: %v", result)
}

func GetTaskResult(clientKey, taskId string) (map[string]interface{}, error) {
	payload := map[string]string{
		"clientKey": clientKey,
		"taskId":    taskId,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return nil, err
	}

	pollClient := &http.Client{Timeout: 15 * time.Second}
	resp, err := pollClient.Post(apiBaseUrl+"/getTaskResult", "application/json; charset=utf-8", bytes.NewBuffer(body))
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var result map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, err
	}
	return result, nil
}

func runTurnstileDemo(clientKey string, cfg *Config) {
	fmt.Println(strings.Repeat("=", 60))
	fmt.Println("  🚀 SolverCF API - Cloudflare Turnstile Demo (Golang)")
	fmt.Println(strings.Repeat("=", 60))

	siteUrl := cfg.Turnstile.WebsiteUrl
	if siteUrl == "" {
		siteUrl = "https://solvercf.com/demo/cloudflare-turnstile"
	}
	siteKey := cfg.Turnstile.WebsiteKey
	if siteKey == "" {
		siteKey = "0x4AAAAAAB__YGWiObopXheP"
	}
	verifyUrl := cfg.Turnstile.VerifyUrl
	if verifyUrl == "" {
		verifyUrl = "https://solvercf.com/token/demo/verify-turnstile"
	}

	fmt.Println("\n[1/3] 📝 Creating Turnstile task...")
	taskId, err := CreateTask(clientKey, siteUrl, siteKey, "TurnstileTask", "")
	if err != nil {
		fmt.Printf("      [x] Create task failed: %v\n", err)
		return
	}
	fmt.Printf("      [✓] Task ID: %s\n", taskId)

	fmt.Println("\n[2/3] ⏳ Polling result every 1.5s...")
	var token, userAgent string
	start := time.Now()

	for attempt := 1; time.Since(start) < 90*time.Second; attempt++ {
		time.Sleep(1500 * time.Millisecond)
		elapsed := fmt.Sprintf("%.1fs", time.Since(start).Seconds())

		res, err := GetTaskResult(clientKey, taskId)
		if err != nil {
			fmt.Printf("      ➜ [#%d] Polling warning: %v\n", attempt, err)
			continue
		}

		if errId, ok := res["errorId"].(float64); ok && errId != 0 {
			fmt.Printf("      [x] Polling error: code %v (%v)\n", res["errorCode"], res["errorDescription"])
			return
		}

		status, _ := res["status"].(string)
		if status == "ready" {
			if sol, ok := res["solution"].(map[string]interface{}); ok {
				token, _ = sol["token"].(string)
				userAgent, _ = sol["userAgent"].(string)
			}
			totalTime := fmt.Sprintf("%.2fs", time.Since(start).Seconds())
			cost := res["cost"]
			fmt.Printf("      [✓] Solved in %s | Cost: $%v\n", totalTime, cost)
			break
		} else if status == "failed" || status == "expired" {
			fmt.Printf("      [x] Task ended with status: %s\n", status)
			return
		} else {
			fmt.Printf("      ➜ [#%d] Status: %s (%s elapsed)...\n", attempt, status, elapsed)
		}
	}

	if token == "" {
		fmt.Println("      [x] Timeout waiting for token.")
		return
	}

	tokenPreview := token
	if len(token) > 50 {
		tokenPreview = token[:40] + "..." + token[len(token)-10:]
	}
	fmt.Printf("      [✓] Token: %s\n", tokenPreview)

	fmt.Println("\n[3/3] 📡 Submitting token to verification endpoint...")
	verifyPayload, _ := json.Marshal(map[string]string{"token": token})
	req, _ := http.NewRequest("POST", verifyUrl, bytes.NewBuffer(verifyPayload))
	req.Header.Set("Content-Type", "application/json; charset=utf-8")
	req.Header.Set("Referer", siteUrl)
	if userAgent != "" {
		req.Header.Set("User-Agent", userAgent)
	}

	resp, err := httpClient.Do(req)
	if err != nil {
		fmt.Printf("      [x] Verification error: %v\n", err)
		return
	}
	defer resp.Body.Close()

	bodyBytes, _ := io.ReadAll(resp.Body)
	var formatted map[string]interface{}
	_ = json.Unmarshal(bodyBytes, &formatted)

	// Format nested rawJson if string
	if rawStr, ok := formatted["rawJson"].(string); ok {
		var rawObj map[string]interface{}
		if err := json.Unmarshal([]byte(rawStr), &rawObj); err == nil {
			formatted["rawJson"] = rawObj
		}
	}

	prettyBytes, _ := json.MarshalIndent(formatted, "", "  ")
	fmt.Println("\nVerify Response:")
	fmt.Println(string(prettyBytes))

	fmt.Println(strings.Repeat("-", 60))
	if success, ok := formatted["success"].(bool); ok && success {
		fmt.Println("[🎉 SUCCESS] Cloudflare Turnstile verified successfully!")
	} else {
		fmt.Println("[x] Verification failed.")
	}
	fmt.Println(strings.Repeat("=", 60))
}

func runRecaptchaV3Demo(clientKey string, cfg *Config) {
	fmt.Println(strings.Repeat("=", 60))
	fmt.Println("  🚀 SolverCF API - Google reCAPTCHA v3 Demo (Golang)")
	fmt.Println(strings.Repeat("=", 60))

	siteUrl := cfg.RecaptchaV3.WebsiteUrl
	if siteUrl == "" {
		siteUrl = "https://solvercf.com/demo/recaptchav3"
	}
	siteKey := cfg.RecaptchaV3.WebsiteKey
	if siteKey == "" {
		siteKey = "6LeSYmUtAAAAADy78hcvyfvGXqWKqN4aaFs2vyG8"
	}
	pageAction := cfg.RecaptchaV3.PageAction
	if pageAction == "" {
		pageAction = "demo_page"
	}
	verifyUrl := cfg.RecaptchaV3.VerifyUrl
	if verifyUrl == "" {
		verifyUrl = "https://solvercf.com/token/demo/verify-recaptcha"
	}

	fmt.Println("\n[1/3] 📝 Creating reCAPTCHA v3 task...")
	taskId, err := CreateTask(clientKey, siteUrl, siteKey, "RecaptchaV3TaskProxyless", pageAction)
	if err != nil {
		fmt.Printf("      [x] Create task failed: %v\n", err)
		return
	}
	fmt.Printf("      [✓] Task ID: %s\n", taskId)

	fmt.Println("\n[2/3] ⏳ Polling result every 1.5s...")
	var token, userAgent string
	start := time.Now()

	for attempt := 1; time.Since(start) < 90*time.Second; attempt++ {
		time.Sleep(1500 * time.Millisecond)
		elapsed := fmt.Sprintf("%.1fs", time.Since(start).Seconds())

		res, err := GetTaskResult(clientKey, taskId)
		if err != nil {
			fmt.Printf("      ➜ [#%d] Polling warning: %v\n", attempt, err)
			continue
		}

		if errId, ok := res["errorId"].(float64); ok && errId != 0 {
			fmt.Printf("      [x] Polling error: code %v (%v)\n", res["errorCode"], res["errorDescription"])
			return
		}

		status, _ := res["status"].(string)
		if status == "ready" {
			if sol, ok := res["solution"].(map[string]interface{}); ok {
				token, _ = sol["token"].(string)
				userAgent, _ = sol["userAgent"].(string)
			}
			totalTime := fmt.Sprintf("%.2fs", time.Since(start).Seconds())
			cost := res["cost"]
			fmt.Printf("      [✓] Solved in %s | Cost: $%v\n", totalTime, cost)
			break
		} else if status == "failed" || status == "expired" {
			fmt.Printf("      [x] Task ended with status: %s\n", status)
			return
		} else {
			fmt.Printf("      ➜ [#%d] Status: %s (%s elapsed)...\n", attempt, status, elapsed)
		}
	}

	if token == "" {
		fmt.Println("      [x] Timeout waiting for token.")
		return
	}

	tokenPreview := token
	if len(token) > 50 {
		tokenPreview = token[:40] + "..." + token[len(token)-10:]
	}
	fmt.Printf("      [✓] Token: %s\n", tokenPreview)

	fmt.Println("\n[3/3] 📡 Submitting token to verification endpoint...")
	verifyPayload, _ := json.Marshal(map[string]string{"token": token})
	req, _ := http.NewRequest("POST", verifyUrl, bytes.NewBuffer(verifyPayload))
	req.Header.Set("Content-Type", "application/json; charset=utf-8")
	req.Header.Set("Referer", siteUrl)
	if userAgent != "" {
		req.Header.Set("User-Agent", userAgent)
	}

	resp, err := httpClient.Do(req)
	if err != nil {
		fmt.Printf("      [x] Verification error: %v\n", err)
		return
	}
	defer resp.Body.Close()

	bodyBytes, _ := io.ReadAll(resp.Body)
	var formatted map[string]interface{}
	_ = json.Unmarshal(bodyBytes, &formatted)

	var score interface{}
	var host string
	if rawStr, ok := formatted["rawJson"].(string); ok {
		var rawObj map[string]interface{}
		if err := json.Unmarshal([]byte(rawStr), &rawObj); err == nil {
			formatted["rawJson"] = rawObj
			score = rawObj["score"]
			if h, ok := rawObj["hostname"].(string); ok {
				host = h
			}
		}
	}

	prettyBytes, _ := json.MarshalIndent(formatted, "", "  ")
	fmt.Println("\nVerify Response:")
	fmt.Println(string(prettyBytes))

	fmt.Println(strings.Repeat("-", 60))
	if success, ok := formatted["success"].(bool); ok && success {
		scoreText := ""
		if score != nil {
			scoreText = fmt.Sprintf(" (Score: %v)", score)
		}
		fmt.Printf("[🎉 SUCCESS] reCAPTCHA v3 verified successfully!%s\n", scoreText)
		if score != nil || host != "" {
			fmt.Printf("[📊 Result] Score: %v | Action: %s | Host: %s\n", score, pageAction, host)
		}
	} else {
		fmt.Println("[x] Verification failed.")
	}
	fmt.Println(strings.Repeat("=", 60))
}

func main() {
	cfg, err := loadConfig()
	if err != nil {
		fmt.Printf("[!] Error loading config: %v\n", err)
		return
	}

	clientKey := os.Getenv("SOLVERCF_CLIENT_KEY")
	if clientKey == "" {
		clientKey = os.Getenv("SOLVERCF_API_KEY")
	}
	if clientKey == "" {
		clientKey = cfg.ClientKey
	}
	if clientKey == "" {
		clientKey = cfg.ApiKey
	}

	if clientKey == "" || clientKey == "YOUR_API_KEY_HERE" {
		fmt.Println("[!] Error: Please configure clientKey in config.json or export SOLVERCF_API_KEY.")
		return
	}

	mode := "all"
	if len(os.Args) > 1 {
		mode = strings.ToLower(os.Args[1])
	}

	if mode == "turnstile" || mode == "all" {
		runTurnstileDemo(clientKey, cfg)
	}

	if mode == "all" {
		fmt.Println()
	}

	if mode == "recaptcha" || mode == "recaptchav3" || mode == "all" {
		runRecaptchaV3Demo(clientKey, cfg)
	}
}
