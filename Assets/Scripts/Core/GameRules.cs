namespace RetroBowl.Core
{
    /// <summary>
    /// Feature toggles. Defaults match original Retro Bowl (offense-focused, no mash extras).
    /// Flip these back on later to restore custom systems — set any flag <c>true</c>.
    /// </summary>
    public static class GameRules
    {
        /// <summary>Player cycles defenders (J/K) pre-snap and steers one post-snap (Tecmo).</summary>
        public static bool EnablePlayerDefense = true;

        /// <summary>
        /// When opponent has the ball, run AI offense snaps (pass/run) so the game continues.
        /// Turn off only if you fully simulate opponent drives instead.
        /// </summary>
        public static bool EnableAiOffenseWhenDefending = true;

        /// <summary>
        /// NES Tecmo contact: non-dive touch → mash Z (A) break/wrap lock.
        /// Dive contact stays an instant tackle (see TecmoContact / dive controllers).
        /// </summary>
        public static bool EnableTecmoContact = true;

        /// <summary>
        /// Tecmo “popcorn”: if Strength (Hitting Power) differs by ≥50 on the 1–99
        /// scale, the stronger player auto-wins contact (no mash / SoftTackle roll).
        /// </summary>
        public static bool EnableTecmoPopcorn = true;

        /// <summary>Min Strength gap for popcorn (Tecmo FAQ: 50 HP / ~8 notches).</summary>
        public const int TecmoPopcornHpGap = 50;

        /// <summary>
        /// Extra defenders joining an active Tecmo mash lock add Strength to pile HP
        /// (re-check popcorn + bias wrap mash).
        /// </summary>
        public static bool EnableTecmoHpStack = true;

        /// <summary>Max assist tacklers beyond the primary wrap (pile size = 1 + this).</summary>
        public const int TecmoMaxAssistTacklers = 3;

        /// <summary>Dive phases through blockers (disable colliders; range checks still tackle).</summary>
        public static bool EnableTecmoDiveGhost = true;

        /// <summary>Mash / hold Tecmo A during a dive to stretch slide distance.</summary>
        public static bool EnableTecmoDiveMashExtend = true;

        /// <summary>Universal ContactBattle mash on every O↔D touch.</summary>
        public static bool EnableContactBattle = true;

        /// <summary>Legacy TackleBattle QTE (dive/hard-hit break meter).</summary>
        public static bool EnableTackleBattleQte = false;

        /// <summary>Mash B / Shift run button for sprint (offense + defense chase).</summary>
        public static bool EnableStaminaSprint = true;

        /// <summary>OL soft engage may start ContactBattle (bull-rush era).</summary>
        public static bool EnableLineEngageBattles = true;
    }
}
