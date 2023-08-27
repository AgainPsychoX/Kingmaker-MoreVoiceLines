using HarmonyLib;
using JetBrains.Annotations;
using Kingmaker.Localization;
using System;
using UnityEngine;

namespace MoreVoiceLines
{
    public static class LocalizedStringPatches
    {
        [HarmonyPatch(typeof(LocalizedString), "PlayVoiceOver")]
        public static class PlayVoiceOver
        {
            static bool Prefix(ref LocalizedString __instance, ref VoiceOverStatus __result, [CanBeNull] MonoBehaviour target = null)
            {
                VoiceOverStatus voiceOverStatus = new();
                var onEnd = new EventHandler((sender, args) =>
                {
                    // Flag VoiceOverStatus as ended (indirectly to avoid reflection) 
                    voiceOverStatus.HandleCallback(null, AkCallbackType.AK_EndOfEvent, null); 
                });
                if (MoreVoiceLines.TryPlayVoiceOver(__instance.Key, onEnd))
                {
                    __result = voiceOverStatus;
                    return false; // skip original
                }
                return true; // continue with original
            }
        }
    }
}
