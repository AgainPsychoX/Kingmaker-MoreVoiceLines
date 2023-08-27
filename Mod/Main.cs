using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using UnityEngine;
using UnityModManagerNet;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Utility;
using MoreVoiceLines.IPC;

namespace MoreVoiceLines
{
    public class MoreVoiceLines
    {
        public static Settings Settings;
        public static bool Enabled;
        public static UnityModManager.ModEntry ModEntry;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            guiSelectedPathOrUUID = Path.Combine(GetDirectory(), "test", "Prologue_Jaethal_01.wav");

            InitializePlayer();
            LoadAudioMetadata();

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }

        static string guiSelectedPathOrUUID = "";

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Settings.Volume = GUILayout.HorizontalSlider(Settings.Volume, 0f, 1f, GUILayout.Width(300f));
            GUILayout.Label($" {Settings.Volume:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
            //GUILayout.Space(10);
            //Settings.SpeedRatio = GUILayout.HorizontalSlider(Settings.SpeedRatio, 0f, 5f, GUILayout.Width(300f));
            //GUILayout.Label($" {Settings.SpeedRatio:p0}", GUILayout.ExpandWidth(false));
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Pitch", GUILayout.ExpandWidth(false));
            //GUILayout.Space(10);
            //Settings.Pitch = GUILayout.HorizontalSlider(Settings.Pitch, 0f, 5f, GUILayout.Width(300f));
            //GUILayout.Label($" {Settings.Pitch:p0}", GUILayout.ExpandWidth(false));
            //GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Play", "Provide path to play audio from file, or UUID to play voice line recipe"), GUILayout.ExpandWidth(false));
            guiSelectedPathOrUUID = GUILayout.TextField(guiSelectedPathOrUUID, GUILayout.MinWidth(200f));
            if (GUILayout.Button("Random", GUILayout.ExpandWidth(false)))
            {
                guiSelectedPathOrUUID = knownLocalizedStringUUIDs.Random();
            }
            GUI_PlayButton();
            if (GUILayout.Button("Stop", GUILayout.ExpandWidth(false)))
            {
                StopAudio();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Debug (might require restart)", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Settings.Debug = GUILayout.Toggle(Settings.Debug, $" {Settings.Debug}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        static void GUI_PlayButton()
        {
            // Try as path
            string path;
            try
            {
                path = Path.GetFullPath(guiSelectedPathOrUUID);
                if (!File.Exists(path)) path = null;
            }
            catch
            {
                path = null;
            }
            if (path != null) {
                if (GUILayout.Button("Play", GUILayout.ExpandWidth(false)))
                {
                    PlayAudio(path);
                }
                return;
            }
            
            // Try as UUID
            var uuid = guiSelectedPathOrUUID.ToLower();
            if (knownLocalizedStringUUIDs.Contains(uuid))
            {
                if (GUILayout.Button("Play", GUILayout.ExpandWidth(false)))
                {
                    PlayRecipe(uuid);
                }
                return;
            }

            // Or disable the button
            GUI.enabled = false;
            GUILayout.Button(new GUIContent("Play", "Invalid path or unknow UUID."), GUILayout.ExpandWidth(false));
            GUI.enabled = true;
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);

            new MessageWriteable(MessageType.SettingsUpdated).TrySend(playerPipeClient);
        }

        static string GetDirectory()
        {
            return ModEntry.Path;
        }

        static void LogException(Exception ex) => ModEntry.Logger.LogException(ex);
        static void LogError(string message) => ModEntry.Logger.Error(message);
        static void LogWarning(string message) => ModEntry.Logger.Warning(message);
        static void Log(string message) => ModEntry.Logger.Log(message);

        static void LogDebug(string message)
        {
            if (Settings.Debug)
            {
                ModEntry.Logger.Log("[Debug] " + message);
            }
        }

        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

        static Process playerProcess;
        static NamedPipeClientStream playerPipeClient;
        static NamedPipeServerStream gamePipeServer;

        static void InitializePlayer()
        {            
            playerProcess?.Kill();
            playerProcess = Process.Start(Path.Combine(GetDirectory(), "player/MoreVoiceLinesPlayer.exe"));
            Log($"Started player process ID={playerProcess.Id}");

            Task.Run(IPC);

            // TODO: listen/patch game exit to kill player process
        }

        static async Task IPC()
        {
            Log($"Connecting IPC...");

            try
            {
                // Connect to audio player, to request playing audio etc.
                playerPipeClient = new NamedPipeClientStream(".", "MoreVoiceLinesPlayer", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
                LogDebug($"Trying to connect audio player client pipe...");
                {
                    int retryAttempt = 0;
                    int maxRetries = 10;
                    while (!playerPipeClient.IsConnected)
                    {
                        try
                        {
                            await Task.Delay(100); // wait a bit to make sure player process is ready
                            //await playerPipeClient.ConnectAsync(100); // async not implemented, wtf?
                            playerPipeClient.Connect(100);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Failed to connect audio player client pipe ({++retryAttempt} / {maxRetries})");
                            if (retryAttempt > maxRetries)
                            {
                                LogError($"Failed to connect audio player client pipe");
                                LogException(ex);
                                return;
                            }
                        }
                    }
                }
                Log($"Audio player pipe connected");

                // Open the server pipe to get notified about stuff, like audio finishing
                gamePipeServer = new NamedPipeServerStream("MoreVoiceLines", PipeDirection.InOut); // Beware! Async pipes are bugged/not implemented, fucking Unity...
                Log("Game-side pipe server started");
                gamePipeServer.WaitForConnection();
                if (gamePipeServer.IsConnected)
                {
                    throw new Exception("Server pipe not connected after waiting for connection");
                }
                Log("Audio player connected");

                while (gamePipeServer.IsConnected)
                {
                    await HandleMessage(gamePipeServer, gamePipeServer);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in IPC code");
                LogException(ex);

            }
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

        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

        static readonly HashSet<string> knownLocalizedStringUUIDs = new();
        static EventHandler onEnd = null;

        static void LoadAudioMetadata()
        {
            knownLocalizedStringUUIDs.Clear();

            var path = Path.Combine(GetDirectory(), "audio_metadata.csv");
            using (var streamReader = File.OpenText(path))
            {
                var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    knownLocalizedStringUUIDs.Add(line.Substring(0, line.IndexOf('|')));
                }
            }
            Log($"Found {knownLocalizedStringUUIDs.Count} localized string UUIDs ready to be voiced over");
        }

        public static bool TryPlayVoiceOver(string localizedStringUUID, EventHandler onEndHandler)
        {
            if (knownLocalizedStringUUIDs.Contains(localizedStringUUID))
            {
                onEnd = onEndHandler;
                PlayRecipe(localizedStringUUID);
                return true;
            }
            return false;
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
    }
}
