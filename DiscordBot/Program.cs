using DISCORD_BOT.injections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;

namespace DISCORD_BOT;

public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        //builder.Services.AddHostedService<Announcements>();
        builder.Services.AddSingleton<IVoiceStateService, VoiceStateService>();
        builder.Services.AddSingleton<MusicStream>();
        builder.Services
            .AddDiscordGateway(options =>
            {
                options.Presence = new PresenceProperties(UserStatusType.Online)
                {
                    Activities =
                    [
                        new UserActivityProperties("Custom Status", UserActivityType.Custom)
                        {
                            State = "At your service. The greatest of all Shiba is here to attend to all your needs"
                        }
                    ]
                };
                options.Intents = GatewayIntents.GuildMessages
                                  | GatewayIntents.DirectMessages
                                  | GatewayIntents.MessageContent
                                  | GatewayIntents.DirectMessageReactions
                                  | GatewayIntents.GuildMessageReactions
                                  | GatewayIntents.Guilds | GatewayIntents.GuildEmojisAndStickers
                                  | GatewayIntents.GuildVoiceStates;
            }).AddGatewayHandlers(typeof(Program).Assembly).AddApplicationCommands().AddDiscordGateway();
        builder.Services.Configure<AppConfig>(builder.Configuration);
        var host = builder.Build();

        host.AddModules(typeof(Program).Assembly);
        host.UseGatewayHandlers();
        await host.RunAsync();
    }
}

public class AppSettings
{
    public List<string> Ends { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
}

public class AppConfig
{
    public AppSettings App { get; set; } = new();
}