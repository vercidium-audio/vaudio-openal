namespace vaudio_openal;

internal class OpenALSource
{
    OpenAL.managed.ALSource source;
    OpenAL.managed.ALFilter filter;
    OpenAL.managed.ALReverbEffect reverbEffect;

    internal OpenALSource(OpenAL.managed.ALSource source, OpenAL.managed.ALReverbEffect reverbEffect)
    {
        this.source = source;
        this.reverbEffect = reverbEffect;

        filter = new(1, 1);
    }

    internal void Play() => source.Play();

    internal void UpdateFilter(vaudio.LowPassFilter vFilter)
    {
        filter.SetGain(vFilter.GainLF, vFilter.GainHF);
        source.SetFilter(reverbEffect, filter, filter);
    }
}
