// Sets up configuration (appsettings.json, env vars, command-line args), logging, and the
// DI service collection. Nothing is "built" yet — this is just the configuration stage.
using Sciel.Blueprint.Service.Categoires;

var builder = WebApplication.CreateBuilder(args);

// Registers .NET's built-in OpenAPI document generator, which produces the OpenAPI/Swagger
// JSON describing this API's routes and schemas.
builder.Services.AddOpenApi();

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
