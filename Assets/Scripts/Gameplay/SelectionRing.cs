using UnityEngine;
using TMPro;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// White elliptical marker under the controlled player plus Retro Bowl–style
    /// jersey number floating above their head.
    /// </summary>
    public class SelectionRing : MonoBehaviour
    {
        public Transform follow;
        public Vector3 offset = new Vector3(0f, -0.55f, 0.1f);
        public Vector3 numberOffset = new Vector3(0f, 0.78f, -0.05f);
        public bool onlyWhenPlaying = true;
        public float numberFontSize = 5.2f;

        SpriteRenderer sr;
        TextMeshPro numberLabel;
        Transform cachedFollow;
        int cachedJersey = int.MinValue;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            EnsureNumberLabel();
        }

        void LateUpdate()
        {
            if (follow == null || !follow.gameObject.activeInHierarchy)
            {
                SetVisible(false);
                return;
            }

            bool show = true;
            var gm = Core.GameManager.Instance;
            if (gm != null)
            {
                // Hide during player kick meter / banners.
                // Player-receive fielding / player-kick gunner: ring stays on the controlled unit.
                bool kickMeter = KickingController.Instance != null
                                 && KickingController.Instance.IsActive;
                bool kickCover = PlayerDefenseController.Instance != null
                                 && PlayerDefenseController.Instance.IsKickoffCoverage;
                if (kickMeter || gm.waitingForNextPlay)
                    show = false;
                else if (kickCover || (gm.isKicking && !gm.IsLiveReturn))
                    show = follow != null;
                else if (onlyWhenPlaying)
                    show = gm.currentState == Core.GameState.Playing;
            }

            SetVisible(show);
            if (!show) return;

            transform.position = follow.position + offset;
            transform.rotation = Quaternion.identity;

            RefreshJerseyIfNeeded();
            if (numberLabel != null)
            {
                numberLabel.transform.position = follow.position + numberOffset;
                numberLabel.transform.rotation = Quaternion.identity;
            }
        }

        public void SetFollow(Transform t)
        {
            follow = t;
            cachedFollow = null;
            cachedJersey = int.MinValue;
            RefreshJerseyIfNeeded();
        }

        void SetVisible(bool show)
        {
            if (sr != null) sr.enabled = show;
            if (numberLabel != null) numberLabel.enabled = show;
        }

        void RefreshJerseyIfNeeded()
        {
            EnsureNumberLabel();
            if (numberLabel == null || follow == null) return;

            int num = ResolveJersey(follow);
            if (follow == cachedFollow && num == cachedJersey) return;

            cachedFollow = follow;
            cachedJersey = num;
            numberLabel.text = num.ToString();
        }

        static int ResolveJersey(Transform t)
        {
            if (t == null) return 1;
            var stats = UnitRuntimeStats.Of(t);
            if (stats != null && stats.jerseyNumber > 0)
                return Mathf.Clamp(stats.jerseyNumber, 1, 99);
            return 1;
        }

        void EnsureNumberLabel()
        {
            if (numberLabel != null) return;

            var existing = transform.Find("JerseyNumber");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                numberLabel = go.GetComponent<TextMeshPro>();
            }
            else
            {
                go = new GameObject("JerseyNumber");
                go.transform.SetParent(transform, false);
            }

            if (numberLabel == null)
                numberLabel = go.AddComponent<TextMeshPro>();

            GameFonts.Apply(numberLabel);
            numberLabel.fontSize = numberFontSize;
            numberLabel.alignment = TextAlignmentOptions.Center;
            numberLabel.color = Color.black;
            numberLabel.fontStyle = FontStyles.Bold;
            numberLabel.textWrappingMode = TextWrappingModes.NoWrap;
            numberLabel.overflowMode = TextOverflowModes.Overflow;
            numberLabel.rectTransform.sizeDelta = new Vector2(2.5f, 1.5f);
            numberLabel.sortingOrder = 95;

            // Black digit, white outline — Retro Bowl selected-player callout.
            numberLabel.outlineWidth = 0.28f;
            numberLabel.outlineColor = Color.white;
            if (numberLabel.fontMaterial != null)
            {
                numberLabel.fontMaterial = new Material(numberLabel.fontMaterial);
                numberLabel.fontMaterial.EnableKeyword("OUTLINE_ON");
                numberLabel.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
                numberLabel.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            }

            numberLabel.text = "1";
            numberLabel.enabled = false;
        }
    }
}
