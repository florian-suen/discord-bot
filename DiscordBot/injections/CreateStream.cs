using System.Diagnostics;
using NetCord.Gateway.Voice;

namespace DISCORD_BOT.injections;

public class CreateStream(IVoiceStateService voiceStateService)
{
    public required Task? CancellationToken;
    public required Process? Ffmpeg;
    public required Stream? OutStream;
    public required CancellationTokenSource Source;
    public required OpusEncodeStream? Stream;
    public required CancellationToken Token;
    public required Process? YtDlpProcess;

    public async Task CloseAsync(ulong guildId)
    {
        if (Ffmpeg is not null && Stream is not null && YtDlpProcess is not null && OutStream is not null)
        {
            await Source.CancelAsync();
            await Stream.FlushAsync(Token);
            await Stream.DisposeAsync();
            await OutStream.FlushAsync(Token);
            YtDlpProcess.Dispose();
            Ffmpeg.Dispose();
            Source.Dispose();
        }
    }


    public async Task StartStream(VoiceClient voiceClient, ulong guildId, string track)
    {
        Source = new CancellationTokenSource();
        Token = Source.Token;
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone), null, Token);

        OutStream ??= voiceClient.CreateOutputStream();

        Stream = new OpusEncodeStream(OutStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);


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

        YtDlpProcess = Process.Start(ytDlpStartInfo)!;
        Ffmpeg = Process.Start(startInfo)!;


        var pipeTask = YtDlpProcess.StandardOutput.BaseStream.CopyToAsync(Ffmpeg.StandardInput.BaseStream, Token)
            .ContinueWith(_ => Ffmpeg.StandardInput.Close(), Token);

        await Ffmpeg.StandardOutput.BaseStream.CopyToAsync(Stream, Token);
        await pipeTask;


        await CloseAsync(guildId);
    }
}