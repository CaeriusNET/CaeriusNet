# Security Policy

We take the security of **CaeriusNet** seriously. This document describes how to report
vulnerabilities responsibly and what to expect from us in return.

## Supported Versions

Only the **latest minor** released to [nuget.org](https://www.nuget.org/packages/CaeriusNet)
receives security fixes. Older minors may receive a backport on a best-effort basis when the
fix is mechanical and the change surface is small.

| Version | Supported          |
| ------- | ------------------ |
| latest  | ✅                 |
| < latest minor | ⚠️ best-effort |

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security reports.**

Use GitHub's private vulnerability reporting:

1. Navigate to [`Security` → `Report a vulnerability`](https://github.com/CaeriusNET/CaeriusNet/security/advisories/new)
   on this repository.
2. Provide a clear description, reproduction steps, affected versions, and (if possible) a
   proposed fix or mitigation.
3. We will acknowledge receipt within **72 hours** and provide a status update within
   **7 calendar days**.

If GitHub Security Advisories are unavailable to you, email the maintainers via the address
listed on the [GitHub profile of the repository owner](https://github.com/AriusII).

### What to include

- Affected package version(s) and target framework.
- Connection-string topology if relevant (Encrypt, MARS, ApplicationIntent…).
- Minimal reproducer (a single test or a short C# program is ideal).
- Impact assessment (data exfiltration, RCE, DoS, privilege escalation…).
- Any CVE/CWE references.

### What we promise

- A non-public triage in the security advisory.
- A coordinated disclosure timeline (typically **≤ 90 days** unless extension is mutually
  agreed).
- Credit in the advisory and the [CHANGELOG](./CHANGELOG.md), unless you prefer to remain
  anonymous.
- A patched release pushed to nuget.org and a CVE assigned via GitHub when applicable.

## Hardening Notes for Consumers

CaeriusNet is intentionally narrow: stored procedures, TVPs and transactions only. To stay
safe in production:

- **Always** use `Encrypt=True` and **never** disable certificate validation outside of
  controlled test environments.
- Run with the **least-privileged SQL login** required by your stored procedures.
- Treat the `[GenerateDto]` / `[GenerateTvp]` source-generator output as part of your
  security boundary — review the generated code in PR diffs.
- Pin the package version in your `.csproj` and let Dependabot raise security PRs.

## Dependencies

We track upstream advisories for `Microsoft.Data.SqlClient`,
`Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Logging.Abstractions`, and
`StackExchange.Redis`. Dependabot is enabled for the entire repository.
