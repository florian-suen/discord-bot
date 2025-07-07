using DISCORD_BOT.injections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        builder.Services.AddSingleton<IVoiceStateService, VoiceStateService>();
        builder.Services.AddSingleton<CreateStream>();
        builder.Services
            .AddDiscordGateway(options =>
            {
                options.Intents = GatewayIntents.GuildMessages
                                  | GatewayIntents.DirectMessages
                                  | GatewayIntents.MessageContent
                                  | GatewayIntents.DirectMessageReactions
                                  | GatewayIntents.GuildMessageReactions
                                  | GatewayIntents.Guilds | GatewayIntents.GuildEmojisAndStickers
                                  | GatewayIntents.GuildVoiceStates;
            }).AddGatewayHandlers(typeof(Program).Assembly).AddApplicationCommands()
            ;
        var host = builder.Build();

        host.AddModules(typeof(Program).Assembly);
        host.UseGatewayHandlers();
        await host.RunAsync();
    }
}