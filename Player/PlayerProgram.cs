﻿using System;
using System.Linq;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Diagnostics;
using NAudio.Wave;
using MoreVoiceLines.IPC;

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
        static CancellationTokenSource audioCancellationTokenSource = new();
        static CancellationTokenSource serverCancellationTokenSource = new();
        static NamedPipeServerStream? pipeServer;
        static NamedPipeClientStream? gamePipeClient;

        static async Task Main(string[] args)
        {        
            settings = PlayerSettings.Load();

            if (!args.Contains("--no-suicide"))
            {
                // Start "suicide" task that ensures the process exits 
                var suicideThread = new Thread(() =>
                {
                    Thread.Sleep(1000);
                    if (Process.GetProcessesByName("Kingmaker").Length == 0)
                    {
                        Log($"Kingmaker exited, stopping the audio player too");
                        serverCancellationTokenSource.Cancel();
                        Thread.Sleep(1000); // a bit of time for peaceful exit
                        Environment.Exit(0);
                    }
                });
                suicideThread.Start();
            }

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

            var cancellationToken = serverCancellationTokenSource.Token;

            // Wait for connection to handle incoming requests from the game
            {
                pipeServer = new NamedPipeServerStream("MoreVoiceLinesPlayer", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                Log("Audio player pipe server started");

                try
                {
                    await pipeServer.WaitForConnectionAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                } 
                Log("Game-side connected");
            }

            // Connect to game-side to notify about stuff, like audio finished
            {
                gamePipeClient = new(".", "MoreVoiceLines", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
                Log($"Trying to connect to game-side client pipe...");
                var stopwatch = Stopwatch.StartNew();
                while (!gamePipeClient.IsConnected)
                {
                    try
                    {
                        await Task.Delay(100, cancellationToken);
                        await gamePipeClient.ConnectAsync(100, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Failed to connect to game-side client pipe ({stopwatch.ElapsedMilliseconds}ms)");
                        if (stopwatch.ElapsedMilliseconds > 5000)
                        {
                            LogError($"Failed to connect game-side client pipe, ignoring");
                            LogException(ex);
                            break;
                        }
                    }
                }
                if (gamePipeClient.IsConnected)
                {
                    Log($"Game-side client pipe connected");
                }
            }

            // Handle incoming messages
            while (pipeServer.IsConnected)
            {
                await HandleMessage(pipeServer, pipeServer, cancellationToken);
            }

            pipeServer.Close();
            Log("Pipe server closed");

            gamePipeClient.Close();
            Log("Game-side client pipe closed");

            audioPlaybackEngine.Dispose();
        }

        static async Task HandleMessage(Stream input, Stream output, CancellationToken cancellationToken)
        {
            using var message = new MessageReadable();
            await message.ReceiveAsync(input, cancellationToken);
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
                    audioCancellationTokenSource.Cancel();
                    audioCancellationTokenSource = new CancellationTokenSource();
                    {
                        string uuid = message.ReadString();
                        Gender gender = (Gender)message.ReadInt32();
                        bool isKingdom = message.ReadBoolean();
                        _ = PlayRecipe(uuid, gender, isKingdom, audioCancellationTokenSource.Token);
                    }
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
                    finally
                    {
                        audioPlaybackEngine.RemoveMixerInput(thing);
                    }
                }

                if (gamePipeClient != null && gamePipeClient.IsConnected)
                {
                    LogDebug($"Sending FinishedAudio notification");
                    new MessageWriteable(MessageType.FinishedAudio, 0).TrySend(gamePipeClient);
                }
            }
            catch (TaskCanceledException)
            {
                LogDebug($"Cancelled");
            }
            catch (Exception ex)
            {
                LogError("Error playing audio");
                LogException(ex);
            }
        }

        /// <summary>
        /// Gender enum, should be exactly like `Kingmaker.Blueprints.Gender`.
        /// </summary>
        internal enum Gender
        {
            Male,
            Female
        }

        static async Task PlayRecipe(string uuid, Gender rulerGender, bool isKingdom, CancellationToken cancellationToken)
        {
            try
            {
                LogDebug($"PlayRecipe uuid='{uuid}', rulerGender={rulerGender}, isKingdom={isKingdom}");

                if (!localizedStringUuidToRecipe.TryGetValue(uuid, out string? recipe))
                {
                    throw new Exception("Recipe not found for given localized string UUID");
                }

                foreach (var recipePart in recipe.Split('+'))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                    
                    string partSpecifier;
                    switch (recipePart[0])
                    {
                        case 'd': /* delay */
                            var milliseconds = int.Parse(recipePart[1..]);
                            LogDebug($"Playing recipe: Waiting for {milliseconds}ms");
                            await Task.Delay(milliseconds, cancellationToken);
                            continue;
                        case 'p': /* part */
                            partSpecifier = recipePart[1..];
                            break;
                        case 's': /* female/male */
                            partSpecifier = recipePart[1..] + '_' + (rulerGender == Gender.Male ? 'm' : 'f');
                            break;
                        case 'e': /* barony/kingdom */
                            partSpecifier = recipePart[1..] + '_' + (isKingdom ? 'k' : 'b');
                            break;
                        case 'x': /* female/male + barony/kingdom */
                            partSpecifier = recipePart[1..] + '_' + (rulerGender == Gender.Male ? 'm' : 'f') + (isKingdom ? 'k' : 'b');
                            break;
                        case 'n': /* name */
                        case 'r': /* race */
                        case 'k': /* kingdom name */
                            // Not implemented yet, recipe should not use those.
                            // The prepared audio files can have the variables baked in the audio anyway.
                            LogWarning($"Playing recipe: Part '{recipePart[0]}' not implemented, skipping");
                            continue; // to next part
                        default:
                            LogWarning($"Playing recipe: Unknown part '{recipePart[0]}', skipping");
                            continue; // to next part
                    }

                    var fileName = uuid + '_' + partSpecifier + ".wav";
                    var path = Path.Combine(GetDirectory(), "../audio", fileName);
                    await PlayAudio(path, cancellationToken);
                }

                new MessageWriteable(MessageType.FinishedRecipe, 0).TrySend(gamePipeClient);
            }
            catch (TaskCanceledException)
            {
                LogDebug($"Cancelled");

            }
            catch (Exception ex)
            {
                LogError("Error playing recipe");
                LogException(ex);
            }
        }
    }
}