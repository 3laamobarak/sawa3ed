# Architecture review and implementation decisions

Reference reviewed: `3laamobarak/Base-Repository-.Net`, commit `da9fce85b24d1858722a9fb35a74a659b15f1075`. The target `3laamobarak/sawa3ed` was empty. The original repository is unchanged; this is a new Sawa3ed foundation, not a migration of an existing deployed database.

## Findings corrected

| Reference issue | New behavior |
| --- | --- |
| Account `AddRole` had no authorization requirement | Admin permission policy, fixed role catalogue, no caller-supplied claims |
| Long-lived access tokens, plaintext/reused refresh tokens | Short access JWTs, hashed random refresh tokens, rotation, replay revocation |
| Username used as `sub`, `uid` and `roles` inconsistently consumed | Stable user ID in `sub`; explicit `role`, `permission`, `sid` and security-stamp mapping |
| No immediate access-token invalidation | Indexed session/user check on authenticated requests; logout/password/role changes invalidate sessions |
| `Random` OTPs, plaintext storage, returned codes, no guess budget | Cryptographic six-digit OTPs, purpose binding, keyed hashes, expiry, five attempts, resend throttling and durable email queue |
| Application directly referenced Infrastructure; Domain referenced EF | Inward dependencies with framework-free Domain/Application |
| Some repository calls saved, others did not | Mutations only track; Unit of Work owns commit/transaction |
| Audit applied only in one async save path; range deletes could become physical | All save overloads audit and soft delete; automatic filters and concurrency tokens |
| `FindAsync` could return tracked deleted data | Filtered LINQ lookup |
| Unbounded/unstably ordered list reads | Bounded pages, projection, stable ordering and indexes |
| Uploads trusted MIME, were public, and stored compressed bytes in SQL | Private external byte storage, metadata-only SQL, signatures, limits, ownership, scanner integration |
| Chat comparison lowercased sender then compared to `User` | Exact `user`/`assistant` roles covered by a regression test |
| Chat swallowed errors and persisted apology messages as successful answers | Explicit 502/504, atomic successful message pair, bounded context and response |
| Wildcard CORS and weak refresh cookie configuration | Explicit origins; bearer/body token contract; no automatic auth cookies |
| Raw exception messages exposed through controllers | Central Problem Details and redacted structured logs |
| IDE/cache metadata and unrelated template features | Clean solution, `.gitignore`, setup scripts, test project, CI and documentation |

The reference's Stripe secret setting is a placeholder, not a confirmed exposed credential. It was not copied. No live credentials are included in this project.

## SOLID design and extension guide

One type per file is a navigation convention. SOLID also requires separating responsibilities and keeping callers independent of implementation details. The foundation applies both:

| Principle | Concrete implementation |
| --- | --- |
| Single responsibility | `AccountService`, `AuthSessionService`, `PasswordService` and `EmailVerificationService` handle separate identity use cases. OTP challenge state, OTP hashing, JWT issuance and session revocation have separate components. Each EF entity mapping has an `IEntityTypeConfiguration<T>` file; audit/soft-delete processing is isolated from mappings. |
| Open/closed | Add a storage backend through `IFileStorage`, an AI provider through `IChatClient`, a scanner through `IUploadScanner`, or email delivery through `IEmailTransport`. Register the replacement in the relevant composition module; file/chat/outbox workflows do not change. |
| Liskov substitution | SQLite and SQL Server implementations use the same persistence contracts and HTTP regression suite. Repository mutations stage changes and never secretly commit. Chat adapters preserve ordered roles, honor cancellation, and return a reply or an explicit failure; fake providers exercise those contracts. New adapters must preserve these behaviors. |
| Interface segregation | Controllers depend on focused account, session, password, verification and role contracts instead of one broad auth interface. Chat persistence and context construction have separate contracts. Application code does not receive `DbContext` or `IQueryable`. |
| Dependency inversion | Application file/chat services depend on ports declared in Application; Infrastructure supplies their adapters. Identity services use abstractions for OTP challenges, hashing, email enqueueing, token issuance and session revocation. Controllers depend on Application contracts; composition selects concrete implementations. |

### Where to make changes

| Change | Location |
| --- | --- |
| Request/response shape or use-case interface | One named file in `Application/Auth`, `Application/Files` or `Application/Chat` |
| Identity account/session/password workflow | Matching service in `Infrastructure/Identity` |
| Chat orchestration or context policy | `Application/Chat/ChatService.cs` or `ChatContextBuilder.cs` |
| Entity indexes, relationships or column limits | Matching file in `Infrastructure/Persistence/Configurations` |
| Audit fields, soft deletion or concurrency stamping | `Infrastructure/Persistence/AuditChangeProcessor.cs` |
| Database provider registration | `Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` |
| JWT validation parameters or live session check | `ConfigureJwtBearerOptions.cs` or `SessionValidationEvents.cs` |
| Email retry/lease rules or provider transport | `EmailOutboxProcessor.cs` or an `IEmailTransport` implementation |
| HTTP security, quotas, Swagger or pipeline ordering | Focused files under `Api/Configuration`, `Api/Security` and `Api/Observability` |

Keep one handwritten top-level type in each matching file, including DTOs and interfaces. Private nested test helpers may remain next to their owning test; EF-generated partial migration/designer files follow EF's conventions. Group files by feature inside each layer. Prefer composition; introduce interfaces at real substitution or dependency boundaries, not for every stateless helper.

Transaction boundaries belong to the use-case implementation. `IOtpChallengeService` stages changes in its caller's serializable transaction; callers persist failed guesses too. Password reset, OTP consumption and session revocation commit atomically. `IEmailQueue` stages encrypted outbox data in that same database transaction. The dispatcher schedules work, the outbox processor manages leases/retries, and the selected transport performs delivery.

Chat loads a tracked conversation through `IChatRepository`, builds bounded context, calls `IChatClient` without holding a database transaction, then commits the message pair through `IUnitOfWork`. The conversation version still detects concurrent sends and deletion. The refactor preserves existing HTTP routes, JSON contracts and database schemas; existing migration sets remain authoritative.

## Performance decisions

- A modular monolith fits the current foundation: one deployable service, no premature message broker or distributed transaction.
- Async IO and cancellation throughout; long AI calls do not hold database transactions.
- Default untracked DTO projections, bounded history/pages, supporting indexes and no binary SQL payloads.
- Memory cache for the low-volatility role catalogue. Authorization/session state remains authoritative in the database to preserve immediate revocation.
- No global entity cache, no cache of OTPs/passwords/tokens, and no shared user-response cache. Educational metadata can later use a versioned cache with explicit invalidation after writes.
- Per-IP admission before authentication, per-user/IP endpoint quotas, a global in-flight request cap, password lockout and bounded upload/context sizes.
- Database retries are intentionally not enabled for arbitrary writes: blindly replaying auth or external side effects can duplicate work. Introduce retries only around well-defined idempotent transactions.
- Query latency, database capacity and p95/p99 request latency must be measured in the target environment. `benchmarks/smoke.js` is a starting scenario, not a claimed result.

## Roles and claims

| Role | Foundation permissions |
| --- | --- |
| Student | Chat; own identity/files/history where applicable |
| Parent | Chat; own identity/history; no linked-student access yet |
| Teacher | Chat; private uploads |
| Support | Chat; private uploads |
| Supervisor | Chat; private uploads |
| Admin | Role assignment; chat; private uploads |

`Permissions.ForRole` is the sole permission catalogue. JWT permissions are derived from trusted stored roles. There is no arbitrary claim editor or caller-provided signup role. When adding a permission, define its policy, endpoints, tests and role mapping together. Logout/role changes are effective on subsequent requests; an already authorized in-flight request can finish.

## Requirements mapped to future modules

The supplied Arabic specification is retained in `requirements.ar.md`. These areas are planned, not implemented here:

1. **Learning content:** subject → skill → level → lesson, teacher ownership, draft/review/publish workflow; private media entitlements separate from upload ownership.
2. **Question bank and exams:** immutable published question versions, correct-answer isolation, server-authoritative timing/scoring, autosave idempotency, randomized attempt snapshots and progression rules.
3. **Subscriptions/payments:** verified signed provider webhooks, idempotent event inbox, transaction ledger, payment/entitlement separation; never activate from a browser success page.
4. **Student/parent relationship:** verified guardian links and per-student resource authorization; a Parent role alone must not grant access.
5. **Study plans and mastery:** configurable mastery calculations, evidence/history, diagnostic tests and readiness scores validated against real learning outcomes.
6. **Notifications/certificates:** durable delivery, opt-in preferences, issuance/verification identifiers and retention rules.
7. **AI personalization:** curated retrieval with entitlement checks, evaluation dataset, prompt-injection resistance, student-data minimization, monitoring and explicit cost limits.

Before exposing any such endpoint, add its domain rules, DTOs, application use case, resource policy, EF mapping/indexes, migration for both providers and integration tests. Do not generate generic CRUD controllers over database entities.
