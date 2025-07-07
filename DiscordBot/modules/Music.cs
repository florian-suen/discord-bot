using System.Diagnostics;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DISCORD_BOT.modules;

public class Music(IVoiceStateService voiceStateService) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    
    private readonly IVoiceStateService _voiceStateService = voiceStateService;
    public required Process? Ffmpeg;
    public required OpusEncodeStream? Stream;



    private async Task<VoiceClient> InitializeVoiceClient(GatewayClient client,ulong guildId,VoiceState voiceState) {
        
         var voiceClient =  await client.JoinVoiceChannelAsync(
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

    private async Task CloseAsync(ulong guildId)
    {
        if (Ffmpeg is not null && Stream is not null)
        {
            Ffmpeg.Kill();
            await Stream.FlushAsync();
            if(_voiceStateService.VoiceStates.TryGetValue(guildId,out var voiceClient)) voiceClient.Dispose();;
            
            await Stream.DisposeAsync();
        }
    }
    
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
        VoiceClient? voiceClient = null;
       Console.Write(_voiceStateService.VoiceStates.TryGetValue(guild.Id,out var voicsde));
       if (_voiceStateService.VoiceStates.TryGetValue(guild.Id, out var voice) == false)
           voiceClient = await InitializeVoiceClient(client, guild.Id, voiceState);
       else voiceClient = voice;
       
       if (voiceClient is null) return;
        
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));
        await RespondAsync(InteractionCallback.Message($"Playing {Path.GetFileName(track)}!"));
 
        
        var outStream = voiceClient.CreateOutputStream();
        Stream = new(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);


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
          Ffmpeg = Process.Start(startInfo)!;


        var pipeTask = ytDlpProcess.StandardOutput.BaseStream.CopyToAsync(Ffmpeg.StandardInput.BaseStream)
            .ContinueWith(_ => Ffmpeg.StandardInput.Close());

        await Ffmpeg.StandardOutput.BaseStream.CopyToAsync(Stream);
        await pipeTask;
        
        ytDlpProcess.Close();
        await CloseAsync(guild.Id);
    }

    
    
    
    

    [SlashCommand("echo", "Creates echo", Contexts = [InteractionContextType.Guild])]
    public async Task<string> EchoAsync()
    {
        var guild = Context.Guild!;
        var userId = Context.User.Id;

     
        if (!guild.VoiceStates.TryGetValue(userId, out var voiceState))
            return "You are not connected to any voice channel!";

        var client = Context.Client;

     
        var voiceClient = await client.JoinVoiceChannelAsync(
            guild.Id,
            voiceState.ChannelId.GetValueOrDefault(),
            new VoiceClientConfiguration
            {
                ReceiveHandler = new VoiceReceiveHandler(), // Required to receive voice
                Logger = new ConsoleLogger()
            });

 
        await voiceClient.StartAsync();

 
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

 
        var outStream = voiceClient.CreateOutputStream(false);

        voiceClient.VoiceReceive += args =>
        {
           
            if (voiceClient.Cache.Users.TryGetValue(args.Ssrc, out var voiceUserId) && voiceUserId == userId)
                outStream.Write(args.Frame);
            return default;
        };

   
        return "Echo!";
    }
}