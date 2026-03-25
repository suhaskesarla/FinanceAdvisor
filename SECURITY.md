# Security Policy

## Reporting a vulnerability

**Do NOT open a public GitHub issue for security vulnerabilities.**

Because this project handles brokerage API tokens and financial data,
security issues must be reported privately.

**Email:** your-email@domain.com
**Response time:** within 48 hours
**Resolution target:** within 7 days for critical issues

---

## What to include in your report

- Description of the vulnerability
- Steps to reproduce
- Potential impact (what an attacker could achieve)
- Suggested fix if you have one

---

## Scope

The following are in scope for security reports:

- Telegram Bot token or Zerodha API key exposure
- Zerodha session token handling weaknesses
- Telegram webhook spoofing or replay attack vectors
- Secrets appearing in logs or error messages
- Dependency vulnerabilities with a direct exploit path

The following are out of scope:

- Vulnerabilities in Telegram, Zerodha, or Google's own infrastructure
- Issues requiring physical access to the deployment machine
- Social engineering attacks

---

## Supported versions

Only the latest version on `main` is actively supported.
