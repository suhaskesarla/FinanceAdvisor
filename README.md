# FinanceAdvisor — AI Personal Financial Copilot

> A self-hosted AI financial advisor that connects to your Zerodha brokerage
> account, monitors real-time market data, and delivers conversational
> portfolio analysis via Telegram.

![CI](https://github.com/your-handle/FinanceAdvisor/actions/workflows/ci.yml/badge.svg)
![Coverage](https://sonarcloud.io/api/project_badges/measure?project=FinanceAdvisor&metric=coverage)
![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=FinanceAdvisor&metric=alert_status)

Built with **.NET 8** · **Semantic Kernel** · **Gemini Flash** · **Azure App Service**

---

## What it does

- Answers natural language questions about your portfolio via Telegram
- Routes simple commands (`/balance`, `/holdings`) at <500ms with zero AI cost
- Uses a **two-agent architecture** to structurally eliminate LLM hallucinations
- Delivers a proactive morning briefing at 8 AM with overnight changes and top headlines
- Ingests real-time market news via RSS — no fragile scraping

---

## Architecture

Full design document: [docs/architecture.md](docs/architecture.md)

The system uses a **Two-Agent Sequential Handoff** pattern:

| Agent | Role | Plugins |
|---|---|---|
| Agent 1 — Data Gatherer | Calls Zerodha, Yahoo Finance, and RSS feeds concurrently. Compiles a structured JSON payload. Does not speak to the user. | All `[KernelFunction]` plugins |
| Agent 2 — Financial Analyst | Receives the user prompt and the JSON payload. Reasons purely over the data. | **Zero** — structurally cannot hallucinate |

The JSON payload is the hard boundary between agents. Agent 2 has no tools and
no access to external data — it can only reason about what Agent 1 has already
fetched and validated.

---

## Tech stack

| Layer | Technology |
|---|---|
| Framework | .NET 8 ASP.NET Core |
| AI Orchestration | Microsoft Semantic Kernel |
| LLM | Google Gemini Flash (version pinned in config) |
| Portfolio Data | Zerodha Kite Connect API |
| Market Data | Yahoo Finance API |
| News | RSS feeds (Moneycontrol, Economic Times, Mint) |
| User Interface | Telegram Bot API (Webhooks) |
| Hosting | Azure App Service |
| Resilience | Microsoft.Extensions.Http.Resilience |
| Caching | IMemoryCache (Redis planned — see roadmap) |

---

## Getting started locally

See [docs/dev-setup.md](docs/dev-setup.md) for the full setup guide.

**Quick start:**
```bash
git clone https://github.com/your-handle/FinanceAdvisor
cd FinanceAdvisor
dotnet restore

# Set secrets via .NET user secrets (never appsettings.json)
cd src/FinanceAdvisor.API
dotnet user-secrets set "Telegram:BotToken" "your-token"
dotnet user-secrets set "Telegram:WebhookUrl" "https://your-ngrok-url/api/telegram/webhook"
dotnet user-secrets set "Zerodha:ApiKey" "your-key"
dotnet user-secrets set "Zerodha:ApiSecret" "your-secret"
dotnet user-secrets set "Gemini:ApiKey" "your-key"

dotnet run
```

> **Note on Zerodha:** Kite Connect requires a manual browser-based OAuth login
> once per trading day. See [docs/dev-setup.md](docs/dev-setup.md) for the
> daily token refresh process.

---

## Project structure

```
src/
├── FinanceAdvisor.Core         # Interfaces, DTOs, domain models, constants
├── FinanceAdvisor.Services     # Engines, orchestration, formatting, jobs
└── FinanceAdvisor.API          # Web API, middleware, DI wiring, background worker
tests/
├── FinanceAdvisor.Tests.Unit
└── FinanceAdvisor.Tests.Integration
docs/
├── architecture.md
├── engineering-playbook.md
├── dev-setup.md
└── adr/                        # Architecture Decision Records
```

---

## Running tests

```bash
dotnet test --configuration Release
```

Coverage thresholds enforced in CI:

| Project | Minimum coverage |
|---|---|
| FinanceAdvisor.Core | 90% |
| FinanceAdvisor.Services | 75% |
| FinanceAdvisor.API | 50% |

---

## Roadmap

Planned improvements documented in [docs/architecture.md](docs/architecture.md):

- [ ] Distributed cache (Redis / Azure Cache for Redis)
- [ ] Queue-based processing (Azure Service Bus)
- [ ] Persistent user state (PostgreSQL / Azure Cosmos DB)
- [ ] Observability (Azure Application Insights)
- [ ] Rate limiting per user (ASP.NET Rate Limiting Middleware)
- [ ] Risk Engine, Sentiment Analysis Engine
- [ ] Multi-instance deployment

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
Please open an issue before submitting a PR.

---

## Security

See [SECURITY.md](SECURITY.md).
Do not open public issues for security vulnerabilities.

---

## License

MIT — see [LICENSE](LICENSE)
