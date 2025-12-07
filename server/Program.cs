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


// app.MapGet("/api/hello", async (HttpRequest request, NpgsqlConnection db) =>
// {

// })

app.MapGet("/tasks", async (HttpRequest request) =>
{
    var tasks = new List<Dictionary<string, object>>();
    await using var cmd = dataSource.CreateCommand("SELECT * FROM data");
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

app.MapPost("/api/hello", async (HttpRequest request) =>
{
    var data = await System.Text.Json.JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(request.Body);

    var name = data?["name"] ?? "";

    await using var cmd = dataSource.CreateCommand("INSERT INTO data (name) VALUES ($1)");
    cmd.Parameters.AddWithValue(name);
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok(new { Message = "Form submitted successfully", Name = name });
});


app.Run();