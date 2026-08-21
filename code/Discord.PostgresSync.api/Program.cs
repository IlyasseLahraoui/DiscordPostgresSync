
//Builder om een webapplicatie te maken 
using Discord.PostgresSync.api.Requests;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["ApiKey"];

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("ApiKey is not configured.");
}

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health")
    {
        await next();
        return;
    }

    var requestApiKey = context.Request.Headers["X-Api-Key"].ToString();

    if (!string.Equals(requestApiKey, apiKey, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

app.MapGet("/health", async (IConfiguration configuration) =>
{
    var connectionString = configuration.GetConnectionString("Postgres");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "PostgreSQL connection string is not configured.");
    }

    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var databaseResult = await command.ExecuteScalarAsync();

        return Results.Ok(new
        {
            status = "healthy",
            api = "connected",
            database = "connected",
            databaseResult
        });
    }
    catch
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "PostgreSQL is unavailable.");
    }
})
//Uses the name of the endpoint to generate the OpenAPI documentation for this endpoint
.WithName("GetHealth")
.WithOpenApi();

app.MapPost("/messages", (DiscordMessage message) =>
{
    if (string.IsNullOrWhiteSpace(message.MessageId) ||
      string.IsNullOrWhiteSpace(message.ChannelId) ||
      string.IsNullOrWhiteSpace(message.AuthorId))
    {
        return Results.BadRequest(new
        {
            error = "messageId, channelId, and authorId are required."
        });
    }

    return Results.Created($"/messages/{message.MessageId}", message);

}).WithName("CreateMessage")
.WithOpenApi();

app.MapPost("/messagesJson", async (
    JsonElement payload,
    IConfiguration configuration) =>
{
    if (payload.ValueKind != JsonValueKind.Object)
    {
        return Results.BadRequest(new
        {
            error = "The request body must be a JSON object."
        });
    }

    if (!payload.TryGetProperty("messageId", out var messageIdElement) ||
        string.IsNullOrWhiteSpace(messageIdElement.GetString()))
    {
        return Results.BadRequest(new
        {
            error = "A non-empty messageId property is required."
        });
    }

    var connectionString = configuration.GetConnectionString("Postgres");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "PostgreSQL connection string is not configured.");
    }

    const string sql = """
        INSERT INTO discord_messages (message_id, payload)
        VALUES (@messageId, @payload::jsonb)
        ON CONFLICT (message_id) DO NOTHING;
        """;

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("messageId", messageIdElement.GetString()!);
    command.Parameters.AddWithValue("payload", payload.GetRawText());

    var insertedRows = await command.ExecuteNonQueryAsync();

    if (insertedRows == 0)
    {
        return Results.Conflict(new
        {
            error = "A message with this messageId already exists."
        });
    }

    return Results.Created($"/messages/{messageIdElement.GetString()}", payload);
})
.WithName("CreateMessageJson")
.WithOpenApi();

app.Run();
