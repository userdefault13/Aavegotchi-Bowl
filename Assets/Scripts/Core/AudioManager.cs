using UnityEngine;

namespace RetroBowl.Core
{
    /// <summary>
    /// Match SFX. If clips are not assigned in the Inspector, procedural tones are generated
    /// at runtime (placeholders — not Retro Bowl rips).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Sound Effects (optional overrides)")]
        public AudioClip whistleSound;
        public AudioClip tackleSound;
        public AudioClip catchSound;
        public AudioClip throwSound;
        public AudioClip tipSound;
        public AudioClip incompleteSound;
        public AudioClip kickSound;
        public AudioClip scoreSound;
        public AudioClip crowdCheer;
        public AudioClip crowdBoo;

        [Header("Settings")]
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;

        bool proceduralReady;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("AudioManager");
            if (host.GetComponent<AudioManager>() == null)
                host.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (GameManager.Instance == null || gameObject != GameManager.Instance.gameObject)
                    DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            EnsureSources();
            EnsureProceduralClips();
            UpdateVolume();
        }

        void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }
        }

        void EnsureProceduralClips()
        {
            if (proceduralReady) return;
            proceduralReady = true;

            if (whistleSound == null) whistleSound = MakeTone("Whistle", 1400f, 0.12f, 0.35f, fadeOut: true);
            if (tackleSound == null) tackleSound = MakeNoiseBurst("Tackle", 0.09f, 0.55f);
            if (catchSound == null) catchSound = MakeTone("Catch", 520f, 0.07f, 0.4f, fadeOut: true);
            if (throwSound == null) throwSound = MakeTone("Throw", 280f, 0.08f, 0.3f, sweepTo: 180f);
            if (tipSound == null) tipSound = MakeTone("Tip", 880f, 0.05f, 0.35f, fadeOut: true);
            if (incompleteSound == null) incompleteSound = MakeTone("Incomplete", 220f, 0.14f, 0.28f, sweepTo: 140f);
            if (kickSound == null) kickSound = MakeTone("Kick", 160f, 0.1f, 0.45f, sweepTo: 90f);
            if (scoreSound == null) scoreSound = MakeChord("Score", new[] { 523f, 659f, 784f }, 0.35f, 0.4f);
            if (crowdCheer == null) crowdCheer = MakeNoiseBurst("Cheer", 0.45f, 0.25f, highPass: false);
            if (crowdBoo == null) crowdBoo = MakeTone("Boo", 110f, 0.35f, 0.3f, sweepTo: 80f);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null) return;
            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null)
                musicSource.Stop();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayWhistle() => PlaySFX(whistleSound);
        public void PlayTackle() => PlaySFX(tackleSound);
        public void PlayCatch() => PlaySFX(catchSound);
        public void PlayThrow() => PlaySFX(throwSound);
        public void PlayTip() => PlaySFX(tipSound);
        public void PlayIncomplete() => PlaySFX(incompleteSound);
        public void PlayKick() => PlaySFX(kickSound);
        public void PlayScore() => PlaySFX(scoreSound);
        public void PlayCrowdCheer() => PlaySFX(crowdCheer);
        public void PlayCrowdBoo() => PlaySFX(crowdBoo);

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
                musicSource.volume = musicVolume;
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null)
                sfxSource.volume = sfxVolume;
        }

        void UpdateVolume()
        {
            if (musicSource != null) musicSource.volume = musicVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        // ── Procedural clip helpers (placeholder SFX) ────────────────────

        static AudioClip MakeTone(
            string name,
            float freq,
            float duration,
            float volume,
            float sweepTo = -1f,
            bool fadeOut = false)
        {
            int sampleRate = 22050;
            int samples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            var data = new float[samples];
            float end = sweepTo > 0f ? sweepTo : freq;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float f = Mathf.Lerp(freq, end, t);
                float env = fadeOut ? (1f - t) : Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * (i / (float)sampleRate)) * volume * env;
            }
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeChord(string name, float[] freqs, float duration, float volume)
        {
            int sampleRate = 22050;
            int samples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Sin(t * Mathf.PI);
                float s = 0f;
                for (int f = 0; f < freqs.Length; f++)
                    s += Mathf.Sin(2f * Mathf.PI * freqs[f] * (i / (float)sampleRate));
                data[i] = (s / freqs.Length) * volume * env;
            }
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoiseBurst(string name, float duration, float volume, bool highPass = true)
        {
            int sampleRate = 22050;
            int samples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            var data = new float[samples];
            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Sin(t * Mathf.PI);
                float n = Random.Range(-1f, 1f);
                if (highPass)
                {
                    float hp = n - prev;
                    prev = n;
                    n = hp;
                }
                data[i] = n * volume * env;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
