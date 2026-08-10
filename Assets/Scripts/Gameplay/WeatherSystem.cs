using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public enum WeatherKind
    {
        Clear,
        Rain,
        Snow,
        Windy
    }

    /// <summary>
    /// Match weather + wind. Nudges throw landings and kick aim/power (Retro options feel).
    /// Visuals are lightweight placeholders — not New Star art.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Current")]
        public WeatherKind kind = WeatherKind.Clear;
        [Tooltip("Across-field wind (+ = toward sideline +Y).")]
        public float windY;
        [Tooltip("Downfield wind (+ = toward scoring endzone).")]
        public float windX;
        [Range(0f, 1f)] public float intensity = 0.35f;

        ParticleSystem fx;
        Transform fxRoot;

        public string Label => kind switch
        {
            WeatherKind.Rain => $"RAIN  WIND {WindArrow()}",
            WeatherKind.Snow => $"SNOW  WIND {WindArrow()}",
            WeatherKind.Windy => $"WINDY  {WindArrow()}",
            _ => windY * windY + windX * windX > 0.04f ? $"CLEAR  BREEZE {WindArrow()}" : "CLEAR"
        };

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("WeatherSystem");
            if (host.GetComponent<WeatherSystem>() == null)
                host.AddComponent<WeatherSystem>();
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RollForMatch(bool forceClear = false)
        {
            if (forceClear)
            {
                kind = WeatherKind.Clear;
                windX = 0f;
                windY = 0f;
                intensity = 0f;
            }
            else
            {
                float roll = Random.value;
                if (roll < 0.55f) kind = WeatherKind.Clear;
                else if (roll < 0.75f) kind = WeatherKind.Windy;
                else if (roll < 0.90f) kind = WeatherKind.Rain;
                else kind = WeatherKind.Snow;

                intensity = kind == WeatherKind.Clear
                    ? Random.Range(0f, 0.25f)
                    : Random.Range(0.35f, 0.85f);

                float dirY = Random.value < 0.5f ? -1f : 1f;
                float dirX = Random.Range(-0.4f, 0.4f);
                float strength = kind switch
                {
                    WeatherKind.Windy => Mathf.Lerp(0.55f, 1.2f, intensity),
                    WeatherKind.Rain => Mathf.Lerp(0.25f, 0.7f, intensity),
                    WeatherKind.Snow => Mathf.Lerp(0.35f, 0.9f, intensity),
                    _ => Mathf.Lerp(0f, 0.35f, intensity)
                };
                windY = dirY * strength;
                windX = dirX * strength * 0.65f;
            }

            RebuildFx();
            Debug.Log($"Weather: {Label}");
        }

        /// <summary>Bias a throw landing for wind / wet ball.</summary>
        public Vector3 ApplyThrowDrift(Vector3 target)
        {
            if (kind == WeatherKind.Clear && intensity < 0.1f)
                return target;

            float wet = kind == WeatherKind.Rain || kind == WeatherKind.Snow ? 1.25f : 1f;
            target.y += windY * 0.55f * wet;
            target.x += windX * 0.35f * wet;
            // Light random flutter in bad weather.
            if (kind != WeatherKind.Clear)
            {
                target.y += Random.Range(-0.25f, 0.25f) * intensity;
                target.x += Random.Range(-0.15f, 0.15f) * intensity;
            }
            return target;
        }

        /// <summary>Kick aim bias (added to aimOffset) and power mul.</summary>
        public void ApplyKickModifiers(ref float aimOffset, ref float power01)
        {
            aimOffset += windY * 0.45f;
            // Headwind shortens, tailwind helps.
            power01 *= 1f - windX * 0.08f;
            if (kind == WeatherKind.Snow)
                power01 *= 0.92f;
            else if (kind == WeatherKind.Rain)
                power01 *= 0.96f;
            power01 = Mathf.Clamp01(power01);
        }

        string WindArrow()
        {
            if (Mathf.Abs(windY) < 0.15f && Mathf.Abs(windX) < 0.15f) return "—";
            string across = windY > 0.2f ? "↑" : windY < -0.2f ? "↓" : "";
            string down = windX > 0.2f ? "→" : windX < -0.2f ? "←" : "";
            return $"{across}{down}";
        }

        void RebuildFx()
        {
            if (fxRoot != null)
                Destroy(fxRoot.gameObject);

            if (kind == WeatherKind.Clear)
                return;

            var go = new GameObject("WeatherFx");
            fxRoot = go.transform;
            go.transform.position = Vector3.zero;

            fx = go.AddComponent<ParticleSystem>();
            var main = fx.main;
            main.loop = true;
            main.startLifetime = kind == WeatherKind.Snow ? 2.2f : 1.1f;
            main.startSize = kind == WeatherKind.Snow ? 0.12f : 0.05f;
            main.startSpeed = kind == WeatherKind.Snow ? 1.2f : 8f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var col = fx.colorOverLifetime;
            col.enabled = false;
            main.startColor = kind == WeatherKind.Snow
                ? new Color(0.95f, 0.95f, 1f, 0.85f)
                : new Color(0.65f, 0.75f, 0.95f, 0.55f);

            var emission = fx.emission;
            emission.rateOverTime = Mathf.Lerp(40f, 160f, intensity);

            var shape = fx.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(110f, 20f, 1f);

            var vel = fx.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(windX * 2f);
            vel.y = new ParticleSystem.MinMaxCurve(-3f + windY * 1.5f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 60;

            fx.Play();
        }
    }
}
