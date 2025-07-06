using System.Diagnostics;
using NetCord;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DISCORD_BOT.modules;

public class Music : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Plays music", Contexts = [InteractionContextType.Guild])]
    public async Task PlayAsync(string track)
    {
        if (!Uri.IsWellFormedUriString(track, UriKind.Absolute))
        {
            await RespondAsync(InteractionCallback.Message("Invalid track! I only accept Youtube!"));
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


        var voiceClient = await client.JoinVoiceChannelAsync(
            guild.Id,
            voiceState.ChannelId.GetValueOrDefault(),
            new VoiceClientConfiguration
            {
                Logger = new ConsoleLogger()
            });


        await voiceClient.StartAsync();


        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));


        await RespondAsync(InteractionCallback.Message($"Playing {Path.GetFileName(track)}!"));


        var outStream = voiceClient.CreateOutputStream();


        OpusEncodeStream stream = new(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);


        ProcessStartInfo ytDlpStartInfo = new("yt-dlp")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ytDlpStartInfo.ArgumentList.Add("-f");
        ytDlpStartInfo.ArgumentList.Add("bestaudio");
        ytDlpStartInfo.ArgumentList.Add("-o");
        ytDlpStartInfo.ArgumentList.Add("-");
        ytDlpStartInfo.ArgumentList.Add("--no-playlist");
        ytDlpStartInfo.ArgumentList.Add(track);


        ProcessStartInfo startInfo = new("ffmpeg")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var arguments = startInfo.ArgumentList;


        arguments.Add("-i");
        arguments.Add("-");


        arguments.Add("-ac");
        arguments.Add("2");


        arguments.Add("-f");
        arguments.Add("s16le");


        arguments.Add("-ar");
        arguments.Add("48000");

        arguments.Add("pipe:1");

        var ytDlpProcess = Process.Start(ytDlpStartInfo)!;
        var ffmpeg = Process.Start(startInfo)!;


        var pipeTask = ytDlpProcess.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream)
            .ContinueWith(_ => ffmpeg.StandardInput.Close());

        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);

        await pipeTask;

        await stream.FlushAsync();

        ytDlpProcess.Close();
        ffmpeg.Close();
    }


    [SlashCommand("echo", "Creates echo", Contexts = [InteractionContextType.Guild])]
    public async Task<string> EchoAsync()
    {
        var guild = Context.Guild!;
        var userId = Context.User.Id;

        // Get the user voice state
        if (!guild.VoiceStates.TryGetValue(userId, out var voiceState))
            return "You are not connected to any voice channel!";

        var client = Context.Client;

        // You should check if the bot is already connected to the voice channel.
        // If so, you should use an existing 'VoiceClient' instance instead of creating a new one.
        // You also need to add a synchronization here. 'JoinVoiceChannelAsync' should not be used concurrently for the same guild
        var voiceClient = await client.JoinVoiceChannelAsync(
            guild.Id,
            voiceState.ChannelId.GetValueOrDefault(),
            new VoiceClientConfiguration
            {
                ReceiveHandler = new VoiceReceiveHandler(), // Required to receive voice
                Logger = new ConsoleLogger()
            });

        // Connect
        await voiceClient.StartAsync();

        // Enter speaking state, to be able to send voice
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

        // Create a stream that sends voice to Discord
        var outStream = voiceClient.CreateOutputStream(false);

        voiceClient.VoiceReceive += args =>
        {
            // Pass current user voice directly to the output to create echo
            if (voiceClient.Cache.Users.TryGetValue(args.Ssrc, out var voiceUserId) && voiceUserId == userId)
                outStream.Write(args.Frame);
            return default;
        };

        // Return the response
        return "Echo!";
    }
}