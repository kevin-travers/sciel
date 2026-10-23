// Sets up configuration (appsettings.json, env vars, command-line args), logging, and the
// DI service collection. Nothing is "built" yet — this is just the configuration stage.
using Microsoft.EntityFrameworkCore;
using Sciel.Blueprint.Data;
using Sciel.Blueprint.Service.Categoires;
using Sciel.Blueprint.Core.Common;
using Sciel.Blueprint.Core.Categories;
using Sciel.Blueprint.Data.Categories;

var builder = WebApplication.CreateBuilder(args);

// Registers .NET's built-in OpenAPI document generator, which produces the OpenAPI/Swagger
// JSON describing this API's routes and schemas.
builder.Services.AddOpenApi();

// Reads the "ConnectionStrings:Blueprint" key out of the layered configuration
// (appsettings.json -> appsettings.<Environment>.json -> user secrets in
// Development -> environment variables, each overriding the last). Locally this
// comes from "dotnet user-secrets set ConnectionStrings:Blueprint ..." so the real
// Postgres credentials never land in a file that gets committed; in production the
// same key is supplied via a ConnectionStrings__Blueprint environment variable.
var connectionString = builder.Configuration.GetConnectionString("Blueprint");

// Registers BlueprintDbContext in DI as scoped (one instance per HTTP request),
// configured to talk to Postgres via Npgsql using the connection string above.
// Anything downstream — a repository, an endpoint handler — can now just take a
// BlueprintDbContext parameter and have it injected automatically.
builder.Services.AddDbContext<BlueprintDbContext>(options => 
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IRequestContext>(_ => new RequestContext
{
    UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
    OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
    SpaceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
    AccessToken = "dev-token"
});

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

// TODO: add logging
// Finalizes the DI container and produces the actual runnable WebApplication. After this
// line you can no longer register new services — only configure the middleware pipeline/routes.
var app = builder.Build();

// Checks the hosting environment (set via ASPNETCORE_ENVIRONMENT or the launch profile).
if(app.Environment.IsDevelopment())
{
    // Maps the actual OpenAPI JSON document endpoint. Gated to Development here so the
    // API's schema isn't exposed by default in Production.
    app.MapOpenApi();
}

// Middleware: redirects any plain HTTP request to its HTTPS equivalent.
app.UseHttpsRedirection();

// TODO: add builder.Services.AddAuthentication(...).AddJwtBearer(...) (once the token
// issuer/Authority is decided) and app.UseAuthentication() here, before UseAuthorization().
// Without it, HttpContext.User is never populated, so IRequestContext has no claims to read.
// Not added yet because UseAuthentication() with no registered scheme throws on every
// request, not just authorized ones — needs real issuer/audience config to go in together.

// TODO: app.MapCategoryEndpoints(); etc. once the minimal API endpoint extensions exist.
app.MapCategoryEndpoints();
// Starts the Kestrel server and blocks, listening for requests until the process stops.
app.Run();
