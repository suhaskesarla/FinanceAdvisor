# Local Development Setup

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Git | Latest | https://git-scm.com |
| Node.js | 20+ (for commitlint) | https://nodejs.org |
| ngrok | Latest | https://ngrok.com (for Telegram webhook testing) |
| Visual Studio / Rider / VS Code | Latest | Your preference |

---

## First-time setup

### 1. Clone and restore
```bash
git clone https://github.com/your-handle/FinanceAdvisor
cd FinanceAdvisor
dotnet restore
```

### 2. Install commitlint and husky
```bash
npm install
npx husky init
chmod +x .husky/pre-commit .husky/commit-msg .husky/pre-push
```

### 3. Set user secrets (never use appsettings.json for secrets)
```bash
cd src/FinanceAdvisor.API

dotnet user-secrets set "Telegram:BotToken" "your-telegram-bot-token"
dotnet user-secrets set "Telegram:WebhookUrl" "https://your-ngrok-url/api/telegram/webhook"
dotnet user-secrets set "Zerodha:ApiKey" "your-zerodha-api-key"
dotnet user-secrets set "Zerodha:ApiSecret" "your-zerodha-api-secret"
dotnet user-secrets set "Gemini:ApiKey" "your-gemini-api-key"
```

### 4. Start ngrok (exposes localhost to Telegram)
```bash
ngrok http 5000
```
Copy the `https://` URL and update your `Telegram:WebhookUrl` secret.

### 5. Run the application
```bash
dotnet run --project src/FinanceAdvisor.API
```

On startup, the app will automatically register the webhook URL with Telegram.

---

## Daily Zerodha token refresh

Kite Connect requires a manual OAuth login once per trading day.

1. Visit: `https://kite.zerodha.com/connect/login?api_key=YOUR_API_KEY`
2. Log in with your Zerodha credentials
3. After redirect, copy the `request_token` from the URL parameters
4. Send `/refresh YOUR_REQUEST_TOKEN` to your Telegram bot

The bot will exchange it for an access_token and cache it for the session.

---

## Running tests

```bash
# All tests
dotnet test

# With coverage report
dotnet test --collect:"XPlat Code Coverage"

# Specific project
dotnet test tests/FinanceAdvisor.Tests.Unit
```

---

## Verifying code quality locally

```bash
# Format check (must pass before commit)
dotnet format --verify-no-changes --severity error

# Fix formatting automatically
dotnet format

# Full build in Release mode
dotnet build --configuration Release
```

---

## IDE recommendations

**VS Code extensions:**
- C# Dev Kit
- GitLens
- Conventional Commits (joshgoebel.commit-message-editor)

**Rider / Visual Studio:**
- Enable EditorConfig support (on by default)
- Enable "Treat warnings as errors" in build settings (handled by Directory.Build.props)
