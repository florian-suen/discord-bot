using System.ComponentModel;
using System.Diagnostics;
using NetCord.Gateway.Voice;

namespace DISCORD_BOT.injections;

//currently only works for one guild one bot but ideally with guildId/dictionary for each class instance
public class CreateStream(IVoiceStateService voiceStateService)
{
    public required bool Active;
    public required Process? Ffmpeg;
    public required Stream? OutStream;
    public required CancellationTokenSource? Source;
    public required bool SpeakingState;
    public required OpusEncodeStream? Stream;
    public required CancellationToken Token;
    public required Process? YtDlpProcess;


    public async Task CloseAsync()
    {
        if (Active && Ffmpeg is not null && Stream is not null && YtDlpProcess is not null && OutStream is not null &&
            Source is not null)
        {
            await Source.CancelAsync();
            await Stream.FlushAsync();
            await Stream.DisposeAsync();
            Stream = null;
            await OutStream.FlushAsync();
            await TerminateProcess(YtDlpProcess);
            await TerminateProcess(Ffmpeg);
            Active = false;
            Source.Dispose();
            Source = null;
        }
    }


    private async Task TerminateProcess(Process? process)
    {
        if (process == null)
            return;

        try
        {
            if (process.SafeHandle.IsClosed || process.HasExited)
                return;


            if (process.StartInfo.RedirectStandardInput)
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                    Console.WriteLine("Is Closed Already");
                }

                await Task.Delay(100);
            }


            if (!process.HasExited)
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
                Console.WriteLine("Disposal Errors");
            }
        }
    }


    public async Task StartStream(VoiceClient voiceClient, ulong guildId, string track)
    {
        await CloseAsync();

        Active = true;
        Source = new CancellationTokenSource();
        Token = Source.Token;
        if (SpeakingState == false)
        {
            await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone), null, Token);
            SpeakingState = true;
        }


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


        var newYtDlp = Process.Start(ytDlpStartInfo)! ??
                       throw new InvalidOperationException("yt-dlp failed to start");
        var newFfmpeg = Process.Start(startInfo)! ??
                        throw new InvalidOperationException("ffmpeg failed to start");


        YtDlpProcess = newYtDlp;

        Ffmpeg = newFfmpeg;


        try
        {
            var pipeTask = YtDlpProcess.StandardOutput.BaseStream.CopyToAsync(
                Ffmpeg.StandardInput.BaseStream,
                Token
            ).ContinueWith(async _ =>
            {
                try
                {
                    Ffmpeg.StandardInput.Close();
                    await Ffmpeg.StandardInput.DisposeAsync();
                }
                catch
                {
                    Console.WriteLine("Is Closed Already");
                }
            }, Token);


            var ffmpegOutputTask = Ffmpeg.StandardOutput.BaseStream.CopyToAsync(Stream, Token);
            await Task.WhenAny(
                ffmpegOutputTask,
                Ffmpeg.WaitForExitAsync(Token)
            );

            if (Ffmpeg.HasExited && Ffmpeg.ExitCode != 0)
                throw new Exception($"FFmpeg crashed with exit code: {Ffmpeg.ExitCode}");


            await pipeTask;
        }
        catch (TaskCanceledException e)
        {
            Console.WriteLine("Streaming was cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Streaming error: {ex}");
            throw;
        }
        finally
        {
            if (Token.IsCancellationRequested == false) await CloseAsync();
        }
    }
}