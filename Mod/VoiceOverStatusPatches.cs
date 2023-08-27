using HarmonyLib;
using JetBrains.Annotations;
using Kingmaker.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MoreVoiceLines
{
    public static class VoiceOverStatusPatches
    {
        [HarmonyPatch(typeof(VoiceOverStatus), "Stop")]
        public static class Stop
        {
            static bool Prefix()
            {
                MoreVoiceLines.StopAudio();
                return true;
            }
        }
    }
}
