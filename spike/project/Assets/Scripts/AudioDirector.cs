using System;
using System.Globalization;
using UnityEngine;

/// <summary>Runtime-only synthesized soundscape. No assets or Unity random state are used.</summary>
[DisallowMultipleComponent]
public sealed class AudioDirector : MonoBehaviour
{
    const int SampleRate = 44100;
    AudioClip bell, wind, step;
    AudioSource bellSource, windSource, stepSource;
    AudioListener ownedListener;
    float bellRms, windRms, stepRms;
    float windScale = 1;
    public float MasterVolume { get; private set; } = .8f;
    public bool MasterVolumeApplied => Mathf.Approximately(bellSource.volume, MasterVolume) &&
        Mathf.Approximately(stepSource.volume, .25f * MasterVolume) &&
        Mathf.Approximately(windSource.volume, Mathf.Clamp(.18f * windScale, .02f, .32f) * MasterVolume);
    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        if (bellSource != null) bellSource.volume = MasterVolume;
        if (stepSource != null) stepSource.volume = .25f * MasterVolume;
        SetWindScale(windScale);
    }
    public float BellPlayedAtDistance { get; private set; } = -1;
    public int BellCount { get; private set; }
    public int Footsteps { get; private set; }
    public bool ClipsPass => bellRms >= .02f && windRms >= .01f && stepRms >= .01f;
    public bool RevealPass => ClipsPass && BellCount == 1 && BellPlayedAtDistance >= 44 && BellPlayedAtDistance <= 46;

    void Awake()
    {
        bell = MakeBell();
        wind = MakeNoise(true);
        step = MakeNoise(false);
        bellSource = Source(bell, 1, false);
        windSource = Source(wind, .18f, true);
        stepSource = Source(step, .25f, false);
        if (FindFirstObjectByType<AudioListener>() == null)
            ownedListener = gameObject.AddComponent<AudioListener>();
        SetMasterVolume(MasterVolume);
        windSource.Play();
    }

    AudioSource Source(AudioClip clip, float volume, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.dopplerLevel = 0;
        source.volume = volume;
        source.loop = loop;
        source.clip = clip;
        return source;
    }

    /// <summary>风声随与教堂的距离缩放（1.0 = 默认 0.18 音量）。</summary>
    public void SetWindScale(float scale)
    {
        windScale = scale;
        if (windSource != null) windSource.volume = Mathf.Clamp(.18f * scale, .02f, .32f) * MasterVolume;
    }

    AudioClip MakeBell()
    {
        var samples = new float[SampleRate * 4];
        double[] ratios = { 1, 2.02, 2.99, 4.21, 5.43 };
        double[] amplitudes = { 1, .55, .38, .22, .12 };
        for (int n = 0; n < samples.Length; n++)
        {
            double t = n / (double)SampleRate;
            double value = 0;
            for (int i = 0; i < ratios.Length; i++)
                value += amplitudes[i] * Math.Sin(2 * Math.PI * 196 * ratios[i] * t) * Math.Exp(-t * (1.1 + .35 * i));
            double envelope = Math.Min(1, t / .008) * Math.Min(1, (samples.Length - 1 - n) / (SampleRate * .2));
            samples[n] = (float)(value * .4 * envelope); // Peak bounded by .4 * sum(amplitudes) = .908.
        }
        return Clip("Synth Bell 196Hz", samples, out bellRms);
    }

    AudioClip MakeNoise(bool looping)
    {
        int count = looping ? SampleRate * 8 : (int)(SampleRate * .12f);
        var samples = new float[count];
        var random = new System.Random(looping ? 1701 : 1702);
        float alpha = looping ? .06f : .25f;
        for (int n = 0; n < count; n++) samples[n] = (float)(random.NextDouble() * 2 - 1);
        float filtered = 0;
        if (looping)
        {
            // Warm up one full period. Its residual (.94^352800) is negligible,
            // so the following pass is the periodic low-pass solution at the seam.
            for (int n = 0; n < count; n++) filtered += alpha * (samples[n] - filtered);
        }
        for (int n = 0; n < count; n++)
        {
            filtered += alpha * (samples[n] - filtered);
            double t = n / (double)SampleRate;
            double envelope = looping ? 1 + .35 * Math.Sin(2 * Math.PI * .125 * t)
                : Math.Min(1, t / .003) * Math.Exp(-t * 32) * Math.Min(1, (count - 1 - n) / (SampleRate * .02));
            samples[n] = filtered * (float)envelope;
        }
        if (looping) return Clip("Synth Snow Wind", samples, out windRms);
        return Clip("Synth Footstep", samples, out stepRms);
    }

    static AudioClip Clip(string name, float[] samples, out float rms)
    {
        double sum = 0;
        foreach (float value in samples) sum += value * value;
        rms = (float)Math.Sqrt(sum / samples.Length);
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Called directly inside MistReveal's pulse branch, never by a second distance poll.
    public void PlayBell(float distance)
    {
        if (BellCount != 0) return;
        BellCount++;
        BellPlayedAtDistance = distance;
        bellSource.Play();
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[AUDIO_BELL] count={0} distance={1:F3} frame={2}", BellCount, distance, Time.frameCount));
    }

    public void PlayFootstep()
    {
        stepSource.PlayOneShot(step);
        Footsteps++;
    }

    void OnApplicationQuit()
    {
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[AUDIO] bell_len={0:F2} bell_rms={1:F6} wind_len={2:F2} wind_rms={3:F6} step_len={4:F2} step_rms={5:F6} bell_played_at_d={6:F3} footsteps={7}",
            bell.length, bellRms, wind.length, windRms, step.length, stepRms, BellPlayedAtDistance, Footsteps));
    }

    void OnDestroy()
    {
        if (bellSource != null) Destroy(bellSource);
        if (windSource != null) Destroy(windSource);
        if (stepSource != null) Destroy(stepSource);
        if (ownedListener != null) Destroy(ownedListener);
        if (bell != null) Destroy(bell);
        if (wind != null) Destroy(wind);
        if (step != null) Destroy(step);
    }
}
