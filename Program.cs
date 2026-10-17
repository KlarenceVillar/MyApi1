using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connStr = builder.Configuration.GetConnectionString("Default");

app.MapGet("/api/health", () =>
    Results.Ok(new { status = "healthy", host = Environment.MachineName }));

app.MapGet("/api/items", async () =>
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    // Create and seed the table on first call
    await using (var init = new SqlCommand(@"
        IF OBJECT_ID('dbo.Items') IS NULL
        BEGIN
            CREATE TABLE dbo.Items (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL);
            INSERT INTO dbo.Items (Name) VALUES ('Keyboard'), ('Mouse'), ('Monitor');
        END", conn))
    {
        await init.ExecuteNonQueryAsync();
    }

    var items = new List<object>();
    await using var cmd = new SqlCommand("SELECT Id, Name FROM dbo.Items", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        items.Add(new { id = reader.GetInt32(0), name = reader.GetString(1) });

    return Results.Ok(items);
});

app.Run();