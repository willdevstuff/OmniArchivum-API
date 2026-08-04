using Scalar.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;
using OmniArchivum.Api.Models.Entities;
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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OmniArchivumDbContext>();
    db.Database.Migrate();

    if (!db.Notes.Any())
    {
        var tag = new Tag
        {
            Name = "test-tag",
            Category = "demo"
        };

        var note = new Note
        {
            Title = "Test Note",
            BodyMarkdown = "This is a sample note seeded automatically on first run. Feel free to edit or delete it."
        };

        note.NoteTags.Add(new NoteTag { Tag = tag });

        db.Notes.Add(note);
        db.SaveChanges();
    }
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
