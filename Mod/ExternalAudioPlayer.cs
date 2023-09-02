using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Kingmaker.Blueprints;
using Kingmaker;
using MoreVoiceLines.IPC;

using static MoreVoiceLines.MoreVoiceLines; // for easier logging

namespace MoreVoiceLines
{
    public class ExternalAudioPlayer
    {
        static Settings Settings
        {
            get => MoreVoiceLines.Settings; // easeir access
        }

        public enum AudioPlayerState
        {
            Starting,
            Connecting,
            Running,
            Stopping,
            Stopped,
        }

        static CancellationTokenSource cancellationTokenSource = new();
        static Process playerProcess;
        static NamedPipeClientStream playerPipeClient;
        static volatile NamedPipeServerStream gamePipeServer;
        static volatile AudioPlayerState state = AudioPlayerState.Stopped;
        static Thread serverStoppingThread;

        public static AudioPlayerState State
        {
            get => state;
        }

        public static int PID
        {
            get {
                try { return playerProcess?.Id ?? 0; }
                catch { return 0; }
            }
        }

        /// <summary>
        /// Task to intializes the player (or re-initializes if already running).
        /// </summary>
        public static async Task Initialize()
        {
            if (state == AudioPlayerState.Starting)
            {
                LogWarning("Already starting, cannot reinitialize in middle of initialization");
                return; 
            }
            
            // Try clean up previous state
            await Stop();
            if (Settings.KillAllAudioPlayerProcesses)
            {
                await KillAllProcesses();
            }
            if (playerProcess != null && !playerProcess.HasExited)
            {
                LogError("Failed to kill the previous audio player process, cannot reinitialize; Try restarting the game");
                return;
            }

            state = AudioPlayerState.Starting;

            // Start new audio player process
            LogDebug($"Starting the player process...");
            var playerProcessStartInfo = new ProcessStartInfo()
            {
                WorkingDirectory = Path.Combine(GetDirectory(), "player"),
                FileName = Path.Combine(GetDirectory(), "player/MoreVoiceLinesPlayer.exe"),

                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!Settings.ShowAudioPlayerConsoleWindow)
            {
                // This will still blink for a second. The `UseShellExecute` needs to be `true` for hidden start,
                // but `false` for logging output.
                playerProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                playerProcessStartInfo.CreateNoWindow = true;
            }
            playerProcess = new Process()
            {
                StartInfo = playerProcessStartInfo,
            };
            if (Settings.Debug)
            {
                playerProcess.OutputDataReceived += (sender, args) => LogRaw("[AudioPlayer] " + args.Data);
                playerProcess.ErrorDataReceived += (sender, args) => LogRaw("[AudioPlayer (stderr)] " + args.Data);
            }
            playerProcess.Start();
            if (Settings.Debug)
            {
                playerProcess.BeginOutputReadLine();
            }
            LogDebug($"Started the player process ID={playerProcess.Id}");

            _ = Task.Run(IPC);
        }

        /// <summary>
        /// Signals to stop the audio player.
        /// </summary>
        public static async Task Stop()
        {
            if (state != AudioPlayerState.Stopped)
            {
                LogDebug("Stopping IPC");
                cancellationTokenSource.Cancel();

                // Try inform the audio player process
                new MessageWriteable(MessageType.Exit).TrySend(playerPipeClient);

                // Wait for peaceful stop
                var stopwatch = Stopwatch.StartNew();
                do
                {
                    await Task.Delay(500);
                    if (stopwatch.ElapsedMilliseconds > 5000)
                    {
                        LogError("IPC task not responding to cancel signal");
                        return;
                    }
                }
                while (state != AudioPlayerState.Stopped);

                LogDebug("Stopped IPC");
                cancellationTokenSource = new();
                return;
            }
        }

        /// <summary>
        /// Uses Windows built-in `taskkill.exe` to kill all `MoreVoiceLinesPlayer.exe` that might be hanging 
        /// in the background, including ones from other game instances (if you run multiple instances somehow).
        /// </summary>
        public static async Task KillAllProcesses()
        {
            Log($"Killing existing player process(es)...");

            // Start the task killing
            var killerProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "taskkill.exe", // safe built-in Windows executable
                    Arguments = "/f /im MoreVoiceLinesPlayer.exe",

                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,

                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            killerProcess.Start();

            // Output to game logs
            while (!killerProcess.StandardOutput.EndOfStream)
            {
                LogDebug("[taskkill.exe] " + killerProcess.StandardOutput.ReadLine());
            }

            // Wait for exit without blocking too much
            do
            {
                await Task.Yield();
            }
            while (!killerProcess.WaitForExit(100));
        }

        /// <summary>
        /// Task handling Inter-Process-Communications between the game (the mod) and the external audio player 
        /// (used by the mod). Two bidirectional named pipes are set up - one each way - processing requests 
        /// and responding if necessary. After connecting the client pipe and setting up the server pipe, 
        /// the task keeps running handling incoming messages.
        /// </summary>
        static async Task IPC()
        {
            /* Asynchronous named pipes are not fully implemented or bugged in Unity
             * (or at least the server, or at least this project, or I don't know how to use them...)
             * 
             * `await playerPipeClient.ConnectAsync(100);` -> throws not implemented.
             * `await gamePipeServer.WaitForConnectionAsync(cancellationToken);` -> thows not implemented,
             *      or `ERROR_PIPE_LISTENING` == `0x0218`: "Waiting for a process to open the other end of the pipe".
             * `new NamedPipeServerStream(..., 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);`
             *      -> fails on non-async `WaitForConnect` as well (`ERROR_PIPE_LISTENING`).
             * 
             * Therefore, as workaround, synchronized server pipe is used with synchronized waiting for connection,
             * along with separate task that, crates fake client to stop the wait if necessary.
             * I tried using `cancellationToken.Register(gamePipeServer.Close))` approach too, but it still failed.
             * 
             * Inspiration: https://stackoverflow.com/a/61112728/4880243, https://stackoverflow.com/a/1191677/4880243
             */

            Log($"Connecting IPC...");
            state = AudioPlayerState.Connecting;

            var cancellationToken = cancellationTokenSource.Token;
            try
            {
                // Connect to audio player, to request playing audio etc.
                {
                    playerPipeClient = new NamedPipeClientStream(".", "MoreVoiceLinesPlayer", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
                    LogDebug($"Trying to connect audio player client pipe...");
                    var stopwatch = Stopwatch.StartNew();
                    while (!playerPipeClient.IsConnected)
                    {
                        try
                        {
                            await Task.Delay(100, cancellationToken);
                            playerPipeClient.Connect(100);
                        }
                        catch (TaskCanceledException)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            LogDebug($"Failed to connect audio player client pipe ({stopwatch.ElapsedMilliseconds}ms)");
                            if (stopwatch.ElapsedMilliseconds > 5000)
                            {
                                throw;
                            }
                        }
                    }
                    var serverProcessId = NamedPipesUtils.GetNamedPipeServerProcessId(playerPipeClient);
                    Log($"Audio player client pipe connected to server process (PID {serverProcessId})");
                }

                // Open the server pipe and wait for connection to get notified about stuff like audio finishing
                {
                    // Setup thread for workaround for async pipe server issue
                    LogDebug("[NoAsyncServerWorkaround] Setting up task to close the pipe server on cancellation");
                    serverStoppingThread = new Thread(() =>
                    {
                        cancellationToken.WaitHandle.WaitOne();
                        LogDebug("[NoAsyncServerWorkaround] Cancellation in effect");

                        if (gamePipeServer != null)
                        {
                            if (state == AudioPlayerState.Connecting)
                            {
                                LogDebug("[NoAsyncServerWorkaround] Connecting fake client to the pipe server");
                                using (NamedPipeClientStream fake = new("MoreVoiceLines"))
                                {
                                    try
                                    {
                                        fake.Connect(1000);
                                    }
                                    catch
                                    {
                                        LogDebug("[NoAsyncServerWorkaround] Failed to connect fake client");
                                    }
                                }
                            }

                            if (gamePipeServer.IsConnected)
                            {
                                // Note: Might still hang here, as sync read must be stopped somehow else?
                                //  Yet, with exit message to audio player process it works good enough I hope.
                                LogDebug("[NoAsyncServerWorkaround] Disconnecting");
                                gamePipeServer.Disconnect();
                                LogDebug("[NoAsyncServerWorkaround] Disconnected");
                            }
                        }
                    });
                    serverStoppingThread.Start();

                    // Actually open the pipe server
                    gamePipeServer = new NamedPipeServerStream("MoreVoiceLines", PipeDirection.InOut);
                    Log("Game-side pipe server started");
                    gamePipeServer.WaitForConnection();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!gamePipeServer.IsConnected)
                    {
                        throw new Exception("Server pipe not connected after waiting for connection");
                    }
                    var clientProcessId = NamedPipesUtils.GetNamedPipeClientProcessId(gamePipeServer);
                    Log($"Game-side server pipe got connection from client process (PID {clientProcessId})");
                }

                // Main loop, handling next messages
                state = AudioPlayerState.Running;
                while (gamePipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    await HandleMessage(gamePipeServer, gamePipeServer, cancellationToken);
                }
                state = AudioPlayerState.Stopping;

                // Jump to cancellation handler below, to avoid doubling code (yes, exception-driven logic... High level languages XD)
                throw new TaskCanceledException();
            }
            catch (TaskCanceledException)
            {
                Log("IPC cancelled");
                state = AudioPlayerState.Stopping;
            }
            catch (Exception ex)
            {
                LogError("IPC failed");
                LogException(ex);
            }
            finally
            {
                // Clean pipes
                if (playerPipeClient != null)
                {
                    LogDebug("Closing audio player client pipe");
                    playerPipeClient.Close();
                    playerPipeClient = null;
                }
                if (gamePipeServer != null)
                {
                    LogDebug("Closing game-side server pipe");
                    gamePipeServer.Close();
                    gamePipeServer = null;
                }

                // Stop server stopping thread if it's still up somehow
                serverStoppingThread.Join(100);
                serverStoppingThread.Abort();

                // Kill player process after delay if it's still up somehow
                if (!playerProcess.WaitForExit(1000))
                {
                    LogWarning("Audio player process long alive after pipes closed, killing");
                    playerProcess.Kill();
                }
                playerProcess = null;

                state = AudioPlayerState.Stopped;
            }
        }

        /// <summary>
        /// Handles messages (well, from the external audio player connected to local server pipe).
        /// </summary>
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
                    playerPipeClient.Close();
                    cancellationTokenSource.Cancel();
                    gamePipeServer.Disconnect();
                    return;
                case MessageType.FinishedAudio:
                    return;
                case MessageType.FinishedRecipe:
                    if (onEnd != null)
                    {
                        onEnd(null, null);
                        onEnd = null;
                    }
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

        /// <summary>
        /// Signals to start playing specified audio.
        /// </summary>
        /// <param name="path">Path to the audio to play</param>
        public static void PlayAudio(string path)
        {
            Log($"Playing audio from path '{path}'");

            using var message = new MessageWriteable(MessageType.PlayAudio);
            message.Write(path);
            message.TrySend(playerPipeClient);
        }

        /// <summary>
        /// Signals to start playing specified voice-line recipe.
        /// </summary>
        /// <param name="uuid">LocalizedString UUID</param>
        public static void PlayRecipe(string uuid)
        {
            Log($"Playing recipe for UUID '{uuid}'");

            var playerUnit = Game.Instance.DialogController?.ActingUnit ?? Game.Instance.Player?.MainCharacter;
            var gender = playerUnit == null
                ? (new System.Random().Next(2) == 0 ? Gender.Male : Gender.Female)
                : playerUnit.Gender;

            using var message = new MessageWriteable(MessageType.PlayRecipe);
            message.Write(uuid);
            message.Write((int)gender);
            message.Write(Game.Instance.Player.PlayerIsKing);
            message.TrySend(playerPipeClient);
        }

        /// <summary>
        /// Signals to stop playing audio (effectively also stops recipe).
        /// </summary>
        public static void StopAudio()
        {
            Log($"Stoping audio (and recipe)");

            new MessageWriteable(MessageType.StopAudio).TrySend(playerPipeClient);

            if (onEnd != null)
            {
                onEnd(null, null);
                onEnd = null;
            }
        }

        /// <summary>
        /// Singals the external audio player that settings file was updated, 
        /// which might include update to playback parameters like volume etc.
        /// </summary>
        internal static void SettingsUpdated()
        {
            new MessageWriteable(MessageType.SettingsUpdated).TrySend(playerPipeClient);
        }
    }
}
