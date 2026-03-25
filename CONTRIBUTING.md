# Contributing to FinanceAdvisor

Thank you for your interest in contributing. Please read this document
fully before writing any code — it will save both of us time.

---

## Before you start

**Open an issue first.**

Describe what you want to build or fix and wait for a maintainer to approve
the approach before writing any code. This prevents effort being spent on
PRs that won't merge due to architectural conflicts.

For bug reports, use the Bug Report issue template.
For new features, use the Feature Request issue template.

---

## Setup

1. Fork the repository on GitHub
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/FinanceAdvisor
   cd FinanceAdvisor
   ```
3. Follow the local environment guide: [docs/dev-setup.md](docs/dev-setup.md)
4. Create a branch from `main`:
   ```bash
   git checkout -b feature/FIN-XX-short-description
   ```

---

## Making changes

### Standards to follow
- Coding standards: [docs/engineering-playbook.md](docs/engineering-playbook.md)
- Commit format: Conventional Commits (enforced by commitlint)
- All new public methods must have XML doc comments
- All new engine methods must have unit tests for cache hit, cache miss,
  and exception paths

### Before pushing
```bash
dotnet format --verify-no-changes --severity error
dotnet build --configuration Release
dotnet test --no-build --configuration Release
```

All three must pass. CI will reject PRs where they don't.

---

## Submitting a PR

- Fill in the PR template completely — PRs with empty templates are closed
- Link the GitHub issue your PR addresses
- Keep PRs small and focused — one concern per PR
- A maintainer will review within 7 days

---

## What we will not accept

| Not accepted | Reason |
|---|---|
| Plugins added to Agent 2 (FinancialAnalystAgent) | Breaks the zero-hallucination guarantee — this is a core architectural invariant |
| New external API dependencies without prior issue discussion | Affects security surface and maintenance burden |
| Changes to IPortfolioEngine, IMarketDataProvider, or INewsEngine without a deprecation path | Breaking interface changes affect all implementors |
| Hardcoded secrets, tokens, or API keys | Security — use user secrets or environment variables |
| Magic numbers in production code | All constants belong in AppConstants.cs |
| PRs that reduce test coverage below thresholds | Core: 90%, Services: 75%, API: 50% |

---

## License

By contributing, you agree that your contributions will be licensed
under the same MIT licence that covers this project.
