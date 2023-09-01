using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MoreVoiceLines
{
    [XmlRoot(ElementName = "Settings")]
    public class PlayerSettings
    {
        public float Volume = 0.5f;
        public float SpeedRatio = 1f;
        public float Pitch = 1f;

        public static PlayerSettings Load()
        {
            using FileStream stream = File.OpenRead(Path.Combine(PlayerProgram.GetDirectory(), "../Settings.xml"));
            var maybeLoaded = new XmlSerializer(typeof(PlayerSettings)).Deserialize(stream) as PlayerSettings;
            return maybeLoaded ?? throw new Exception("Failed to serialize");
        }
    }
}
