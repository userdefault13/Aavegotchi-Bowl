using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Central match feedback: procedural SFX + camera punch for Retro-style readability.
    /// Uses generated tones (no New Star asset rips).
    /// </summary>
    public static class MatchPresentation
    {
        public static void EnsureAudio()
        {
            AudioManager.EnsureExists();
        }

        public static void Snap()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayWhistle();
            Shake(0.08f, 0.12f);
        }

        public static void Throw()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayThrow();
        }

        public static void Catch()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayCatch();
            Shake(0.1f, 0.14f);
        }

        public static void Tackle()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayTackle();
            Shake(0.22f, 0.18f);
        }

        public static void Incomplete()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayIncomplete();
        }

        public static void Tip()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayTip();
            Shake(0.12f, 0.12f);
        }

        public static void Interception()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayCrowdBoo();
            AudioManager.Instance?.PlayCatch();
            Shake(0.28f, 0.25f);
        }

        public static void Score()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayScore();
            AudioManager.Instance?.PlayCrowdCheer();
            Shake(0.35f, 0.35f);
        }

        public static void Kick()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayKick();
            Shake(0.15f, 0.16f);
        }

        /// <summary>Opening kickoff "GET READY!" beat.</summary>
        public static void GetReady()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayWhistle();
        }

        public static void FirstDown()
        {
            EnsureAudio();
            AudioManager.Instance?.PlayCrowdCheer();
            Shake(0.12f, 0.15f);
        }

        public static void Shake(float amplitude, float duration)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.Shake(amplitude, duration);
        }
    }
}
