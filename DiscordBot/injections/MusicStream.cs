using System.ComponentModel;
using System.Diagnostics;
using NetCord.Gateway.Voice;
using NetCord.Rest;

namespace DISCORD_BOT.injections;

//currently only works for one guild one bot but ideally with guildId/dictionary for each guild instance
//Update Music Stream to store guild ids for each process
public class MusicStream(IVoiceStateService voiceStateService, RestClient restClient)
{
    private readonly MusicQueue _musicTrack = new();
    public required bool Active;
    public required Process? Ffmpeg;
    public required Stream? OutStream;
    public required CancellationTokenSource? Source;
    public required bool SpeakingState;
    public required OpusEncodeStream? Stream;
    public required CancellationToken Token;
    public required Process? YtDlpProcess;


    public MusicQueue MusicTracks()
    {
        return _musicTrack;
    }

    public async Task StartStream(VoiceClient voiceClient, ulong guildId, ulong channelId, bool previous = false)
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
        if (previous) _musicTrack.Previous();
        await Play(channelId);
    }

    private async Task Play(ulong channelId)
    {
        while (_musicTrack?.Next() != null)
        {
            var currentTrack = _musicTrack?.Current;
            await restClient.SendMessageAsync(channelId, $"Currently Playing {currentTrack}");
            if (currentTrack is not null)
            {
                var stream = await _streamTask(currentTrack);

                if (stream == false) break;
            }
        }

        Console.WriteLine("Playback finished - queue is empty");
    }


    public async Task<bool> CloseAsync()
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

            return true;
        }

        return false;
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


    private async Task<bool> _streamTask(string track)
    {
        if (OutStream is null) return false;
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
            return false;
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

        return true;
    }


    public class MusicQueue
    {
        private readonly Random _rng = new();
        private readonly LinkedList<string> _trackList = new();
        private LinkedListNode<string>? _currentNode;


        public string? Current => _currentNode?.Value;


        public bool IsEmpty => _trackList.Count == 0;
        public int Count => _trackList.Count;


        public void Enqueue(string track)
        {
            _trackList.AddLast(track);
        }

        public void EnqueueRange(IEnumerable<string> tracks)
        {
            foreach (var track in tracks)
                _trackList.AddLast(track);
        }

        public string? Next()
        {
            if (_currentNode == null)
            {
                _currentNode = _trackList.First;
                return _currentNode?.Value;
            }

            if (_currentNode.Next == null) return null;

            _currentNode = _currentNode.Next;

            return _currentNode.Value;
        }

        public bool HasNext()
        {
            if (_currentNode == null)
            {
                if (IsEmpty) return false;
                _currentNode = _trackList.First;
            }

            if (_currentNode?.Next == null) return false;

            return true;
        }


        public bool HasPrevious()
        {
            if (_currentNode == null) return false;

            if (_currentNode.Previous == null) return false;

            return true;
        }

        public string? Previous()
        {
            if (_currentNode?.Previous == null)
                return null;

            _currentNode = _currentNode.Previous;

            if (_currentNode.Previous is not null)
            {
                _currentNode = _currentNode.Previous;
                return _currentNode?.Previous?.Value;
            }

            _currentNode = null;
            return null;
        }


        public bool Remove(int index)
        {
            if (index < 0 || index >= _trackList?.Count)
                return false;

            var currentNode = _trackList?.First;

            for (var i = 0; i < index; i++) currentNode = currentNode?.Next;

            if (currentNode is not null) _trackList?.Remove(currentNode);
            return true;
        }

        public bool SkipTo(string track)
        {
            var node = _trackList.Find(track);
            if (node == null)
                return false;

            _currentNode = node;
            return true;
        }

        public void Clear()
        {
            _trackList.Clear();
            _currentNode = null;
        }


        public void Shuffle()
        {
            if (_trackList.Count <= 1)
                return;

            var list = _trackList.ToList();
            var n = list.Count;

            while (n > 1)
            {
                n--;
                var k = _rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            _trackList.Clear();
            foreach (var track in list)
                _trackList.AddLast(track);


            if (_currentNode != null)
                _currentNode = _trackList.Find(_currentNode.Value);
        }

        public IReadOnlyList<string> GetAllTracks()
        {
            return _trackList.ToList().AsReadOnly();
        }
    }
}