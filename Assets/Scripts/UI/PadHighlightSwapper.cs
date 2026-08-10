using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroBowl.UI
{
    /// <summary>
    /// Drives a play card's controller icon. The selected card plays the source GIF —
    /// alternating white-outline and blue-outline frames — while every other card holds frame 1.
    /// </summary>
    public class PadHighlightSwapper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Source GIF runs at 10fps, so each frame holds for a tenth of a second.</summary>
        const float FrameSeconds = 0.1f;

        Image target;
        Sprite idle;
        Sprite highlighted;
        bool pointerInside;
        bool cursorFocused;
        float animStartTime;

        bool Animating => (pointerInside || cursorFocused) && highlighted != null;

        public static PadHighlightSwapper Attach(GameObject host, Image icon, Sprite idle, Sprite highlighted)
        {
            if (host == null || icon == null) return null;

            var swapper = host.GetComponent<PadHighlightSwapper>();
            if (swapper == null)
                swapper = host.AddComponent<PadHighlightSwapper>();

            swapper.target = icon;
            swapper.idle = idle;
            swapper.highlighted = highlighted;
            swapper.Apply();
            return swapper;
        }

        /// <summary>Called by the menu cursor when focus moves onto or off this card.</summary>
        public void SetCursorFocused(bool focused)
        {
            if (cursorFocused == focused) return;
            cursorFocused = focused;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            Apply();
        }

        void OnDisable()
        {
            pointerInside = false;
            cursorFocused = false;
            Apply();
        }

        void Update()
        {
            if (target == null || !Animating) return;

            // Menus can run while the game is paused, so drive the flip off unscaled time.
            // Starts on the highlighted frame so selection reads immediately.
            int frame = Mathf.FloorToInt((Time.unscaledTime - animStartTime) / FrameSeconds);
            var sprite = frame % 2 == 0 ? highlighted : idle;
            if (sprite != null && target.sprite != sprite)
                target.sprite = sprite;
        }

        void Apply()
        {
            if (target == null) return;

            if (Animating)
            {
                animStartTime = Time.unscaledTime;
                if (highlighted != null)
                    target.sprite = highlighted;
                return;
            }

            if (idle != null)
                target.sprite = idle;
        }
    }
}
