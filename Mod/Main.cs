using System;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;
using HarmonyLib;
using Kingmaker.Utility;
using MoreVoiceLines.IPC;
using System.Collections.Generic;

namespace MoreVoiceLines
{
    public class MoreVoiceLines
    {
        internal static Settings Settings;
        internal static bool Enabled;
        internal static UnityModManager.ModEntry ModEntry;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            guiSelectedPathOrUUID = Path.Combine(GetDirectory(), "test", "Prologue_Jaethal_01.wav");

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            LoadAudioMetadata();
            Task.Run(ExternalAudioPlayer.Initialize);

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

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Play", 
                "Provide path to play audio from file, or UUID to play voice line recipe"), 
                GUILayout.ExpandWidth(false));
            guiSelectedPathOrUUID = GUILayout.TextField(guiSelectedPathOrUUID, GUILayout.MinWidth(200f));
            if (GUILayout.Button("Random", GUILayout.ExpandWidth(false)))
            {
                guiSelectedPathOrUUID = knownLocalizedStringUUIDs.Random();
            }
            GUI_PlayButton();
            if (GUILayout.Button("Stop", GUILayout.ExpandWidth(false)))
            {
                ExternalAudioPlayer.StopAudio();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Debug mode (might require game restart)", 
                "Does more debug logging, might produce clog after long gaming sessions.\n"), 
                GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Settings.Debug = GUILayout.Toggle(Settings.Debug, $" {Settings.Debug}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart audio player", GUILayout.ExpandWidth(false)))
            {
                Task.Run(ExternalAudioPlayer.Initialize);
            }
            if (GUILayout.Button("Reload audio metadata", GUILayout.ExpandWidth(false)))
            {
                LoadAudioMetadata();
            }
            GUILayout.EndHorizontal();

            if (Settings.Debug)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kill all audio player processes on (re)start", GUILayout.ExpandWidth(false));
                GUILayout.Space(10);
                Settings.KillAllAudioPlayerProcesses = GUILayout.Toggle(Settings.KillAllAudioPlayerProcesses, 
                    $" {Settings.KillAllAudioPlayerProcesses}", GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }
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
                    ExternalAudioPlayer.PlayAudio(path);
                }
                return;
            }
            
            // Try as UUID
            var uuid = guiSelectedPathOrUUID.ToLower();
            if (knownLocalizedStringUUIDs.Contains(uuid))
            {
                if (GUILayout.Button("Play", GUILayout.ExpandWidth(false)))
                {
                    ExternalAudioPlayer.PlayRecipe(uuid);
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
            ExternalAudioPlayer.SettingsUpdated();
        }

        internal static string GetDirectory()
        {
            return ModEntry.Path;
        }

        internal static void LogException(Exception ex) => ModEntry.Logger.LogException(ex);
        internal static void LogError(string message) => ModEntry.Logger.Error(message);
        internal static void LogWarning(string message) => ModEntry.Logger.Warning(message);
        internal static void Log(string message) => ModEntry.Logger.Log(message);

        internal static void LogDebug(string message)
        {
            if (Settings.Debug)
            {
                ModEntry.Logger.Log("[Debug] " + message);
            }
        }

        internal static void LogRaw(string message) => ModEntry.Logger.NativeLog(message);




        static readonly HashSet<string> knownLocalizedStringUUIDs = new();
        internal static EventHandler onEnd = null;

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
                ExternalAudioPlayer.PlayRecipe(localizedStringUUID);
                return true;
            }
            return false;
        }
    }
}
