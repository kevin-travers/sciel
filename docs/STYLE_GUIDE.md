# Sciel — C# / .NET / Clean Architecture Style Guide

Conventions for every .NET service under `services/`. Modeled on the
[jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) template
(CQRS vertical slices, `Result`-based error handling, Scrutor-registered handlers — no MediatR), the
official [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions),
[.NET framework design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/),
and [EF Core docs](https://learn.microsoft.com/en-us/ef/core/), adapted to Sciel's multi-tenant
(`Organization` → `Space`) domain.

When this guide and the upstream sources disagree, this guide wins — it exists to capture the
project-specific calls, not restate the generic ones.

This replaces the previous repository-pattern guide. See **§11 Migrating existing code** for what
that means for `Sciel.Blueprint` concretely.

---

## 1. Solution structure

Each service is five projects, dependencies pointing inward only. `Infrastructure` and `Worker` gain
sub-areas as real work shows up (e.g. a Terraform runner); don't pre-build folders for them.

| Project | Owns | May reference |
|---|---|---|
| `{Service}.Common` | `Result`/`Result<T>`, `Error`/`ErrorType`, `ValidationError`, `Entity` (base, domain events), `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider` | nothing in-solution |
| `{Service}.Domain` | Entities, enums, domain events, `{Entity}Errors` catalogs | `Common` |
| `{Service}.Application` | Use cases as vertical slices (`{Feature}/{UseCase}/`): `ICommand`/`IQuery` + handlers, FluentValidation validators, validation/logging decorators, abstractions (`IApplicationDbContext`, `IRequestContext`, `IDateTimeProvider` impl contract) | `Domain`, `Common` |
| `{Service}.Infrastructure` | EF Core `DbContext` + `IEntityTypeConfiguration<T>` + migrations, `IRequestContext` implementation, any external system clients (cloud SDKs, Terraform runner), `DateTimeProvider` | `Application` |
| `{Service}.Service` | Minimal-API endpoints (`IEndpoint`, auto-discovered), request/response DTOs, composition root (`Program.cs`) | `Infrastructure` |
| `{Service}.Worker` *(add when needed)* | Background hosted services (drift polling, auto-heal loop) | `Infrastructure` |

Naming stays `Sciel.{Service}.{Layer}` (e.g. `Sciel.Blueprint.Domain`), matching the existing
`Sciel.Blueprint.Core` / `.Data` / `.Service` convention — only the layer list changes.

**No repository interfaces.** The CQRS template's biggest departure from the old guide: `Domain` has
no `I{Entity}Repository`. Application handlers query/mutate through `IApplicationDbContext` directly.
This cuts a layer of pass-through interfaces that added indirection without adding behavior — EF
Core's `DbSet<T>` *is* the repository abstraction once it's reached only through an interface owned by
`Application` (so `Domain`/`Application` still never reference EF Core or Npgsql types directly).

If a genuinely reusable, non-trivial query shows up in three+ handlers, extract a method on
`IApplicationDbContext` or a small query-object — don't reintroduce a generic repository "just in
case."

**One `Common` per service, for now.** `services/Blueprint` is the only service today. If a
second service needs the same `Result`/`Error`/`Entity` primitives, extract them into a shared NuGet
package (`services/Sciel.Common`) at that point rather than speculatively building one now —
cross-service packages add versioning and release overhead that isn't worth paying before there's a
second consumer.

## 2. Folder structure: vertical slices, not horizontal layers-within-a-layer

```
Domain/
  Categories/
    Category.cs
    CategoryErrors.cs
    CategoryCreatedDomainEvent.cs
    CategoryRemovedDomainEvent.cs

Application/
  Abstractions/
    Data/IApplicationDbContext.cs
    Identity/IRequestContext.cs
    Messaging/ICommand.cs, ICommandHandler.cs, IQuery.cs, IQueryHandler.cs
    Behaviors/ValidationDecorator.cs, LoggingDecorator.cs
    Pagination/PagedResult.cs
  Categories/
    CategoryCacheKeys.cs
    Create/
      CreateCategoryCommand.cs
      CreateCategoryCommandHandler.cs
      CreateCategoryCommandValidator.cs
    Get/
      GetCategoriesQuery.cs
      GetCategoriesQueryHandler.cs
      CategoryResponse.cs
    GetById/
      GetCategoryByIdQuery.cs
      GetCategoryByIdQueryHandler.cs
      CategoryResponse.cs
    Remove/
      RemoveCategoryCommand.cs
      RemoveCategoryCommandHandler.cs
      RemoveCategoryCommandValidator.cs
    Restore/
      RestoreCategoryCommand.cs
      RestoreCategoryCommandHandler.cs

Infrastructure/
  Database/
    BlueprintDbContext.cs
    BlueprintDbContextFactory.cs
    Migrations/
  Categories/
    CategoryConfiguration.cs
  Identity/
    RequestContext.cs
  Time/
    DateTimeProvider.cs

Service/
  Endpoints/
    IEndpoint.cs
    Tags.cs
    Categories/
      Create.cs
      Get.cs
      GetById.cs
      Remove.cs
      Restore.cs
  Extensions/
    EndpointExtensions.cs
    ServiceCollectionExtensions.cs
  Infrastructure/
    CustomResults.cs
```

- **One folder per use case** under `Application/{Feature}/{UseCase}/` — every file for that slice
  (command/query, handler, validator, response) lives together. Don't split them into parallel
  `Commands/`, `Handlers/`, `Validators/` folders; that's the type-folder anti-pattern the old guide
  already rejected, now applied one level deeper.
- **One folder per feature** in `Domain/{Feature}/` (entity, error catalog, domain events) and
  `Infrastructure/{Feature}/` (EF configuration) — unchanged from the old guide.
- **One file per use case** under `Service/Endpoints/{Feature}/{UseCase}.cs`, mirroring the
  `Application/{Feature}/{UseCase}/` folder it calls into.
- Reject top-level `Entities/`, `Interfaces/`, `Models/`, `DTOs/`, `Repositories/`, `Handlers/`
  catch-alls anywhere in the solution.

## 3. CQRS, no MediatR

- `ICommand` / `ICommand<TResponse>` (no response data / returns data) and `IQuery<TResponse>`,
  handled by `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`,
  `IQueryHandler<TQuery, TResponse>`. All return `Task<Result>` / `Task<Result<TResponse>>`.
- **Handlers are `internal sealed`** with primary constructors, auto-registered by **Scrutor**
  (`services.Scan(...)` in `Application/DependencyInjection.cs`) — never manually register a handler,
  validator, or endpoint.
- Handlers are wrapped, in order, by a **validation decorator** (FluentValidation — commands only)
  and a **logging decorator** (structured start/success/failure logs). Both are generic, registered
  once via `services.Decorate(...)`; adding a new slice gets both for free.
- A command/query class is a plain property-bag (`public sealed class ... : ICommand<TResponse>`),
  not a record — matches the template; keeps the validator's `RuleFor(c => c.X)` lambdas trivial and
  avoids accidental structural-equality surprises on types that are never compared.

```csharp
// Application/Categories/Create/CreateCategoryCommand.cs
public sealed class CreateCategoryCommand : ICommand<Guid>
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

// Application/Categories/Create/CreateCategoryCommandHandler.cs
internal sealed class CreateCategoryCommandHandler(
    IApplicationDbContext context,
    IRequestContext requestContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        bool nameTaken = await context.Categories
            .AnyAsync(c => c.Name == command.Name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<Guid>(CategoryErrors.NameConflict(command.Name));
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrganizationId = requestContext.OrganizationId,
            SpaceId = requestContext.SpaceId,
            Name = command.Name,
            Description = command.Description
        };

        category.Raise(new CategoryCreatedDomainEvent(category.Id));

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
```

## 4. Errors, not exceptions

- Expected failures return `Result.Failure(...)` / `Result.Failure<T>(...)` with an `Error`. Never
  throw for a condition a caller is expected to handle (not found, conflict, validation, unauthorized).
  `CategoryNameConflictException` goes away entirely — see `CategoryErrors.NameConflict` above.
- `Error` has a `Code` (`"{FeaturePlural}.{Reason}"`, e.g. `"Categories.NotFound"`), a `Description`,
  and an `ErrorType` (`NotFound` → 404, `Conflict` → 409, `Problem` → 400, `Failure` → 500). Factories
  live on a static `{Entity}Errors` class beside the entity in `Domain/{Feature}/`.
- Exceptions are reserved for truly unexpected failures (a bug, infrastructure outage) and are left to
  bubble to the global exception handler (`Service/Infrastructure/GlobalExceptionHandler.cs` →
  `ProblemDetails`). A unique-index race from a concurrent insert is the one place infrastructure
  exceptions get caught deliberately: catch the specific Postgres unique-violation
  (`PostgresException { SqlState: "23505" }`), map it to the same `Error.Conflict`, and return a
  `Result` — this is translating an infra signal at the boundary, not using exceptions for ordinary
  control flow.
- Endpoints translate exclusively via `result.Match(Results.Ok, CustomResults.Problem)` (or
  `Results.NoContent` for void commands) — no endpoint does its own `try`/`catch` around a handler
  call, and no endpoint re-queries to decide what status code to return (today's
  `CategoriesEndpoints.GetCategory` catching its own thrown `Exception` to produce a 404 is exactly
  the pattern this replaces).

```csharp
// Domain/Categories/CategoryErrors.cs
public static class CategoryErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Categories.NotFound", $"The category with Id = '{id}' was not found.");

    public static Error NameConflict(string name) =>
        Error.Conflict("Categories.NameConflict", $"A category named '{name}' already exists.");
}
```

## 5. Domain entities & events

- Entities are `sealed class`, inherit `Common.Entity` (gives `DomainEvents` + `Raise(...)`),
  mutable (`{ get; set; }`) for EF Core change tracking, plain `Guid Id`.
- Entities hold **foreign-key ids, not navigation properties** (shadow relationships configured in
  `Infrastructure`). `Category.OrganizationId` / `SpaceId` stay as they are today; no
  `Organization Organization { get; set; }` navigation.
- One domain event per file, `sealed record`, named `{Entity}{PastTenseVerb}DomainEvent` implementing
  `IDomainEvent`, carrying ids only (never the entity itself):
  `CategoryCreatedDomainEvent(Guid CategoryId)`.
- Commands that change state raise the event via `entity.Raise(new XDomainEvent(id))` **before**
  `SaveChangesAsync`. `Infrastructure`'s `SaveChanges` interceptor (or a `DomainEventsDispatcher`
  invoked right after `SaveChangesAsync`) dispatches to `IDomainEventHandler<T>` implementations,
  registered the same Scrutor way as command/query handlers.
- Domain events start as a hook point, not a messaging system — it's fine for a feature to raise an
  event with no handler yet. Don't build a handler "just in case"; add it when something needs to
  react (e.g. invalidating a cache key, sending a notification).

## 6. Validation

- Every command gets a FluentValidation `{Command}Validator : AbstractValidator<TCommand>` beside it
  in the same use-case folder. Queries don't — the validation decorator only wraps commands.
- Validators check **input shape** (required, length, format) only. Business rules that need a
  database round-trip (name uniqueness, "does the referenced Space exist") belong in the handler,
  returning a `Result.Failure` with the appropriate `{Entity}Errors` factory — don't call
  `IApplicationDbContext` from inside a validator.

## 7. Multi-tenancy & `IRequestContext`

- `IRequestContext` (today's interface, unchanged in shape: `UserId`, `OrganizationId`, `SpaceId`,
  `AccessToken`) moves to `Application/Abstractions/Identity/IRequestContext.cs` — it's an application
  abstraction, not a domain concept, mirroring where the template puts `IUserContext`.
- **Every write handler stamps `OrganizationId`/`SpaceId` from `IRequestContext`, never from the
  command.** Request/response DTOs never carry these fields (see §9) — there is nothing for a client
  to override even if a handler forgot to stamp, but stamp explicitly anyway as the enforcement point.
- **Tenant isolation is a global query filter, not per-handler discipline.** `BlueprintDbContext`
  takes `IRequestContext` as a constructor dependency and applies
  `HasQueryFilter(e => e.OrganizationId == _organizationId && e.SpaceId == _spaceId)` per tenant-scoped
  entity type in `OnModelCreating`, composed with the existing soft-delete filter
  (`DeletedAtUtc == null`). A handler that forgets to filter by tenant still can't leak another
  tenant's rows — the filter is the backstop, not a convention to remember.
- `RequestContext` (the implementation) lives in `Infrastructure/Identity/RequestContext.cs`. Until
  real auth exists it stays a dev stub reading hardcoded/test values (as `Program.cs` does today);
  swapping in JWT-claims-based resolution later is an `Infrastructure`-only change, nothing in
  `Application` moves.

## 8. EF Core / Infrastructure

- **`IApplicationDbContext`** (in `Application/Abstractions/Data/`) exposes `DbSet<T>` per aggregate
  and `SaveChangesAsync` — nothing else. `BlueprintDbContext` (in `Infrastructure/Database/`)
  implements it. Handlers depend on the interface; only `Infrastructure` and migration tooling see the
  concrete `DbContext`.
- **Fluent API only**, one `IEntityTypeConfiguration<T>` per entity in
  `Infrastructure/{Feature}/{Entity}Configuration.cs`, applied via
  `modelBuilder.ApplyConfigurationsFromAssembly(...)` — unchanged from the old guide.
- **A design-time `DbContext` factory** (`IDesignTimeDbContextFactory<T>`) in `Infrastructure`, so
  `dotnet ef migrations add` doesn't need to boot `Service`'s full DI container — already in place
  (`BlueprintDbContextFactory`), keep it.
- **`snake_case` naming via `EFCore.NamingConventions`.** Adopt this now rather than quoting every
  identifier: Postgres is case-sensitive on unquoted identifiers, and the current
  `builder.ToTable("Categories")` / unquoted PascalCase columns only work by accident until the first
  query is written by hand. Add the package and `.UseSnakeCaseNamingConvention()` in
  `AddInfrastructure`, then drop the explicit `ToTable(...)` calls and let the convention derive
  `categories`, `organization_id`, etc.
- **Enforce uniqueness with a filtered/composite unique index**, not just a check-then-insert query —
  the application-level check in §4 is for a fast, friendly error; the index is what actually prevents
  the race.
- **Composite index on `(OrganizationId, SpaceId)`** (or a 3-column index with the natural key, as
  `CategoryConfiguration` already does) on every tenant-scoped table — backs the global query filter
  and almost every list query.
- **No tracking by default** for query handlers: `.AsNoTracking()` on anything that isn't about to be
  mutated and saved in the same handler.
- **Queries project straight to a `{X}Response` DTO** via `.Select(...)` — never return `Category`
  (the domain entity) out of a query handler. List queries return `PagedResult<T>` (search + sort +
  page), same shape as today.
- **Concurrency tokens (`RowVersion`)** on any entity both a user action and an automated process can
  write — not needed yet (nothing automated touches `Category`), add it when a `Worker` does.
- **Scoped `DbContext` discipline in `Worker`**: resolve a new scope per polling iteration via
  `IServiceScopeFactory`; never inject a scoped `DbContext`/`IApplicationDbContext` into a singleton
  `BackgroundService`.
- **Migrations** live in `Infrastructure/Migrations`, named descriptively,
  `PascalCase_With_Underscores` (`Add_RefreshTokens`-style).

## 9. API / endpoint conventions

- Endpoints implement `Service/Endpoints/IEndpoint.cs`'s `IEndpoint` (`void MapEndpoint(IEndpointRouteBuilder app)`), auto-discovered by assembly scanning (`AddEndpoints`/`MapEndpoints` in
  `Service/Extensions/EndpointExtensions.cs`) — never manually add `app.Map...` calls in `Program.cs`
  for a feature endpoint.
- An endpoint resolves its handler interface directly from DI (constructor-injected lambda parameter,
  e.g. `ICommandHandler<CreateCategoryCommand, Guid> handler`), maps `Request` → command/query, calls
  `handler.Handle(...)`, and returns `result.Match(Results.Ok, CustomResults.Problem)` /
  `Results.NoContent`. **No business logic in endpoints** — that includes the `GetCategory` not-found
  translation that lives in the endpoint today; that becomes `CategoryErrors.NotFound` returned by the
  query handler.
- `.WithTags(Tags.Categories)` on every endpoint group (a static `Tags` class, one const per feature).
- **One request record per write operation**, named `{Verb}{Entity}Request`, living in `Service` next
  to its endpoint — never in `Domain`/`Application`. Create and Update get separate types even when
  identical today.
- **Request/response DTOs never carry server-owned fields** (`Id`, `OrganizationId`, `SpaceId`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `DeletedAtUtc`) — unchanged from the old guide, now doubly enforced
  by the tenant fields never existing on the command either (§7).
- **Update requests mirror the entity's full shape (PUT semantics)**; don't support partial updates
  (PATCH) without a concrete need.
- **Delete endpoints return `204 No Content`**, no body.
- Auth placeholders (`.RequireAuthorization()`) are added once real authentication exists; don't add a
  no-op call ahead of that.

## 10. C# language & analyzer conventions

- **File-scoped namespaces**, **one public type per file**, filename matches the type name.
- **Nullable reference types enabled** solution-wide; a bare `string` is never null at runtime
  (`= string.Empty` on entities), `string?` means genuinely optional.
- **`var`** when the right-hand side makes the type obvious; spell out the type otherwise.
- **Primary constructors** for handlers and other DI-constructed classes with no extra logic in the
  constructor body — replaces today's hand-written null-checking constructors
  (`CategoryRepository`'s `?? throw new ArgumentNullException(...)` goes away; DI never passes null for
  a registered service, so the check is the kind of defensive validation that doesn't happen).
- **Collection expressions** (`[.. x]`, `= []`) over `.ToList()` / explicit `new List<T>()`.
- **Async methods suffixed `Async`**, last parameter `CancellationToken cancellationToken` (no default
  value inside `Application`/`Infrastructure` — only truly top-level/optional call sites default it).
- **Interfaces prefixed `I`**, mirrored name with their primary implementation.
- **`record` for DTOs** (responses, requests), **`class` for entities, commands/queries, and
  services/handlers** — commands/queries stay mutable property-bags per §3, not records.
- Fix the typos already in the codebase while touching this code: `Categoires` → `Categories`,
  `cateogry` → `category`, `paged` (parameter in `GetAllAsync`) → `page`.
- **Analyzer-strict, solution-wide**, via a root `Directory.Build.props`:
  `TreatWarningsAsErrors`, `AnalysisMode = All`, `AnalysisLevel = latest`, `EnforceCodeStyleInBuild`,
  plus the `SonarAnalyzer.CSharp` package. Pair with a root `Directory.Packages.props`
  (`ManagePackageVersionsCentrally`) so every service's `.csproj` references a package without a
  version string, and every service stays on the same version. A shared root `.editorconfig` encodes
  the rest of this section as enforced rules (`csharp_style_namespace_declarations = file_scoped:error`,
  etc.) rather than relying on reviewers to catch drift.

## 11. Testing

Mirrors the layer structure, one test project per concern rather than per layer:

| Project | Covers |
|---|---|
| `{Service}.Application.UnitTests` | Handler tests (one per `Result.Failure` path + happy path, using a fake/InMemory `IApplicationDbContext` implementation), validator tests (`TestValidate`) |
| `{Service}.ArchitectureTests` | `NetArchTest.Rules` layer-boundary assertions: `Domain` has no dependency on `Application`/`Infrastructure`/`Service`; `Application` has no dependency on `Infrastructure`/`Service`; `Infrastructure` has no dependency on `Service` |
| `{Service}.IntegrationTests` | Real HTTP via `WebApplicationFactory`, real Postgres via `Testcontainers.PostgreSql` — don't mock `DbContext` here |

New/changed handlers need unit tests for every failure path plus the happy path (assert persisted
state and raised domain events); new/changed endpoints need an integration test over real HTTP;
new/changed validators need a `TestValidate` case per rule. `ArchitectureTests` is what makes "Domain
references only Common" an enforced fact instead of a doc claim — add it before or alongside the
first real restructuring pass, not after.

## 12. Build & tooling

- `dotnet build {Service}.slnx`
- `dotnet test {Service}.slnx` — integration tests need Docker running (Testcontainers).
- Keep the build warning-clean; a warning is a build failure under `TreatWarningsAsErrors`.

---

## What changes for `Sciel.Blueprint` specifically

This is a paradigm shift from the current repository-based `Sciel.Blueprint.Core` /
`.Data` / `.Service`, not an incremental tweak. Concretely:

- `Sciel.Blueprint.Core` splits into `Sciel.Blueprint.Common` (new: `Result`, `Error`,
  `ErrorType`, `Entity`, `IDomainEvent`) and `Sciel.Blueprint.Domain` (`Category`, new
  `CategoryErrors`, new domain events).
- `ICategoryRepository` and `CategoryRepository` are deleted. Their five methods become five
  Application use-case folders under `Application/Categories/` (`Create`, `Get`, `GetById`, `Remove`,
  `Restore` — `Update`/`GetDeleted` too, once implemented), each with a command/query + handler
  (+ validator for commands) talking to `IApplicationDbContext` directly.
- `Sciel.Blueprint.Data` becomes `Sciel.Blueprint.Infrastructure`; `CategoryConfiguration` moves over
  unchanged in spirit (gains `.UseSnakeCaseNamingConvention()` fallout — table/column names change).
- `Sciel.Blueprint.Service`'s `CategoriesEnpoints.cs`/`CategoryExtensions.cs` become one `IEndpoint`
  file per use case under `Service/Endpoints/Categories/`; the try/catch-driven 404 in `GetCategory`
  and `BadRequest<string>` in `CreateCategory` are replaced by `Result`/`CustomResults.Problem`.
- `CategoryNameConflictException` is deleted in favor of `CategoryErrors.NameConflict`.

We'll work through this restructuring project by project next.
