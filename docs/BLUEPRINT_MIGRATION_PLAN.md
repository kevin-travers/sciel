# Blueprint → Clean Architecture Migration Plan

Concrete, ordered steps to take `services/Blueprint` from its current repository-pattern shape to
the CQRS vertical-slice shape in `docs/STYLE_GUIDE.md`. Each step lists what's **added**,
**removed**, **updated**, or **renamed**, why, and what it buys you.

## Approach & ordering rule

`Category` is the only feature in the codebase today and no migration has been applied yet (the
`Migrations/` folder is empty) — there's no running system or deployed schema to keep alive in
parallel. So this is one coordinated restructure, not an old-system/new-system migration, done as
**one phase per project in dependency order**: `Common` → `Domain` → `Application` →
`Infrastructure` → `Service`. Because arrows point inward only, each phase only ever depends on a
phase you've already finished — the solution builds (or fails obviously) at the end of every phase,
instead of accumulating broken intermediate states across five projects at once.

Commit after each phase. Run `dotnet build Sciel.Blueprint.slnx` at the end of every phase; it should
go from "doesn't exist yet" straight to "compiles," never through a half-wired state.

---

## Phase 0 — Repo-wide scaffolding

Touches no business logic. Do this first so every project created afterward is analyzer-strict from
its first file instead of being retrofitted later.

- **Add** `services/Directory.Build.props` — `TargetFramework net10.0`, `Nullable enable`,
  `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `AnalysisMode All`, `AnalysisLevel latest`,
  `EnforceCodeStyleInBuild true`, `SonarAnalyzer.CSharp` package reference.
  - *Why:* today each `.csproj` repeats `TargetFramework`/`Nullable`/`ImplicitUsings` by hand and has
    no analyzer configuration at all.
  - *Benefit:* one place controls every service's language-version and strictness settings; a new
    project under `services/` inherits them for free by virtue of being in the folder tree.
- **Add** `services/Directory.Packages.props` — `ManagePackageVersionsCentrally true`, with a
  `<PackageVersion>` entry per package the solution uses (EF Core, Npgsql, FluentValidation, Scrutor,
  NetArchTest.Rules, Shouldly, xunit, Testcontainers.PostgreSql, etc.).
  - *Why:* right now `Sciel.Blueprint.Service.csproj` and `.Data.csproj` each pin EF Core to a
    slightly different patch version (`10.0.13` vs `10.0.12`) independently.
  - *Benefit:* every project resolves the same version of a shared dependency; bumping EF Core is a
    one-line change instead of a grep-and-replace across `.csproj` files.
- **Add** `services/.editorconfig` (`root = true`) — adapted from the template's: file-scoped
  namespaces enforced as errors, `var` rules, expression-bodied member rules, naming rules, and the
  specific `dotnet_diagnostic.*`/`S*` suppressions the template carries.
  - *Why:* there's no `.editorconfig` in the repo today, so style is whatever each file happens to be
    written in — hence the current mix of 2-space/4-space indent and the `Categoires`/`cateogry`
    typos slipping through.
  - *Benefit:* style becomes a build error, not a code-review nit; IDEs auto-format to the same rules
    for everyone.

**Validate:** `dotnet build Sciel.Blueprint.slnx` — should be unaffected (no project references the
new props files yet; they apply automatically once projects exist under `services/`).

---

## Phase 1 — `Sciel.Blueprint.Common` (new)

The one project with zero in-solution dependencies — build it first so `Domain` has something to
reference.

- **Add** `src/Sciel.Blueprint.Common/Sciel.Blueprint.Common.csproj` — plain class library, no
  package references.
- **Add** `Result.cs`, `ErrorType.cs`, `Error.cs`, `ValidationError.cs` — ported from the template
  near-verbatim (`Result`/`Result<T>`, `Error` with `Code`/`Description`/`ErrorType`, the four
  `Error.NotFound`/`Conflict`/`Problem`/`Failure` factories).
  - *Why:* nothing in the current codebase can express "this failed, here's a typed reason" without
    throwing. `CategoryNameConflictException` and the ad hoc `catch (Exception)` in
    `CategoriesEndpoints.GetCategory` exist because there's no alternative today.
  - *Benefit:* every later handler's signature (`Task<Result<Guid>>`) documents its own failure modes;
    endpoints get one uniform translation path (`result.Match(...)`) instead of a `try`/`catch` per
    endpoint.
- **Add** `Entity.cs` — base class giving `DomainEvents`/`Raise(...)`/`ClearDomainEvents()`.
  - *Why:* `Category` has no way to record "something happened" today; state changes are silent.
  - *Benefit:* lets `Domain` and `Application` opt into eventing per-entity, incrementally, without a
    messaging framework.
- **Add** `IDomainEvent.cs`, `IDomainEventHandler.cs`.
  - *Why/Benefit:* paired with `Entity.Raise(...)` above — the contract a dispatcher and handlers
    implement against.
- **Add** `IDateTimeProvider.cs` (`DateTime UtcNow { get; }`).
  - *Why:* nothing currently calls `DateTime.UtcNow` from application code (timestamps are
    DB-generated), but the moment a handler needs "now" for business logic (e.g. computing
    `DeletedAtUtc` itself instead of leaving it to EF), a raw `DateTime.UtcNow` call becomes
    untestable.
  - *Benefit:* handler unit tests can inject a fixed clock; this is cheap to add now versus
    retrofitting once three handlers already call `DateTime.UtcNow` directly.

**Validate:** `dotnet build src/Sciel.Blueprint.Common` — compiles standalone, no other project
references it yet.

---

## Phase 2 — `Sciel.Blueprint.Domain` (rename + trim `Core`)

- **Rename** `src/Sciel.Blueprint.Core/` → `src/Sciel.Blueprint.Domain/`, and
  `Sciel.Blueprint.Core.csproj` → `Sciel.Blueprint.Domain.csproj` (namespace
  `Sciel.Blueprint.Core.*` → `Sciel.Blueprint.Domain.*` throughout).
  - *Why:* "Core" doesn't say what the project is for; the style guide's layer is explicitly
    `Domain` — entities, enums, domain events, error catalogs, nothing else.
  - *Benefit:* the project name now matches what `ArchitectureTests` (Phase 7) asserts about it.
- **Update** `Sciel.Blueprint.Domain.csproj` — add `<ProjectReference>` to
  `Sciel.Blueprint.Common.csproj`.
- **Update** `Categories/Category.cs` — make `sealed`, inherit `Common.Entity`.
  - *Why:* needs `Raise(...)` to record domain events (used in Phase 3's handlers).
  - *Benefit:* `Category` can now participate in the event flow without any other shape change.
- **Add** `Categories/CategoryErrors.cs` — static class with `NotFound(Guid)` → `Error.NotFound`,
  `NameConflict(string)` → `Error.Conflict`, codes `"Categories.NotFound"` /
  `"Categories.NameConflict"`.
- **Remove** `Categories/CategoryNameConflictException.cs`.
  - *Why:* a thrown exception for an expected, user-facing condition (a duplicate name) is exactly
    what `Result`/`Error` replaces; `CategoryErrors.NameConflict` is its direct successor.
  - *Benefit:* the handler that returns this error (Phase 3) has a return type that documents the
    failure instead of hiding it behind a `throw`.
- **Add** `Categories/CategoryCreatedDomainEvent.cs`, `CategoryRemovedDomainEvent.cs`,
  `CategoryRestoredDomainEvent.cs` — `sealed record ... (Guid CategoryId) : IDomainEvent`.
  - *Why:* nothing currently announces "a category was created/removed/restored"; every consumer
    that might eventually care (cache invalidation, audit log, notifications) has no hook to attach
    to.
  - *Benefit:* the hook exists from day one even with zero handlers attached — adding a reaction
    later never requires touching the command handler that raises the event.
- **Remove** `Common/IRequestContext.cs`, `Common/RequestContext.cs`, `Common/PagedResult.cs` from
  this project (they move to `Application`/`Infrastructure` in Phases 3–4).
  - *Why:* none of these are domain concepts — `IRequestContext` is an application-layer
    abstraction (mirrors the template's `IUserContext`), `PagedResult<T>` is an application-layer
    pagination shape, and `RequestContext` (the implementation) is infrastructure.
  - *Benefit:* `Domain` ends up containing only what the style guide says it should — entities, their
    events, their errors — which is what makes "Domain has no dependency on Application" a
    meaningful, checkable claim in Phase 7.

**Validate:** `dotnet build src/Sciel.Blueprint.Domain` — will fail until `Data`/`Service` are updated
to the new namespace in later phases; that's expected mid-migration. Confirm it compiles in
isolation (no project currently references it yet after the rename, so this build should succeed on
its own).

---

## Phase 3 — `Sciel.Blueprint.Application` (new)

The largest phase: this is where the repository pattern actually disappears.

### 3a. Abstractions

- **Add** `Abstractions/Data/IApplicationDbContext.cs` — `DbSet<Category> Categories { get; }`,
  `Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)`.
- **Add** (moved from `Domain`) `Abstractions/Identity/IRequestContext.cs` — same shape
  (`UserId`, `OrganizationId`, `SpaceId`, `AccessToken`).
- **Add** (moved from `Domain`) `Abstractions/Pagination/PagedResult.cs` — unchanged.
- **Add** `Abstractions/Messaging/ICommand.cs`, `ICommandHandler.cs`, `IQuery.cs`,
  `IQueryHandler.cs` — ported from the template.
- **Add** `Abstractions/Behaviors/ValidationDecorator.cs`, `LoggingDecorator.cs` — ported from the
  template (generic over `TCommand`/`TQuery`).
- **Add** `DependencyInjection.cs` — `AddApplication()`: Scrutor scan registering every
  `ICommandHandler<>`/`ICommandHandler<,>`/`IQueryHandler<,>`/`IDomainEventHandler<>`
  implementation, `services.Decorate(...)` wiring validation-then-logging, and
  `AddValidatorsFromAssembly(...)`.
  - *Why (all of the above):* this is the layer that doesn't exist at all today — there is currently
    no "use case" abstraction, just a 6-method repository interface. `IApplicationDbContext` is what
    replaces `ICategoryRepository`: handlers query/mutate through it directly instead of through a
    hand-written method per operation.
  - *Benefit:* new use cases stop requiring a new repository method + a new interface signature;
    they're just a new folder. Cross-cutting concerns (validation, logging) apply to every handler
    automatically via the decorators, instead of being hand-written (or forgotten) per method.

### 3b. The five `Category` use-case slices

Each is a new folder `Application/Categories/{UseCase}/`, replacing the corresponding
`ICategoryRepository` method:

| New slice | Replaces | Files |
|---|---|---|
| `Create/` | `AddAsync` | `CreateCategoryCommand.cs`, `CreateCategoryCommandHandler.cs`, `CreateCategoryCommandValidator.cs` |
| `Get/` | `GetAllAsync` | `GetCategoriesQuery.cs`, `GetCategoriesQueryHandler.cs`, `CategoryResponse.cs` |
| `GetById/` | `GetByIdAsync` | `GetCategoryByIdQuery.cs`, `GetCategoryByIdQueryHandler.cs`, `CategoryResponse.cs` |
| `Remove/` | `RemoveAsync` | `RemoveCategoryCommand.cs`, `RemoveCategoryCommandHandler.cs`, `RemoveCategoryCommandValidator.cs` |
| `Restore/` | `RestoreAsync` | `RestoreCategoryCommand.cs`, `RestoreCategoryCommandHandler.cs` |

- *Why one row per slice instead of one combined "category service":* this is the vertical-slice
  rule from the style guide (§2/§3) — each use case is independently addable/removable/testable.
- *Benefit, concretely for this codebase:* `GetAllAsync`/`UpdateAsync`/`GetDeletedAsync` are
  `NotImplementedException` stubs today because the repository interface committed to their shape
  before anyone needed them. A `GetCategoriesQueryHandler` only gets written when `Get/` is actually
  scaffolded — there's no interface forcing five methods to exist before four of them are ready.
- **Remove** nothing yet in this phase — `ICategoryRepository`/`CategoryRepository` are deleted in
  Phase 4 once `Infrastructure` no longer needs them and `Service` has been repointed (Phase 5) to
  call handlers instead.
- `CreateCategoryCommandHandler` is the one worth calling out explicitly: it does the name-uniqueness
  check (`context.Categories.AnyAsync(...)`), constructs the `Category`, stamps
  `OrganizationId`/`SpaceId` from `IRequestContext` (never from the command), raises
  `CategoryCreatedDomainEvent`, and returns `Result.Failure<Guid>(CategoryErrors.NameConflict(...))`
  on conflict — see the worked example already in `docs/STYLE_GUIDE.md` §3.
- `RemoveCategoryCommandHandler` replaces today's `RemoveAsync`, which just sets `DeletedAtUtc` — now
  it also raises `CategoryRemovedDomainEvent` and returns `CategoryErrors.NotFound(id)` instead of
  requiring the caller to have already loaded the entity (today's endpoint does its own
  `GetByIdAsync` first to work around that).

**Validate:** `dotnet build src/Sciel.Blueprint.Application` — depends on `Domain` + `Common` only,
both already built; should compile standalone.

---

## Phase 4 — `Sciel.Blueprint.Infrastructure` (rename + rework `Data`)

- **Rename** `src/Sciel.Blueprint.Data/` → `src/Sciel.Blueprint.Infrastructure/`, csproj and
  namespace to match (`Sciel.Blueprint.Infrastructure.*`).
- **Update** `BlueprintDbContext.cs`:
  - Implement `Application.Abstractions.Data.IApplicationDbContext`.
  - Add an `IRequestContext requestContext` constructor parameter (stored as a field).
  - In `OnModelCreating`, add the tenant filter using the already-defined-but-unused
    `QueryFilterNames.Tenant` constant: `HasQueryFilter(QueryFilterNames.Tenant, category =>
    category.OrganizationId == _organizationId && category.SpaceId == _spaceId)`, composed with the
    existing soft-delete filter.
  - *Why:* tenant isolation is currently enforced only by `CategoryRepository` remembering to add a
    `.Where(...)` clause on every method (and `GetAllAsync`/`GetDeletedAsync` don't, since they're
    unimplemented) — there is no backstop.
  - *Benefit:* every query through `IApplicationDbContext.Categories`, from any current or future
    handler, is automatically tenant-scoped — a handler would have to go out of its way
    (`IgnoreQueryFilters()`) to leak cross-tenant data, rather than go out of its way to prevent it.
- **Update** `BlueprintDbContextFactory.cs` — construct a placeholder `IRequestContext` (the dev
  values currently hardcoded in `Program.cs`) to satisfy the new constructor parameter at design
  time. *Why:* `dotnet ef migrations add` builds the model, not real requests, so the tenant filter's
  actual values don't matter here — only that the context can be constructed at all.
- **Remove** `Categories/CategoryRepository.cs`, and `ICategoryRepository.cs` (deleted from `Domain`
  in this phase, having been fully superseded by Phase 3's handlers + `IApplicationDbContext`).
  - *Why:* its job is done — every method it had either became a handler-side LINQ query or is
    superseded by the interface `BlueprintDbContext` now implements directly.
  - *Benefit:* one less interface/implementation pair to keep in sync; `BlueprintDbContext` is the
    single place that talks to `DbSet<Category>`.
- **Update** `Categories/CategoryConfiguration.cs` — drop the explicit `builder.ToTable("Categories")`
  call (see next bullet).
- **Add** `EFCore.NamingConventions` package reference; **update** `AddInfrastructure()`'s
  `UseNpgsql(...)` call to also chain `.UseSnakeCaseNamingConvention()`.
  - *Why:* no migration has been generated yet, so this is the only point in the project's life where
    switching naming conventions is free — after the first migration ships, it's a breaking schema
    rename instead. The current `"Categories"` PascalCase table name only works today because nothing
    has queried Postgres by hand yet; unquoted PascalCase identifiers get silently lowercased by
    Postgres the moment someone does.
  - *Benefit:* `categories`/`organization_id`/`created_at_utc` match Postgres community convention and
    work correctly whether or not an identifier happens to be quoted.
- **Add** `Identity/RequestContext.cs` (moved from `Domain/Common/RequestContext.cs`, namespace
  updated) — unchanged shape/content; still a dev stub for now.
- **Add** `Time/DateTimeProvider.cs` — one-line `IDateTimeProvider` implementation
  (`DateTime.UtcNow`).
- **Add** `DependencyInjection.cs` — `AddInfrastructure(IConfiguration configuration)`: reads the
  `Blueprint` connection string, registers `BlueprintDbContext` (as both itself and
  `IApplicationDbContext`), `IRequestContext`, `IDateTimeProvider`.
  - *Why:* this registration logic currently lives inline in `Program.cs` (`AddDbContext<...>`,
    `AddScoped<IRequestContext>(...)`, `AddScoped<ICategoryRepository, CategoryRepository>()`).
  - *Benefit:* `Program.cs` becomes a short, uniform composition of `AddApplication()` /
    `AddInfrastructure(...)` / `AddPresentation()` calls (Phase 5) instead of a growing list of
    `AddScoped<...>` lines that have to be remembered per feature.

**Validate:** `dotnet build src/Sciel.Blueprint.Infrastructure` — depends on `Application`, which is
already built.

---

## Phase 5 — `Sciel.Blueprint.Service` (rework)

- **Add** `Endpoints/IEndpoint.cs` — `void MapEndpoint(IEndpointRouteBuilder app)`.
- **Add** `Endpoints/Tags.cs` — `public const string Categories = "Categories";`.
- **Add** `Extensions/EndpointExtensions.cs` — `AddEndpoints(Assembly)` (DI registration of every
  `IEndpoint`) and `MapEndpoints(WebApplication)` (calls `MapEndpoint` on each), both via assembly
  scanning.
- **Add** `Infrastructure/CustomResults.cs` — `CustomResults.Problem(Result)` →
  `ProblemHttpResult`/`ProblemDetails`, mapping `ErrorType` to status code.
  - *Why/Benefit (all three above):* this is what makes endpoint registration automatic — a new
    `IEndpoint` in `Endpoints/{Feature}/{UseCase}.cs` is picked up by `MapEndpoints()` with no edit to
    `Program.cs`, the same way handlers are picked up by Scrutor in `Application`.
- **Add** `Endpoints/Categories/Create.cs`, `GetById.cs`, `Get.cs`, `Remove.cs`, `Restore.cs` — each
  an `internal sealed class : IEndpoint` resolving its handler interface from DI and returning
  `result.Match(Results.Ok, CustomResults.Problem)` / `Results.NoContent`.
- **Remove** `Endpoints/Categories/CategoriesEndpoints.cs`, `CategoryExtensions.cs`.
  - *Why:* `CategoriesEndpoints.GetCategory` currently throws its own exception and catches it to
    produce a 404 — the exact anti-pattern `Result`/`CustomResults.Problem` exists to remove.
    `CategoryExtensions.ToCategoryResponse()`/`ToCategory()` stop being needed once query handlers
    project straight to `CategoryResponse` via `.Select(...)` and command handlers build `Category`
    inline (both shown in Phase 3).
  - *Benefit:* no endpoint contains translation logic beyond request→command mapping and
    `result.Match(...)`; all five endpoints follow one template.
- **Update** `Endpoints/Categories/CreateCategoryRequest.cs` — unchanged shape (`Name`,
  `Description` only — it already follows the "no server-owned fields" rule), namespace fixed
  (`Sciel.Blueprint.Service.Categoires` → `Sciel.Blueprint.Service.Endpoints.Categories`, fixing the
  existing typo).
- **Update** `CategoryResponse.cs` — namespace fix only; content unchanged.
- **Add** `Extensions/ServiceCollectionExtensions.cs` — `AddPresentation()` (OpenAPI, Scalar — moved
  out of `Program.cs`'s inline calls).
- **Update** `Program.cs`:
  - Replace the inline `AddDbContext<...>`/`AddScoped<IRequestContext>(...)`/
    `AddScoped<ICategoryRepository, ...>()` block with
    `builder.Services.AddApplication().AddPresentation().AddInfrastructure(builder.Configuration);`.
  - Replace `app.MapCategoryEndpoints();` with `app.MapEndpoints();`.
  - Keep the existing `appsettings`/user-secrets/connection-string comments and the auth `TODO` —
    they're still accurate and worth keeping, not migration noise.
  - *Why:* mirrors the template's `Program.cs` composition exactly (§1 of the style guide).
  - *Benefit:* `Program.cs` stops growing by one line per feature; adding `Category`'s sixth use case
    (or a whole new entity) touches zero lines here.

**Validate:** `dotnet build Sciel.Blueprint.slnx` end-to-end, then `dotnet run --project
src/Sciel.Blueprint.Service` and exercise all five endpoints manually (or via
`services/postman/collections`) against a local Postgres.

---

## Phase 6 — Solution file & project wiring

- **Update** `Sciel.Blueprint.slnx` — replace the three `<Project Path>` entries with the five new
  project paths (`Common`, `Domain`, `Application`, `Infrastructure`, `Service`), plus the three test
  projects added in Phase 7.
  - *Why:* the `.slnx` still points at `Sciel.Blueprint.Core`/`.Data` paths that no longer exist after
    the renames above.
  - *Benefit:* `dotnet build`/`dotnet test` against the `.slnx` picks up everything in one command
    again.

---

## Phase 7 — Test projects (new — none exist today)

`tests/` is currently empty, so this phase is pure addition, not migration.

- **Add** `tests/Sciel.Blueprint.Application.UnitTests/` — handler tests (one per `Result.Failure`
  path + happy path) using a fake/in-memory `IApplicationDbContext`; validator tests via
  FluentValidation's `TestValidate`.
- **Add** `tests/Sciel.Blueprint.ArchitectureTests/` — `NetArchTest.Rules` assertions: `Domain` has no
  dependency on `Application`/`Infrastructure`/`Service`; `Application` has no dependency on
  `Infrastructure`/`Service`; `Infrastructure` has no dependency on `Service`.
  - *Why this one specifically, now:* every "Why"/"Benefit" claim in Phases 2–5 about layer
    boundaries is just a doc assertion until something enforces it. This is also the cheapest time to
    add it — five small projects, easy to verify the rules pass immediately.
  - *Benefit:* a future change that accidentally adds `using Sciel.Blueprint.Infrastructure;` to
    `Application` fails the build, not code review.
- **Add** `tests/Sciel.Blueprint.IntegrationTests/` — `WebApplicationFactory`-based HTTP tests against
  a real Postgres via `Testcontainers.PostgreSql`, covering the five endpoints.

**Validate:** `dotnet test Sciel.Blueprint.slnx` (Docker running, for the integration tests and
Testcontainers).

---

## Phase 8 — Cleanup pass

- **Remove** any leftover `bin/`/`obj/` folders under the old `Sciel.Blueprint.Core`/`.Data` paths if
  the rename left stragglers (rename-on-disk tooling sometimes does).
- **Update** `services/.postman` / `services/postman` collections if any request URLs or example
  payloads reference the old response/request shapes (they shouldn't, since the HTTP contract is
  unchanged — only the implementation behind it moved).
- **Grep** the solution for `Sciel.Blueprint.Core`/`Sciel.Blueprint.Data` to confirm no stale
  `using`/namespace references survived the renames.

---

## Deferred, deliberately not part of this pass

- `UpdateCategoryCommand` and `GetDeletedCategoriesQuery` — today's `NotImplementedException` stubs.
  Scaffold them as their own slices once there's a concrete need, using `Create`/`Get` as the
  templates; inventing their validation/business rules here would be guessing requirements, not
  restructuring existing code.
- Real authentication (`IRequestContext` stays a dev stub). Swapping in JWT-claims-based resolution is
  an `Infrastructure`-only change later and doesn't block any of the above.
- A `Worker` project, `RowVersion` concurrency tokens, `HybridCache` — none are needed until something
  automated writes to `Category` or a hot read shows up; the style guide calls these out as "add when
  needed," not day-one scaffolding.
