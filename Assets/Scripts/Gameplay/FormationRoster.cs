using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Tecmo / Retro Bowl roster: packed into a narrow hash band so the full
    /// formation fits on the green between the yard numbers.
    /// </summary>
    public static class FormationRoster
    {
        // Retro Bowl reference spacing:
        // OL shoulder-to-shoulder on the hashes; WRs wide but inset from the
        // white sideline / OOB (RetroLookApplier.SidelineY) so sprites stay on green.
        /// <summary>WR/CB lane half-width. Keep = SidelineY − 1.0 (currently 4.85).</summary>
        public const float PlayBandHalf = 4.85f;
        public const float OlGap = 0.72f;       // tight OL stack (scaled with PlayerScale)
        public const float PlayerScale = 1.32f; // 1.5× prior 0.88
        /// <summary>
        /// Football Visual scale. Sprite is 24×16 @ 16 PPU (native 1.5×1.0 world).
        /// Length ≈ ⅓ gotchi height so the ball matches Aavegotchi proportions.
        /// </summary>
        public const float BallScale = PlayerScale * 0.34f / 1.5f; // ≈ 0.299
        /// <summary>Catch / loft trigger radius — slightly larger than the visual oval.</summary>
        public const float BallCatchRadius = 0.33f;
        /// <summary>Hit radius for top-down circle players (SphereCollider).</summary>
        public const float BodyRadius = 0.57f;
        /// <summary>
        /// Visual half-width of a player circle (sprite world radius at PlayerScale).
        /// OL/DL noses sit this far off the LOS so edges kiss on the blue stick.
        /// </summary>
        public const float LineNose = PlayerScale * 0.5f;

        public static readonly string[] OffensiveLine =
            { "OL_LT", "OL_LG", "OL_C", "OL_RG", "OL_RT" };

        public static readonly string[] SkillOffense =
            { "WR_Top", "WR_Bot", "TE", "RB" };

        public static readonly string[] DefensiveLine =
            { "DL_1", "DL_2", "DL_3", "DL_4" };

        public static readonly string[] Linebackers =
            { "LB_1", "LB_2" };

        public static readonly string[] Secondary =
            { "CB_Top", "CB_Bot", "S" };

        public static float CurrentLosYard()
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.currentYardLine;
            return RetroLookApplier.LosYard;
        }

        /// <summary>+1 / −1 offense attack direction (absolute field).</summary>
        public static int DriveDir
            => FieldManager.Instance != null
                ? FieldManager.NormDir(FieldManager.Instance.driveDirection)
                : 1;

        /// <summary>+1 / −1 kickoff flight direction.</summary>
        public static int KickDir
            => FieldManager.Instance != null
                ? FieldManager.NormDir(FieldManager.Instance.kickDirection)
                : 1;

        /// <summary>Absolute tee yard for the active kickoff (35 or 65).</summary>
        public static float KickoffTeeYardAbs
            => FieldManager.Instance != null
                ? FieldManager.Instance.KickoffTeeAbsoluteYard
                : KickoffTeeYard;

        public static float KickoffKickerYardAbs
            => KickoffTeeYardAbs - KickoffKickerBehindYards * KickDir;

        public static float KickoffCoverageYardAbs
            => KickoffTeeYardAbs + KickoffCoverageAheadYards * KickDir;

        public static float KickoffReceiveWallYardAbs
            => KickoffTeeYardAbs + KickoffReceiveWallAheadYards * KickDir;

        public static float KickoffReturnerYardAbs
            => KickDir > 0 ? KickoffReturnerYard : 100f - KickoffReturnerYard;

        public static float KickoffCameraYardAbs
            => KickoffTeeYardAbs + KickoffCameraAheadYards * KickDir;

        /// <summary>Player kickoff returner after Tecmo select (default RB).</summary>
        public static string DesignatedKickReturnerName = "RB";

        public static void EnsureAndPlace(float losYard)
        {
            var root = GetOrCreateRoot();
            EnsureOffense(root);
            EnsureDefense(root);
            HideLegacyExtras();
            PlaceOffense(losYard);
            PlaceDefense(losYard);
            WireQuarterback();
            StatBridge.ApplyMatchSides();
            RetroLookApplier.RestyleUnitsForPossession();
        }

        public static void PlaceOnly(float losYard)
        {
            OffensiveBlocker.ClearKickoffWallOnlyBlockers();
            ResetSkillPlayersForNextPlay();
            ResetFootballForNextPlay(losYard);
            PlaceOffense(losYard);
            PlaceDefense(losYard);
            StatBridge.ApplyMatchSides();
            RetroLookApplier.RestyleUnitsForPossession();
        }

        /// <summary>GET READY! / Receive — clear the hash marks (Retro Bowl empty field).</summary>
        public static void HideForKickoffIntro()
        {
            foreach (var name in AllUnitNames())
            {
                // Hide every scene instance (including duplicates from older bugs).
                foreach (var go in FindAllByNameIncludingInactive(name))
                    go.SetActive(false);
            }

            foreach (var ball in FindAllFootballsIncludingInactive())
                ball.SetActive(false);

            // No controlled player during empty-field intro.
            var ring = FindByNameIncludingInactive("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(null);
            }
        }

        // Tecmo-tuned kickoff geometry (absolute yards; kick travels +X toward 100):
        // tee 35, longer kicker run-up, walls spaced, deep returner near own 9.
        public const float KickoffTeeYard = 35f;
        public const float KickoffKickerBehindYards = 5.5f;
        public const float KickoffCoverageAheadYards = 22f;
        public const float KickoffReceiveWallAheadYards = 32f;
        /// <summary>Legacy absolute refs (tee ± offsets when KickDir=+1).</summary>
        public const float KickoffKickerYard = 29.5f;
        public const float KickoffCoverageYard = 57f;
        public const float KickoffReceiveWallYard = 67f;
        public const float KickoffReturnerYard = 91f;    // ~receiving 9
        public const float KickoffCameraAheadYards = 14f;
        public const float KickoffCameraYard = 49f;

        /// <summary>
        /// Unit used as the visible kicker circle during kickoff aim / approach.
        /// Player receive → opponent S; player kicks → Quarterback.
        /// </summary>
        public static string KickoffKickerName(bool receiverIsPlayer)
            => receiverIsPlayer ? "S" : "Quarterback";

        /// <summary>Active (or inactive) kicker transform for the current kickoff side.</summary>
        public static Transform FindKickoffKicker()
        {
            bool playerReceives = FieldManager.Instance != null
                                  && FieldManager.Instance.KickoffReceiverIsPlayer;
            return FindByNameIncludingInactive(KickoffKickerName(playerReceives))?.transform;
        }

        /// <summary>
        /// Kickoff walls span nearly sideline→sideline (symmetric about Y=0).
        /// Normal offense uses the narrower <see cref="PlayBandHalf"/> hash band.
        /// </summary>
        public static float KickoffWallHalfY
            => Mathf.Max(1f, RetroLookApplier.SidelineY - BodyRadius - 0.4f);

        /// <summary>
        /// Retro Bowl dynamic kickoff: isolated kicker + tee, two tight vertical walls
        /// ~5 yards apart on the landing side, deep returner. Used while the kick
        /// mini-game is active.
        /// </summary>
        public static void PlaceKickoffReturn(bool receiverIsPlayer)
        {
            // Reactivate the original styled roster (GameObject.Find skips inactive —
            // without this we used to spawn invisible duplicate units).
            DedupAndReactivateRoster();
            float tee = KickoffTeeYardAbs;
            EnsureAndPlace(tee);

            // Offense units = player colors; defense = opponent.
            // Opening receive: player is receiving → blue wall + returner, red coverage + kicker.
            string[] receiveWall;
            string returnerName;
            string[] coverWall;
            string kickerName = KickoffKickerName(receiverIsPlayer);
            if (receiverIsPlayer)
            {
                returnerName = string.IsNullOrEmpty(DesignatedKickReturnerName)
                    ? "RB"
                    : DesignatedKickReturnerName;
                receiveWall = new[]
                {
                    "Quarterback", "WR_Top", "WR_Bot", "TE",
                    "OL_LT", "OL_LG", "OL_C", "OL_RG", "OL_RT"
                };
                coverWall = new[]
                {
                    "DL_1", "DL_2", "DL_3", "DL_4",
                    "LB_1", "LB_2", "CB_Top", "CB_Bot"
                };
            }
            else
            {
                returnerName = "S";
                receiveWall = new[]
                {
                    "DL_1", "DL_2", "DL_3", "DL_4",
                    "LB_1", "LB_2", "CB_Top", "CB_Bot"
                };
                coverWall = new[]
                {
                    "RB", "WR_Top", "WR_Bot", "TE",
                    "OL_LT", "OL_LG", "OL_C", "OL_RG", "OL_RT"
                };
            }

            PlaceKickoffWall(coverWall, KickoffCoverageYardAbs);
            PlaceKickoffWall(receiveWall, KickoffReceiveWallYardAbs);
            EnsureKickoffReceiveWallBlockers(receiveWall, returnerName);

            // Deep returner — slight hash offset so the lane reads like RB.
            Place(returnerName, AtKickoff(KickoffReturnerYardAbs, KickoffWallHalfY * 0.55f));
            // AI returner is S (DefenderAI) — needs ReceiverController for the live return.
            EnsureKickoffReturner(returnerName);
            // Isolated kicker a few yards behind the tee on the hashes (Y≈0).
            Place(kickerName, AtKickoff(KickoffKickerYardAbs, 0f));

            // One ball on the tee — strip duplicate Football meshes (stacked brown spheres).
            var ball = DedupAndParkKickoffBall();
            if (FieldManager.Instance != null && ball != null)
                FieldManager.Instance.ballTransform = ball.transform;

            // Circles on every kickoff unit (kicker included) — avoids dark blob / missing sprite.
            RetroLookApplier.RestyleKickoffRoster(receiverIsPlayer, kickerName);

            // Strip leftover player-control from the prior down so a catch cannot
            // hand WASD to the wrong unit (especially after a player kick → AI S).
            ClearKickoffControlFlags();

            // Hide endzone goalposts during kick aim so they cannot read as the tee origin.
            RetroLookApplier.SetGoalpostsVisible(false);

            StatBridge.ApplyKickoffSides(receiverIsPlayer);

            // Frame midfield (kicker + tee on left, both walls on right).
            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null)
                {
                    if (ball != null)
                        cc.SetTarget(ball.transform);
                    cc.SnapToYard(KickoffCameraYardAbs);
                    // Snap orthographic size immediately — Lerp left the tee clipped for a beat.
                    if (cam.orthographic)
                        cam.orthographicSize = cc.kickoffOrthoSize;
                }
                else
                {
                    cam.transform.position = new Vector3(
                        YardToWorldX(KickoffCameraYardAbs),
                        cam.transform.position.y,
                        cam.transform.position.z);
                }
            }

            // No player control during pure kick aim — also clear any leftover
            // pocket/scramble state so a prior down cannot resume mid-kickoff.
            var qb = FindByNameIncludingInactive("Quarterback");
            if (qb != null)
            {
                var qbc = qb.GetComponent<QuarterbackController>();
                if (qbc != null)
                    qbc.FreezeForKickoff();
                else
                {
                    var pc = qb.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.RemoveBall();
                        pc.hasBall = false;
                        pc.SetControlled(false);
                    }
                }
            }

            // Kicker is never the controlled unit / ball-carrier during aim —
            // keep the circle active + on hashes behind the tee.
            var kicker = FindByNameIncludingInactive(kickerName);
            if (kicker != null)
            {
                kicker.SetActive(true);
                Place(kickerName, AtKickoff(KickoffKickerYardAbs, 0f));
                var pc = kicker.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.RemoveBall();
                    pc.hasBall = false;
                    pc.SetControlled(false);
                }
                var visual = kicker.transform.Find("Visual");
                if (visual != null)
                    visual.gameObject.SetActive(true);
            }

            // Ball last — restyle can race Destroy(dup); guarantee sprite on tee at Y=0.
            ball = DedupAndParkKickoffBall();
            if (FieldManager.Instance != null && ball != null)
                FieldManager.Instance.ballTransform = ball.transform;

            var ring = FindByNameIncludingInactive("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(null);
            }
        }

        /// <summary>Stack units evenly across full playable Y (top sideline ↔ bottom).</summary>
        /// <summary>
        /// After Tecmo returner select — re-seat the receive wall and put the
        /// chosen unit at the deep returner spot.
        /// </summary>
        public static void PlaceDesignatedKickReturner()
        {
            if (FieldManager.Instance == null || !FieldManager.Instance.KickoffReceiverIsPlayer)
                return;

            string returnerName = string.IsNullOrEmpty(DesignatedKickReturnerName)
                ? "RB"
                : DesignatedKickReturnerName;
            string[] receiveWall =
            {
                "Quarterback", "WR_Top", "WR_Bot", "TE",
                "OL_LT", "OL_LG", "OL_C", "OL_RG", "OL_RT"
            };

            PlaceKickoffWall(receiveWall, KickoffReceiveWallYardAbs);
            Place(returnerName, AtKickoff(KickoffReturnerYardAbs, KickoffWallHalfY * 0.55f));
            EnsureKickoffReturner(returnerName);
            EnsureKickoffReceiveWallBlockers(receiveWall, returnerName);
            if (FieldManager.Instance != null)
                StatBridge.ApplyKickoffSides(FieldManager.Instance.KickoffReceiverIsPlayer);
        }

        static void PlaceKickoffWall(string[] names, float yard)
        {
            if (names == null || names.Length == 0) return;
            float half = KickoffWallHalfY;
            int n = names.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                // Symmetric about Y=0 — do not pack into y≤0 only.
                float laneY = Mathf.Lerp(-half, half, t);
                // Slight X stagger so the wall reads as a Tecmo wedge, not a stick.
                float stagger = ((i & 1) == 0 ? -1.2f : 1.2f) * KickDir;
                Place(names[i], AtKickoff(yard + stagger, laneY));
            }
        }

        /// <summary>
        /// Deep kickoff returner must be able to run <see cref="ReceiverController"/> return
        /// logic — including opponent S when the player is kicking.
        /// </summary>
        static void EnsureKickoffReturner(string returnerName)
        {
            if (string.IsNullOrEmpty(returnerName)) return;
            var go = FindByNameIncludingInactive(returnerName);
            if (go == null) return;

            go.SetActive(true);
            if (go.GetComponent<ReceiverController>() == null)
                go.AddComponent<ReceiverController>();
            if (go.GetComponent<StaminaSprint>() == null)
                go.AddComponent<StaminaSprint>();
            PlayerStun.GetOrAdd(go);

            var rc = go.GetComponent<ReceiverController>();
            if (rc != null)
            {
                rc.hasBall = false;
                rc.SetPlayerControlled(false);
                rc.isRunningRoute = false;
                rc.ClearTackleState();
            }
        }

        /// <summary>No unit starts a kickoff still marked player-controlled from the last snap.</summary>
        static void ClearKickoffControlFlags()
        {
            foreach (var name in AllUnitNames())
            {
                var go = FindByNameIncludingInactive(name);
                if (go == null) continue;

                var rc = go.GetComponent<ReceiverController>();
                if (rc != null)
                {
                    rc.hasBall = false;
                    rc.SetPlayerControlled(false);
                    rc.isRunningRoute = false;
                    rc.ClearTackleState();
                }

                var ai = go.GetComponent<DefenderAI>();
                if (ai != null)
                    ai.IsPlayerControlled = false;

                var pc = go.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.RemoveBall();
                    pc.hasBall = false;
                    pc.SetControlled(false);
                }

                var qbc = go.GetComponent<QuarterbackController>();
                if (qbc != null)
                    qbc.FreezeForKickoff();
            }

            if (DefensePlayDirector.Instance != null)
                DefensePlayDirector.SetAiOffensePossession(false);
            if (PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.Cancel();
        }

        /// <summary>
        /// Receive-wall units get OffensiveBlocker so they peel/soft-hold coverage on the return.
        /// Permanent OL/TE keep blockers; WR/QB get temporary kickoffWallOnly components.
        /// </summary>
        static void EnsureKickoffReceiveWallBlockers(string[] receiveWall, string returnerName)
        {
            if (receiveWall == null) return;
            foreach (var name in receiveWall)
            {
                if (string.IsNullOrEmpty(name) || name == returnerName) continue;
                var go = FindByNameIncludingInactive(name);
                if (go == null) continue;
                // Opponent receive wall is DefenderAI — leave coverage/return AI alone for now.
                if (go.GetComponent<DefenderAI>() != null) continue;

                var blocker = go.GetComponent<OffensiveBlocker>();
                if (blocker == null)
                    blocker = go.AddComponent<OffensiveBlocker>();

                bool permanent =
                    System.Array.IndexOf(OffensiveLine, name) >= 0
                    || name == "TE"
                    || name.StartsWith("TE");
                blocker.kickoffWallOnly = !permanent;
                blocker.isTightEnd = name == "TE" || name.StartsWith("TE");
                blocker.ResetForNextPlay();
                PlayerStun.GetOrAdd(go);
            }
        }

        // Extra point / FG: tee on hashes, kicker a few yards behind, uprights visible.
        public const float ExtraPointTeeYard = 85f;
        public const float UprightKickerBehindYards = 4f;
        /// <summary>Frame tee + uprights (posts on EZ back / world X ≈ ±55.4).</summary>
        public const float UprightKickCameraLeadYards = 10f;

        /// <summary>
        /// Kicker for PAT / FG. Player try → QB; AI try → S (AI usually auto-resolves XP).
        /// </summary>
        public static string UprightKickerName()
        {
            bool playerKicks = FieldManager.Instance == null
                               || FieldManager.Instance.isPlayerPossession;
            return playerKicks ? "Quarterback" : "S";
        }

        public static Transform FindUprightKicker()
            => FindByNameIncludingInactive(UprightKickerName())?.transform;

        /// <summary>PAT tee at the XP hash (typically yard 85 / 15-yard attempt).</summary>
        public static void PlaceExtraPointKick()
        {
            float tee = FieldManager.Instance != null
                ? FieldManager.Instance.currentYardLine
                : ExtraPointTeeYard;
            PlaceUprightKick(tee);
        }

        /// <summary>Field-goal tee at current LOS.</summary>
        public static void PlaceFieldGoalKick()
        {
            float tee = FieldManager.Instance != null
                ? FieldManager.Instance.currentYardLine
                : CurrentLosYard();
            PlaceUprightKick(tee);
        }

        /// <summary>
        /// Sparse FG/XP look: isolated kicker behind the tee, ball on hashes, uprights on.
        /// </summary>
        public static void PlaceUprightKick(float teeYard)
        {
            DedupAndReactivateRoster();
            EnsureAndPlace(teeYard);

            string kickerName = UprightKickerName();
            foreach (var name in AllUnitNames())
            {
                if (name == kickerName) continue;
                var go = FindByNameIncludingInactive(name);
                if (go != null) go.SetActive(false);
            }

            int dir = DriveDir;
            float kickerYard = teeYard - UprightKickerBehindYards * dir;
            Place(kickerName, At(kickerYard, 0f));

            var qb = FindByNameIncludingInactive("Quarterback");
            if (qb != null)
            {
                var qbc = qb.GetComponent<QuarterbackController>();
                if (qbc != null)
                    qbc.FreezeForKickoff();
                else
                {
                    var pc = qb.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.RemoveBall();
                        pc.hasBall = false;
                        pc.SetControlled(false);
                    }
                }
            }

            var kicker = FindByNameIncludingInactive(kickerName);
            if (kicker != null)
            {
                kicker.SetActive(true);
                Place(kickerName, At(kickerYard, 0f));
                var pc = kicker.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.RemoveBall();
                    pc.hasBall = false;
                    pc.SetControlled(false);
                }
                var visual = kicker.transform.Find("Visual");
                if (visual != null)
                    visual.gameObject.SetActive(true);
            }

            var ball = DedupAndParkBallAt(teeYard);
            if (FieldManager.Instance != null && ball != null)
                FieldManager.Instance.ballTransform = ball.transform;

            // Player kicks → white QB circle (receiverIsPlayer=false in kickoff restyle).
            bool playerKicks = FieldManager.Instance == null
                               || FieldManager.Instance.isPlayerPossession;
            RetroLookApplier.RestyleKickoffRoster(!playerKicks, kickerName);
            RetroLookApplier.SetGoalpostsVisible(true);

            ball = DedupAndParkBallAt(teeYard);
            if (FieldManager.Instance != null && ball != null)
                FieldManager.Instance.ballTransform = ball.transform;

            float camYard = teeYard + UprightKickCameraLeadYards * dir;
            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null)
                {
                    if (ball != null)
                        cc.SetTarget(ball.transform);
                    cc.SnapToYard(camYard);
                }
                else
                {
                    cam.transform.position = new Vector3(
                        YardToWorldX(camYard),
                        cam.transform.position.y,
                        cam.transform.position.z);
                }
            }

            var ring = FindByNameIncludingInactive("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(null);
            }
        }

        /// <summary>Keep a single parked football on the tee; kill duplicate brown meshes.</summary>
        static GameObject DedupAndParkKickoffBall()
            => DedupAndParkBallAt(KickoffTeeYardAbs);

        static GameObject DedupAndParkBallAt(float teeYard)
        {
            Vector3 tee = At(teeYard, 0f);
            var ball = FindFootballIncludingInactive();
            if (ball == null)
            {
                RetroLookApplier.RestyleFootball();
                ball = FindFootballIncludingInactive();
            }

            if (ball != null)
            {
                ball.SetActive(true);
                ball.transform.localScale = Vector3.one;
                var fb = ball.GetComponent<FootballBehavior>();
                if (fb != null)
                    fb.ResetToParked(tee);
                else
                    ball.transform.position = tee;
            }

            RetroLookApplier.RestyleFootball();
            return ball != null ? ball : FindFootballIncludingInactive();
        }

        static float YardToWorldX(float yard)
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.YardToWorldX(yard);
            return yard - 50f;
        }

        /// <summary>
        /// Training Facility drill: keep QB + one skill target + one coverage defender.
        /// Everyone else is hidden. Routes applied for the selected play.
        /// </summary>
        public static void ApplyTrainingMatchup(string skillUnit, string coverageUnit, OffensivePlay play)
        {
            float los = CurrentLosYard();
            PlaceOnly(los);

            var keep = new System.Collections.Generic.HashSet<string>
            {
                "Quarterback",
                skillUnit ?? "",
                coverageUnit ?? ""
            };

            foreach (var name in AllUnitNames())
            {
                if (keep.Contains(name)) continue;
                foreach (var go in FindAllByNameIncludingInactive(name))
                {
                    if (go != null) go.SetActive(false);
                }
            }

            // Ensure drill units are on.
            foreach (var name in keep)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var go = FindByNameIncludingInactive(name);
                if (go != null) go.SetActive(true);
            }

            if (play != null)
            {
                Playbook.Select(play);
                Playbook.ApplyRoutesToFormation();
            }

            // Park selection ring on QB for pass drills, RB for runs.
            var ring = FindByNameIncludingInactive("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                var followName = play != null && play.Type == OffensivePlayType.Run
                    ? skillUnit
                    : "Quarterback";
                var follow = FindByNameIncludingInactive(followName);
                if (sr != null && follow != null)
                    sr.SetFollow(follow.transform);
            }
        }

        /// <summary>Coverage partner for a training skill target.</summary>
        public static string CoverageForSkill(string skillUnit, bool isRun)
        {
            if (isRun || skillUnit == "RB")
                return "LB_1";
            if (skillUnit == "WR_Top") return "CB_Top";
            if (skillUnit == "WR_Bot") return "CB_Bot";
            if (skillUnit == "TE") return "LB_2";
            return "CB_Top";
        }

        static System.Collections.Generic.IEnumerable<string> AllUnitNames()
        {
            yield return "Quarterback";
            foreach (var n in OffensiveLine) yield return n;
            foreach (var n in SkillOffense) yield return n;
            foreach (var n in DefensiveLine) yield return n;
            foreach (var n in Linebackers) yield return n;
            foreach (var n in Secondary) yield return n;
        }

        static void ResetSkillPlayersForNextPlay()
        {
            // Also ensure blockers exist on already-spawned OL / TE (hot reload / old scenes).
            foreach (var name in OffensiveLine)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                if (go.GetComponent<OffensiveBlocker>() == null)
                    go.AddComponent<OffensiveBlocker>();
                PlayerStun.GetOrAdd(go);
            }
            var te = GameObject.Find("TE");
            if (te != null)
            {
                if (te.GetComponent<OffensiveBlocker>() == null)
                {
                    var b = te.AddComponent<OffensiveBlocker>();
                    b.isTightEnd = true;
                }
                PlayerStun.GetOrAdd(te);
            }

            foreach (var name in SkillOffense)
            {
                var go = GameObject.Find(name);
                if (go == null)
                    go = FindByNameIncludingInactive(name);
                if (go == null) continue;

                try { go.tag = "Receiver"; }
                catch (UnityException) { }

                // Drop any stuck ball visuals under this receiver.
                DetachChildFootballs(go.transform);

                var blocker = go.GetComponent<OffensiveBlocker>();
                if (blocker != null)
                    blocker.ResetForNextPlay();

                var rc = go.GetComponent<ReceiverController>();
                if (rc != null)
                {
                    rc.hasBall = false;
                    rc.SetPlayerControlled(false);
                    rc.isRunningRoute = false;
                    rc.ResetPassAdjust();
                    rc.ClearTackleState();
                }
                var stam = go.GetComponent<StaminaSprint>();
                if (stam == null) stam = go.AddComponent<StaminaSprint>();
                stam.ResetStamina();
            }

            // Reset OL blockers.
            GameObject[] line;
            try { line = GameObject.FindGameObjectsWithTag("Lineman"); }
            catch { line = System.Array.Empty<GameObject>(); }
            foreach (var go in line)
            {
                if (go == null) continue;
                var blocker = go.GetComponent<OffensiveBlocker>();
                if (blocker != null)
                    blocker.ResetForNextPlay();
            }

            var qb = GameObject.Find("Quarterback");
            if (qb != null)
            {
                // Player is a built-in tag — only assign when needed (avoids TagManager noise).
                if (!qb.CompareTag("Player"))
                {
                    try { qb.tag = "Player"; }
                    catch (UnityException) { }
                }
                DetachChildFootballs(qb.transform);
                var pc = qb.GetComponent<PlayerController>();
                bool playerOffense = FieldManager.Instance == null
                                     || FieldManager.Instance.isPlayerPossession;
                if (pc != null)
                {
                    pc.RemoveBall();
                    pc.hasBall = true;
                    pc.SetControlled(playerOffense);
                }
                var qbc = qb.GetComponent<QuarterbackController>();
                if (qbc != null)
                    qbc.ResetPlayState();
                var stam = qb.GetComponent<StaminaSprint>();
                if (stam == null) stam = qb.AddComponent<StaminaSprint>();
                stam.ResetStamina();
            }

            // Clear player-control flags on all defenders between plays.
            GameObject[] defenders;
            try { defenders = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { defenders = System.Array.Empty<GameObject>(); }
            foreach (var d in defenders)
            {
                if (d == null) continue;
                var ai = d.GetComponent<DefenderAI>();
                if (ai != null) ai.IsPlayerControlled = false;
                // S may carry a ReceiverController for AI kickoff returns — clear carrier state.
                var rc = d.GetComponent<ReceiverController>();
                if (rc != null)
                {
                    rc.hasBall = false;
                    rc.SetPlayerControlled(false);
                    rc.isRunningRoute = false;
                    rc.ClearTackleState();
                }
                try { d.tag = "Defender"; }
                catch (UnityException) { }
                PlayerStun.GetOrAdd(d);
                var stun = d.GetComponent<PlayerStun>();
                if (stun != null) stun.Clear();
                var stam = d.GetComponent<StaminaSprint>();
                if (stam == null) stam = d.AddComponent<StaminaSprint>();
                stam.ResetStamina();
            }

            bool onDefense = FieldManager.Instance != null
                             && !FieldManager.Instance.isPlayerPossession;
            // Baseline Retro Bowl: defense is sim — watch the AI offense (QB), not a CB.
            bool watchAiOffense = onDefense && !GameRules.EnablePlayerDefense;

            var ring = GameObject.Find("SelectionRing");
            var cam = Camera.main;
            if (onDefense && !watchAiOffense)
            {
                // Player defense — keep Q/E selection. PlaceOnly runs every pre-snap
                // LateUpdate via RetroLookApplier; never stomp ring/camera back to CB_Top.
                Transform follow = null;
                if (PlayerDefenseController.Instance != null
                    && PlayerDefenseController.Instance.IsActive
                    && PlayerDefenseController.Instance.ControlledUnit != null)
                {
                    follow = PlayerDefenseController.Instance.ControlledUnit;
                }
                else
                {
                    var cb = GameObject.Find("CB_Top");
                    if (cb != null) follow = cb.transform;
                }

                if (ring != null && follow != null)
                {
                    var sr = ring.GetComponent<SelectionRing>();
                    if (sr != null) sr.SetFollow(follow);
                }
                if (cam != null && follow != null)
                {
                    var cc = cam.GetComponent<CameraController>();
                    if (cc != null) cc.SetTarget(follow);
                }
            }
            else
            {
                // Player offense, or sim defense watching AI drive.
                if (ring != null)
                {
                    var sr = ring.GetComponent<SelectionRing>();
                    if (sr != null)
                        sr.SetFollow(watchAiOffense ? null : (qb != null ? qb.transform : null));
                }

                if (cam != null && qb != null)
                {
                    var cc = cam.GetComponent<CameraController>();
                    if (cc != null)
                    {
                        cc.SetTarget(qb.transform);
                        cc.FollowPlayer();
                    }
                }
            }
        }

        static void DetachChildFootballs(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null) continue;
                bool isBall = child.CompareTag("Football")
                              || child.GetComponent<FootballBehavior>() != null
                              || child.name.Contains("Football");
                if (!isBall) continue;
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        /// <summary>Destroy thrown/caught balls and park one clean football on the LOS.</summary>
        static void ResetFootballForNextPlay(float losYard)
        {
            Vector3 parkPos = At(losYard, 0.08f);
            FootballBehavior keep = null;

            var all = UnityEngine.Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Include);
            foreach (var fb in all)
            {
                if (fb == null) continue;
                // Skip prefab assets.
                if (!fb.gameObject.scene.IsValid()) continue;

                if (fb.ShouldClearBetweenPlays || keep != null)
                {
                    UnityEngine.Object.Destroy(fb.gameObject);
                    continue;
                }

                keep = fb;
            }

            // Any leftover Football-tagged objects without a clean parked instance.
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag("Football"); }
            catch { tagged = System.Array.Empty<GameObject>(); }

            foreach (var go in tagged)
            {
                if (go == null) continue;
                if (keep != null && go == keep.gameObject) continue;
                var fb = go.GetComponent<FootballBehavior>();
                if (fb == keep) continue;
                // Destroy extras / orphans still in the scene.
                if (keep == null && fb != null && !fb.ShouldClearBetweenPlays)
                {
                    keep = fb;
                    continue;
                }
                UnityEngine.Object.Destroy(go);
            }

            if (keep == null)
            {
                var prefab = Resources.Load<GameObject>("Football");
                GameObject go;
                if (prefab != null)
                    go = UnityEngine.Object.Instantiate(prefab);
                else
                {
                    go = new GameObject("Football");
                    try { go.tag = "Football"; }
                    catch (UnityException) { }
                    go.AddComponent<FootballBehavior>();
                }

                keep = go.GetComponent<FootballBehavior>();
                if (keep == null)
                    keep = go.AddComponent<FootballBehavior>();
            }

            if (keep != null)
            {
                keep.ResetToParked(parkPos);
                RetroLookApplier.RestyleFootball();
            }
        }

        static GameObject FindByNameIncludingInactive(string name)
        {
            foreach (var go in FindAllByNameIncludingInactive(name))
                return go;
            return null;
        }

        static System.Collections.Generic.List<GameObject> FindAllByNameIncludingInactive(string name)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in all)
            {
                if (t == null || t.name != name) continue;
                if (t.hideFlags != HideFlags.None) continue;
                if (!t.gameObject.scene.IsValid()) continue; // prefab asset
                list.Add(t.gameObject);
            }
            return list;
        }

        /// <summary>
        /// After HideForKickoffIntro, reactivate the styled roster and drop any
        /// unstyled duplicates created when GameObject.Find missed inactive units.
        /// </summary>
        static void DedupAndReactivateRoster()
        {
            foreach (var name in AllUnitNames())
            {
                var copies = FindAllByNameIncludingInactive(name);
                if (copies.Count == 0) continue;

                GameObject keep = PickBestUnitInstance(copies);
                foreach (var go in copies)
                {
                    if (go == keep) continue;
                    // Rename so same-frame Find(name) cannot pick a pending Destroy.
                    go.name = name + "_Dup";
                    go.SetActive(false);
                    Object.Destroy(go);
                }

                if (keep != null)
                    keep.SetActive(true);
            }
        }

        static GameObject PickBestUnitInstance(System.Collections.Generic.List<GameObject> copies)
        {
            GameObject best = null;
            int bestScore = int.MinValue;
            foreach (var go in copies)
            {
                if (go == null) continue;
                int score = 0;
                var visual = go.transform.Find("Visual");
                if (visual != null)
                {
                    score += 2;
                    var sr = visual.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) score += 8;
                }
                if (go.GetComponent<ReceiverController>() != null
                    || go.GetComponent<DefenderAI>() != null
                    || go.GetComponent<PlayerController>() != null
                    || go.GetComponent<OffensiveBlocker>() != null)
                    score += 3;
                if (go.activeInHierarchy) score += 1;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = go;
                }
            }
            return best ?? copies[0];
        }

        static GameObject FindFootballIncludingInactive()
        {
            var all = FindAllFootballsIncludingInactive();
            if (all.Count == 0) return null;

            GameObject keep = null;
            foreach (var go in all)
            {
                if (go == null) continue;
                var fb = go.GetComponent<FootballBehavior>();
                if (fb != null && !fb.ShouldClearBetweenPlays)
                {
                    keep = go;
                    break;
                }
                if (keep == null) keep = go;
            }

            foreach (var go in all)
            {
                if (go == null || go == keep) continue;
                // Hide immediately — Destroy is end-of-frame; brown mesh dups otherwise stack on the tee.
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
                go.name = "Football_Dup";
                go.SetActive(false);
                Object.Destroy(go);
            }

            return keep;
        }

        static System.Collections.Generic.List<GameObject> FindAllFootballsIncludingInactive()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var go in FindAllByNameIncludingInactive("Football"))
                list.Add(go);

            try
            {
                foreach (var fb in Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Include))
                {
                    if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                    if (!list.Contains(fb.gameObject))
                        list.Add(fb.gameObject);
                }
            }
            catch (UnityException) { /* tag / type issues */ }

            return list;
        }

        static Transform GetOrCreateRoot()
        {
            var go = GameObject.Find("Roster");
            if (go == null) go = new GameObject("Roster");
            return go.transform;
        }

        static void EnsureOffense(Transform root)
        {
            EnsureUnit(root, "Quarterback", "Player", typeof(PlayerController), typeof(QuarterbackController), typeof(StaminaSprint));
            foreach (var name in OffensiveLine)
                EnsureUnit(root, name, "Lineman", typeof(OffensiveBlocker));
            EnsureUnit(root, "WR_Top", "Receiver", typeof(ReceiverController), typeof(StaminaSprint));
            EnsureUnit(root, "WR_Bot", "Receiver", typeof(ReceiverController), typeof(StaminaSprint));
            EnsureUnit(root, "TE", "Receiver", typeof(ReceiverController), typeof(OffensiveBlocker), typeof(StaminaSprint));
            EnsureUnit(root, "RB", "Receiver", typeof(ReceiverController), typeof(StaminaSprint));
        }

        static void EnsureDefense(Transform root)
        {
            foreach (var name in DefensiveLine)
                EnsureUnit(root, name, "Defender", typeof(DefenderAI));
            foreach (var name in Linebackers)
                EnsureUnit(root, name, "Defender", typeof(DefenderAI));
            foreach (var name in Secondary)
                EnsureUnit(root, name, "Defender", typeof(DefenderAI));
        }

        static GameObject EnsureUnit(Transform root, string name, string tag, params System.Type[] components)
        {
            // Must see inactive units (kickoff intro hides the roster).
            var go = FindByNameIncludingInactive(name);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(root, true);
            }
            else if (go.transform.parent == null && root != null)
            {
                go.transform.SetParent(root, true);
            }

            try { go.tag = tag; }
            catch (UnityException)
            {
                Debug.LogWarning($"[Roster] Tag '{tag}' missing — add it in Tags & Layers.");
            }

            go.SetActive(true);

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            ArcadeMove.ConfigureKinematicBody(body);

            // Top-down 2D: spheres (not Y-capsules). Capsule gizmos read as a
            // white U/bracket around the OL when Game-view Gizmos are on.
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
                Object.Destroy(capsule);

            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = BodyRadius;
            col.center = Vector3.zero;

            if (go.transform.Find("Visual") == null)
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform, false);
                visual.AddComponent<SpriteRenderer>();
            }

            foreach (var t in components)
            {
                if (go.GetComponent(t) == null)
                    go.AddComponent(t);
            }

            var pc = go.GetComponent<PlayerController>();
            if (pc != null)
                pc.SetControlled(tag == "Player");

            return go;
        }

        static void PlaceOffense(float los)
        {
            float g = OlGap;
            float band = PlayBandHalf;
            // Offense behind LOS opposite drive direction (dir +1 → smaller X).
            float dir = DriveDir;

            // 5 OL — tight vertical stack, noses on the LOS (hashes).
            float[] olY = { -g * 2f, -g, 0f, g, g * 2f };
            for (int i = 0; i < OffensiveLine.Length; i++)
                Place(OffensiveLine[i], At(los - LineNose * dir, olY[i]));

            // WRs wide but inside the green (see PlayBandHalf vs SidelineY).
            Place("WR_Top", At(los - 0.3f * dir, band));
            Place("WR_Bot", At(los - 0.3f * dir, -band));
            // Slot/TE just outside the RT, still in the box.
            Place("TE", At(los - 0.15f * dir, g * 2f + 0.85f));

            // Backfield ~3–4 yards behind the LOS.
            Place("Quarterback", At(los - 3.2f * dir, 0.15f));
            Place("RB", At(los - 4.0f * dir, -0.85f));

            var ball = FindFootballIncludingInactive();
            if (ball != null)
            {
                ball.SetActive(true);
                ball.transform.position = At(los, 0.08f);
            }
        }

        static void PlaceDefense(float los)
        {
            float g = OlGap;
            float band = PlayBandHalf;
            float dir = DriveDir;
            // Defense ahead of LOS in drive direction.
            float dLos = los + LineNose * dir;

            // DL stacked opposite the OL.
            float[] dlY = { -g * 1.6f, -g * 0.55f, g * 0.55f, g * 1.6f };
            for (int i = 0; i < DefensiveLine.Length; i++)
                Place(DefensiveLine[i], At(dLos, dlY[i]));

            // LBs a couple yards off the ball.
            Place("LB_1", At(dLos + 2.2f * dir, 1.1f));
            Place("LB_2", At(dLos + 2.2f * dir, -1.1f));

            // CBs shade the WRs (slightly inside so they stay on green too).
            float cbBand = band - 0.2f;
            Place("CB_Top", At(dLos + 1.0f * dir, cbBand));
            Place("CB_Bot", At(dLos + 1.0f * dir, -cbBand));
            Place("S", At(dLos + 4.5f * dir, 0.2f));
        }

        static void WireQuarterback()
        {
            var qb = FindByNameIncludingInactive("Quarterback");
            if (qb == null) return;
            qb.SetActive(true);

            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc == null) return;

            if (qbc.ballPrefab == null)
            {
                var res = Resources.Load<GameObject>("Football");
                if (res != null) qbc.ballPrefab = res;
                else
                {
                    var sceneBall = GameObject.FindGameObjectWithTag("Football");
                    if (sceneBall != null) qbc.ballPrefab = sceneBall;
                }
            }

            var list = new System.Collections.Generic.List<Transform>();
            foreach (var n in SkillOffense)
            {
                var go = GameObject.Find(n);
                if (go != null) list.Add(go.transform);
            }
            qbc.receivers = list.ToArray();
        }

        static void HideLegacyExtras()
        {
            var keep = new System.Collections.Generic.HashSet<string> { "Quarterback" };
            foreach (var n in OffensiveLine) keep.Add(n);
            foreach (var n in SkillOffense) keep.Add(n);
            foreach (var n in DefensiveLine) keep.Add(n);
            foreach (var n in Linebackers) keep.Add(n);
            foreach (var n in Secondary) keep.Add(n);

            foreach (var tag in new[] { "Player", "Receiver", "Defender", "Lineman" })
            {
                GameObject[] found;
                try { found = GameObject.FindGameObjectsWithTag(tag); }
                catch { continue; }

                foreach (var go in found)
                {
                    if (go == null) continue;
                    if (!keep.Contains(go.name))
                        go.SetActive(false);
                }
            }
        }

        static void Place(string name, Vector3 pos)
        {
            var go = FindByNameIncludingInactive(name);
            if (go == null) return;
            go.SetActive(true);
            // Caller already clamped (At / AtKickoff) — do not re-clamp to PlayBandHalf.
            pos.z = 0f;
            go.transform.position = pos;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                // Kinematic bodies — never set linear/angular velocity (Unity warns).
                ArcadeMove.ConfigureKinematicBody(rb);
                ArcadeMove.Apply(rb, Vector3.zero);
                rb.position = pos;
            }
        }

        /// <summary>World X from yard line; Y clamped inside the playable apron.</summary>
        static Vector3 At(float yard, float laneY)
            => ClampLane(new Vector3(YardToWorldX(yard), laneY, 0f));

        /// <summary>Kickoff placement — full sideline Y (not the narrower hash play-band).</summary>
        static Vector3 AtKickoff(float yard, float laneY)
            => ClampKickoffLane(new Vector3(YardToWorldX(yard), laneY, 0f));

        static Vector3 ClampLane(Vector3 pos)
        {
            // Normal downs: keep skill players inside the hash play-band.
            float limit = RetroLookApplier.SidelineY - BodyRadius - 0.2f;
            limit = Mathf.Min(limit, PlayBandHalf);
            pos.y = Mathf.Clamp(pos.y, -limit, limit);
            pos.z = 0f;
            return pos;
        }

        static Vector3 ClampKickoffLane(Vector3 pos)
        {
            float limit = RetroLookApplier.SidelineY - BodyRadius - 0.2f;
            pos.y = Mathf.Clamp(pos.y, -limit, limit);
            pos.z = 0f;
            return pos;
        }
    }
}
