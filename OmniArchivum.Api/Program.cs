using Scalar.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
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

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OmniArchivumDbContext>();

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
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OmniArchivumDbContext>();

    db.Database.Migrate();
    DemoData.SeedIfEmpty(db);
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

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

// Exposes Program for WebApplicationFactory<Program> in OmniArchivum.Api.Tests.
public partial class Program { }
