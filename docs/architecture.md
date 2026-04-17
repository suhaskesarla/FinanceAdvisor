System Architecture & Design: AI Financial Advisor

1. Executive Summary

This document outlines the architecture for a custom AI-driven personal financial copilot. The system securely connects to live brokerage data via Zerodha, ingests real-time market news via RSS, and utilizes a Large Language Model (Gemini 1.5 Flash) to provide conversational financial analysis.

To ensure low latency, zero hallucinations, and high cost-efficiency, the system employs a "Structured Data First" pattern, an intelligent Query Router to bypass the AI for simple commands, shared in-memory caching, and strict timeout boundaries.

2. Technology Stack

Application Framework: .NET 8 http://ASP.NET  Core (Web API + Background Worker)

AI Orchestration: Microsoft Semantic Kernel

LLM Reasoning Engine: Google Gemini 1.5 Flash API

Portfolio Data: Zerodha Kite Connect API (₹500/month Paid Tier)

Market Data: Free Financial APIs (e.g., Yahoo Finance, Alpha Vantage)

News Data: Free RSS Feeds (Moneycontrol, Economic Times, Mint)

User Interface: Telegram Bot API (via Webhooks)

Cloud Hosting: Azure App Service

3. System Architecture Diagram





ADR 1 (Agent Isolation): The "Analyst" AI has NO external tools. It only reasons over the JSON payload provided by the Gatherer to prevent hallucinations.

ADR 2 (Global Timeouts): The Azure Web API passes a strict CancellationToken (originating from the Telegram Webhook) all the way down through Semantic Kernel to the external APIs.

ADR 3 (Cost vs Isolation Tradeoff): The Background Worker and the Web API share the same Azure App Service process to avoid dual-deployment cost at MVP scale. Accepted risk: background jobs compete for memory and thread pool with live webhook traffic. Mitigation path: extract the Worker to a separate Azure Function when nightly jobs cause measurable API latency spikes.

4. Core Architectural Principles

A. Structured Data First (The Two-Agent Handoff) To structurally eliminate LLM tool-hallucination, the system utilizes a Two-Agent Sequential Handoff.

Agent 1 (Data Gatherer): Has exclusive access to the C# Data Engines via Native Plugins. It fetches data concurrently (e.g., Zerodha holdings, Yahoo Finance prices) and compiles a strict JSON payload. It does not speak to the user.

Agent 2 (Financial Analyst): Has ZERO external tools. It receives the user's prompt and the Gatherer's JSON payload. It acts strictly as a reasoning engine,structurally prevented from hallucinating data lookups.

B. Query Routing (Cost & Latency Optimization) Before invoking Semantic Kernel, a C# Rule Engine intercepts the query.

Simple Queries: Exact commands (e.g., /balance, /holdings) hit the rule engine, fetch data directly from the cache or Engine, and return immediately. Cost: $0. Latency: < 500ms.

Complex Queries: Strategy questions (e.g., "Should I reduce my banking exposure?") are routed to the Semantic Kernel for tool calling and LLM reasoning.

C. Unified Engine-Level Caching (IMemoryCache) Caching is abstracted directly into the Engine classes. Whether a request originates from the fast-path Query Router or the AI Orchestrator looping through tools, the system never makes duplicate external API calls within the TTL window.

Portfolio Data: 30 seconds TTL.

Market/Index Data: 60 seconds TTL.

News Feeds: 10 minutes TTL.

D. Resilience & Timeout Protection To prevent webhook pile-ups and ensure a responsive UI, strict CancellationToken boundaries are enforced natively through .NET:

External API Timeout: 3 seconds via .NET 8 native resilience pipelines (Microsoft.Extensions.Http.Resilience). If Yahoo or RSS hangs, the engine returns a partial payload and Agent 2 reasons over available data. Agent 2 carries its own hard 5s LLM timeout. Combined worst-case budget: 3s (API timeout) + 5s (LLM timeout) + formatting overhead must not exceed the 10s global webhook budget. Fallback messages returned to the user if budgets are breached: LLM timeout → "Analysis is taking too long — please try again in a moment." Zerodha unavailable → "Portfolio data is temporarily unavailable. Market and news context is still active." Total failure → "Something went wrong on our end. Please try again shortly.

E. Interface-Driven Data Sources All external data providers are wrapped in interfaces (e.g., IMarketDataProvider { GetStockPrice(string ticker); }). If a free data source changes its terms, a new provider can be swapped in via Dependency Injection without refactoring the core logic.

F. Observability & Logging Using built-in .NET ILogger, the system records the AI's execution path.

Example Log: User Query: "Why is portfolio down?" | Tools Triggered: [PortfolioEngine, MarketEngine] | Latency: 2.1s

5. Data Engine Modules

The system separates domain knowledge into specific engines:

Portfolio Engine: Authenticates with Zerodha via the Kite Connect OAuth flow. Note: Kite Connect requires a manual browser-based login once per trading day to generate a new request_token, which is then exchanged for an access_token and cached for the session. The engine reads holdings, calculates daily changes, and maps sector exposures.

Market Engine: Pulls broader context (like the Nifty 50 movement) so the AI understands if a stock drop is isolated or market-wide.

News Engine: Parses structured XML from top financial publishers to summarize sentiment without fragile web scraping.

6. Implementation Phases

Sprint 1: Infrastructure & Plumbing. Scaffold the .NET 8 Web API, configure Azure App Service deployment, setup Key Vault secrets, DI, Caching, and the Telegram Webhook receiver.

Sprint 2: Authentication, Data Engines & Query Router:. Implement Zerodha Kite Connect OAuth flow (including daily token refresh handling), build the Portfolio cache layer, integrate Yahoo Finance and XML RSS parsers, and build the C# Query Router for fast-path commands (/balance, /holdings). Fast-path commands must be fully testable independently of the AI brain.

Sprint 3: AI Brain & Interaction. Initialize Semantic Kernel, create the C# Native Plugins (Portfolio, Market, News engines as KernelFunctions on Agent 1 only), and establish the Two-Agent Gemini handoff — Agent 1 (data gatherer, all plugins) compiling a structured JSON payload, handed off to Agent 2 (financial analyst, zero plugins) for pure reasoning.

Sprint 4: Operations & Proactive Work. Build the IHostedService background worker to execute the 8:00 AM Morning Briefing and Nightly Portfolio Snapshots. Add structured telemetry.
