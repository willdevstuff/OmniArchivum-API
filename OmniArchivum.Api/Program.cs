using System.Threading.RateLimiting;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;
using OmniArchivum.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddDbContext<OmniArchivumDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OmniArchivumDb")));

builder.Services.AddControllers();

builder.Services.AddScoped<INotesService, NotesService>();

// Every read and write is scoped to an owner taken from the caller's signed token, so
// each guest session sees only its own copy of the archive.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOwnerContext, OwnerContext>();

// One instance, shared between issuing and validating. Two instances would each
// generate their own ephemeral key when none is configured, so tokens issued by one
// would fail validation by the other.
var sessionTokens = new SessionTokenService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(sessionTokens);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = sessionTokens.ValidationParameters);

builder.Services.AddAuthorization();

// Each guest session gets its own copy of the demo archive, so old ones have to be
// reclaimed or the database grows with every visit.
builder.Services.AddScoped<GuestDataCleaner>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<GuestDataCleanupService>();
}

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OmniArchivumDbContext>();

// This API is publicly writable so the demo is genuinely usable, which also means
// anyone can script it. Reads are deliberately unrestricted — browsing and searching
// should never be throttled — while writes are capped per client IP, which is enough
// to stop bulk junk without getting in a real visitor's way.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("reads");
        }

        // Accurate only because UseForwardedHeaders runs first — without it every
        // request would look like it came from the Container Apps ingress.
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                "https://mango-ground-058108a0f.7.azurestaticapps.net",
                "http://localhost:8080",
                "http://127.0.0.1:8080")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Interactive API docs are served in every environment on purpose: this is a public
// portfolio API, and browsable docs are part of what it's demonstrating. There is no
// private data behind it and every endpoint is already publicly reachable.
app.MapOpenApi();
app.MapScalarApiReference();

// Apply migrations at startup so both a fresh clone and a fresh deploy against an empty
// database work with no manual step. EF Core takes an advisory lock before applying, so
// this stays safe when more than one replica starts at once.
// Skipped under "Testing", where the test fixture owns schema setup.
// Seeding is no longer done here: demo content now belongs to a session and is created
// when one is issued, so each visitor gets their own copy rather than sharing one.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OmniArchivumDbContext>();

    db.Database.Migrate();
}

// Azure Container Apps' ingress terminates TLS and forwards plain HTTP internally.
// Without this, UseHttpsRedirection() below would see every request as insecure and
// redirect-loop, since it can't see the client's original scheme was https.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

app.UseCors("Frontend");

// After UseCors, so a rejected request still carries CORS headers and the browser
// can read the 429 rather than reporting an opaque network error.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

// Exposes Program for WebApplicationFactory<Program> in OmniArchivum.Api.Tests.
public partial class Program { }
