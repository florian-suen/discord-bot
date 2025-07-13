using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace Discord_Bot.events;

public class MessageCreateHandler(RestClient client, ILogger<MessageCreateHandler> logger)
    : IMessageCreateGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        if (message.Author.IsBot) return;
        var result = message.Content.ToLower() switch
        {
            var text when text.Contains("think of kanzo") => "He is a mere peasant. Not a Lord like me!",
            var text when text.Contains("think of kai") => "Doesn't know anything about LV. Useless!",
            var text when text.Contains("good night my lord") =>
                "Goodnight my subject. May you have a sweet dreams of your lord, Suren.",
            var text when text.Contains("lord suren") => "Yes you're correct. I am your Shiba Lord.",
            var text when text.Contains("suren") => "You may refer to me as Lord Suren.",

            var text when text.Contains("lv") =>
                "Louis Vuitton is a French luxury fashion house founded in 1854 by Louis Vuitton himself. Originally specializing in high-quality travel trunks and leather goods, the brand became renowned for its craftsmanship, innovative designs, and timeless elegance. The iconic LV monogram, introduced in 1896, remains one of the most recognizable symbols in fashion, representing exclusivity and sophistication. Over the years, Louis Vuitton expanded beyond luggage to include handbags, clothing, shoes, accessories, and even fragrances, all while maintaining its reputation for luxury. The brand collaborates with renowned artists and designers, keeping its aesthetic fresh while honoring its heritage. Owned by LVMH, the world's largest luxury group, Louis Vuitton continues to set trends and define opulence in the global fashion industry. Its products are often seen as status symbols, coveted for their quality, prestige, and enduring style.",
            _ => null
        };

        if (result is not null) await client.SendMessageAsync(message.ChannelId, result);
    }
}