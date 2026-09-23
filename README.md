# Sawa3ed | سواعد

A .NET 10 API foundation for the Saudi Qudrat/Tahsili learning platform. It includes authentication, role/permission enforcement, private file uploads, a learning-assistant integration, persistence foundations and operational controls.

**No education, question-bank, exam, subscription, payment, parent-linking or other business-model endpoints are implemented.** Those modules remain future work as requested. See [the Arabic requirements](docs/requirements.ar.md) and [the implementation roadmap](docs/architecture.md).

## Requirements

- [.NET SDK 10.0.401](https://dotnet.microsoft.com/download/dotnet/10.0) or a later stable patch in that feature band. `global.json` rejects previews.
- Any current editor; Visual Studio users need a version that supports .NET 10 and `.slnx`.
- SQLite is included for local testing. SQL Server is supported with separate migrations; Docker is optional.
- No SMTP or AI account is required just to start the API locally.

## Clone and run — Windows PowerShell

```powershell
git clone https://github.com/3laamobarak/sawa3ed.git
cd sawa3ed
powershell -ExecutionPolicy Bypass -File scripts/setup-dev.ps1
dotnet run --project src/Sawa3ed.Api --launch-profile http
```

The setup script creates **two independent random secrets** in .NET user-secrets and restores packages/tools. Run it once; running it again rotates the keys and invalidates existing sessions/codes.

macOS/Linux:

```bash
git clone https://github.com/3laamobarak/sawa3ed.git
cd sawa3ed
bash scripts/setup-dev.sh
dotnet run --project src/Sawa3ed.Api --launch-profile http
```

Open **http://localhost:5080/swagger**. Development startup applies SQLite migrations and seeds the six role names. Local database, uploads, keys and emails are ignored by Git. The development HTTP profile binds localhost; use HTTPS for remote access.

## Test login in Swagger

1. Call `POST /api/v1/auth/register` with email, displayName and a password of at least 12 characters containing uppercase, lowercase, a digit and a symbol.
2. Open the newest `.eml` file in `src/Sawa3ed.Api/App_Data/mail`. The background email dispatcher normally writes it within three seconds. This local pickup mode is limited to Development/Testing.
3. Call `POST /api/v1/auth/email/confirm` with the email and six-digit code.
4. Call `POST /api/v1/auth/login`. Copy `accessToken`.
5. Click **Authorize**, paste **only the token**, then call `/api/v1/auth/me`. Swagger adds `Bearer` automatically.

The public registration response intentionally looks the same for an existing account. New users receive **Student only**. The API never returns OTP codes or password-reset tokens in HTTP responses.

The refresh token is returned in the login JSON. `POST /auth/refresh` rotates it: replace the saved token after every success. Reusing an old token revokes that session. Store refresh tokens in a platform secure store or a server-side browser BFF; this API does not create browser auth cookies or a frontend storage strategy.

## Create the first administrator

Stop the running API. First run the API once, or run `dotnet run --project src/Sawa3ed.Api -- --migrate`. Then:

```powershell
dotnet user-secrets set "Bootstrap:Email" "your-admin@example.com" --project src/Sawa3ed.Api
dotnet user-secrets set "Bootstrap:Password" "YOUR-UNIQUE-STRONG-PASSWORD" --project src/Sawa3ed.Api
dotnet run --project src/Sawa3ed.Api -- --seed-admin
dotnet user-secrets remove "Bootstrap:Password" --project src/Sawa3ed.Api
dotnet user-secrets remove "Bootstrap:Email" --project src/Sawa3ed.Api
dotnet run --project src/Sawa3ed.Api --launch-profile http
```

Replace the placeholders locally. Bootstrap creates a **new confirmed administrator** and refuses to promote an existing account. There is no default admin, public admin signup or public claim-writing endpoint. After login, use the role endpoints to grant Teacher, Support, Supervisor, Parent or Admin to the appropriate accounts. Role changes revoke all sessions of the affected user, who must log in again. Parent is only a role at this stage; it does not grant access to a student's data.

## Endpoint surface

All paths below start with `/api/v1` unless stated otherwise.

| Endpoint | Access / behavior |
| --- | --- |
| `POST /auth/register` | Public; queues email confirmation; Student only |
| `POST /auth/login` | Public; confirmed email, lockout after failed attempts |
| `POST /auth/refresh` | Public; single-use refresh-token rotation |
| `POST /auth/otp/request` | Public; purpose `ConfirmEmail` or `ResetPassword` |
| `POST /auth/email/confirm` | Public; consume confirmation OTP |
| `POST /auth/password/forgot` | Public; generic response, queues reset OTP |
| `POST /auth/password/reset` | Public; consume reset OTP; revoke sessions |
| `POST /auth/password/change` | Authenticated; requires current password; revoke sessions |
| `GET /auth/me` | Authenticated current user |
| `POST /auth/logout` | Revoke current session immediately |
| `POST /auth/logout-all` | Revoke all sessions immediately |
| `GET /roles` | Admin; cached role catalogue and permission mapping |
| `POST /roles/users/{userId}` | Admin; body `{ "role": "Teacher" }` |
| `DELETE /roles/users/{userId}/{role}` | Admin; self-removal of Admin is rejected |
| `POST /files` | Admin/Teacher/Support/Supervisor; multipart files/folders |
| `GET /files` | Owner-only paginated metadata |
| `GET /files/{id}` | Owner-only attachment download; range requests supported |
| `DELETE /files/{id}` | Owner-only soft delete |
| `POST /chat/messages` | Authenticated learning assistant; requires provider configuration |
| `GET /chat/{conversationId}` | Owner-only paginated history |
| `DELETE /chat/{conversationId}` | Owner-only history soft delete |
| `/health/live`, `/health/ready` | Public process/readiness probes; no credentials/details |

Errors use Problem Details with a trace ID. Pagination defaults to 20, maximum 100. The HTTP examples are in [Sawa3ed.http](Sawa3ed.http).

## Private uploads and folders

Send multipart fields `Files` (one or more) and optional `RelativePaths` in matching order. Example: `Files=lesson.pdf`, `RelativePaths=math/fractions/lesson.pdf`. Browser directory selection uses each file's `webkitRelativePath`; the frontend must send these values. Folders are virtual metadata; clients never control filesystem paths. Empty folders are not represented, and archives are not extracted.

- JPEG, PNG, WebP, PDF and MP4 are allowed after extension and signature checks.
- Defaults: 10 MB/file, 10 files/request, 50 MB/request. Validated configuration supports at most 20 MB/file and 100 MB/batch.
- Bytes are streamed from the ASP.NET form buffer to private storage; they are **not stored as database blobs**. Multipart requests may spool to temporary disk, so provision and monitor that disk.
- Random storage keys, SHA-256 checksums, traversal rejection, owner checks and attachment downloads. MIME supplied by the client is not trusted.
- Production requires a reachable ClamAV `Scanning:Host`; scanning failures reject the upload. Signature checking alone is not malware detection or full media decoding.
- No public static-files middleware. Even administrators cannot download another user's file via these endpoints.
- Soft-deleted bytes are retained privately. Configure a retention/purge process before production; see [operations](docs/operations.md).

For full-size teaching videos, add object-storage multipart uploads and a media-transcoding pipeline. The current upload endpoint is for bounded files, not multi-gigabyte video ingestion. Replace `IFileStorage` to use Azure Blob/S3 without changing HTTP contracts.

## Configure the learning assistant

Chat is disabled by default and returns an explicit `503 chat_disabled`. Configure a **chat-completions-compatible provider** through user-secrets:

```powershell
dotnet user-secrets set "Chat:Enabled" "true" --project src/Sawa3ed.Api
dotnet user-secrets set "Chat:BaseUrl" "https://YOUR-PROVIDER/v1/" --project src/Sawa3ed.Api
dotnet user-secrets set "Chat:ApiKey" "YOUR-PROVIDER-KEY" --project src/Sawa3ed.Api
dotnet user-secrets set "Chat:Model" "YOUR-SUPPORTED-MODEL" --project src/Sawa3ed.Api
```

The adapter calls `chat/completions`, preserves `user`/`assistant` roles, limits history/context/output, cancels timed-out requests, rejects redirects, limits response bytes, and stores a turn only after a valid reply. It does not automatically retry paid calls. Provider failures return 502/504 and never appear as fabricated assistant history. Render replies as untrusted text or sanitized Markdown in a frontend.

This is a tutor integration, **not** an implemented diagnostic engine, grading service, retrieval system or access path to student records. Provider credentials, billing, model compatibility and Arabic answer quality require a live provider acceptance test.

## Database and layers

```text
src/Sawa3ed.Domain          Entities and audit/soft-delete contract; no framework dependencies
src/Sawa3ed.Application     Use-case contracts, DTOs, policies and file orchestration
src/Sawa3ed.Infrastructure  Identity, EF, repositories, transactions, email, storage, AI adapters
src/Sawa3ed.Api             HTTP, validation, policies, limits, Swagger and composition
tests/Sawa3ed.Tests         Unit and HTTP integration tests
```

Application references Domain. Infrastructure implements Application contracts. API composes them. Domain/Application do not reference EF, Identity or ASP.NET. Identity users live in Infrastructure rather than coupling the educational domain to the Identity framework.

Repository mutations only track changes; `IUnitOfWork.SaveChangesAsync` commits. `ExecuteInTransactionAsync` owns a transaction and rolls back/clears tracking on failure. Reads are untracked by default, paginated, projected and stably ordered. `GetAsync` uses a filtered query rather than `FindAsync`, which could return a tracked deleted entity.

All four `SaveChanges` overloads apply UTC audit fields, current actor, optimistic-concurrency versions and soft deletion. Global filters apply automatically to domain entities. `ExecuteUpdate`/`ExecuteDelete` bypass these hooks; they are deliberately restricted to session revocation and expiring outbox data in this implementation. Use explicit reviewed workflows for restore or permanent deletion.

SQLite and SQL Server have **separate checked-in migration sets and snapshots**. Development auto-migrates; production startup never changes schema.

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project src/Sawa3ed.Infrastructure --context SqliteAppDbContext --output-dir Persistence/Migrations/Sqlite
dotnet ef migrations add YourChange --project src/Sawa3ed.Infrastructure --context SqlServerAppDbContext --output-dir Persistence/Migrations/SqlServer
```

To use SQL Server, set `Database:Provider=SqlServer` and `ConnectionStrings:Default` through user-secrets/environment, then run `--migrate`. Use `dotnet ef migrations script --idempotent --context SqlServerAppDbContext --project src/Sawa3ed.Infrastructure` for a reviewed production deployment script. Migrations create **new Sawa3ed databases**; do not apply them to the old template's production database.

## Build and verify

```powershell
dotnet restore Sawa3ed.slnx
dotnet build Sawa3ed.slnx -c Release --no-restore
dotnet test Sawa3ed.slnx -c Release --no-build
dotnet list Sawa3ed.slnx package --vulnerable --include-transitive --no-restore
```

Tests cover registration/confirmation, OTP attempt limits/purpose, refresh replay, revocation, privilege boundaries, private files, chat isolation/deletion races, provider failures/timeouts, rate limiting, Swagger, audit/soft delete, transaction rollback, concurrency and architecture dependencies. GitHub Actions runs the HTTP suite against both SQLite and SQL Server and checks both migration snapshots. To run integration tests against a disposable SQL Server locally, set `SAWA3ED_TEST_SQLSERVER` to a connection string whose login may create/drop test databases. The fixture creates isolated `Sawa3edTest_...` databases and deletes only those databases. Dependabot proposes updates; compilation and dependency vulnerability warnings fail the build.

For an optional local SQL Server setup, copy `.env.example` to `.env`, fill strong independent secrets, then run `docker compose up --build`. The SQL Server image needs an x64 Docker host. This compose file is for local development and binds published ports to loopback. It uses SQL Server's SA account for convenience only; production requires a least-privileged application login and a separate migration identity.

See [architecture and audit](docs/architecture.md) and [production operations](docs/operations.md) before deploying. This foundation does not claim to eliminate every future defect or establish production performance without measurements.
