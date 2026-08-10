using UnityEngine;
using UnityEngine.SceneManagement;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Applies Retro Bowl reference placeholders in true 2D:
    /// X = downfield (left→right), Y = across field (up on screen).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RetroLookApplier : MonoBehaviour
    {
        [SerializeField] bool applyOnAwake = true;

        // Field.png is 1300×237. White sideline bands sit at texture rows
        // ~30–32 and ~202–204 (PIL top→bottom); inner-edge span = 170px.
        // Keep width 120 so sideline / yard-line proportions match the Retro look.
        // Goal lines are px 150–1150 (1000px playable) → world ±PlayableHalfWidth;
        // FieldManager yardLength is synced to that so EZ paint does not spill past yard 0/100.
        const float FieldWorldWidth = 120f;
        const float FieldPixelWidth = 1300f;
        const float FieldPlayablePixelWidth = 1000f; // goal line → goal line
        /// <summary>World |X| of painted goal lines with <see cref="FieldWorldWidth"/>.</summary>
        public static float PlayableHalfWidth =>
            FieldWorldWidth * 0.5f * (FieldPlayablePixelWidth / FieldPixelWidth);
        /// <summary>
        /// Field.png blue endzone outer edge inset from texture edge (px 50 / 1250).
        /// Goalposts sit on this back line (not mid-EZ).
        /// </summary>
        const float FieldEndzoneBackInsetPx = 50f;
        /// <summary>World |X| of the endzone back line / goalpost base.</summary>
        public static float GoalpostWorldXAbs =>
            FieldWorldWidth * 0.5f
            - FieldWorldWidth * (FieldEndzoneBackInsetPx / FieldPixelWidth);
        /// <summary>
        /// Fraction of Field.png height from inner white sideline to inner white sideline.
        /// Measured: 170 / 237. Wrong values shift the art vs OOB (was 0.82 → chalk at ~±5.12).
        /// </summary>
        const float FieldPlayableFraction = 170f / 237f;
        /// <summary>
        /// Playable-band UV center on Field.png (slightly above 0.5). Used to Y-shift the
        /// sprite so both baked sidelines land on ±SidelineY.
        /// </summary>
        const float FieldPlayableCenterUv = ((204.5f / 237f) + (34.5f / 237f)) * 0.5f;
        // Classic Retro Bowl play framing — field fills the view.
        const float CamOrtho = 7.0f;
        /// <summary>WR split — near the yard numbers, inset from SidelineY.</summary>
        public const float PlayBandHalf = FormationRoster.PlayBandHalf;
        /// <summary>
        /// Single source of truth: white sideline Y and OOB threshold.
        /// Field art, procedural chalk, FieldManager, and carrier OOB all use this.
        /// </summary>
        public const float SidelineY = 5.85f;

        void Awake()
        {
            if (applyOnAwake)
                Apply();
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        void Start()
        {
            // FieldManager may not exist yet during Awake (−100); re-sync bounds here.
            SyncFieldManagerBounds();
            RefreshReceiverRoutes();
        }

        void LateUpdate()
        {
            if (GameManager.Instance == null) return;

            // Lock formation on menu and during play-call (pre-snap).
            // Never stomp kickoff intro (empty field), kick mini-game return look,
            // or end-of-play banners.
            bool kickoffOrBanner = GameManager.Instance.isKicking
                                   || GameManager.Instance.isKickoffReturn
                                   || GameManager.Instance.isInterceptionReturn
                                   || GameManager.Instance.waitingForNextPlay
                                   || (FieldManager.Instance != null
                                       && FieldManager.Instance.PendingKickoff);
            bool lockFormation = !kickoffOrBanner
                                 && (GameManager.Instance.currentState != GameState.Playing
                                     || GameManager.Instance.isPreSnap);
            if (lockFormation)
            {
                float los = FormationRoster.CurrentLosYard();
                FormationRoster.PlaceOnly(los);
            }

            // Always keep LOS (blue) + first-down (yellow) markers in sync.
            ApplyDownMarkers();
        }

        [ContextMenu("Apply Retro Look Now")]
        public void Apply()
        {
            // Lite Aavegotchi skins: P1 = USDC, P2 = UNI (stable across offense/defense).
            // Falls back to colored circles if Resources/Gotchi is missing.
            GotchiTeamSprites.InvalidateCache();
            var p1 = GotchiTeamSprites.PlayerOne
                     ?? MakeCircleSprite(new Color(0.2f, 0.45f, 1f, 1f));
            var p2 = GotchiTeamSprites.PlayerTwo
                     ?? MakeCircleSprite(new Color(0.95f, 0.2f, 0.2f, 1f));
            // Current offense / defense roles wear P1 or P2 based on possession.
            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            var offenseSprite = playerOffense ? p1 : p2;
            var defenseSprite = playerOffense ? p2 : p1;
            var qb = offenseSprite;
            var ballSprite = LoadSprite("Retro/Football", new Vector2(0.5f, 0.5f));
            var ringSprite = LoadSprite("Retro/SelectionRing", new Vector2(0.5f, 0.5f));
            bool trainingLook = IsTrainingFacilityLook();
            var fieldSprite = trainingLook
                ? (LoadSprite("Retro/TrainingField", new Vector2(0.5f, 0.5f))
                   ?? LoadSprite("Retro/Field", new Vector2(0.5f, 0.5f)))
                : LoadSprite("Retro/Field", new Vector2(0.5f, 0.5f));
            var goalpostSprite = LoadSprite("Retro/Goalpost", new Vector2(0.2f, 0f));
            var goalpostFlipSprite = LoadSprite("Retro/GoalpostFlip", new Vector2(0.8f, 0f));

            if (fieldSprite == null)
            {
                Debug.LogError("[RetroLook] Missing Resources/Retro/Field.");
                return;
            }

            float los = FormationRoster.CurrentLosYard();
            FormationRoster.EnsureAndPlace(los);

            ApplyField2D(fieldSprite);
            if (trainingLook)
                ApplyTrainingFacilityDressing();
            else
                ApplyCrowdAndSidelines2D();
            if (!trainingLook)
                ApplyGoalposts(goalpostSprite, goalpostFlipSprite);
            else
                HideGoalposts();
            ApplyDownMarkers();
            SyncFieldManagerBounds();
            RestyleTagged("Player", qb);
            RestyleTagged("Receiver", offenseSprite);
            RestyleTagged("Lineman", offenseSprite);
            RestyleTagged("Defender", defenseSprite);
            lastSkinnedPlayerOffense = playerOffense;
            DisablePlayerControlOnTag("Receiver");
            DisablePlayerControlOnTag("Defender");
            DisablePlayerControlOnTag("Lineman");
            RestyleBall(ballSprite);
            EnsureSelectionRing(ringSprite);
            ApplyCamera2D();

            Debug.Log("[RetroLook] Formation fitted to field hash band at yard " + los
                      + $" (SidelineY={SidelineY}, PlayBandHalf={PlayBandHalf}"
                      + $", P1={(playerOffense ? "offense" : "defense")} USDC"
                      + (trainingLook ? ", TRAINING FIELD" : "") + ")");
        }

        static bool? lastSkinnedPlayerOffense;

        /// <summary>
        /// Re-tint roster so P1 stays USDC and P2 stays UNI after possession flips.
        /// Safe to call every pre-snap PlaceOnly — no-ops when possession is unchanged.
        /// </summary>
        public static void RestyleUnitsForPossession()
        {
            if (IsKickoffPresentation())
                return;

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            if (lastSkinnedPlayerOffense == playerOffense)
                return;
            lastSkinnedPlayerOffense = playerOffense;

            var p1 = GotchiTeamSprites.PlayerOne
                     ?? MakeCircleSprite(new Color(0.2f, 0.45f, 1f, 1f));
            var p2 = GotchiTeamSprites.PlayerTwo
                     ?? MakeCircleSprite(new Color(0.95f, 0.2f, 0.2f, 1f));
            var offenseSprite = playerOffense ? p1 : p2;
            var defenseSprite = playerOffense ? p2 : p1;

            RestyleTagged("Player", offenseSprite);
            RestyleTagged("Receiver", offenseSprite);
            RestyleTagged("Lineman", offenseSprite);
            RestyleTagged("Defender", defenseSprite);
        }

        /// <summary>True for Training Facility / tutorial practice — use teal QB-mode field.</summary>
        public static bool IsTrainingFacilityLook()
        {
            return PracticeMode.IsTrainingFacility
                   || PracticeMode.IsTrainingFacilityTutorial;
        }

        /// <summary>Re-apply field art after entering Training Facility mid-session.</summary>
        public static void RefreshTrainingFacilityField()
        {
            var applier = UnityEngine.Object.FindAnyObjectByType<RetroLookApplier>();
            if (applier != null)
                applier.Apply();
            else
            {
                var host = new GameObject("RetroLookApplier");
                host.AddComponent<RetroLookApplier>().Apply();
            }
        }

        /// <summary>
        /// Training Facility: no stadium crowd / yellow coaching boxes.
        /// Places Retro Bowl Training Roof glass walls along both sidelines.
        /// </summary>
        static void ApplyTrainingFacilityDressing()
        {
            foreach (var stale in new[]
                     {
                         "SidelineWhiteTop", "SidelineWhiteBot",
                         "SidelineDashGrayTop", "SidelineDashGrayBot",
                         "SidelineDashYellowTop", "SidelineDashYellowBot",
                         "Crowd2D", "CrowdNear2D", "Crowd", "CrowdNear"
                     })
            {
                var go = GameObject.Find(stale);
                if (go != null) go.SetActive(false);
            }

            // Soft green grass apron under the near sideline (screen bottom letterbox).
            float botApronH = Mathf.Max(0.35f, CamOrtho - SidelineY - 0.05f);
            PlaceSolidStrip("SidelineApronBot",
                new Vector3(0f, -SidelineY - botApronH * 0.5f - 0.06f, 0f),
                botApronH, -13, new Color(0.12f, 0.38f, 0.16f, 1f));

            // Glass walls (Training Roof) just outside the white chalk on both sidelines.
            var glass = LoadSprite("Retro/TrainingRoof", new Vector2(0.5f, 0.5f));
            if (glass != null)
            {
                const float glassHalf = 0.48f;
                PlaceTrainingGlassWall("TrainingGlassTop", glass,
                    SidelineY + glassHalf + 0.08f, flipY: false);
                PlaceTrainingGlassWall("TrainingGlassBot", glass,
                    -SidelineY - glassHalf - 0.08f, flipY: true);
            }
            else
            {
                Debug.LogWarning("[RetroLook] Missing Resources/Retro/TrainingRoof.");
            }
        }

        /// <summary>Stretch the Training Roof lattice across the field width on a sideline.</summary>
        static void PlaceTrainingGlassWall(string name, Sprite sprite, float y, bool flipY)
        {
            var go = FindOrCreate(name);
            go.SetActive(true);
            StripMeshComponents(go);
            var sr = GetOrAddSpriteRenderer(go);
            sr.sprite = sprite;
            sr.sortingOrder = -12;
            sr.color = Color.white;
            sr.flipY = flipY;
            sr.flipX = false;

            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            float nativeW = sprite.rect.width / ppu;
            float nativeH = sprite.rect.height / ppu;
            const float targetH = 0.96f;
            go.transform.SetPositionAndRotation(new Vector3(0f, y, 0f), Quaternion.identity);
            go.transform.localScale = new Vector3(
                FieldWorldWidth / Mathf.Max(0.01f, nativeW),
                targetH / Mathf.Max(0.01f, nativeH),
                1f);
        }

        static void HideGoalposts()
        {
            SetGoalpostsVisible(false);
        }

        // Ball / LOS yard. Offense stacks left, defense right — same X per unit, spread on Y.
        public const float LosYard = 40f;

        /// <summary>
        /// Absolute yard → world X. Matches FieldManager (goal lines = Field.png paint).
        /// </summary>
        public static float YardToX(float yard)
        {
            if (FieldManager.Instance != null)
                return FieldManager.Instance.YardToWorldX(yard);

            float half = PlayableHalfWidth;
            return (yard / 100f) * (half * 2f) - half;
        }

        public static Vector3 AtYard(float yard, float laneY)
            => new Vector3(YardToX(yard), laneY, 0f);

        /// <summary>
        /// Retro Bowl sticks: blue = line of scrimmage, yellow = first-down marker.
        /// </summary>
        static void ApplyDownMarkers()
        {
            float losYard = FormationRoster.CurrentLosYard();
            float fdYard = losYard + 10f;

            if (FieldManager.Instance != null)
            {
                losYard = FieldManager.Instance.currentYardLine;
                fdYard = FieldManager.Instance.FirstDownYard;
            }

            fdYard = Mathf.Clamp(fdYard, 0f, 100f);
            losYard = Mathf.Clamp(losYard, 0f, 100f);

            PlaceYardLineMarker(
                "LosLine2D",
                losYard,
                new Color(0.2f, 0.55f, 1f, 0.95f),
                0.14f);

            PlaceYardLineMarker(
                "FirstDownLine2D",
                fdYard,
                new Color(1f, 0.88f, 0.12f, 0.95f),
                0.14f);
        }

        static void PlaceYardLineMarker(string name, float yard, Color color, float thickness)
        {
            var go = FindOrCreate(name);
            go.SetActive(true);
            StripMeshComponents(go);
            var sr = GetOrAddSpriteRenderer(go);
            sr.sprite = MakeLineSprite(color);
            sr.sortingOrder = -8;
            sr.color = Color.white;

            float x = YardToX(yard);
            go.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.identity);
            // Span the playable band between the sidelines.
            go.transform.localScale = new Vector3(thickness, SidelineY * 2.05f, 1f);
        }

        static void ApplyField2D(Sprite fieldSprite)
        {
            // Hide old 3D field mesh
            var old = GameObject.Find("Field");
            if (old != null)
            {
                foreach (var r in old.GetComponentsInChildren<MeshRenderer>(true))
                    r.enabled = false;
                var col = old.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            var field = FindOrCreate("Field2D");
            StripMeshComponents(field);
            var sr = GetOrAddSpriteRenderer(field);
            sr.sprite = fieldSprite;
            sr.sortingOrder = -20;
            sr.flipX = false;
            sr.flipY = false;

            // Scale + Y-shift so Field.png's baked white sidelines land on ±SidelineY
            // (same Y as procedural chalk, FieldManager OOB, and carrier checks).
            float fieldWorldHeight = (SidelineY * 2f) / Mathf.Max(0.05f, FieldPlayableFraction);
            float fieldYOffset = (0.5f - FieldPlayableCenterUv) * fieldWorldHeight;
            float ppu = fieldSprite.pixelsPerUnit > 0 ? fieldSprite.pixelsPerUnit : 100f;
            float nativeW = fieldSprite.rect.width / ppu;
            float nativeH = fieldSprite.rect.height / ppu;
            field.transform.position = new Vector3(0f, fieldYOffset, 0f);
            field.transform.rotation = Quaternion.identity;
            field.transform.localScale = new Vector3(
                FieldWorldWidth / Mathf.Max(0.01f, nativeW),
                fieldWorldHeight / Mathf.Max(0.01f, nativeH),
                1f);

            foreach (var name in new[]
                     {
                         "YardLines", "EndZone_Home", "EndZone_Away", "NearSideline", "Crowd",
                         "SidelineDecor", "NearSideline2D", "SidelineFar2D", "SidelineNear2D",
                         "CrowdNear2D", "Crowd2D", "CrowdNear",
                         "TrainingGlassTop", "TrainingGlassBot"
                     })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }
        }

        /// <summary>
        /// Retro Bowl play framing: sideline chalk + yellow dashes + bottom apron.
        /// Crowd strip removed — it sat on top of the white sideline and cluttered the edge.
        /// </summary>
        static void ApplyCrowdAndSidelines2D()
        {
            // Field.png already paints white sideline chalk at ±SidelineY — do not
            // stack another solid white strip (reads as a thick double line).
            foreach (var stale in new[]
                     {
                         "SidelineWhiteTop", "SidelineWhiteBot",
                         "SidelineDashGrayTop", "SidelineDashGrayBot",
                         // Hide any previous crowd strips (blocking the sideline).
                         "Crowd2D", "CrowdNear2D", "Crowd", "CrowdNear"
                     })
            {
                var go = GameObject.Find(stale);
                if (go != null) go.SetActive(false);
            }

            // Thin yellow coaching-box dashes just outside the white chalk (Retro Bowl).
            PlaceDashedLine("SidelineDashYellowTop", SidelineY + 0.22f, new Color(0.95f, 0.85f, 0.15f, 1f), -10);
            PlaceDashedLine("SidelineDashYellowBot", -SidelineY - 0.22f, new Color(0.95f, 0.85f, 0.15f, 1f), -10);

            // Bottom apron only — thin green + orange pylons like Retro Bowl.
            float botApronH = Mathf.Max(0.35f, CamOrtho - SidelineY - 0.05f);
            PlaceSolidStrip("SidelineApronBot", new Vector3(0f, -SidelineY - botApronH * 0.5f - 0.06f, 0f),
                botApronH, -13, new Color(0.12f, 0.38f, 0.16f, 1f));
            PlaceBottomYardMarkers();
        }

        static void PlaceSolidStrip(string name, Vector3 pos, float height, int order, Color color)
        {
            var go = FindOrCreate(name);
            go.SetActive(true);
            StripMeshComponents(go);
            var sr = GetOrAddSpriteRenderer(go);
            sr.sprite = MakeLineSprite(color);
            sr.sortingOrder = order;
            sr.color = Color.white;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(FieldWorldWidth, Mathf.Max(0.05f, height), 1f);
        }

        static void PlaceDashedLine(string name, float y, Color color, int order)
        {
            var go = FindOrCreate(name);
            go.SetActive(true);
            StripMeshComponents(go);
            // Clear old dash children.
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(go.transform.GetChild(i).gameObject);

            go.transform.position = new Vector3(0f, y, 0f);
            float dashW = 1.1f;
            float gap = 0.55f;
            float startX = -FieldWorldWidth * 0.5f + 1f;
            float endX = FieldWorldWidth * 0.5f - 1f;
            int idx = 0;
            for (float x = startX; x < endX; x += dashW + gap)
            {
                var dash = new GameObject($"Dash_{idx++}");
                dash.transform.SetParent(go.transform, false);
                var sr = dash.AddComponent<SpriteRenderer>();
                sr.sprite = MakeLineSprite(color);
                sr.sortingOrder = order;
                dash.transform.localPosition = new Vector3(x + dashW * 0.5f, 0f, 0f);
                dash.transform.localScale = new Vector3(dashW, 0.07f, 1f);
            }
        }

        static void PlaceBottomYardMarkers()
        {
            var root = FindOrCreate("SidelineMarkersBot");
            root.SetActive(true);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.transform.GetChild(i).gameObject);

            // Orange pylons every 10 yards along the near sideline (Retro Bowl).
            for (int yard = 0; yard <= 100; yard += 10)
            {
                float x = YardToX(yard);
                var m = new GameObject($"Pylon_{yard}");
                m.transform.SetParent(root.transform, false);
                var sr = m.AddComponent<SpriteRenderer>();
                sr.sprite = MakeLineSprite(new Color(0.95f, 0.45f, 0.1f, 1f));
                sr.sortingOrder = -9;
                m.transform.position = new Vector3(x, -SidelineY - 0.22f, 0f);
                m.transform.localScale = new Vector3(0.35f, 0.22f, 1f);
            }
        }

        /// <summary>
        /// Gooseneck goalposts at each endzone (X = downfield). Left uses arm→field, right is flipped.
        /// Bases sit on the painted endzone back line (Field.png blue outer edge).
        /// </summary>
        static void ApplyGoalposts(Sprite leftPost, Sprite rightPost)
        {
            float postX = GoalpostWorldXAbs;
            PlaceGoalpost("GoalpostLeft", leftPost, new Vector3(-postX, 0f, 0f));
            PlaceGoalpost("GoalpostRight", rightPost != null ? rightPost : leftPost, new Vector3(postX, 0f, 0f));
        }

        static void PlaceGoalpost(string name, Sprite sprite, Vector3 pos)
        {
            if (sprite == null) return;

            var go = FindOrCreate(name);
            go.SetActive(true);
            StripMeshComponents(go);
            var sr = GetOrAddSpriteRenderer(go);
            sr.sprite = sprite;
            sr.sortingOrder = -5;
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);

            float ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 16f;
            float nativeH = sprite.rect.height / ppu;
            float scale = 7f / Mathf.Max(0.01f, nativeH);
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Kickoff tee sits midfield — hide endzone posts so they never read as the kick origin.
        /// </summary>
        public static void SetGoalpostsVisible(bool visible)
        {
            foreach (var name in new[] { "GoalpostLeft", "GoalpostRight" })
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t == null || t.name != name) continue;
                        if (t.hideFlags != HideFlags.None) continue;
                        if (!t.gameObject.scene.IsValid()) continue;
                        go = t.gameObject;
                        break;
                    }
                }
                if (go != null) go.SetActive(visible);
            }
        }

        static void ApplyCamera2D()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = CamOrtho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);

            // Slight upward bias keeps both sidelines framed.
            float camY = 0.35f;
            float x = YardToX(FormationRoster.CurrentLosYard());
            cam.transform.SetPositionAndRotation(new Vector3(x, camY, -10f), Quaternion.identity);

            var cc = cam.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            var follow = FindByTag("Player");
            if (follow != null) cc.SetTarget(follow.transform);
            cc.offset = new Vector3(0f, camY, -10f);
            cc.orthoSize = CamOrtho;
            cc.zoomedOrthoSize = CamOrtho * 0.82f;
            cc.minX = -50f;
            cc.maxX = 50f;
            cc.minY = -SidelineY - 0.8f;
            cc.maxY = SidelineY + 1.2f;
        }

        /// <summary>Keep FieldManager OOB / clamp and yard mapping on the painted field.</summary>
        static void SyncFieldManagerBounds()
        {
            if (FieldManager.Instance == null) return;
            FieldManager.Instance.sidelineHalf = SidelineY;
            // fieldLength stays 100 (yards). Only yardLength scales so yard 0/100
            // sit on Field.png goal lines (±PlayableHalfWidth). Do NOT set
            // fieldLength = playable world width — that broke WorldX→yard and
            // awarded TDs ~4 yards before the painted endzone.
            FieldManager.Instance.fieldLength = 100f;
            FieldManager.Instance.yardLength = (PlayableHalfWidth * 2f) / 100f;
        }

        static void RestyleTagged(string tag, Sprite sprite)
        {
            foreach (var go in FindAll(tag))
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    r.enabled = false;

                // Disable capsule mesh look; keep collider.
                var capsule = go.GetComponent<MeshFilter>();
                if (capsule != null)
                {
                    var mr = go.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }

                var visual = go.transform.Find("Visual");
                if (visual == null)
                {
                    var v = new GameObject("Visual");
                    v.transform.SetParent(go.transform, false);
                    visual = v.transform;
                }

                var sr = GetOrAddSpriteRenderer(visual.gameObject);
                sr.sprite = sprite;
                sr.flipX = false;
                // Sized to sit on the hashes like Retro Bowl sprites.
                visual.localScale = Vector3.one * FormationRoster.PlayerScale;
                visual.localPosition = Vector3.zero;
                // Depth sort applied by GotchiFacingView (lower Y → higher order).
                sr.sortingOrder = 50;

                if (visual.GetComponent<BillboardSprite>() == null)
                    visual.gameObject.AddComponent<BillboardSprite>();

                AttachGotchiFacing(visual.gameObject, go.transform, tag);

                ConfigureBody(go);

                // Top-down circles use SphereCollider (avoids Y-capsule gizmo "U" in editor).
                var oldCapsule = go.GetComponent<CapsuleCollider>();
                if (oldCapsule != null)
                    Object.Destroy(oldCapsule);

                var col = go.GetComponent<SphereCollider>();
                if (col == null) col = go.AddComponent<SphereCollider>();
                col.radius = FormationRoster.BodyRadius;
                col.center = Vector3.zero;
                col.isTrigger = true;
            }
        }

        static Sprite MakeCircleSprite(Color fill, int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            float r = (size - 1) * 0.5f;
            float rInner = r - 1.2f;
            var clear = new Color(0f, 0f, 0f, 0f);
            var outline = new Color(0f, 0f, 0f, 0.85f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= rInner)
                        tex.SetPixel(x, y, fill);
                    else if (d <= r)
                        tex.SetPixel(x, y, outline);
                    else
                        tex.SetPixel(x, y, clear);
                }
            }

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        static Sprite MakeLineSprite(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        /// <summary>
        /// Re-apply the Retro football sprite after the ball is reactivated
        /// (e.g. kickoff intro hid it and left a mesh/black circle).
        /// Also destroys duplicate Football instances so the tee never shows stacked brown meshes.
        /// </summary>
        public static void RestyleFootball()
        {
            var ballSprite = LoadSprite("Retro/Football", new Vector2(0.5f, 0.5f));
            // Tall white "U" placeholders read as a goalpost at the tee — prefer a brown oval.
            if (ballSprite == null || ballSprite.rect.height > ballSprite.rect.width * 1.15f)
                ballSprite = MakeFootballSprite();
            RestyleBall(ballSprite);
        }

        /// <summary>Procedural brown football (no external rip) — readable on the tee.</summary>
        static Sprite MakeFootballSprite()
        {
            const int w = 24;
            const int h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var brown = new Color(0.55f, 0.27f, 0.11f, 1f);
            var dark = new Color(0.35f, 0.16f, 0.05f, 1f);
            var lace = new Color(0.96f, 0.96f, 0.96f, 1f);
            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            float rx = 10.2f;
            float ry = 6.2f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float d = nx * nx + ny * ny;
                    if (d > 1f) tex.SetPixel(x, y, clear);
                    else if (d > 0.78f) tex.SetPixel(x, y, dark);
                    else tex.SetPixel(x, y, brown);
                }
            }

            for (int x = 7; x <= 16; x++)
            {
                tex.SetPixel(x, 7, dark);
                tex.SetPixel(x, 8, dark);
            }
            foreach (var y in new[] { 5, 6, 9, 10 })
                for (int x = 10; x <= 13; x++)
                    tex.SetPixel(x, y, lace);
            tex.SetPixel(11, 7, lace);
            tex.SetPixel(12, 7, lace);
            tex.SetPixel(11, 8, lace);
            tex.SetPixel(12, 8, lace);

            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        /// <summary>
        /// Kickoff presentation: P1 USDC on offense-tagged roster names, P2 UNI on defenders.
        /// Kickoff placement keeps those tags franchise-stable (names move, tags stay).
        /// </summary>
        public static void RestyleKickoffRoster(bool receiverIsPlayer, string kickerName)
        {
            var p1 = GotchiTeamSprites.PlayerOne
                     ?? MakeCircleSprite(new Color(0.2f, 0.45f, 1f, 1f));
            var p2 = GotchiTeamSprites.PlayerTwo
                     ?? MakeCircleSprite(new Color(0.95f, 0.2f, 0.2f, 1f));

            RestyleTagged("Player", p1);
            RestyleTagged("Receiver", p1);
            RestyleTagged("Lineman", p1);
            RestyleTagged("Defender", p2);
            lastSkinnedPlayerOffense = null; // next scrimmage PlaceOnly re-evaluates

            // Kicker must always be a visible player circle at the tee.
            if (!string.IsNullOrEmpty(kickerName))
            {
                GameObject kicker = null;
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t == null || t.name != kickerName) continue;
                    if (t.hideFlags != HideFlags.None) continue;
                    if (!t.gameObject.scene.IsValid()) continue;
                    kicker = t.gameObject;
                    break;
                }

                if (kicker != null)
                {
                    kicker.SetActive(true);
                    // Receiving: opponent kicks (P2). Player kicks: P1.
                    bool kickerIsP2 = receiverIsPlayer;
                    StyleUnitCircle(kicker, kickerIsP2 ? p2 : p1, playerOne: !kickerIsP2);
                }
            }

            RestyleFootball();
        }

        static void StyleUnitCircle(GameObject go, Sprite sprite, bool playerOne)
        {
            if (go == null || sprite == null) return;

            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                r.enabled = false;

            var visual = go.transform.Find("Visual");
            if (visual == null)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(go.transform, false);
                visual = v.transform;
            }

            var sr = GetOrAddSpriteRenderer(visual.gameObject);
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.enabled = true;
            sr.flipX = false;
            sr.sortingOrder = 50;
            visual.localScale = Vector3.one * FormationRoster.PlayerScale;
            visual.localPosition = Vector3.zero;
            visual.gameObject.SetActive(true);

            if (visual.GetComponent<BillboardSprite>() == null)
                visual.gameObject.AddComponent<BillboardSprite>();

            bool offenseSide = !go.CompareTag("Defender");
            AttachGotchiFacing(visual.gameObject, go.transform, playerOne, offenseSide);

            ConfigureBody(go);

            var oldCapsule = go.GetComponent<CapsuleCollider>();
            if (oldCapsule != null)
                Object.Destroy(oldCapsule);

            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.radius = FormationRoster.BodyRadius;
            col.center = Vector3.zero;
            col.isTrigger = true;
        }

        /// <summary>
        /// P1 = USDC, P2 = UNI. Scrimmage: follows possession. Kickoff: offense tags stay P1.
        /// </summary>
        public static bool IsPlayerOneForTag(string tag)
        {
            bool offenseSide = tag != "Defender";
            if (IsKickoffPresentation())
                return offenseSide;

            bool playerOffense = FieldManager.Instance == null
                                 || FieldManager.Instance.isPlayerPossession;
            return offenseSide == playerOffense;
        }

        static bool IsKickoffPresentation()
        {
            if (FieldManager.Instance != null && FieldManager.Instance.PendingKickoff)
                return true;
            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (gm.isKicking) return true;
            if (gm.IsLiveReturn) return true;
            return false;
        }

        static void AttachGotchiFacing(GameObject visualGo, Transform motionRoot, string tag)
        {
            bool offenseSide = tag != "Defender";
            bool playerOne = IsPlayerOneForTag(tag);
            AttachGotchiFacing(visualGo, motionRoot, playerOne, offenseSide);
        }

        static void AttachGotchiFacing(
            GameObject visualGo, Transform motionRoot, bool playerOne, bool offenseSide)
        {
            if (visualGo == null) return;
            if (GotchiTeamSprites.Get(playerOne, GotchiFacing.Front) == null)
                return;

            var view = visualGo.GetComponent<GotchiFacingView>();
            if (view == null)
                view = visualGo.AddComponent<GotchiFacingView>();
            view.motionRoot = motionRoot;
            view.SetIdentity(playerOne, offenseSide);
            view.SetFacing(GotchiFacingView.PreSnapFacing(offenseSide), force: true);
        }

        static void RestyleBall(Sprite ballSprite)
        {
            if (ballSprite == null) return;

            // Collect every scene Football — duplicates leave stacked brown mesh spheres on the tee.
            var balls = new System.Collections.Generic.List<GameObject>();
            foreach (var fb in Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Include))
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (!balls.Contains(fb.gameObject))
                    balls.Add(fb.gameObject);
            }

            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Football"))
                {
                    if (go != null && go.scene.IsValid() && !balls.Contains(go))
                        balls.Add(go);
                }
            }
            catch (UnityException) { /* tag missing */ }

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.name != "Football") continue;
                if (t.hideFlags != HideFlags.None) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (!balls.Contains(t.gameObject))
                    balls.Add(t.gameObject);
            }

            if (balls.Count == 0) return;

            GameObject keep = null;
            foreach (var go in balls)
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

            foreach (var go in balls)
            {
                if (go == null || go == keep) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
                go.name = "Football_Dup";
                go.SetActive(false);
                Object.Destroy(go);
            }

            if (keep == null) return;

            var ball = keep;
            ball.SetActive(true);
            ball.transform.localScale = Vector3.one;
            ball.transform.SetParent(null, true);

            // Scene Football ships with FootballBehavior disabled — enable it so
            // flight / mesh suppression run.
            var behavior = ball.GetComponent<FootballBehavior>();
            if (behavior != null && !behavior.enabled)
                behavior.enabled = true;

            // Prefab / MatchScene ship a Unity sphere MeshRenderer — kill it hard.
            StripFootballMeshes(ball);

            // Extra Visual_* children from prior restyles — keep one "Visual".
            for (int i = ball.transform.childCount - 1; i >= 0; i--)
            {
                var child = ball.transform.GetChild(i);
                if (child == null) continue;
                if (child.name == "Visual") continue;
                if (child.name.StartsWith("Visual") || child.GetComponent<SpriteRenderer>() != null)
                {
                    child.gameObject.SetActive(false);
                    Object.Destroy(child.gameObject);
                }
            }

            var visual = ball.transform.Find("Visual");
            if (visual == null)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(ball.transform, false);
                visual = v.transform;
            }

            // Never put SpriteRenderer on the same GO as a leftover MeshFilter.
            StripFootballMeshes(visual.gameObject);

            var sr = GetOrAddSpriteRenderer(visual.gameObject);
            sr.sprite = ballSprite;
            sr.color = Color.white;
            sr.enabled = true;
            sr.sortingOrder = 25;
            // Sized vs Aavegotchi PlayerScale (see FormationRoster.BallScale).
            visual.localScale = Vector3.one * FormationRoster.BallScale;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.gameObject.SetActive(true);

            BallSpinAnimator.Attach(visual.gameObject);

            ConfigureBody(ball);

            var sphere = ball.GetComponent<SphereCollider>();
            if (sphere == null) sphere = ball.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = FormationRoster.BallCatchRadius;
            sphere.center = Vector3.zero;
        }

        /// <summary>Remove Unity sphere mesh leftovers that read as a black circle.</summary>
        static void StripFootballMeshes(GameObject go)
        {
            if (go == null) return;
            // DestroyImmediate so the black sphere cannot flash / stick for a frame.
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].enabled = false;
                Object.DestroyImmediate(renderers[i]);
            }
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i] == null) continue;
                Object.DestroyImmediate(filters[i]);
            }
        }

        static void EnsureSelectionRing(Sprite ringSprite)
        {
            if (ringSprite == null) return;
            var qb = FindByTag("Player");
            if (qb == null) return;

            var ring = FindOrCreate("SelectionRing");
            StripMeshComponents(ring);
            var sr = GetOrAddSpriteRenderer(ring);
            sr.sprite = ringSprite;
            sr.sortingOrder = 4;
            ring.transform.localScale = Vector3.one * 0.95f;

            var comp = ring.GetComponent<SelectionRing>();
            if (comp == null) comp = ring.AddComponent<SelectionRing>();
            comp.SetFollow(qb.transform);
        }

        static GameObject FindOrCreate(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
                go = new GameObject(name);
            return go;
        }

        static SpriteRenderer GetOrAddSpriteRenderer(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = go.AddComponent<SpriteRenderer>();
            return sr;
        }

        static void StripMeshComponents(GameObject go)
        {
            // SpriteRenderer cannot share a GO with MeshFilter/MeshRenderer.
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
        }

        static void ConfigureBody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            // Kinematic + no interpolation — script MovePosition, hard stops (no coast).
            ArcadeMove.ConfigureKinematicBody(rb);
            var p = go.transform.position;
            go.transform.position = new Vector3(p.x, p.y, 0f);
        }

        static void PlaceFirst(string tag, Vector3 pos)
        {
            var go = FindByTag(tag);
            if (go != null) go.transform.position = pos;
        }

        static void PlaceNamedOrIndexed(string tag, Vector3[] slots)
        {
            var objs = FindAll(tag);
            System.Array.Sort(objs, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < objs.Length && i < slots.Length; i++)
                objs[i].transform.position = slots[i];
        }

        static void RefreshReceiverRoutes()
        {
            if (Playbook.Selected != null)
            {
                Playbook.ApplyRoutesToFormation();
                return;
            }

            foreach (var go in FindAll("Receiver"))
            {
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null) continue;
                rc.GenerateRoute();
                rc.isRunningRoute = false;
            }
        }

        static void DisablePlayerControlOnTag(string tag)
        {
            foreach (var go in FindAll(tag))
            {
                var pc = go.GetComponent<PlayerController>();
                if (pc != null) pc.SetControlled(false);
            }
        }

        static Sprite LoadSprite(string resourcePath, Vector2 pivot)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 16f);
        }

        static GameObject FindByTag(string tag)
        {
            try { return GameObject.FindGameObjectWithTag(tag); }
            catch { return null; }
        }

        static GameObject[] FindAll(string tag)
        {
            try { return GameObject.FindGameObjectsWithTag(tag); }
            catch { return System.Array.Empty<GameObject>(); }
        }
    }
}
