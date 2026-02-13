using Scalar.AspNetCore;
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


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

/*app.MapGet("/notes", async (OmniArchivumDbContext db) =>
    await db.Notes.OrderByDescending(n => n.UpdatedUtc).ToListAsync());

app.MapPost("/notes", async (OmniArchivumDbContext db, Note note) =>
{
    note.Id = Guid.NewGuid();
    note.CreatedUtc = DateTimeOffset.UtcNow;
    note.UpdatedUtc = note.CreatedUtc;

    db.Notes.Add(note);
    await db.SaveChangesAsync();

    return Results.Created($"/notes/{note.Id}", note);
}); */

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
