using DISCORD_BOT;
using DISCORD_BOT.injections;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Discord_Bot.events;

public class VoiceEvent(GatewayClient gatewayClient, IVoiceStateService voiceStateService, CreateStream createStream)
    : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState arg)
    {
        if (voiceStateService.VoiceStates.TryGetValue(arg.GuildId, out var voiceClient))
        {
            var count = gatewayClient.Cache.Guilds[arg.GuildId].VoiceStates.Count(s =>
                s.Value.GuildId == arg.GuildId && s.Value.ChannelId == arg.ChannelId);

            Console.WriteLine(count);
            if (count > 1) return;
            voiceStateService.VoiceStates.TryRemove(arg.GuildId, out var removedVoiceClient);
            if (removedVoiceClient is not null) await removedVoiceClient.CloseAsync();
            var voiceProperties = new VoiceStateProperties(arg.GuildId, null);
            await gatewayClient.UpdateVoiceStateAsync(voiceProperties);
            createStream.SpeakingState = false;
            createStream.OutStream = null;
            await createStream.CloseAsync();
        }
    }
}