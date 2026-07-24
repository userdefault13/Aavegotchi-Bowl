using UnityEngine;

namespace RetroBowl.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Sound Effects")]
        public AudioClip whistleSound;
        public AudioClip tackleSound;
        public AudioClip catchSound;
        public AudioClip scoreSound;
        public AudioClip crowdCheer;
        public AudioClip crowdBoo;

        [Header("Settings")]
        [Range(0f, 1f)]
        public float musicVolume = 0.5f;
        [Range(0f, 1f)]
        public float sfxVolume = 0.7f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
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

            UpdateVolume();
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource != null && clip != null)
            {
                musicSource.clip = clip;
                musicSource.Play();
            }
        }

        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }
        }

        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource != null && clip != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        public void PlayWhistle()
        {
            PlaySFX(whistleSound);
        }

        public void PlayTackle()
        {
            PlaySFX(tackleSound);
        }

        public void PlayCatch()
        {
            PlaySFX(catchSound);
        }

        public void PlayScore()
        {
            PlaySFX(scoreSound);
        }

        public void PlayCrowdCheer()
        {
            PlaySFX(crowdCheer);
        }

        public void PlayCrowdBoo()
        {
            PlaySFX(crowdBoo);
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
        }

        void UpdateVolume()
        {
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
        }
    }
}
