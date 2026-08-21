using System.Net.Http.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
	.AddUserSecrets<Program>()
	.Build();

var botToken = configuration["Discord:BotToken"];
var apiBaseUrl = configuration["Api:BaseUrl"];
var apiKey = configuration["Api:Key"];

if (string.IsNullOrWhiteSpace(botToken) ||
	string.IsNullOrWhiteSpace(apiBaseUrl) ||
	string.IsNullOrWhiteSpace(apiKey))
{
	throw new InvalidOperationException(
		"Discord:BotToken, Api:BaseUrl, and Api:Key must be configured with User Secrets.");
}

var discordClient = new DiscordSocketClient(new DiscordSocketConfig
{
	GatewayIntents = GatewayIntents.Guilds |
					 GatewayIntents.GuildMessages |
					 GatewayIntents.MessageContent
});

using var httpClient = new HttpClient
{
	BaseAddress = new Uri($"{apiBaseUrl.TrimEnd('/')}/")
};

discordClient.Log += message =>
{
	Console.WriteLine($"{message.Severity}: {message.Message}");
	return Task.CompletedTask;
};

discordClient.MessageReceived += async message =>
{
	Console.WriteLine($"Received message {message.Id} from {message.Author.Username} in channel {message.Channel.Id}.");

	if (message.Author.IsBot)
	{
		Console.WriteLine($"Ignored bot message {message.Id}.");
		return;
	}

	var payload = new
	{
		messageId = message.Id.ToString(),
		channelId = message.Channel.Id.ToString(),
		author = new
		{
			id = message.Author.Id.ToString(),
			username = message.Author.Username
		},
		content = message.Content,
		sentAt = message.Timestamp
	};

	using var request = new HttpRequestMessage(HttpMethod.Post, "messagesJson")
	{
		Content = JsonContent.Create(payload)
	};
	request.Headers.Add("X-Api-Key", apiKey);

	Console.WriteLine($"Sending message {message.Id} to the API.");
	using var response = await httpClient.SendAsync(request);

	if (response.IsSuccessStatusCode)
	{
		Console.WriteLine($"API stored message {message.Id}: {(int)response.StatusCode} {response.ReasonPhrase}.");
		return;
	}

	var error = await response.Content.ReadAsStringAsync();
	Console.WriteLine($"API rejected Discord message {message.Id}: {(int)response.StatusCode} {error}");
};

await discordClient.LoginAsync(TokenType.Bot, botToken);
await discordClient.StartAsync();

Console.WriteLine("Discord bot is running. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite);
