
//Builder om een webapplicatie te maken 
var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/health", () =>
{
    return Results.Ok(new { status = "healthy"});
})
//Uses the name of the endpoint to generate the OpenAPI documentation for this endpoint
.WithName("GetHealth")
.WithOpenApi();

app.MapPost("/messages", (Message message) =>
{
    // Here you can add logic to process the message, e.g., save it to a database or send it to a message queue.
    return Results.Ok(new { status = "message received", message = message });
})

app.Run();
