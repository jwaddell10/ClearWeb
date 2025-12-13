using Npgsql;

var connectionString = "Host=localhost:5432;Username=jwaddell10;Password=Happy*90;Database=clearweb";
await using var dataSource = NpgsqlDataSource.Create(connectionString);

var builder = WebApplication.CreateBuilder(args);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("AllowReactApp");

app.MapGet("/tasks", async (HttpRequest request) =>
{
    var tasks = new List<Dictionary<string, object>>();
    await using var cmd = dataSource.CreateCommand("SELECT * FROM tasks");
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var row = new Dictionary<string, object>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.GetValue(i);
        }
        tasks.Add(row);
    }

    return Results.Json(tasks);
});

app.MapPost("/tasks", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(request.Body);

    var name = data?["name"] ?? "";

    await using var cmd = dataSource.CreateCommand(
        "INSERT INTO tasks (name, position) " +
        "SELECT $1, COALESCE(MAX(position), 0) + 10 FROM tasks " +
        "RETURNING id, name, position"
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


app.MapDelete("/api/tasks/{id}", async (int id) =>
{
    await using var cmd = dataSource.CreateCommand($"DELETE FROM tasks WHERE id = {id}");
    await using var reader = await cmd.ExecuteReaderAsync();

    return Results.Ok($"{id} deleted");
});

app.MapPut("/api/tasks/update/{id}", async (int id, HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, System.Text.Json.JsonElement>>(request.Body);

    if (data == null || !data.ContainsKey("task"))
    {
        return Results.BadRequest("Invalid request body");
    }

    var task = data["task"];

    // Extract name and position from the task object
    var name = task.GetProperty("name").GetString() ?? "";
    int? position = task.TryGetProperty("position", out var posElement)
        ? posElement.GetInt32()
        : null;

    // Update the task
    await using var cmd = dataSource.CreateCommand(
        "UPDATE tasks SET name = $1, position = $2 WHERE id = $3"
    );
    cmd.Parameters.AddWithValue(name);
    cmd.Parameters.AddWithValue(position.HasValue ? position.Value : DBNull.Value);
    cmd.Parameters.AddWithValue(id);

    var rowsAffected = await cmd.ExecuteNonQueryAsync();

    if (rowsAffected == 0)
    {
        return Results.NotFound($"Task with id {id} not found");
    }

    return Results.Ok(new
    {
        Message = "Task updated successfully",
        Id = id,
        Name = name,
        Position = position
    });
});

app.MapPut("/api/tasks/reorder", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, System.Text.Json.JsonElement>>(request.Body);

    if (data == null || !data.ContainsKey("tasks"))
    {
        return Results.BadRequest("Invalid request body");
    }

    var tasksArray = data["tasks"].EnumerateArray();

    // Use a transaction to update all positions atomically
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