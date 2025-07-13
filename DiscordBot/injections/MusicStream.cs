using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;

namespace DISCORD_BOT.injections;

//currently only works for one guild one bot but ideally with guildId/dictionary for each guild instance
//Update Music Stream to store guild ids for each process
public class MusicStream(
    IVoiceStateService voiceStateService,
    RestClient restClient,
    GatewayClient gatewayClient,
    IOptions<AppConfig> config)
{
    private readonly IOptions<AppConfig> _config = config;
    private readonly MusicQueue _musicTrack = new(config);

    public required bool Active;
    public required int? CurrentIndex;
    public required Process? FetchProcess;
    public required Process? Ffmpeg;
    public required Stream? OutStream;
    public required CancellationTokenSource? Source;
    public required bool SpeakingState;
    public required OpusEncodeStream? Stream;
    public required CancellationToken Token;

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
        if (_musicTrack?.Next() is null && _musicTrack?.Current is not null)
        {
            var currentTrack = _musicTrack?.Current;
            CurrentIndex = _musicTrack!.CurrentIndex;
            var activityName = currentTrack!.Value.title.Contains("...Loading")
                ? "some rock & roll!"
                : currentTrack!.Value.title.Substring(10);


            var presence = new PresenceProperties(UserStatusType.DoNotDisturb)
            {
                Activities =
                [
                    new UserActivityProperties(activityName, UserActivityType.Listening)
                    {
                        State = "Your Lord is playing some music!"
                    }
                ]
            };
            await gatewayClient.UpdatePresenceAsync(presence);
            await restClient.SendMessageAsync(channelId, $"Currently Playing {currentTrack.Value.url}",
                cancellationToken: Token);
            await _streamTask(currentTrack.Value.url);
        }


        else
        {
            while (_musicTrack?.Next() != null)
            {
                var currentTrack = _musicTrack?.Current;

                var activityName = currentTrack!.Value.title.Contains("...Loading")
                    ? "some rock & roll!"
                    : currentTrack!.Value.title.Substring(10);


                var presence = new PresenceProperties(UserStatusType.DoNotDisturb)
                {
                    Activities =
                    [
                        new UserActivityProperties(activityName, UserActivityType.Listening)
                        {
                            State = "Your Lord is playing some music!"
                        }
                    ]
                };
                await gatewayClient.UpdatePresenceAsync(presence);


                if (currentTrack is not ({ } title, { } url)) continue;
                await restClient.SendMessageAsync(channelId, $"Currently Playing {url}", cancellationToken: Token);
                var stream = await _streamTask(url);
                if (stream == false) break;
            }
        }

        Console.WriteLine("Playback finished - queue is empty");
    }


    public async Task<bool> CloseAsync()
    {
        if (Active && Ffmpeg is not null && Stream is not null && FetchProcess is not null && OutStream is not null &&
            Source is not null)
        {
            await Source.CancelAsync();
            await Stream.FlushAsync();
            await Stream.DisposeAsync();
            Stream = null;
            await OutStream.FlushAsync();
            await TerminateProcess(FetchProcess);
            await TerminateProcess(Ffmpeg);
            Active = false;
            Source.Dispose();
            Source = null;


            var closePresence = new PresenceProperties(UserStatusType.Online)
            {
                Activities =
                [
                    new UserActivityProperties("Idling", UserActivityType.Custom)
                    {
                        State = "Your friendly neighbourhood Shiba"
                    }
                ]
            };
            await gatewayClient.UpdatePresenceAsync(closePresence);


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
        var name = _config.Value.App.Name;
        if (OutStream is null) return false;
        Stream = new OpusEncodeStream(OutStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);
        ProcessStartInfo fetchStartInfo = new(name)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        fetchStartInfo.ArgumentList.Add("-f");
        fetchStartInfo.ArgumentList.Add("bestaudio");
        fetchStartInfo.ArgumentList.Add("-o");
        fetchStartInfo.ArgumentList.Add("-");
        fetchStartInfo.ArgumentList.Add("--no-playlist");
        fetchStartInfo.ArgumentList.Add(track);


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


        var newFetch = Process.Start(fetchStartInfo)! ??
                       throw new InvalidOperationException($"{name} failed to start");
        var newFfmpeg = Process.Start(startInfo)! ??
                        throw new InvalidOperationException("ffmpeg failed to start");


        FetchProcess = newFetch;

        Ffmpeg = newFfmpeg;


        try
        {
            var pipeTask = FetchProcess.StandardOutput.BaseStream.CopyToAsync(
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
}

public class MusicQueue(IOptions<AppConfig> config)
{
    private readonly IOptions<AppConfig> _config = config;
    private readonly Random _rng = new();
    private readonly LinkedList<(string title, string url)> _trackList = new();

    private LinkedListNode<(string title, string url)>? _currentNode;


    public (string title, string url)? Current => _currentNode?.Value;

    public int CurrentIndex => _currentNode is null ? -1 : GetTrackIndex(_currentNode);

    public bool IsEmpty => _trackList.Count == 0;
    public int Count => _trackList.Count;


    private async Task FetchModifyNodeTitle(string track, LinkedListNode<(string, string)> node)
    {
        var fetchName = _config.Value.App.Name;
        var fetch = new ProcessStartInfo(fetchName)
        {
            RedirectStandardError = true,
            Arguments = $"--get-title {track}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };


        using var process = Process.Start(fetch);
        var output = await process?.StandardOutput.ReadToEndAsync()!;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"Process error: {error}");
            throw new Exception(error);
        }

        ;

        var index = GetTrackIndex(node);
        var title = $"Track {index} - {output}";
        node.Value = (title, track);
    }


    public void Enqueue(string track)
    {
        var index = _trackList.Count + 1;
        var title = $"Track {index} ...Loading";

        var node = _trackList.AddLast((title, track));

        _ = FetchModifyNodeTitle(track, node);
    }

    public void EnqueueRange(IEnumerable<string> tracks)
    {
        foreach (var track in tracks)
        {
            var index = _trackList.Count + 1;
            var title = $"Track {index} ...Loading";

            var node = _trackList.AddLast((title, track));

            _ = FetchModifyNodeTitle(track, node);
        }
    }

    public (string, string)? Next()
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
            return true;
        }

        return _currentNode?.Next != null;
    }


    public bool HasPrevious()
    {
        if (_currentNode == null)
        {
            if (IsEmpty) return false;
            return true;
        }


        return _currentNode.Previous != null;
    }

    public (string, string)? Previous()
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
        //One update to make is when title is still loading and someone removes in succession

        try
        {
            if (index < 0 || index >= _trackList?.Count)
                return false;

            var currentNode = _trackList?.First;

            for (var i = 0; i < index; i++) currentNode = currentNode?.Next;

            if (currentNode is not null) _trackList?.Remove(currentNode);

            var newTrackNumber = 1;
            var currentNodeToMutate = _trackList?.First;
            while (currentNodeToMutate is not null)
            {
                var splitName = currentNodeToMutate.Value.title.Split('-', 2)[1];
                var url = currentNodeToMutate.Value.url;
                currentNodeToMutate.Value = ($"Track {newTrackNumber} - {splitName}", url);
                newTrackNumber++;
                currentNodeToMutate = currentNodeToMutate.Next;
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error Removing - {e.Message}");
            return false;
        }
    }

    public bool SkipTo(int index)
    {
        // var node = _trackList.Find(track);
        // if (node == null)
        //     return false;
        //
        // _currentNode = node;
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

    public IReadOnlyList<(string name, string url)> GetAllTracks()
    {
        return _trackList.ToList().AsReadOnly();
    }

    public int GetTrackIndex(LinkedListNode<(string Title, string Url)> node)
    {
        var index = 1;
        var current = _trackList.First;
        while (current != null && current != node)
        {
            index++;
            current = current.Next;
        }

        return index;
    }
}