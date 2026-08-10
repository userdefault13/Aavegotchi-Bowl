using UnityEngine;

namespace RetroBowl.Gameplay
{
    public enum OffensivePlayType
    {
        Pass,
        Run
    }

    public enum RunConcept
    {
        None,
        InsideZone,
        OutsideZone,
        Dive,
        Sweep
    }

    /// <summary>One named play: pass route package or run concept.</summary>
    public sealed class OffensivePlay
    {
        public string Id;
        public string DisplayName;
        public OffensivePlayType Type;
        public string Hint;
        public RunConcept Run;
        /// <summary>Relative waypoints (X downfield, Y across) from each receiver's snap spot.</summary>
        public Vector2[] WrTop;
        public Vector2[] WrBot;
        public Vector2[] Te;
        public Vector2[] Rb;

        public Vector2[] GetRoute(string unitName)
        {
            if (string.IsNullOrEmpty(unitName)) return null;
            if (unitName.StartsWith("WR_Top")) return WrTop;
            if (unitName.StartsWith("WR_Bot")) return WrBot;
            if (unitName.StartsWith("TE")) return Te;
            if (unitName.StartsWith("RB")) return Rb;
            return null;
        }
    }

    /// <summary>
    /// Static Retro Bowl–simple playbook. Pass concepts have distinct route packages;
    /// run concepts hand the ball to the RB for player control.
    /// Active 4+4 slots feed the Tecmo-style match modal (editable from FO Playbook).
    /// </summary>
    public static class Playbook
    {
        public const int ModalSlotCount = 4;

        public static OffensivePlay Selected { get; private set; }

        /// <summary>Defense's pre-snap guess (same 8 modal plays). Match → blitz boost.</summary>
        public static OffensivePlay DefenseGuess { get; private set; }

        public static bool HasDefenseGuess => DefenseGuess != null;

        public static bool DefenseGuessMatchesOffense =>
            DefenseGuess != null
            && Selected != null
            && !string.IsNullOrEmpty(DefenseGuess.Id)
            && !string.IsNullOrEmpty(Selected.Id)
            && string.Equals(DefenseGuess.Id, Selected.Id, System.StringComparison.Ordinal);

        static string[] activeRunIds;
        static string[] activePassIds;
        static bool[] runSlotFlipped;
        static bool[] passSlotFlipped;
        static readonly System.Collections.Generic.Dictionary<string, OffensivePlay> mirrorCache =
            new System.Collections.Generic.Dictionary<string, OffensivePlay>();

        public static readonly OffensivePlay[] PassPlays =
        {
            new OffensivePlay
            {
                Id = "slant",
                DisplayName = "SLANTS",
                Type = OffensivePlayType.Pass,
                Hint = "Quick in-breaks",
                WrTop = new[] { new Vector2(5.5f, 0f), new Vector2(7.5f, -2.8f) },
                WrBot = new[] { new Vector2(5.5f, 0f), new Vector2(7.5f, 2.8f) },
                Te = new[] { new Vector2(3.5f, 0f), new Vector2(5.5f, -3.2f) },
                Rb = new[] { new Vector2(2.5f, 2.4f), new Vector2(5f, 3.2f) }
            },
            new OffensivePlay
            {
                Id = "go",
                DisplayName = "GO / STREAKS",
                Type = OffensivePlayType.Pass,
                Hint = "Vertical shots",
                WrTop = new[] { new Vector2(14f, 0.2f) },
                WrBot = new[] { new Vector2(14f, -0.2f) },
                Te = new[] { new Vector2(11f, -0.4f) },
                Rb = new[] { new Vector2(3f, 2.6f), new Vector2(8f, 3.4f) }
            },
            new OffensivePlay
            {
                Id = "curl",
                DisplayName = "CURLS",
                Type = OffensivePlayType.Pass,
                Hint = "Sit at the sticks",
                WrTop = new[] { new Vector2(8.5f, 0.2f), new Vector2(7.2f, 0.6f) },
                WrBot = new[] { new Vector2(8.5f, -0.2f), new Vector2(7.2f, -0.6f) },
                Te = new[] { new Vector2(6.5f, 0f), new Vector2(8.5f, -2.2f) },
                Rb = new[] { new Vector2(1.5f, -2.2f), new Vector2(4f, -3f) }
            },
            new OffensivePlay
            {
                Id = "cross",
                DisplayName = "CROSSING",
                Type = OffensivePlayType.Pass,
                Hint = "Mesh over the middle",
                WrTop = new[] { new Vector2(6f, 0f), new Vector2(10f, -4.2f) },
                WrBot = new[] { new Vector2(4f, 0f), new Vector2(8.5f, 4f) },
                Te = new[] { new Vector2(5f, 0f), new Vector2(6.5f, 0.3f) },
                Rb = new[] { new Vector2(2f, 2.8f), new Vector2(5.5f, 3.5f) }
            },
            new OffensivePlay
            {
                Id = "te_drag",
                DisplayName = "TE DRAG",
                Type = OffensivePlayType.Pass,
                Hint = "TE across, WR clear",
                WrTop = new[] { new Vector2(12f, 0.4f) },
                WrBot = new[] { new Vector2(7.5f, 0f), new Vector2(6.2f, -0.8f) },
                Te = new[] { new Vector2(2.5f, 0f), new Vector2(4.5f, -4.5f) },
                Rb = new[] { new Vector2(1.8f, 2.2f), new Vector2(4.5f, 3f) }
            },
            new OffensivePlay
            {
                Id = "play_action",
                DisplayName = "PLAY ACTION",
                Type = OffensivePlayType.Pass,
                Hint = "Fake run, deep shot",
                WrTop = new[] { new Vector2(8f, 0f), new Vector2(13f, -2.2f) },
                WrBot = new[] { new Vector2(7f, 0f), new Vector2(10f, 2.4f) },
                Te = new[] { new Vector2(2f, 0.2f), new Vector2(5.5f, 3.6f) },
                // RB sells the run then flares opposite.
                Rb = new[] { new Vector2(2.2f, -0.4f), new Vector2(1.2f, 2.8f), new Vector2(4.5f, 3.6f) }
            }
        };

        public static readonly OffensivePlay[] RunPlays =
        {
            new OffensivePlay
            {
                Id = "inside_zone",
                DisplayName = "INSIDE ZONE",
                Type = OffensivePlayType.Run,
                Run = RunConcept.InsideZone,
                Hint = "Between the tackles",
                WrTop = new[] { new Vector2(4f, -1.2f) },
                WrBot = new[] { new Vector2(4f, 1.2f) },
                Te = new[] { new Vector2(2.5f, 0.2f) },
                Rb = new[] { new Vector2(3.5f, 0.6f), new Vector2(7f, 0.4f) }
            },
            new OffensivePlay
            {
                Id = "outside_zone",
                DisplayName = "OUTSIDE ZONE",
                Type = OffensivePlayType.Run,
                Run = RunConcept.OutsideZone,
                Hint = "Bounce it outside",
                WrTop = new[] { new Vector2(5f, -0.6f) },
                WrBot = new[] { new Vector2(3.5f, 2.2f), new Vector2(7f, 3f) },
                Te = new[] { new Vector2(3f, 0.8f) },
                Rb = new[] { new Vector2(2.5f, -2.2f), new Vector2(6.5f, -3.2f) }
            },
            new OffensivePlay
            {
                Id = "dive",
                DisplayName = "DIVE",
                Type = OffensivePlayType.Run,
                Run = RunConcept.Dive,
                Hint = "Straight ahead",
                WrTop = new[] { new Vector2(3.5f, -0.8f) },
                WrBot = new[] { new Vector2(3.5f, 0.8f) },
                Te = new[] { new Vector2(2f, 0.1f) },
                Rb = new[] { new Vector2(4.5f, 0.15f), new Vector2(8f, 0.1f) }
            },
            new OffensivePlay
            {
                Id = "sweep",
                DisplayName = "SWEEP",
                Type = OffensivePlayType.Run,
                Run = RunConcept.Sweep,
                Hint = "Edge stretch",
                WrTop = new[] { new Vector2(3f, -2.4f), new Vector2(6f, -3.2f) },
                WrBot = new[] { new Vector2(2.5f, 0.4f) },
                Te = new[] { new Vector2(2f, -1.6f) },
                Rb = new[] { new Vector2(1.5f, -2.8f), new Vector2(5.5f, -4.2f), new Vector2(9f, -4.5f) }
            }
        };

        /// <summary>Tecmo-style match modal — active FO slots (skips deactivated).</summary>
        public static OffensivePlay[] ModalRunPlays => FilterActive(EnsureRunSlots(), EnsureRunFlips(), RunPlays);

        public static OffensivePlay[] ModalPassPlays => FilterActive(EnsurePassSlots(), EnsurePassFlips(), PassPlays);

        /// <summary>Active run+pass pool for training / defense guess.</summary>
        public static OffensivePlay[] GetActiveEight() => ModalEight();

        public static void Select(OffensivePlay play) => Selected = play;

        public static void SetDefenseGuess(OffensivePlay play) => DefenseGuess = play;

        public static void ClearDefenseGuess() => DefenseGuess = null;

        /// <summary>
        /// Secret AI guess from the active 8 modal plays (player on offense).
        /// ~1/8 chance to correctly peek at Selected; otherwise picks a different play
        /// so a bad RNG / shared-reference path cannot always "match".
        /// </summary>
        public static void PickAiDefenseGuess()
        {
            var pool = ModalEight();
            if (pool.Length == 0)
            {
                DefenseGuess = null;
                return;
            }

            // Small Tecmo-style "read" — exact match only via this peek.
            if (Selected != null
                && !string.IsNullOrEmpty(Selected.Id)
                && Random.value < (1f / pool.Length))
            {
                DefenseGuess = Selected;
                return;
            }

            // Miss path: never pick the same id as the offense call.
            int guard = 0;
            OffensivePlay pick;
            do
            {
                pick = pool[Random.Range(0, pool.Length)];
                guard++;
            } while (guard < 12
                     && Selected != null
                     && pick != null
                     && string.Equals(pick.Id, Selected.Id, System.StringComparison.Ordinal));

            if (pick != null
                && Selected != null
                && string.Equals(pick.Id, Selected.Id, System.StringComparison.Ordinal))
            {
                // Pool was all the same id — leave unset rather than false-positive blitz.
                DefenseGuess = null;
                return;
            }

            DefenseGuess = pick;
        }

        /// <summary>AI offense call from the same 8 the defense can guess.</summary>
        public static OffensivePlay PickAiOffenseFromModal(bool preferPass)
        {
            var runs = ModalRunPlays;
            var passes = ModalPassPlays;
            if (preferPass && passes.Length > 0)
                return passes[Random.Range(0, passes.Length)];
            if (!preferPass && runs.Length > 0)
                return runs[Random.Range(0, runs.Length)];
            if (passes.Length > 0)
                return passes[Random.Range(0, passes.Length)];
            if (runs.Length > 0)
                return runs[Random.Range(0, runs.Length)];
            return PassPlays[0];
        }

        static OffensivePlay[] ModalEight()
        {
            var runs = ModalRunPlays;
            var passes = ModalPassPlays;
            var pool = new OffensivePlay[runs.Length + passes.Length];
            int n = 0;
            for (int i = 0; i < runs.Length; i++) pool[n++] = runs[i];
            for (int i = 0; i < passes.Length; i++) pool[n++] = passes[i];
            if (n == pool.Length) return pool;
            var trimmed = new OffensivePlay[n];
            for (int i = 0; i < n; i++) trimmed[i] = pool[i];
            return trimmed;
        }

        public static void Clear()
        {
            Selected = null;
            DefenseGuess = null;
            DefenseGuessBlitz.Clear();
        }

        public static OffensivePlay FindPass(int index)
        {
            if (index < 0 || index >= PassPlays.Length) return null;
            return PassPlays[index];
        }

        public static OffensivePlay FindRun(int index)
        {
            if (index < 0 || index >= RunPlays.Length) return null;
            return RunPlays[index];
        }

        public static OffensivePlay FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in RunPlays)
                if (p.Id == id) return p;
            foreach (var p in PassPlays)
                if (p.Id == id) return p;
            return null;
        }

        public static string GetActiveRunId(int slot) => EnsureRunSlots()[ClampSlot(slot)];
        public static string GetActivePassId(int slot) => EnsurePassSlots()[ClampSlot(slot)];

        public static bool IsRunSlotFlipped(int slot) => EnsureRunFlips()[ClampSlot(slot)];
        public static bool IsPassSlotFlipped(int slot) => EnsurePassFlips()[ClampSlot(slot)];

        /// <summary>Mirror the concept across the field — the slot keeps its play, routes swap sides.</summary>
        public static void ToggleRunSlotFlip(int slot)
        {
            slot = ClampSlot(slot);
            var flips = EnsureRunFlips();
            flips[slot] = !flips[slot];
        }

        public static void TogglePassSlotFlip(int slot)
        {
            slot = ClampSlot(slot);
            var flips = EnsurePassFlips();
            flips[slot] = !flips[slot];
        }

        /// <summary>Slot's play with its flip applied, or null when the slot is off.</summary>
        public static OffensivePlay GetRunSlotPlay(int slot)
        {
            slot = ClampSlot(slot);
            var play = FindById(GetActiveRunId(slot));
            if (play == null) return null;
            return IsRunSlotFlipped(slot) ? Mirrored(play) : play;
        }

        public static OffensivePlay GetPassSlotPlay(int slot)
        {
            slot = ClampSlot(slot);
            var play = FindById(GetActivePassId(slot));
            if (play == null) return null;
            return IsPassSlotFlipped(slot) ? Mirrored(play) : play;
        }

        /// <summary>
        /// Mirrored copy of a play: every route's across-field offset is negated, so the concept
        /// attacks the opposite side. Players keep their formation spots (those are fixed), which
        /// keeps the card diagram and what actually runs on the field identical.
        /// Keeps <see cref="OffensivePlay.Id"/> so slot lookups and defense-guess matching still work.
        /// </summary>
        public static OffensivePlay Mirrored(OffensivePlay play)
        {
            if (play == null) return null;
            if (mirrorCache.TryGetValue(play.Id ?? "", out var cached) && cached != null)
                return cached;

            var flipped = new OffensivePlay
            {
                Id = play.Id,
                DisplayName = play.DisplayName,
                Type = play.Type,
                Hint = play.Hint,
                Run = play.Run,
                WrTop = MirrorRoute(play.WrTop),
                WrBot = MirrorRoute(play.WrBot),
                Te = MirrorRoute(play.Te),
                Rb = MirrorRoute(play.Rb)
            };

            mirrorCache[play.Id ?? ""] = flipped;
            return flipped;
        }

        static Vector2[] MirrorRoute(Vector2[] route)
        {
            if (route == null) return null;
            var mirrored = new Vector2[route.Length];
            for (int i = 0; i < route.Length; i++)
                mirrored[i] = new Vector2(route[i].x, -route[i].y);
            return mirrored;
        }

        static bool[] EnsureRunFlips()
        {
            if (runSlotFlipped == null || runSlotFlipped.Length != ModalSlotCount)
                runSlotFlipped = new bool[ModalSlotCount];
            return runSlotFlipped;
        }

        static bool[] EnsurePassFlips()
        {
            if (passSlotFlipped == null || passSlotFlipped.Length != ModalSlotCount)
                passSlotFlipped = new bool[ModalSlotCount];
            return passSlotFlipped;
        }

        public static bool IsRunSlotActive(int slot)
            => !string.IsNullOrEmpty(GetActiveRunId(slot));

        public static bool IsPassSlotActive(int slot)
            => !string.IsNullOrEmpty(GetActivePassId(slot));

        public static void SetActiveRunSlot(int slot, string playId)
        {
            if (string.IsNullOrEmpty(playId))
            {
                EnsureRunSlots()[ClampSlot(slot)] = "";
                return;
            }
            var play = FindById(playId);
            if (play == null || play.Type != OffensivePlayType.Run) return;
            EnsureRunSlots()[ClampSlot(slot)] = play.Id;
        }

        public static void SetActivePassSlot(int slot, string playId)
        {
            if (string.IsNullOrEmpty(playId))
            {
                EnsurePassSlots()[ClampSlot(slot)] = "";
                return;
            }
            var play = FindById(playId);
            if (play == null || play.Type != OffensivePlayType.Pass) return;
            EnsurePassSlots()[ClampSlot(slot)] = play.Id;
        }

        public static void ClearActiveRunSlot(int slot)
            => EnsureRunSlots()[ClampSlot(slot)] = "";

        public static void ClearActivePassSlot(int slot)
            => EnsurePassSlots()[ClampSlot(slot)] = "";

        /// <summary>Toggle: empty → library play, or active → empty (deactivate).</summary>
        public static void ToggleActiveRunSlot(int slot)
        {
            slot = ClampSlot(slot);
            if (IsRunSlotActive(slot))
                ClearActiveRunSlot(slot);
            else if (RunPlays.Length > 0)
                SetActiveRunSlot(slot, RunPlays[Mathf.Min(slot, RunPlays.Length - 1)].Id);
        }

        public static void ToggleActivePassSlot(int slot)
        {
            slot = ClampSlot(slot);
            if (IsPassSlotActive(slot))
                ClearActivePassSlot(slot);
            else if (PassPlays.Length > 0)
                SetActivePassSlot(slot, PassPlays[Mathf.Min(slot, PassPlays.Length - 1)].Id);
        }

        /// <summary>Cycle slot to the next concept in the library (FO Playbook UI).</summary>
        public static void CycleActiveRunSlot(int slot, int delta = 1)
        {
            slot = ClampSlot(slot);
            var ids = EnsureRunSlots();
            if (string.IsNullOrEmpty(ids[slot]))
            {
                if (RunPlays.Length > 0)
                    ids[slot] = RunPlays[0].Id;
                return;
            }
            int idx = IndexOfId(RunPlays, ids[slot]);
            idx = (idx + delta) % RunPlays.Length;
            if (idx < 0) idx += RunPlays.Length;
            ids[slot] = RunPlays[idx].Id;
        }

        public static void CycleActivePassSlot(int slot, int delta = 1)
        {
            slot = ClampSlot(slot);
            var ids = EnsurePassSlots();
            if (string.IsNullOrEmpty(ids[slot]))
            {
                if (PassPlays.Length > 0)
                    ids[slot] = PassPlays[0].Id;
                return;
            }
            int idx = IndexOfId(PassPlays, ids[slot]);
            idx = (idx + delta) % PassPlays.Length;
            if (idx < 0) idx += PassPlays.Length;
            ids[slot] = PassPlays[idx].Id;
        }

        static string[] EnsureRunSlots()
        {
            if (activeRunIds == null || activeRunIds.Length != ModalSlotCount)
                activeRunIds = DefaultIds(RunPlays);
            return activeRunIds;
        }

        static string[] EnsurePassSlots()
        {
            if (activePassIds == null || activePassIds.Length != ModalSlotCount)
                activePassIds = DefaultIds(PassPlays);
            return activePassIds;
        }

        static string[] DefaultIds(OffensivePlay[] library)
        {
            var ids = new string[ModalSlotCount];
            for (int i = 0; i < ModalSlotCount; i++)
                ids[i] = library[Mathf.Min(i, library.Length - 1)].Id;
            return ids;
        }

        static OffensivePlay[] FilterActive(string[] ids, bool[] flips, OffensivePlay[] library)
        {
            var list = new System.Collections.Generic.List<OffensivePlay>(ModalSlotCount);
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                var play = FindById(ids[i]) ?? library[Mathf.Min(i, library.Length - 1)];
                if (play == null) continue;
                if (flips != null && i < flips.Length && flips[i])
                    play = Mirrored(play);
                list.Add(play);
            }
            if (list.Count == 0 && library.Length > 0)
                list.Add(library[0]);
            return list.ToArray();
        }

        static int IndexOfId(OffensivePlay[] library, string id)
        {
            for (int i = 0; i < library.Length; i++)
                if (library[i].Id == id) return i;
            return 0;
        }

        static int ClampSlot(int slot) => Mathf.Clamp(slot, 0, ModalSlotCount - 1);

        /// <summary>Apply the selected play's relative routes onto all skill players.</summary>
        public static void ApplyRoutesToFormation()
        {
            var play = Selected;
            if (play == null) return;

            foreach (var name in FormationRoster.SkillOffense)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null) continue;

                var offsets = play.GetRoute(name);
                if (offsets != null && offsets.Length > 0)
                    rc.SetRelativeRoute(offsets);
                else
                    rc.GenerateRoute();
            }
        }
    }
}
