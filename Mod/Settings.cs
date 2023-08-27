using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;

namespace MoreVoiceLines
{
    [Serializable]
    public class Settings : UnityModManager.ModSettings
    {
        public float Volume = 0.5f;
        public float SpeedRatio = 1f;
        public float Pitch = 1f;
        public bool Debug = false;
        public bool HidePlayer = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
