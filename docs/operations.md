# Operations and deployment

## Configuration

Use .NET user-secrets locally and a managed secret store or injected environment variables in deployment. Nested keys use `__` in environment variables. Never commit completed `.env`, live connection strings, signing keys, SMTP credentials, AI keys or Data Protection keys.

| Setting | Required behavior |
| --- | --- |
| `Jwt__SigningKey` | Independent random secret, at least 64 characters; rotate through a controlled session-expiry plan |
| `Jwt__OtpPepper` | Different random secret, at least 64 characters; rotate to invalidate pending codes |
| `Database__Provider` | `SqlServer` for production; `Sqlite` for local/small development use |
| `ConnectionStrings__Default` | SQL TLS/certificate validation in production; least-privileged login |
| `AllowedHosts` | Actual API hostnames, not `*` |
| `Cors__Origins__0` | Exact trusted frontend origin; add more indexed entries if needed |
| `ReverseProxy__KnownProxies__0` | Actual trusted proxy address; never trust arbitrary forwarded IPs |
| `Email__Host`, `Port`, `From`, `UserName`, `Password` | SMTP using TLS; development pickup must remain disabled in production |
| `DataProtection__KeysPath` | Persistent private key directory; shared by replicas; encrypted volume/managed key protection |
| `Storage__RootPath` | Private persistent directory, outside a served web root; scoped filesystem permissions |
| `Scanning__Host`, `Port` | Private ClamAV daemon; no public unauthenticated scanner exposure |
| `Chat__Enabled`, `BaseUrl`, `ApiKey`, `Model` | Enable only with a compatible configured provider and approved data handling |

## Release procedure

1. Build/test and run dependency audit. Review SQL migration script against a staging clone and back up the production database.
2. Apply migrations once as a deployment job using `dotnet Sawa3ed.Api.dll --migrate`, or a reviewed SQL script. Production app startup does not migrate. Do not run multiple role/bootstrap initializers simultaneously.
3. Bootstrap an administrator through the one-time CLI command if needed, then remove bootstrap credentials. Never run it on each startup.
4. Configure HTTPS termination and trusted proxy forwarding. Keep API and database on private networks where possible.
5. Persist and back up database, file bytes and Data Protection keys together. Filesystem writes and database commits are separate; failed uploads are compensated, but a process crash can leave orphaned private bytes.
6. Verify login, confirmation email, roles, upload/scan/download, AI provider and restore-from-backup in staging. Expose the readiness probe to the orchestrator.

The included Docker Compose configuration is local development. Production must replace its SA credential, use `Production`, configure proper TLS/SMTP/scanning and choose a persistent storage deployment. Swagger is only served in Development/Testing.

## Limits and horizontal scale

Authentication: 20 requests/minute/IP. General API: 120/minute/user. Uploads: 10/minute/user. Chat: 10/minute/user. Pre-auth admission: 300/minute/IP. Global in-flight cap: 100 requests. Rejections return 429 and Retry-After; clients should back off. Account lockout adds a persisted five-failure/15-minute limit. OTPs expire in ten minutes with a five-guess budget and at least 60 seconds between resends.

The in-process rate limiter and memory cache are **per instance**. Before scaling out, enforce aggregate quotas and abuse controls at an API gateway/Redis-backed layer, add global AI spend budgets, and use shared object storage. Persist/shared Data Protection keys are necessary for the email outbox. Configure ClamAV's stream limit to at least your maximum file size. ASP.NET multipart buffering uses temporary disk, which also needs capacity limits/monitoring.

The email outbox stores protected bodies and leases rows atomically. Delivery is at least once; a crash after sending but before acknowledgement can deliver a duplicate email. Delivery failures retry at one-minute intervals up to five attempts, bounded by code expiry. Delivered bodies are erased; expired outbox rows are removed. Pending OTP data is never written to application logs. Protect development `.eml` files because they contain usable codes.

## Retention, cleanup and monitoring

- Establish retention periods for chat/student data with the product owner. Soft delete is not physical erasure or a completed privacy-deletion workflow.
- File deletes retain private bytes. A maintenance job should remove bytes for tombstoned metadata after the retention window and reconcile unreferenced storage keys older than the upload/crash grace period. Do not serve a bucket/directory publicly to bypass ownership checks.
- Refresh-token history must remain until the session expires to detect replay. Purge expired sessions/tokens after an agreed audit window; do not discard active-session token history.
- Monitor database size, query/request latency, authentication failures, 429 rates, 5xx rates, outbox backlog, scanner outages, disk capacity and provider costs.
- Structured JSON logs use trace IDs and endpoint names; they avoid bodies, query strings, credentials, file content and chat content. Retain them in a central logging service with access control.
- `/health/live` indicates process responsiveness; `/health/ready` queries the role table. Neither checks SMTP, storage capacity, ClamAV health or the paid AI provider. Monitor those separately.
- Use a restore drill. A backup that has never been restored is not sufficient evidence of recoverability.

## Known boundaries

No system can guarantee that all future defects or abuse are eliminated. This implementation supplies defensive defaults and regression tests, not a production security certification. MFA/TOTP or WebAuthn for administrators, external OIDC identity, full upload media decoding/transcoding, distributed quotas, automatic retention jobs, managed object-storage implementation, an observability backend and content-entitlement policies are follow-up work. No actual existing production database was supplied or modified.
