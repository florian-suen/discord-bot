using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace DISCORD_BOT;

public class MessageCreateHandler(RestClient client, ILogger<MessageCreateHandler> logger)
    : IMessageCreateGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        if (message.Author.IsBot) return;
        if (message.Content.Contains("think of kanzo", StringComparison.OrdinalIgnoreCase))
            await client.SendMessageAsync(message.ChannelId, "He is a mere peasant. Not a Lord like me!");
        else if (message.Content.Contains("good night my lord", StringComparison.OrdinalIgnoreCase))
            await client.SendMessageAsync(message.ChannelId,
                "Goodnight my subject. May you have a sweet dreams of your lord, Suren.");
        else if (message.Content.Contains("think of kai", StringComparison.OrdinalIgnoreCase))
            await client.SendMessageAsync(message.ChannelId, "Doesn't know anything about LV. Useless!");

        else if (message.Content.Contains("Lord Suren", StringComparison.OrdinalIgnoreCase))
            await client.SendMessageAsync(message.ChannelId, "Yes you're correct. I am your Shiba Lord.");
        else if (message.Content.Contains("Suren", StringComparison.OrdinalIgnoreCase))
            await client.SendMessageAsync(message.ChannelId, "You may refer to me as Lord Suren.");
    }
}