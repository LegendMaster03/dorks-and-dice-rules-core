# Tests

- `RulesCore.Host.Tests/` — .NET 10 xUnit tests using a real ephemeral Kestrel listener to verify lifecycle, configuration failure, liveness/readiness and absence of public/private API routes.
- Existing Python JSON/source-integrity tests remain separate and unchanged.

Run `dotnet test RulesCore.slnx -c Release` for the Phase 1B host; existing Python checks continue through the preexisting structural validation workflow.
