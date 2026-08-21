namespace Discord.PostgresSync.api.Requests
{
    public record DiscordMessage(string MessageId, string ChannelId, string AuthorId, string AutherUserName, string Content, DateTimeOffset SentAt);    
}
