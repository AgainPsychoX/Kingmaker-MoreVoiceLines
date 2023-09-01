using HarmonyLib;
using Kingmaker.Localization;

namespace MoreVoiceLines
{
    public static class VoiceOverStatusPatches
    {
        [HarmonyPatch(typeof(VoiceOverStatus), "Stop")]
        public static class Stop
        {
            static bool Prefix()
            {
                ExternalAudioPlayer.StopAudio();
                return true;
            }
        }
    }
}
