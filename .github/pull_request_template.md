## Jira ticket
<!-- Link: https://your-org.atlassian.net/browse/FIN-XX -->

## What changed and why
<!-- One paragraph — not what the code does, but why it exists.
     What problem does this solve? What decision did you make and why? -->

## Type of change
- [ ] `feat` — new feature
- [ ] `fix` — bug fix
- [ ] `refactor` — no behaviour change
- [ ] `chore` — config, tooling, DI wiring
- [ ] `test` — tests only
- [ ] `docs` — documentation only

## Checklist
- [ ] `dotnet format --verify-no-changes` passes locally
- [ ] `dotnet build --configuration Release` passes
- [ ] `dotnet test` passes with zero failures
- [ ] New public methods have XML doc comments (`///`)
- [ ] No secrets, API keys, or tokens anywhere in this diff
- [ ] `CancellationToken ct` is the last parameter on all new async methods
- [ ] No magic numbers — constants added to `AppConstants.cs` if needed
- [ ] If a new plugin was added — it is on **Agent 1 only**, never Agent 2
- [ ] Test coverage includes cache hit, cache miss, and exception paths for any new engine

## Screenshots / logs (if relevant)
<!-- Paste a Telegram screenshot or log output if this is a behaviour change -->
