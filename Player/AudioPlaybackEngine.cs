using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.DirectoryServices;

namespace MoreVoiceLines
{
    /// <summary>
    /// Adapted from http://mark-dot-net.blogspot.com/2014/02/fire-and-forget-audio-playback-with.html
    /// Useful info https://markheath.net/post/naudio-audio-output-devices
    /// </summary>
    class AudioPlaybackEngine : IDisposable
    {
        private readonly IWavePlayer outputDevice;
        private readonly MixingSampleProvider mixer;

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
            outputDevice = new WaveOut();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
            mixer.ReadFully = true;
            outputDevice.Init(mixer);
            outputDevice.Play();
        }

        public ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }
            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        public ISampleProvider AddMixerInput(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                // Perfect, do nothing
            }
            else if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                input = new MonoToStereoSampleProvider(input);
            }
            else if (input.WaveFormat.Channels == 2 && mixer.WaveFormat.Channels == 1)
            {
                input = new StereoToMonoSampleProvider(input);
            }
            else
            {
                throw new NotImplementedException("Channel count not supported");
            }

            if (input.WaveFormat.SampleRate != mixer.WaveFormat.SampleRate)
            {
                var resampler = new AutoDisposeWaveProvider(new MediaFoundationResampler(input.ToWaveProvider(), mixer.WaveFormat));
                input = resampler.ToSampleProvider();
            }


            outputDevice.Pause();
            var thing = ConvertToRightChannelCount(input);
            mixer.AddMixerInput(thing);
            outputDevice.Play();
            return thing; // return to allow removal in case it was adapted
        }

        public void RemoveMixerInput(ISampleProvider input)
        {
            mixer.RemoveMixerInput(input);
        }

        public void Dispose()
        {
            outputDevice.Dispose();
        }
    }
}
