using DISCORD_BOT;
using DISCORD_BOT.injections;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Discord_Bot.commands;

public class Music(IVoiceStateService voiceStateService, MusicStream musicStream, IOptions<AppConfig> config)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IOptions<AppConfig> _config = config;
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

    [SlashCommand("list", "List all tracks", Contexts = [InteractionContextType.Guild])]
    public async Task ListAsync()
    {
        if (musicStream.MusicTracks().IsEmpty)
        {
            EmbedProperties emptyEmbed = new()
            {
                Title = "No Tracks",
                Description = "Your Lord can't find any tracks to play.",
                Thumbnail =
                    "https://img.freepik.com/premium-vector/cute-shiba-inu-cartoon-character-happy-attractive-shiba-inu-dog-vector-illustration-style_600033-105.jpg",
                Color = new Color(0xFF2600)
            };

            InteractionMessageProperties message = new() { Embeds = [emptyEmbed] };


            await RespondAsync(InteractionCallback.Message(message));
            return;
        }


        await RespondAsync(InteractionCallback.DeferredMessage());


        var allTracks = musicStream.MusicTracks().GetAllTracks();
        var currentIndex = musicStream.CurrentIndex;

        var array = allTracks.Select((item, index) => new EmbedFieldProperties
        {
            Name = index == currentIndex - 1 ? $"{item.name} (Now Playing)" : item.name,
            Value = item.url
        });


        EmbedProperties embed = new()
        {
            Title = "All Tracks",
            Description = "A list of all the songs added to the queue!",
            Thumbnail = "https://i.pinimg.com/474x/fd/cc/07/fdcc0776ae1abd9240e0d9eac636accb.jpg",
            Color = new Color(0xFFA500),
            Fields = array
        };


        await FollowupAsync(new InteractionMessageProperties { Embeds = [embed] });
    }

    [SlashCommand("add", "Add Track", Contexts = [InteractionContextType.Guild])]
    public async Task AddAsync(string track)
    {
        var end = _config.Value.App.Ends;
        var name = _config.Value.App.Name;
        if (!Uri.IsWellFormedUriString(track, UriKind.Absolute))
        {
            await RespondAsync(InteractionCallback.Message("Invalid track!"));
            return;
        }


        var uri = new Uri(track);
        var host = uri.Host.ToLower();

        var correctLink = false;

        foreach (var link in end)
        {
            var isCorrectLink = host.EndsWith(link);
            if (isCorrectLink) correctLink = true;
        }


        if (!correctLink)
        {
            await RespondAsync(InteractionCallback.Message($"I only accept {name} links!"));
            return;
        }


        musicStream.MusicTracks().Enqueue(track);

        await RespondAsync(InteractionCallback.Message("Added track to queue!"));
    }

    [SlashCommand("remove", "Remove track", Contexts = [InteractionContextType.Guild])]
    public async Task RemoveAsync(int index)
    {
        var success = musicStream.MusicTracks().Remove(index - 1);
        if (success) await RespondAsync(InteractionCallback.Message("Removed track from queue!"));
        else
            await RespondAsync(InteractionCallback.Message(
                "Couldn't find track. You may be removing a track whilst another is being added. Please Try again."));
    }

    [SlashCommand("clear", "clear all tracks", Contexts = [InteractionContextType.Guild])]
    public async Task ClearAsync()
    {
        musicStream.MusicTracks().Clear();
        await RespondAsync(InteractionCallback.Message("All Tracks Cleared!"));
    }


    [SlashCommand("next", "Next track", Contexts = [InteractionContextType.Guild])]
    public async Task NextAsync()
    {
        if (musicStream.MusicTracks().IsEmpty || musicStream.MusicTracks().HasNext() == false)
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to play next!"));
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

        await RespondAsync(InteractionCallback.Message("Playing next track!"));
        await musicStream.StartStream(voiceClient, guild.Id, Context.Channel.Id);
    }


    [SlashCommand("previous", "Next track", Contexts = [InteractionContextType.Guild])]
    public async Task PreviousAsync()
    {
        if (musicStream.MusicTracks().IsEmpty || musicStream.MusicTracks().HasPrevious() == false)
        {
            await RespondAsync(InteractionCallback.Message("There is nothing previously to play!"));
            return;
        }

        if (musicStream.MusicTracks().HasNext() == false)
        {
            await RespondAsync(InteractionCallback.Message("There is no previous track!"));
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

        await RespondAsync(InteractionCallback.Message("Playing previous track!"));

        const bool playPreviousTrack = true;
        await musicStream.StartStream(voiceClient, guild.Id, Context.Channel.Id, playPreviousTrack);
    }


    [SlashCommand("stop", "Plays music", Contexts = [InteractionContextType.Guild])]
    public async Task StopAsync()
    {
        if (musicStream.Active)
        {
            await RespondAsync(InteractionCallback.Message("Stopping music..."));
            await musicStream.CloseAsync();
            await Context.Client.Rest.SendMessageAsync(Context.Channel.Id, "Music Stopped!");
        }
        else
        {
            await RespondAsync(InteractionCallback.Message("No music to stop!"));
        }
    }


    [SlashCommand("play", "Plays music", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync()
    {
        if (musicStream.MusicTracks().IsEmpty)
        {
            await RespondAsync(InteractionCallback.Message("There is nothing to play! How dare you waste my time!"));
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

        await RespondAsync(InteractionCallback.Message("I will now play you some melodies."));
        await musicStream.StartStream(voiceClient, guild.Id, Context.Channel.Id);
    }
}