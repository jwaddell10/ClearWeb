using System.Text.Json;
using Mscc.GenerativeAI;
using Npgsql;

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Connection String Logic (Railway & Local) ---
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(rawConnectionString))
{
    throw new InvalidOperationException("Connection string not found");
}

// Convert URI (postgres://) to Npgsql format if necessary
string finalConnectionString = rawConnectionString;
if (rawConnectionString.StartsWith("postgres://") || rawConnectionString.StartsWith("postgresql://"))
{
    var uri = new Uri(rawConnectionString);
    var userInfo = uri.UserInfo.Split(':');
    finalConnectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

await using var dataSource = NpgsqlDataSource.Create(finalConnectionString);

// --- 2. CORS Configuration (Fixes CS0103) ---
// We define 'allowedOrigins' BEFORE builder.Services.AddCors
// --- 2. CORS Configuration ---
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins")
                                        .Get<string[]>()
                                        ?? Array.Empty<string>();

// Debug logging: This will show up in your Railway "Logs" tab
Console.WriteLine($"CORS Allowed Origins: {string.Join(", ", allowedOrigins)}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (allowedOrigins.Any(o => !string.IsNullOrWhiteSpace(o)))
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetIsOriginAllowedToAllowWildcardSubdomains(); // Helps with Amplify subdomains
        }
        else
        {
            // Strict fallback for local dev if variable is missing
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// CRITICAL: Order matters!
app.UseCors("AllowReactApp");

app.MapGet("/", () => "API is running!");

// --- GET all tasks ---
app.MapGet("/tasks", async () =>
{
    var tasks = new List<Dictionary<string, object>>();

    await using var cmd = dataSource.CreateCommand("SELECT * FROM tasks");
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.GetValue(i);

        tasks.Add(row);
    }

    return Results.Json(tasks);
});

// --- POST a new task ---
app.MapPost("/tasks", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(request.Body);

    var name = data?["name"] ?? "";

    await using var cmd = dataSource.CreateCommand(
        @"INSERT INTO tasks (name, position)
          SELECT $1, COALESCE(MAX(position), 0) + 10 FROM tasks
          RETURNING id, name, position"
    );
    cmd.Parameters.AddWithValue(name);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        var returnedName = reader.GetString(1);
        var position = reader.GetInt32(2);

        return Results.Json(new { Id = id, Name = returnedName, Position = position });
    }

    return Results.Problem("Failed to insert task");
});

app.MapPost("/api/ai/chat", async (HttpRequest request) =>
{
    // 1️⃣ Read user prompt
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    var userPrompt = body.GetProperty("prompt").GetString() ?? "";

    // 2️⃣ Fetch tasks from PostgreSQL
    var tasks = new List<string>();
    await using var cmd = dataSource.CreateCommand(
        "SELECT name FROM tasks ORDER BY position"
    );
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        tasks.Add(reader.GetString(0));
    }

    // 3️⃣ Build AI prompt
    var prompt = $"""
    You are a productivity assistant.

    Current tasks:
    - {string.Join("\n- ", tasks)}

    User request:
    {userPrompt}
    """;

    // 4️⃣ Call Gemini via Google AI Studio
    var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Problem("GOOGLE_API_KEY is not set");

    var googleAI = new GoogleAI(apiKey);
    var model = googleAI.GenerativeModel(Model.Gemini25Flash);

    var response = await model.GenerateContent(prompt);

    return Results.Ok(new
    {
        reply = response.Text
    });
});


// --- DELETE a task ---
app.MapDelete("/api/tasks/{id}", async (int id) =>
{
    await using var cmd = dataSource.CreateCommand("DELETE FROM tasks WHERE id = $1");
    cmd.Parameters.AddWithValue(id);
    var rowsAffected = await cmd.ExecuteNonQueryAsync();

    return rowsAffected > 0
        ? Results.Ok($"{id} deleted")
        : Results.NotFound($"Task {id} not found");
});

// --- UPDATE a single task ---
app.MapPut("/api/tasks/update/{id}", async (int id, HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, System.Text.Json.JsonElement>>(request.Body);

    if (data == null || !data.ContainsKey("task"))
        return Results.BadRequest("Invalid request body");

    var task = data["task"];
    var name = task.GetProperty("name").GetString() ?? "";
    int? position = task.TryGetProperty("position", out var posElem)
        ? posElem.GetInt32()
        : null;

    await using var cmd = dataSource.CreateCommand(
        "UPDATE tasks SET name = $1, position = $2 WHERE id = $3"
    );
    cmd.Parameters.AddWithValue(name);
    cmd.Parameters.AddWithValue(position.HasValue ? position.Value : DBNull.Value);
    cmd.Parameters.AddWithValue(id);

    var rowsAffected = await cmd.ExecuteNonQueryAsync();

    return rowsAffected > 0
        ? Results.Ok(new { Message = "Task updated successfully", Id = id, Name = name, Position = position })
        : Results.NotFound($"Task with id {id} not found");
});

// --- REORDER tasks ---
app.MapPut("/api/tasks/reorder", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, System.Text.Json.JsonElement>>(request.Body);

    if (data == null || !data.ContainsKey("tasks"))
        return Results.BadRequest("Invalid request body");

    var tasksArray = data["tasks"].EnumerateArray();

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    try
    {
        foreach (var taskElement in tasksArray)
        {
            var id = taskElement.GetProperty("id").GetInt32();
            var position = taskElement.GetProperty("position").GetInt32();

            await using var cmd = dataSource.CreateCommand(
                "UPDATE tasks SET position = $1 WHERE id = $2"
            );
            cmd.Parameters.AddWithValue(position);
            cmd.Parameters.AddWithValue(id);

            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return Results.Ok(new { Message = "Positions updated successfully" });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Results.Problem($"Failed to update positions: {ex.Message}");
    }
});

app.Run();