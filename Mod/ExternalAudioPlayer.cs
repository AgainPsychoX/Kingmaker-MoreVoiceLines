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
            STARTING,
            CONNECTING,
            RUNNING,
            STOPPING,
            STOPPED,
            CRASHED,
        }

        static CancellationTokenSource cancellationTokenSource = new();
        static Process playerProcess;
        static NamedPipeClientStream playerPipeClient;
        static NamedPipeServerStream gamePipeServer;
        static volatile AudioPlayerState state = AudioPlayerState.STOPPED;

        public static AudioPlayerState State
        {
            get => state;
        }

        /// <summary>
        /// Intializes the player (or re-initializes if already running).
        /// </summary>
        public static async Task Initialize()
        {
            await Stop();

            if (Settings.KillAllAudioPlayerProcesses)
            {
                await KillAllProcesses();
            }

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

            await IPC();
        }

        /// <summary>
        /// Signals to stop the external audio player.
        /// </summary>
        public static async Task Stop()
        {
            if (state is not AudioPlayerState.STOPPED or AudioPlayerState.CRASHED)
            {
                LogDebug("Stopping IPC");
                cancellationTokenSource.Cancel();

                // Wait for peaceful stop
                var stopwatch = Stopwatch.StartNew();
                await Task.Delay(250);
                do
                {
                    await Task.Delay(500);
                    if (stopwatch.ElapsedMilliseconds > 10000)
                    {
                        LogError("IPC task not responding to cancel signal");
                        return;
                    }
                }
                while (state is not AudioPlayerState.STOPPED and not AudioPlayerState.CRASHED);

                if (state is AudioPlayerState.CRASHED)
                {
                    LogWarning("Crashed while stopping"); // but stopped nevertheless
                    return;
                }

                LogDebug("Stopped IPC");
                cancellationTokenSource = new();
                return;
            }
        }

        static async Task KillAllProcesses()
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
            killerProcess.BeginOutputReadLine();
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

        static async Task IPC()
        {
            Log($"Connecting IPC...");
            state = AudioPlayerState.CONNECTING;

            bool didCrash = false;
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
                            //await playerPipeClient.ConnectAsync(100); // async not implemented, wtf?
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
                    Log($"Audio player pipe connected");
                }

                // Open the server pipe to get notified about stuff, like audio finishing
                {
                    gamePipeServer = new NamedPipeServerStream("MoreVoiceLines", PipeDirection.InOut); // Beware! Async pipes are bugged/not implemented, fucking Unity...
                    Log("Game-side pipe server started");
                    gamePipeServer.WaitForConnection(); // TODO: How to stop if stuck here? Need to kill thread, no?
                    if (!gamePipeServer.IsConnected)
                    {
                        throw new Exception("Server pipe not connected after waiting for connection");
                    }
                    Log("Audio player connected");
                }

                // Main loop, handling next messages
                state = AudioPlayerState.RUNNING;
                while (gamePipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    await HandleMessage(gamePipeServer, gamePipeServer, cancellationToken);
                }
                state = AudioPlayerState.STOPPING;

                // Jump to cancellation handler below, to avoid doubling code (yes, exception-driven logic... High level languages XD)
                throw new TaskCanceledException();
            }
            catch (TaskCanceledException)
            {
                Log("IPC cancelled");
                state = AudioPlayerState.STOPPING;
            }
            catch (Exception ex)
            {
                LogError("IPC failed");
                LogException(ex);
                didCrash = true;
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

                // Kill player process after delay if it's still up
                for (int i = 0; i < 10; i++)
                {
                    playerProcess.WaitForExit(100);
                    await Task.Yield();
                }
                if (!playerProcess.WaitForExit(1))
                {
                    LogWarning("Audio player process long alive after pipes closed, killing");
                    playerProcess.Kill();
                }
                playerProcess = null;

                state = didCrash ? AudioPlayerState.CRASHED : AudioPlayerState.STOPPED;
            }
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
                    playerPipeClient.Close();
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

        public static void PlayAudio(string path)
        {
            Log($"Playing audio from path '{path}'");

            using var message = new MessageWriteable(MessageType.PlayAudio);
            message.Write(path);
            message.TrySend(playerPipeClient);
        }

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

        internal static void SettingsUpdated()
        {
            new MessageWriteable(MessageType.SettingsUpdated).TrySend(playerPipeClient);
        }
    }
}
