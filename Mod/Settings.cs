using System;
using UnityModManagerNet;

namespace MoreVoiceLines
{
    [Serializable]
    public class Settings : UnityModManager.ModSettings
    {
        public float Volume = 0.5f;
        public float SpeedRatio = 1f;
        public float Pitch = 1f;

        /// <summary>
        /// If true, extra debugging logging is done, and audio player process console window is shown.
        /// </summary>
        public bool Debug = false;

        public bool ShowAudioPlayerConsoleWindow = false;
        public bool KillAllAudioPlayerProcesses = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
