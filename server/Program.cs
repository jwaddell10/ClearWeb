var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("AllowReactApp");

// GET endpoint
app.MapGet("/api/hello", () => new { Message = "Hello from .NET API!" });

// POST endpoint
app.MapPost("/api/hello", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, string>>(request.Body);
    var name = data?["name"];
    Console.WriteLine("Received name: " + name);
    return Results.Ok(new { Message = "Form submitted successfully", Name = name });
});

app.Run();
