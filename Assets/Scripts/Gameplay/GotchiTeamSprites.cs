using UnityEngine;

namespace RetroBowl.Gameplay
{
    public enum GotchiFacing
    {
        Front = 0,
        Left = 1,
        Right = 2,
        Back = 3
    }

    /// <summary>
    /// USDC / UNI Aavegotchi team skins — 4 facings each.
    /// P1 (human) = maUSDC · P2 (opponent) = maUNI — never swap with offense/defense.
    /// </summary>
    public static class GotchiTeamSprites
    {
        public const string UsdcPrefix = "Gotchi/team_usdc";
        public const string UniPrefix = "Gotchi/team_uni";

        static readonly string[] FacingSuffix =
            { "_front", "_left", "_right", "_back" };

        static Sprite[] usdc;
        static Sprite[] uni;
        static bool loaded;

        /// <summary>Drop cache so rebuilt PNGs reload (e.g. after asset bake).</summary>
        public static void InvalidateCache()
        {
            loaded = false;
            usdc = null;
            uni = null;
        }

        /// <summary>P1 human franchise — maUSDC.</summary>
        public static Sprite PlayerOne => Get(true, GotchiFacing.Front);

        /// <summary>P2 opponent franchise — maUNI.</summary>
        public static Sprite PlayerTwo => Get(false, GotchiFacing.Front);

        /// <summary>Legacy alias — prefer <see cref="PlayerOne"/>.</summary>
        public static Sprite Offense => PlayerOne;

        /// <summary>Legacy alias — prefer <see cref="PlayerTwo"/>.</summary>
        public static Sprite Defense => PlayerTwo;

        public static Sprite GetPlayerOne(GotchiFacing facing) => Get(true, facing);
        public static Sprite GetPlayerTwo(GotchiFacing facing) => Get(false, facing);

        public static Sprite GetOffense(GotchiFacing facing) => Get(true, facing);
        public static Sprite GetDefense(GotchiFacing facing) => Get(false, facing);

        /// <param name="playerOne">True = USDC (P1), false = UNI (P2).</param>
        public static Sprite Get(bool playerOne, GotchiFacing facing)
        {
            EnsureLoaded();
            var set = playerOne ? usdc : uni;
            if (set == null) return null;
            int i = Mathf.Clamp((int)facing, 0, 3);
            return set[i] != null ? set[i] : set[(int)GotchiFacing.Front];
        }

        /// <summary>
        /// Map planar velocity (field X downfield, Y across) to a facing.
        /// Dominant axis wins; idle keeps <paramref name="current"/>.
        /// </summary>
        public static GotchiFacing FacingFromVelocity(Vector2 planar, GotchiFacing current)
        {
            if (planar.sqrMagnitude < 0.0004f)
                return current;

            if (Mathf.Abs(planar.x) >= Mathf.Abs(planar.y))
                return planar.x >= 0f ? GotchiFacing.Right : GotchiFacing.Left;

            // +Y = top of screen → back; −Y → front (camera looks down +Z).
            return planar.y >= 0f ? GotchiFacing.Back : GotchiFacing.Front;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            usdc = LoadSet(UsdcPrefix);
            uni = LoadSet(UniPrefix);
            if (usdc[(int)GotchiFacing.Front] == null)
                Debug.LogWarning($"[GotchiTeamSprites] Missing Resources/{UsdcPrefix}_front");
            if (uni[(int)GotchiFacing.Front] == null)
                Debug.LogWarning($"[GotchiTeamSprites] Missing Resources/{UniPrefix}_front");
        }

        static Sprite[] LoadSet(string prefix)
        {
            var set = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                set[i] = LoadSprite(prefix + FacingSuffix[i]);
                // Alias: bare prefix may be front-only copy.
                if (set[i] == null && i == (int)GotchiFacing.Front)
                    set[i] = LoadSprite(prefix);
            }
            return set;
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;

            tex.filterMode = FilterMode.Point;
            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                64f);
        }
    }
}
