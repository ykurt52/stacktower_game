using UnityEngine;

/// <summary>
/// Singleton that plays procedurally generated game audio.
/// No external audio files needed — all sounds are synthesized at runtime.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource musicSource;

    // Procedural clips
    private AudioClip jumpClip;
    private AudioClip landClip;
    private AudioClip coinClip;
    private AudioClip hitClip;
    private AudioClip deathClip;
    private AudioClip perfectClip;
    private AudioClip placeClip;
    private AudioClip gameOverClip;
    private AudioClip buttonClip;
    private AudioClip swordClip;
    private AudioClip gunClip;
    private AudioClip powerupClip;
    private AudioClip reviveClip;
    private AudioClip stoneClip;
    private AudioClip comboClip;

    private float musicVolume = 0.25f;
    private float sfxVolume = 0.6f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.volume = sfxVolume;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.volume = musicVolume;
        musicSource.loop = true;

        GenerateAllClips();
    }

    private void GenerateAllClips()
    {
        jumpClip = GenerateTone(0.08f, 500f, 800f, WaveType.Square, 0.3f);
        landClip = GenerateTone(0.06f, 200f, 150f, WaveType.Noise, 0.25f);
        coinClip = GenerateCoinSound();
        hitClip = GenerateHitSound();
        deathClip = GenerateDeathSound();
        perfectClip = GeneratePerfectSound();
        placeClip = GenerateTone(0.05f, 300f, 250f, WaveType.Square, 0.2f);
        gameOverClip = GenerateGameOverSound();
        buttonClip = GenerateTone(0.04f, 600f, 500f, WaveType.Sine, 0.2f);
        swordClip = GenerateSwordSound();
        gunClip = GenerateGunSound();
        powerupClip = GeneratePowerupSound();
        reviveClip = GenerateReviveSound();
        stoneClip = GenerateStoneSound();
        comboClip = GenerateTone(0.1f, 400f, 900f, WaveType.Square, 0.25f);
    }

    // ── Public API ──

    public void PlayJump() => Play(jumpClip);
    public void PlayLand() => Play(landClip);
    public void PlayCoin() => Play(coinClip);
    public void PlayHit() => Play(hitClip);
    public void PlayDeath() => Play(deathClip);
    public void PlayPerfect() => Play(perfectClip);
    public void PlayPlace() => Play(placeClip);
    public void PlayGameOver() => Play(gameOverClip);
    public void PlayButton() => Play(buttonClip);
    public void PlaySword() => Play(swordClip);
    public void PlayGun() => Play(gunClip);
    public void PlayPowerup() => Play(powerupClip);
    public void PlayRevive() => Play(reviveClip);
    public void PlayStone() => Play(stoneClip);
    public void PlayCombo() => Play(comboClip);
    public void PlayShoot() => Play(gunClip);
    public void PlayHurt() => Play(hitClip);
    public void PlayEnemyDeath() => Play(deathClip);
    public void PlayExplosion() => Play(deathClip);
    public void PlaySuccess() => Play(perfectClip);

    private void Play(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void StartMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.clip = GenerateBackgroundMusic();
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    // ── Sound Generators ──

    private enum WaveType { Sine, Square, Noise, Saw }

    private AudioClip GenerateTone(float duration, float freqStart, float freqEnd,
        WaveType wave, float volume)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float freq = Mathf.Lerp(freqStart, freqEnd, t);
            float phase = (float)i / sampleRate;
            float envelope = 1f - t; // Linear fade out

            float val = 0;
            switch (wave)
            {
                case WaveType.Sine:
                    val = Mathf.Sin(2f * Mathf.PI * freq * phase);
                    break;
                case WaveType.Square:
                    val = Mathf.Sin(2f * Mathf.PI * freq * phase) > 0 ? 1f : -1f;
                    break;
                case WaveType.Noise:
                    val = Random.Range(-1f, 1f);
                    break;
                case WaveType.Saw:
                    val = 2f * (freq * phase % 1f) - 1f;
                    break;
            }
            samples[i] = val * envelope * volume;
        }

        var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateCoinSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.15f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            // Two quick ascending notes
            float freq = t < 0.5f ? 1200f : 1600f;
            float envelope = (1f - t) * (t < 0.5f ? 1f : 0.8f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * phase) * envelope * 0.3f;
        }

        var clip = AudioClip.Create("Coin", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateHitSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.12f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            float envelope = Mathf.Pow(1f - t, 2f);
            // Low thud + noise
            float thud = Mathf.Sin(2f * Mathf.PI * 120f * phase) * 0.5f;
            float noise = Random.Range(-1f, 1f) * 0.3f * (1f - t * 2f);
            if (noise < -0.3f) noise = -0.3f;
            samples[i] = (thud + noise) * envelope * 0.4f;
        }

        var clip = AudioClip.Create("Hit", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateDeathSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.4f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            float freq = Mathf.Lerp(400f, 80f, t); // Descending pitch
            float envelope = Mathf.Pow(1f - t, 1.5f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * phase) * envelope * 0.35f;
        }

        var clip = AudioClip.Create("Death", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePerfectSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.2f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            // Three ascending notes
            float freq = t < 0.33f ? 800f : (t < 0.66f ? 1000f : 1300f);
            float envelope = 1f - t;
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * phase) * envelope * 0.3f;
        }

        var clip = AudioClip.Create("Perfect", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateGameOverSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.6f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            // Three descending notes
            float freq;
            if (t < 0.25f) freq = 500f;
            else if (t < 0.5f) freq = 400f;
            else freq = 250f;
            float envelope = Mathf.Pow(1f - t, 1.2f);
            float val = Mathf.Sin(2f * Mathf.PI * freq * phase);
            // Add slight square wave character
            val = val > 0 ? Mathf.Min(val * 2f, 1f) : Mathf.Max(val * 2f, -1f);
            samples[i] = val * envelope * 0.3f;
        }

        var clip = AudioClip.Create("GameOver", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateSwordSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.1f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = t < 0.1f ? t * 10f : Mathf.Pow(1f - t, 3f);
            // Swoosh: filtered noise with high freq sweep
            float noise = Random.Range(-1f, 1f);
            float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(2000f, 800f, t) * (float)i / sampleRate);
            samples[i] = (noise * 0.3f + sweep * 0.2f) * envelope * 0.4f;
        }

        var clip = AudioClip.Create("Sword", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateGunSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.08f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = Mathf.Pow(1f - t, 4f); // Very sharp attack
            float noise = Random.Range(-1f, 1f);
            float pop = Mathf.Sin(2f * Mathf.PI * 150f * (float)i / sampleRate);
            samples[i] = (noise * 0.5f + pop * 0.5f) * envelope * 0.35f;
        }

        var clip = AudioClip.Create("Gun", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GeneratePowerupSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.3f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            float freq = Mathf.Lerp(400f, 1200f, t); // Ascending sparkle
            float envelope = Mathf.Sin(t * Mathf.PI); // Bell curve
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * phase) * envelope * 0.3f;
        }

        var clip = AudioClip.Create("Powerup", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateReviveSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.4f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            // Ascending sweep with harmonics
            float freq = Mathf.Lerp(300f, 1000f, t * t);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float val = Mathf.Sin(2f * Mathf.PI * freq * phase) * 0.6f;
            val += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * phase) * 0.3f; // Harmony
            samples[i] = val * envelope * 0.3f;
        }

        var clip = AudioClip.Create("Revive", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateStoneSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.15f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float phase = (float)i / sampleRate;
            // Crystalline chime
            float freq = t < 0.4f ? 1500f : 2000f;
            float envelope = (1f - t);
            float val = Mathf.Sin(2f * Mathf.PI * freq * phase) * 0.5f;
            val += Mathf.Sin(2f * Mathf.PI * freq * 2f * phase) * 0.2f; // Overtone
            samples[i] = val * envelope * 0.3f;
        }

        var clip = AudioClip.Create("Stone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateBackgroundMusic()
    {
        int sampleRate = 44100;
        float duration = 8f; // 8 second loop
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Simple chiptune-style loop
        float[] melody = { 262f, 330f, 392f, 330f, 349f, 392f, 440f, 392f }; // C E G E F G A G
        float noteLen = duration / melody.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int noteIndex = (int)(t / noteLen) % melody.Length;
            float noteT = (t % noteLen) / noteLen;
            float freq = melody[noteIndex];

            // Melody: soft square
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) > 0 ? 0.15f : -0.15f;
            // Bass: octave down sine
            val += Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.1f;
            // Fade each note slightly
            val *= (1f - noteT * 0.3f);

            samples[i] = val * 0.4f;
        }

        var clip = AudioClip.Create("BGM", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
