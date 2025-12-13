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
    //get the highest position and add 10, otherwise make it 0.
    await using var cmd = dataSource.CreateCommand("INSERT INTO tasks (name, position) SELECT $1, COALESCE(MAX(position), 0) + 10 FROM tasks");
    cmd.Parameters.AddWithValue(name);
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok(new { Message = "Form submitted successfully", Name = name });
});

app.MapDelete("/api/tasks/{id}", async (int id) =>
{
    await using var cmd = dataSource.CreateCommand($"DELETE FROM tasks WHERE id = {id}");
    await using var reader = await cmd.ExecuteReaderAsync();

    return Results.Ok($"{id} deleted");
});

app.MapPut("/api/tasks/update/{id}", async (int id) =>
{
    Console.WriteLine($"it runs");
});




app.Run();