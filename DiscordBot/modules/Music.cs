using DISCORD_BOT.injections;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DISCORD_BOT.modules;

public class Music(IVoiceStateService voiceStateService, CreateStream createStream)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceStateService _voiceStateService = voiceStateService;


    private async Task<VoiceClient> InitializeVoiceClient(GatewayClient client, ulong guildId, VoiceState voiceState)
    {
        var voiceClient = await client.JoinVoiceChannelAsync(
            guildId,
            voiceState.ChannelId.GetValueOrDefault(),
            new VoiceClientConfiguration
            {
                Logger = new ConsoleLogger()
            });
        await voiceClient!.StartAsync();

        _voiceStateService.VoiceStates.TryAdd(guildId, voiceClient);

        return voiceClient;
    }


    [SlashCommand("play", "Plays music", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync(string track)
    {
        if (!Uri.IsWellFormedUriString(track, UriKind.Absolute))
        {
            await RespondAsync(InteractionCallback.Message("Invalid track!"));
            return;
        }


        var uri = new Uri(track);
        var host = uri.Host.ToLower();


        var isYoutube =
            host.EndsWith("youtube.com") ||
            host.EndsWith("youtu.be") ||
            host.EndsWith("www.youtube.com");

        if (!isYoutube)
        {
            await RespondAsync(InteractionCallback.Message("I only accept YouTube links!"));
            return;
        }


        var guild = Context.Guild!;


        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out var voiceState))
        {
            await RespondAsync(InteractionCallback.Message("You are not connected to any voice channel!"));
            return;
        }

        var client = Context.Client;
        VoiceClient? voiceClient = null;

        if (_voiceStateService.VoiceStates.TryGetValue(guild.Id, out var voice) == false)
            voiceClient = await InitializeVoiceClient(client, guild.Id, voiceState);
        else
            voiceClient = voice;
        await RespondAsync(InteractionCallback.Message($"Playing {Path.GetFileName(track)}!"));
        await createStream.StartStream(voiceClient, guild.Id, track);
    }
}