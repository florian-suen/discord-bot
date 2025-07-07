using System.Diagnostics;
using NetCord.Gateway.Voice;

namespace DISCORD_BOT.injections;

public class CreateStream(IVoiceStateService voiceStateService)
{
    public required Task? CancellationToken;
    public required Process? Ffmpeg;
    public required Stream? OutStream;
    public required OpusEncodeStream? Stream;
    public required Process? YtDlpProcess;

    private async Task CloseAsync(ulong guildId)
    {
        if (Ffmpeg is not null && Stream is not null && YtDlpProcess is not null && OutStream is not null)
        {
            await OutStream.FlushAsync();
            YtDlpProcess.Close();
            Ffmpeg.Kill();
            await Stream.FlushAsync();
            if (voiceStateService.VoiceStates.TryGetValue(guildId, out var voiceClient)) voiceClient.Dispose();
            await Stream.DisposeAsync();
        }
    }


    public async Task StartStream(VoiceClient voiceClient, ulong guildId, string track)
    {
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));
        OutStream ??= voiceClient.CreateOutputStream();

        if (Stream is not null)
        {
            await Stream.DisposeAsync();
            Stream = null;
        }

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


        var pipeTask = YtDlpProcess.StandardOutput.BaseStream.CopyToAsync(Ffmpeg.StandardInput.BaseStream)
            .ContinueWith(_ => Ffmpeg.StandardInput.Close());

        await Ffmpeg.StandardOutput.BaseStream.CopyToAsync(Stream);
        await pipeTask;


        await CloseAsync(guildId);
    }
}