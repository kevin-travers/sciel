namespace Sciel.Blueprint.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Design-time factory for <see cref="BlueprintDbContext"/>.
///
/// Why this class exists at all: commands like "dotnet ef migrations add" and
/// "dotnet ef database update" need to construct a BlueprintDbContext WITHOUT
/// running the actual Sciel.Blueprint.Service host. There is no WebApplicationBuilder,
/// no configured IServiceCollection, and no DI container at that point — the EF CLI
/// just loads this project's assembly and looks for a type that implements
/// IDesignTimeDbContextFactory&lt;TContext&gt;. If it finds one, it calls
/// CreateDbContext(args) itself and uses whatever BlueprintDbContext comes back
/// purely to read the model (for scaffolding migrations) or to open a connection
/// (for applying them). None of this touches ASP.NET Core's hosting pipeline.
///
/// Because of that, this factory has to build its OWN tiny configuration pipeline
/// here, by hand, instead of reusing builder.Configuration from Program.cs — that
/// object simply doesn't exist yet at design time. We rebuild the same lookup order
/// the real app uses so the connection string comes from the same places a developer
/// already expects: appsettings.json as the base, appsettings.&lt;Environment&gt;.json
/// layered on top, user secrets for local development (so nothing sensitive has to
/// live in a committed file), and environment variables last so production deploys
/// can override everything via env vars without any code change.
/// </summary>
public class BlueprintDbContextFactory : IDesignTimeDbContextFactory<BlueprintDbContext>
{
    // This is called directly by the "dotnet ef" CLI tool — never by the running app.
    // "args" would be whatever extra arguments follow "--" on the ef command line;
    // we don't need any custom ones here, so it's unused.
    public BlueprintDbContext CreateDbContext(string[] args)
    {
        // Mirrors ASPNETCORE_ENVIRONMENT, which is what WebApplication.CreateBuilder
        // normally reads to decide which appsettings.<Environment>.json to layer in.
        // Defaults to "Development" because that's the environment you're almost
        // always in when you're the one typing "dotnet ef ..." by hand at a terminal.
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        // The ef CLI resolves this factory from the Sciel.Blueprint.Data project, but
        // the appsettings*.json files actually live in Sciel.Blueprint.Service. So the
        // base path has to be pointed explicitly at the Service project's directory,
        // relative to where this factory assembly sits on disk (its bin/Debug/... folder).
        var servicePath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sciel.Blueprint.Service");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(servicePath)

            // Base settings, committed to source control. Should NOT contain the real
            // connection string/credentials — only non-secret defaults, if anything.
            .AddJsonFile("appsettings.json", optional: true)

            // Per-environment overrides. appsettings.Development.json is also committed,
            // so this layer is for non-secret, environment-specific settings only.
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)

            // Local-development-only secrets store, kept OUTSIDE this repo entirely
            // (under the user's home directory, keyed by the <UserSecretsId> set in
            // Sciel.Blueprint.Service.csproj). This is where the real local Postgres
            // connection string should actually live:
            //   dotnet user-secrets set "ConnectionStrings:Blueprint" "Host=...;..."
            // Passing the ID as a literal string (rather than AddUserSecrets<T>()) is
            // deliberate: that generic overload resolves the ID from an assembly
            // attribute on T's assembly, and T would have to live in Sciel.Blueprint.Service
            // for that to find anything — but this factory lives in Sciel.Blueprint.Data,
            // which doesn't reference Service. Using the literal GUID sidesteps that
            // without creating a project reference just for this.
            // (This string-id overload is inherently optional — a missing secrets.json
            // just means no values come from this layer, same as the optional:true above.)
            .AddUserSecrets("19b4e98d-a03f-48f4-b0dd-6f9e5842717c")

            // Environment variables last, so they win over everything above. This is
            // how a real deployment supplies the production connection string, e.g.
            //   ConnectionStrings__Blueprint=Host=prod-db;...
            // "__" is how ASP.NET Core's configuration binder represents the ":" that
            // separates "ConnectionStrings" from "Blueprint" in a flat environment
            // variable name.
            .AddEnvironmentVariables()
            .Build();

        // "Blueprint" here must match the same key used everywhere else — e.g.
        // builder.Configuration.GetConnectionString("Blueprint") in Program.cs,
        // and whatever key appsettings.json / user-secrets / the env var above use.
        var connectionString = configuration.GetConnectionString("Blueprint");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string named 'Blueprint' was found. Set it with " +
                "'dotnet user-secrets set \"ConnectionStrings:Blueprint\" \"<value>\"' " +
                "from the Sciel.Blueprint.Service project, or via the " +
                "ConnectionStrings__Blueprint environment variable.");
        }

        // Builds the same kind of DbContextOptions<BlueprintDbContext> that
        // AddDbContext<BlueprintDbContext>(...) would build at normal app startup —
        // just assembled by hand here instead of coming out of the DI container.
        var optionsBuilder = new DbContextOptionsBuilder<BlueprintDbContext>()
            .UseNpgsql(connectionString);

        // BlueprintDbContext's constructor currently only takes DbContextOptions —
        // no IRequestContext parameter — so no additional arguments are needed here.
        return new BlueprintDbContext(optionsBuilder.Options);
    }
}
