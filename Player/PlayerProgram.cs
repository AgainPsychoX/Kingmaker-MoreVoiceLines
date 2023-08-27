using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using NAudio.Wave;
using MoreVoiceLines.IPC;
using NAudio.Wave.SampleProviders;

namespace MoreVoiceLines
{
    internal class PlayerProgram
    {
        static void LogException(Exception ex) => Console.WriteLine(ex.ToString());
        static void LogError(string message) => Console.WriteLine("[Error] " + message);
        static void LogWarning(string message) => Console.WriteLine("[Warning] " + message);
        static void Log(string message) => Console.WriteLine(message);
        static void LogDebug(string message) => Console.WriteLine("[Debug] " + message);

        public static string GetDirectory()
        {
            return Path.Combine(Assembly.GetEntryAssembly()!.Location, "..");
        }

        static PlayerSettings settings = new();
        static readonly Dictionary<string, string> localizedStringUuidToRecipe = new();
        static readonly AudioPlaybackEngine audioPlaybackEngine = new(44100, 1);
        //static IWavePlayer audioOutputDevice = new WaveOut();
        static CancellationTokenSource audioCancellationTokenSource = new();
        static NamedPipeServerStream? pipeServer;
        static NamedPipeClientStream? gamePipeClient;

        static async Task Main(string[] args)
        {
            //await PlayAudio("G:\\Steam\\steamapps\\common\\Pathfinder Kingmaker\\Mods\\MoreVoiceLines\\test\\Prologue_Jaethal_01.wav", audioCancellationTokenSource.Token);
            //await Task.Delay(300); // default latency
         
            settings = PlayerSettings.Load();

            // TODO: keep only one instance alive
            // TODO: suicide if no comms from the mod
            // TODO: hide the player console (if setting set so)

            // Load dialog UUID to recipe mapping
            localizedStringUuidToRecipe.Clear();
            var path = Path.Combine(GetDirectory(), "../audio_metadata.csv");
            using (var streamReader = File.OpenText(path))
            {
                var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    localizedStringUuidToRecipe.Add(parts[0], parts[2]);
                }
            }
            Log($"Loaded {localizedStringUuidToRecipe.Count} localized string UUIDs with voice recipes");

            // Open the server pipe to allow requests from the game
            pipeServer = new NamedPipeServerStream("MoreVoiceLinesPlayer", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Log("Audio player pipe server started");

            pipeServer.WaitForConnection();
            Log("Game-side connected");

            // Connect to game-side to notify about stuff, like audio finished
            await Task.Delay(500);
            gamePipeClient = new(".", "MoreVoiceLines", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
            LogDebug($"Trying to connect to game-side client pipe...");
            int retryAttempt = 0;
            int maxRetries = 10;
            while (!gamePipeClient.IsConnected)
            {
                try
                {
                    await Task.Delay(100); // wait a bit to make sure to game-side is ready
                    await gamePipeClient.ConnectAsync(100);
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to connect to game-side client pipe ({++retryAttempt} / {maxRetries})");
                    if (retryAttempt > maxRetries)
                    {
                        LogError($"Failed to connect game-side client pipe");
                        LogException(ex);
                        break;
                    }
                }
            }
            if (gamePipeClient.IsConnected)
            {
                Log($"Game-side client pipe connected");
            }

            // Handle incoming messages
            while (pipeServer.IsConnected)
            {
                await HandleMessage(pipeServer, pipeServer);
            }   

            pipeServer.Close();
            Log("Pipe server closed");

            audioPlaybackEngine.Dispose();
        }

        static async Task HandleMessage(Stream input, Stream output)
        {
            using var message = new MessageReadable();
            await message.ReceiveAsync(input);
            LogDebug($"Handling message of type {message.Type} and length {message.Length} bytes");
            switch (message.Type)
            {
                case MessageType.None:
                    return;
                case MessageType.Disconnected:
                case MessageType.Exit:
                    audioCancellationTokenSource.Cancel();
                    pipeServer?.Disconnect();
                    return;
                case MessageType.SettingsUpdated:
                    try
                    {
                        settings = PlayerSettings.Load();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to load settings");
                        LogException(ex);
                    }
                    return;
                case MessageType.PlayAudio:
                    audioCancellationTokenSource.Cancel();
                    audioCancellationTokenSource = new CancellationTokenSource();
                    _ = PlayAudio(message.ReadString(), audioCancellationTokenSource.Token);
                    return;
                case MessageType.StopAudio:
                    audioCancellationTokenSource.Cancel();
                    return;
                case MessageType.PlayRecipe:
                    return;
                case MessageType.StopRecipe:
                    audioCancellationTokenSource.Cancel();
                    return;
                case MessageType.EchoResponse:
                    {
                        var length = message.ReadUInt16();
                        var bytes = message.ReadBytes(length);
                        return;
                    }
                case MessageType.EchoRequest:
                    {
                        using var writer = new BinaryWriter(message.GetMemoryStream());
                        writer.BaseStream.Position = 0;
                        writer.Write((int)MessageType.EchoResponse);
                        output.Write(message.GetMemoryStream().GetBuffer(), 0, message.Length);
                        return;
                    }
                default:
                    LogWarning($"Unknown message from game-side module");
                    return;
            }
        }

        static async Task PlayAudio(string path, CancellationToken cancellationToken)
        {
            try
            {
                LogDebug($"PlayAudio path='{path}'");
                using (var audioReader = new AudioFileReader(path))
                {
                    audioReader.Volume = settings.Volume;
                    var thing = audioPlaybackEngine.AddMixerInput(audioReader);
                    try
                    {
                        await Task.Delay(audioReader.TotalTime, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        LogDebug($"Cancelled");
                    }
                    audioPlaybackEngine.RemoveMixerInput(thing);
                }

                if (!cancellationToken.IsCancellationRequested && gamePipeClient != null && gamePipeClient.IsConnected)
                {
                    LogDebug($"BBBBBBBBBBBBBBBBB");
                    new MessageWriteable(MessageType.FinishedAudio, 0).TrySend(gamePipeClient);
                }
            }
            catch (Exception ex)
            {
                LogError("Error playing audio");
                LogException(ex);
            }
        }

        //static IEnumerator<Task> PlayingRecipe(string uuid, string recipe)
        //{
        //    LogDebug($"Playing voice for LocalizedString of UUID: '{uuid}' using recipe '{recipe}'");
        //    foreach (var recipePart in recipe.Split('+'))
        //    {
        //        string partSpecifier = null;
        //        switch (recipePart[0])
        //        {
        //            case 'd': /* delay */
        //                var milliseconds = int.Parse(recipePart.Substring(1));
        //                LogDebug($"PlayingAudio: Waiting for {milliseconds}ms");
        //                yield return Task.Delay(milliseconds, cancellationToken);
        //                break;
        //            case 'p': /* part */
        //                partSpecifier = recipePart.Substring(1);
        //                break;
        //            case 's': /* female/male */
        //                partSpecifier = recipePart.Substring(1) + '_' + (playerUnit.Gender == Gender.Male ? 'm' : 'f');
        //                break;
        //            case 'e': /* barony/kingdom */
        //                partSpecifier = recipePart.Substring(1) + '_' + (Game.Instance.Player.PlayerIsKing ? 'b' : 'k');
        //                break;
        //            case 'x': /* female/male + barony/kingdom */
        //                partSpecifier = recipePart.Substring(1) + '_' + (playerUnit.Gender == Gender.Male ? 'm' : 'f') + (Game.Instance.Player.PlayerIsKing ? 'b' : 'k');
        //                break;
        //            case 'n': /* name */
        //            case 'r': /* race */
        //            case 'k': /* kingdom name */
        //                // Not implemented yet, recipe should not use those.
        //                // The prepared audio files can have the variables baked in the audio anyway.
        //                LogWarning($"Recipe part '{recipePart[0]}' not implemented, skipping");
        //                break;
        //            default:
        //                LogWarning($"Unknown recipe part '{recipePart[0]}', skipping");
        //                break;
        //        }
        //        if (partSpecifier == null)
        //        {
        //            continue;
        //        }

        //        var fileName = uuid + '_' + partSpecifier + ".wav";
        //        var path = Path.Combine(ModEntry.Path, "audio", fileName);
        //        if (!File.Exists(path))
        //        {
        //            LogError($"Missing file '{fileName}', skipping");
        //            continue;
        //        }

        //        using (var audioFile = new WaveFileReader(path))
        //        {
        //            outputDevice.Init(audioFile);
        //            outputDevice.Play();
        //            yield return new WaitForSeconds((float)audioFile.TotalTime.TotalSeconds);
        //        }
        //    }
        //    //if (onEnd != null)
        //    //{
        //    //    onEnd(null, null);
        //    //    onEnd = null;
        //    //}
        //}
    }
}