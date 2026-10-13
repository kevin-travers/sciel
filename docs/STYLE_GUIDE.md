# Sciel — C# / .NET / EF Core Style Guide

Conventions for every .NET service under `services/`. Based on the official
[C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions),
[.NET framework design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/),
and [EF Core docs](https://learn.microsoft.com/en-us/ef/core/), adapted to this codebase's patterns.

When this guide and the Microsoft guidelines disagree, this guide wins — it exists to capture the
project-specific calls, not restate the generic ones.

---

## 1. Solution structure

Each service is a vertical: `Core` → `Data` → `Service`, with `Infrastructure` and `Worker` added only
when the work actually shows up.

| Project | Owns | Must NOT reference |
|---|---|---|
| `{Service}.Core` | Entities, enums, value objects, repository/domain-service **interfaces** | EF Core, ASP.NET Core, any cloud SDK |
| `{Service}.Data` | `DbContext`, `IEntityTypeConfiguration<T>`, migrations, repository **implementations** | ASP.NET Core |
| `{Service}.Infrastructure` *(add when needed)* | External systems: Terraform runner, cloud provider clients, notification senders | — |
| `{Service}.Worker` *(add when needed)* | Background hosted services (drift polling, auto-heal loop) | — |
| `{Service}.Service` | Minimal API endpoints, request/response DTOs, DI composition root | — |

Dependency direction is always inward: `Service` → `Data`/`Infrastructure` → `Core`. `Core` depends on
nothing in the solution. If you find yourself wanting to reference EF Core types from `Core`, stop —
that's a sign the type belongs in `Data` instead.

## 2. Folder structure: feature folders, not type folders

Group by aggregate/domain concept, not by C# construct. Every file about `Category` lives in one
`Categories/` folder — the entity, its enums, its repository interface.

```
Core/
  Common/              IRequestContext, base types, shared exceptions
  Categories/           Category.cs, ICategoryRepository.cs
  Blueprints/            Blueprint.cs, BlueprintStatus.cs, IBlueprintRepository.cs
  Deployments/           Deployment.cs, DeploymentStatus.cs, IDeploymentRepository.cs
```

```
Data/
  Categories/            CategoryConfiguration.cs, CategoryRepository.cs
  Blueprints/            BlueprintConfiguration.cs, BlueprintRepository.cs
  BlueprintDbContext.cs
  BlueprintDbContextFactory.cs      <- design-time factory for `dotnet ef`
```

```
Service/
  Categories/
    CategoriesEndpoints.cs
    CreateCategoryRequest.cs
    UpdateCategoryRequest.cs
    CategoryResponse.cs
```

Reject top-level `Entities/`, `Interfaces/`, `Models/`, `DTOs/` catch-all folders. They scatter one
concept across several places and don't scale past a handful of types.

**Never put a repository folder structure in `Core`.** `Core` has no `Repositories/` folder —
interfaces live beside the entity they belong to (`Categories/ICategoryRepository.cs`), not grouped by
"this is an interface."

## 3. C# language conventions

- **File-scoped namespaces** everywhere: `namespace Sciel.Blueprint.Core.Categories;`
- **One public type per file**, file name matches the type name.
- **Nullable reference types enabled** solution-wide. A `string?` means genuinely optional; a bare
  `string` must never be null at runtime — initialize with `= string.Empty` on entities rather than
  leaving it nullable for convenience.
- **`var`** when the type is obvious from the right-hand side (`var category = new Category();`);
  spell out the type when it isn't (`IReadOnlyList<Category> categories = ...` from a method whose
  return type isn't visible at the call site).
- **Interfaces prefixed `I`**, mirrored name with their primary implementation (`IRequestContext` /
  `RequestContext`, `ICategoryRepository` / `CategoryRepository`).
- **Async methods suffixed `Async`**, last parameter `CancellationToken ct = default`:
  ```csharp
  Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
  ```
- **Entities are mutable** (`{ get; set; }`) for EF Core change tracking. **Immutable
  config/value objects use `required ... { get; init; }`** (see `RequestContext`) — never give a
  type both patterns.
- **Collections returned from repositories/services are `IReadOnlyList<T>`**, never `List<T>` or a
  raw `IEnumerable<T>` that could be re-enumerated against the database.
- **XML doc comments** on every public interface and on any member whose meaning isn't obvious from
  its name — especially invariants ("stamped by the repository, never trust a caller-supplied
  value"). Skip XML docs on trivial, self-explanatory members.
- **`record` for DTOs**, `class` for entities and services. Records give you value equality and
  concise positional syntax for the request/response types described in §5.

## 4. API contract conventions

- **One request record per write operation**, named `{Verb}{Entity}Request`, living in `Service` next
  to its endpoint — never in `Core`. `Core`/`Data` never see HTTP contracts, only mapped entities.
  ```csharp
  public record CreateCategoryRequest(string Name, string? Description);
  ```
- **Create and Update get separate request types**, even when their fields look identical today.
  They diverge in practice (concurrency tokens on update, different required fields) and splitting a
  merged type later is more churn than keeping them apart from the start.
- **Update requests mirror the entity's full shape (PUT semantics)** — required fields required,
  optional fields optional, same as `Create`. The client sends the complete desired state and you
  replace it.
  ```csharp
  public record UpdateCategoryRequest(string Name, string? Description);
  ```
  Do **not** make every field optional to support partial updates (PATCH semantics) unless you have
  a concrete need for it. A plain `string?` can't distinguish "field omitted, leave as-is" from
  "field explicitly cleared to null" — supporting that properly needs a tri-state `Optional<T>`
  wrapper or JSON Merge Patch, which is unjustified complexity for a simple CRUD aggregate.
- **Delete endpoints return `204 No Content`, no body.** A body that only echoes back the ID the
  caller already sent in the route adds no information. Only return a body from a delete if it
  carries something the caller didn't already know — a soft-delete timestamp, or per-item results
  from a bulk delete.
- **Request/response DTOs never carry server-owned fields** (`Id`, `OrganizationId`, `SpaceId`,
  `CreatedAtUtc`). Those are stamped server-side from `IRequestContext`/the database — a DTO that
  could carry them is an over-posting vulnerability waiting to happen.

## 5. EF Core best practices

- **Fluent API only**, never data annotations on entities. One `IEntityTypeConfiguration<T>` per
  entity in `Data/{Aggregate}/{Entity}Configuration.cs`, applied via
  `modelBuilder.ApplyConfigurationsFromAssembly(...)`. Keeps `Core` entities persistence-ignorant.
- **Repositories never expose `IQueryable<T>`** outside `Data`. Methods return materialized
  `Task<T?>` / `Task<IReadOnlyList<T>>`. If a caller needs more flexible filtering than the
  repository offers, add a specific method — don't leak the query surface.
- **A design-time `DbContext` factory** (`IDesignTimeDbContextFactory<TContext>`) in `Data`, so
  `dotnet ef migrations add` works without booting the full `Service` host and its DI container.
- **Multi-tenant isolation via global query filters.** `HasQueryFilter` keyed on
  `IRequestContext.OrganizationId`/`SpaceId` is the backstop — don't rely solely on each repository
  method remembering to filter by tenant.
- **A `SaveChanges` interceptor** stamps `CreatedAtUtc`/`UpdatedAtUtc` and
  `OrganizationId`/`SpaceId` on insert. This is the single place that enforces "never trust a
  caller-supplied value for tenant/audit fields" — don't repeat that logic per repository method.
- **Enforce uniqueness with a filtered unique index**, not just an `ExistsByNameAsync` check before
  insert — the check-then-insert has a race window; the database constraint doesn't.
  ```csharp
  builder.HasIndex(c => new { c.OrganizationId, c.SpaceId, c.Name }).IsUnique();
  ```
- **Concurrency tokens (`RowVersion`)** on any entity that both a user action and an automated
  process (drift detection, auto-heal) can write to. Prevents the heal loop from silently clobbering
  a concurrent manual edit.
- **No-tracking by default.** Query-only repository methods use `AsNoTracking()`; only load tracked
  entities on the path that's actually going to call `SaveChangesAsync`. For read-heavy background
  jobs (drift polling across many deployments), project into DTOs rather than materializing full
  entity graphs.
- **Enums map to `string` via `HasConversion<string>()`** for status-like columns
  (`DeploymentStatus`, `DriftStatus`, `AlertSeverity`). Readable in the database and in logs; the
  storage overhead versus a smallint is irrelevant at this scale.
- **JSON-shaped data (Terraform plan output, diff payloads, variable values) uses native JSON column
  mapping** (`jsonb` on Postgres, SQL Server's JSON support) instead of a serialized string blob —
  keeps it queryable and avoids hand-rolled (de)serialization.
- **Composite index on `(OrganizationId, SpaceId)`** on every tenant-scoped table — it backs both
  the global query filter and almost every list query you'll write.
- **Scoped `DbContext` discipline in `Worker`.** Never inject a scoped `DbContext` into a singleton
  `BackgroundService`. Resolve a new scope per polling iteration via `IServiceScopeFactory`.
- **Migrations live in `Data/Migrations`**, named descriptively
  (`AddCategoriesTable`, not `Migration1`).

## 6. Testing

Mirror the layer structure: `tests/{Service}.Core.Tests`, `tests/{Service}.Data.Tests` (integration,
real database via Testcontainers — don't mock `DbContext`), `tests/{Service}.Service.Tests`
(`WebApplicationFactory`-based).
