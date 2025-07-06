using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DISCORD_BOT.modules;

public class SlashModule : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("lord", "ME!")]
    public static string Lord()
    {
        return "I am your lord of course!";
    }

    [UserCommand("email")]
    public static string? Email(User user)
    {
        return user.Email;
    }

    [MessageCommand("length")]
    public static string? Length(RestMessage message)
    {
        return message.Content.Length.ToString();
    }
}