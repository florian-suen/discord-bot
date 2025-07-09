using System.Collections.Concurrent;
using NetCord.Gateway.Voice;

namespace DISCORD_BOT;

public interface IVoiceStateService
{
    public ConcurrentDictionary<ulong, VoiceClient> VoiceStates { get; }
}

public class VoiceStateService : IVoiceStateService
{
    public ConcurrentDictionary<ulong, VoiceClient> VoiceStates { get; } = new();
}