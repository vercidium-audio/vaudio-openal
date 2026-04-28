using OpenAL;
using OpenAL.managed;
using System;

namespace vaudio_openal;

internal class OpenALSystem
{
    ALDevice device;
    ALContext context;
    ALReverbEffect reverbEffect;

    internal OpenALSystem()
    {        
        // Get the device list from OpenAL        
        var outputDevices = AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER);

        if (outputDevices.Count == 0)
        {
            Console.Error.WriteLine("No output audio devices found. Shutting down");
            Environment.Exit(1);
        }

        device = new(outputDevices[0]);

        context = new(device, new()
        {
            HRTFEnabled = true,
            SampleRate = 48000,
        });

        reverbEffect = new();

        // Set speed of sound (343 meters per second)
        AL.Listenerf(AL.AL_GAIN, 1.0f);
        AL.Listenerf(AL.AL_METERS_PER_UNIT, 1.0f);
        AL.SpeedOfSound(343.0f);
        AL.DistanceModel(AL.AL_INVERSE_DISTANCE);
    }

    uint alBufferID;

    internal void LoadSoundData(string path)
    {
        using var stm = System.IO.File.OpenRead(path);
        using (var vorbis = new NVorbis.VorbisReader(stm))
        {
            // Get the channels & sample rate
            var channels = vorbis.Channels;
            var sampleRate = vorbis.SampleRate;
            int bitDepth = 16;

            long totalSamplesLong = vorbis.TotalSamples * vorbis.Channels;

            // Overflow check: sound is too long to load
            if (totalSamplesLong > int.MaxValue)
                return;

            // Convert OGG to normal samples
            var totalSamples = (int)totalSamplesLong;
            float[] readBuffer = new float[totalSamples];

            vorbis.ReadSamples(readBuffer, 0, totalSamples);

            // Convert floats to shorts
            var shortBuffer = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
                shortBuffer[i] = (short)(readBuffer[i] * short.MaxValue);


            // Return all data about this sound
            var format = AL.GetSoundFormat(channels, bitDepth);
            var duration = (int)vorbis.TotalTime.TotalMilliseconds;

            var data = new SoundBufferData()
            {
                format = format,
                shortData = shortBuffer,
                byteCount = totalSamples * 2,
                sampleRate = sampleRate,
                duration = duration,
            };

            alBufferID = AL.GenBuffer();

            if (data.byteData != null)
                AL.BufferData(alBufferID, data.format, data.byteData, data.byteCount, data.sampleRate);
            else
                AL.BufferData(alBufferID, data.format, data.shortData, data.byteCount, data.sampleRate);
        }
    }

    internal OpenALSource CreateSound(vaudio.Vector3F position)
    {
        var sourceID = AL.GenSource();

        // Out of memory
        if (sourceID == 0)
            return null;

        var source = new ALSource(sourceID);

        source.SetPosition([position.X, position.Y, position.Z]);
        source.SetBuffer(alBufferID);
        source.SetRelative(false);
        source.SetSpatialise(true);
        source.SetMaxDistance(1000);
        source.SetLooping(true);

        return new(source, reverbEffect);
    }

    internal void SetListenerPosition(vaudio.Vector3F position, float pitch, float yaw)
    {
        // The up vector MUST be perpendicular to the look direction, else spatialisation is distorted
        var cameraPitch = pitch;
        var cameraYaw = yaw;

        var listenerOrientation = vaudio.Vector3F.FromPitchYaw(cameraPitch, cameraYaw);
        var listenerUp = vaudio.Vector3F.FromPitchYaw(cameraPitch + MathF.PI / 2, cameraYaw);

        AL.Listenerfv(AL.AL_POSITION, [position.X, position.Y, position.Z]);
        AL.Listenerfv(AL.AL_VELOCITY, [0, 0, 0]);
        AL.Listenerfv(AL.AL_ORIENTATION, [listenerOrientation.X, listenerOrientation.Y, listenerOrientation.Z, listenerUp.X, listenerUp.Y, listenerUp.Z]);
    }

    internal void UpdateReverb(vaudio.EAXReverbResults eax)
    {
        reverbEffect.reflectionsDelay = eax.ReflectionsDelay;
        reverbEffect.density = 0.5f;// Cannot change density at runtime without crackling
        reverbEffect.diffusion = eax.Diffusion;
        reverbEffect.gainLF = eax.GainLF;
        reverbEffect.gainHF = eax.GainHF;
        reverbEffect.gain = eax.Gain;
        reverbEffect.decayTime = eax.DecayTime;
        reverbEffect.decayLFRatio = eax.DecayLFRatio;
        reverbEffect.decayHFRatio = eax.DecayHFRatio;
        reverbEffect.reflectionsGain = eax.ReflectionsGain;
        reverbEffect.lateReverbGain = eax.LateReverbGain;
        reverbEffect.lateReverbDelay = eax.LateReverbDelay;
        reverbEffect.echoTime = eax.EchoTime;
        reverbEffect.echoDepth = eax.EchoDepth;
        reverbEffect.modulationTime = eax.ModulationTime;
        reverbEffect.modulationDepth = eax.ModulationDepth;
        reverbEffect.airAbsorptionGainHF = eax.AirAbsorptionGainHF;
        reverbEffect.hfReference = eax.HFReference;
        reverbEffect.lfReference = eax.LFReference;
        reverbEffect.roomRolloffFactor = eax.RoomRolloffFactor;
        reverbEffect.decayHFLimit = eax.DecayHFLimit;
        reverbEffect.dirty = true;
        reverbEffect.Update();
    }

    internal void Update()
    {
        context.Process();
    }

    class SoundBufferData
    {
        public int format;
        public byte[] byteData;
        public short[] shortData;
        public int byteCount;
        public int sampleRate;
        public int duration;
    }
}
