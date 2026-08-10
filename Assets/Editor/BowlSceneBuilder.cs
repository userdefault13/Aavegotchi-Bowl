#if UNITY_EDITOR
using System.IO;
using System.Linq;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds a Retro Bowl–style MatchScene: orthographic sideline cam, pixel field, sprite players.
/// Menu: Aavegotchi Bowl → Setup Project
/// </summary>
public static class BowlSceneBuilder
{
    const string ScenePath = "Assets/Scenes/MatchScene.unity";
    const string PrefabsPath = "Assets/Prefabs";

    // "Player" is a Unity built-in tag — never add it to TagManager (causes "already registered").
    static readonly string[] RequiredTags =
    {
        "Receiver", "Defender", "Football", "BallCarrier", "Lineman"
    };

    [MenuItem("Aavegotchi Bowl/Setup Project")]
    public static void Build()
    {
        EnsureFolders();
        EnsureTags();
        PixelArtFactory.EnsureAllArt();

        var offenseSprite = PixelArtFactory.LoadSprite("PlayerOffense");
        var defenseSprite = PixelArtFactory.LoadSprite("PlayerDefense");
        var ballSprite = PixelArtFactory.LoadSprite("Football");
        var ringSprite = PixelArtFactory.LoadSprite("SelectionRing");
        var crowdSprite = PixelArtFactory.LoadSprite("Crowd");
        var sidelineSprite = PixelArtFactory.LoadSprite("Sideline");
        var fieldTex = PixelArtFactory.LoadTexture("Field");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting / sky
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.75f);
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.05f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Managers
        CreateEmpty("GameManager", typeof(GameManager), typeof(AudioManager));
        var fieldManagerGo = CreateEmpty("FieldManager", typeof(FieldManager));
        CreateEmpty("ScoreManager", typeof(ScoreManager));
        CreateEmpty("TeamManager", typeof(TeamManager));
        CreateEmpty("SeasonManager", typeof(SeasonManager));
        CreateEmpty("OpponentAI", typeof(OpponentAI));

        // Field visual (X = yards, Z = width)
        BuildField(fieldTex);
        BuildCrowd(crowdSprite, sidelineSprite);

        // Football prefab + scene marker
        var footballPrefab = BuildFootballPrefab(ballSprite);
        var football = (GameObject)PrefabUtility.InstantiatePrefab(footballPrefab);
        football.name = "Football";
        football.transform.position = YardPos(20f, 0f, 0.4f);
        var sceneBallBehavior = football.GetComponent<FootballBehavior>();
        if (sceneBallBehavior != null) sceneBallBehavior.enabled = false;

        // Players — offense left, defense right (Retro Bowl)
        var qb = CreateSpritePlayer("Quarterback", "Player", YardPos(20f, 0f), offenseSprite,
            addPlayer: true, addQb: true);
        WireQb(qb, footballPrefab);

        var receivers = new[]
        {
            CreateSpritePlayer("Receiver1", "Receiver", YardPos(25f, -8f), offenseSprite, addReceiver: true),
            CreateSpritePlayer("Receiver2", "Receiver", YardPos(25f, 8f), offenseSprite, addReceiver: true),
            CreateSpritePlayer("Receiver3", "Receiver", YardPos(28f, 0f), offenseSprite, addReceiver: true)
        };
        WireQbReceivers(qb, receivers);

        float[] defYards = { 42f, 48f, 52f, 48f, 42f };
        float[] defLanes = { -10f, -4f, 0f, 4f, 10f };
        for (int i = 0; i < 5; i++)
            CreateSpritePlayer($"Defender{i + 1}", "Defender", YardPos(defYards[i], defLanes[i]), defenseSprite, addDefender: true);

        // FieldManager ball ref
        var soField = new SerializedObject(fieldManagerGo.GetComponent<FieldManager>());
        soField.FindProperty("ballTransform").objectReferenceValue = football.transform;
        soField.ApplyModifiedPropertiesWithoutUndo();

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 13f;
        cam.backgroundColor = new Color(0.15f, 0.35f, 0.55f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(YardToX(25f), 18f, -24f);
        camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        var camController = camGo.AddComponent<CameraController>();
        var soCam = new SerializedObject(camController);
        soCam.FindProperty("target").objectReferenceValue = qb.transform;
        soCam.FindProperty("offset").vector3Value = new Vector3(0f, 18f, -24f);
        soCam.FindProperty("fixedZ").floatValue = -24f;
        soCam.FindProperty("orthoSize").floatValue = 13f;
        soCam.ApplyModifiedPropertiesWithoutUndo();

        // Selection ring under QB
        var ring = new GameObject("SelectionRing");
        var ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = ringSprite;
        ringSr.sortingOrder = 1;
        ring.transform.localScale = Vector3.one * 2.2f;
        var ringComp = ring.AddComponent<SelectionRing>();
        var soRing = new SerializedObject(ringComp);
        soRing.FindProperty("follow").objectReferenceValue = qb.transform;
        soRing.ApplyModifiedPropertiesWithoutUndo();

        BuildUi(out _, out var gameHud, out _);
        // hide HUD until play
        var hud = GameObject.Find("HUDPanel");
        if (hud != null) hud.SetActive(false);
        if (gameHud != null)
        {
            var soHud = new SerializedObject(gameHud);
            if (hud != null)
            {
                soHud.FindProperty("hudRoot").objectReferenceValue = hud;
                soHud.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Aavegotchi Bowl] Retro Bowl–style MatchScene ready → " + ScenePath);
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    [MenuItem("Aavegotchi Bowl/Fix TMP Fonts In Open Scene")]
    public static void FixTmpFontsInOpenScene()
    {
        var font = TMP_Settings.defaultFontAsset
                   ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                       "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) return;
        var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        foreach (var tmp in texts)
        {
            tmp.font = font;
            if (font.material != null) tmp.fontSharedMaterial = font.material;
            tmp.ForceMeshUpdate(true);
            EditorUtility.SetDirty(tmp);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Aavegotchi Bowl] Reassigned TMP fonts on {texts.Length} texts.");
    }

    static void BuildField(Texture2D fieldTex)
    {
        var field = GameObject.CreatePrimitive(PrimitiveType.Quad);
        field.name = "Field";
        Object.DestroyImmediate(field.GetComponent<MeshCollider>());
        field.AddComponent<MeshCollider>();

        // Quad faces +Z by default; lay flat and span yards on X, width on Z.
        field.transform.position = Vector3.zero;
        field.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // 120 yards long (incl endzones), ~53 wide
        field.transform.localScale = new Vector3(120f, 53.3f, 1f);

        var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "FieldMat" };
        mat.mainTexture = fieldTex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", fieldTex);
        AssetDatabase.CreateAsset(mat, "Assets/Art/Retro/FieldMat.mat");
        field.GetComponent<Renderer>().sharedMaterial = mat;

        // Near sideline white chalk
        var chalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chalk.name = "NearSideline";
        Object.DestroyImmediate(chalk.GetComponent<Collider>());
        chalk.transform.position = new Vector3(0f, 0.02f, -26.5f);
        chalk.transform.localScale = new Vector3(120f, 0.02f, 0.15f);
        chalk.GetComponent<Renderer>().sharedMaterial = GetColorMat("ChalkWhite", Color.white);
    }

    static void BuildCrowd(Sprite crowdSprite, Sprite sidelineSprite)
    {
        // Far sideline crowd (behind far sideline, +Z)
        var crowd = new GameObject("Crowd");
        var sr = crowd.AddComponent<SpriteRenderer>();
        sr.sprite = crowdSprite;
        sr.sortingOrder = -2;
        crowd.transform.position = new Vector3(0f, 1.2f, 28f);
        crowd.transform.rotation = Quaternion.Euler(15f, 180f, 0f);
        crowd.transform.localScale = new Vector3(14f, 2.5f, 1f);

        var side = new GameObject("SidelineDecor");
        var ssr = side.AddComponent<SpriteRenderer>();
        ssr.sprite = sidelineSprite;
        ssr.sortingOrder = -1;
        side.transform.position = new Vector3(0f, 0.05f, 27f);
        side.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        side.transform.localScale = new Vector3(14f, 1.2f, 1f);
    }

    static GameObject BuildFootballPrefab(Sprite ballSprite)
    {
        EnsureDir(PrefabsPath);
        var football = new GameObject("Football");
        football.tag = "Football";
        var sr = football.AddComponent<SpriteRenderer>();
        sr.sprite = ballSprite;
        sr.sortingOrder = 5;
        football.transform.localScale = Vector3.one * 1.2f;
        football.AddComponent<BillboardSprite>();

        var col = football.AddComponent<SphereCollider>();
        col.radius = 0.25f;
        col.isTrigger = true;

        var rb = football.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        football.AddComponent<FootballBehavior>();

        var path = $"{PrefabsPath}/Football.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(football, path);
        Object.DestroyImmediate(football);
        return prefab;
    }

    static GameObject CreateSpritePlayer(
        string name,
        string tag,
        Vector3 position,
        Sprite sprite,
        bool addPlayer = false,
        bool addQb = false,
        bool addReceiver = false,
        bool addDefender = false)
    {
        var go = new GameObject(name);
        go.tag = tag;
        go.transform.position = position;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3;
        visual.transform.localScale = Vector3.one * 2.4f;
        visual.AddComponent<BillboardSprite>();

        var col = go.AddComponent<SphereCollider>();
        col.radius = FormationRoster.BodyRadius;
        col.center = Vector3.zero;
        col.isTrigger = true;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (addPlayer || addQb)
        {
            var pc = go.AddComponent<PlayerController>();
            pc.SetControlled(true);
        }
        if (addQb) go.AddComponent<QuarterbackController>();
        if (addReceiver)
        {
            // Receivers are AI-routed; do not attach a controlled PlayerController.
            go.AddComponent<ReceiverController>();
        }
        if (addDefender) go.AddComponent<DefenderAI>();

        return go;
    }

    static void WireQb(GameObject qb, GameObject footballPrefab)
    {
        var qbController = qb.GetComponent<QuarterbackController>();
        var soQb = new SerializedObject(qbController);
        soQb.FindProperty("ballPrefab").objectReferenceValue = footballPrefab;
        soQb.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireQbReceivers(GameObject qb, GameObject[] receivers)
    {
        var qbController = qb.GetComponent<QuarterbackController>();
        var soQb = new SerializedObject(qbController);
        var receiversProp = soQb.FindProperty("receivers");
        receiversProp.arraySize = receivers.Length;
        for (int i = 0; i < receivers.Length; i++)
            receiversProp.GetArrayElementAtIndex(i).objectReferenceValue = receivers[i].transform;
        soQb.ApplyModifiedPropertiesWithoutUndo();
    }

    static float YardToX(float yard) => yard - 50f;

    // 2D Retro Bowl: X = downfield, Y = across field (lane), Z = 0.
    static Vector3 YardPos(float yard, float laneY, float z = 0f)
        => new Vector3(YardToX(yard), laneY, z);

    static Material GetColorMat(string name, Color color)
    {
        var path = $"Assets/Art/Retro/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { color = color, name = name };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // --- UI (compact Retro Bowl menu) ---

    static void BuildUi(out MenuManager menuManager, out GameHUD gameHud, out PlayCallingUI playCallingUi)
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        menuManager = canvasGo.AddComponent<MenuManager>();
        gameHud = canvasGo.AddComponent<GameHUD>();
        playCallingUi = canvasGo.AddComponent<PlayCallingUI>();

        var mainMenu = CreatePanel(canvasGo.transform, "MainMenuPanel", new Color(0.05f, 0.1f, 0.18f, 0.94f));
        var pauseMenu = CreatePanel(canvasGo.transform, "PauseMenuPanel", new Color(0f, 0f, 0f, 0.75f));
        var gameOver = CreatePanel(canvasGo.transform, "GameOverPanel", new Color(0.1f, 0.05f, 0.05f, 0.9f));
        var quarterBreak = CreatePanel(canvasGo.transform, "QuarterBreakPanel", new Color(0f, 0f, 0f, 0.7f));
        var hudPanel = CreatePanel(canvasGo.transform, "HUDPanel", new Color(0f, 0f, 0f, 0f), raycastTarget: false);
        var playPanel = CreatePanel(canvasGo.transform, "PlayCallingPanel", new Color(0.05f, 0.08f, 0.12f, 0.82f));

        CreateTmp(mainMenu.transform, "Title", "AAVEGOTCHI BOWL", 72, TextAlignmentOptions.Center, AnchorPreset.Center, new Vector2(0, 180), new Vector2(1000, 100));
        var playBtn = CreateButton(mainMenu.transform, "PlayButton", "PLAY", new Vector2(0, 20), new Vector2(280, 70));
        var quitBtn = CreateButton(mainMenu.transform, "QuitButton", "QUIT", new Vector2(0, -80), new Vector2(280, 70));

        CreateTmp(pauseMenu.transform, "PausedTitle", "PAUSED", 64, TextAlignmentOptions.Center, AnchorPreset.Center, new Vector2(0, 160), new Vector2(600, 80));
        var resumeBtn = CreateButton(pauseMenu.transform, "ResumeButton", "Resume", new Vector2(0, 40), new Vector2(260, 60));
        var restartBtn = CreateButton(pauseMenu.transform, "RestartButton", "Restart", new Vector2(0, -40), new Vector2(260, 60));
        var toMenuBtn = CreateButton(pauseMenu.transform, "MainMenuButton", "Main Menu", new Vector2(0, -120), new Vector2(260, 60));

        var goTitle = CreateTmp(gameOver.transform, "GameOverTitle", "GAME OVER", 64, TextAlignmentOptions.Center, AnchorPreset.Center, new Vector2(0, 140), new Vector2(800, 80));
        var finalScore = CreateTmp(gameOver.transform, "FinalScore", "0 - 0", 48, TextAlignmentOptions.Center, AnchorPreset.Center, new Vector2(0, 40), new Vector2(600, 60));
        var playAgainBtn = CreateButton(gameOver.transform, "PlayAgainButton", "Play Again", new Vector2(0, -60), new Vector2(260, 60));
        var exitBtn = CreateButton(gameOver.transform, "ExitButton", "Exit", new Vector2(0, -140), new Vector2(260, 60));

        var qbText = CreateTmp(quarterBreak.transform, "QuarterBreakText", "Quarter Break", 56, TextAlignmentOptions.Center, AnchorPreset.Center, new Vector2(0, 60), new Vector2(800, 80));
        var continueBtn = CreateButton(quarterBreak.transform, "ContinueButton", "Continue", new Vector2(0, -60), new Vector2(260, 60));

        var playerScore = CreateTmp(hudPanel.transform, "PlayerScore", "0", 48, TextAlignmentOptions.Left, AnchorPreset.TopLeft, new Vector2(40, -30), new Vector2(200, 60));
        var opponentScore = CreateTmp(hudPanel.transform, "OpponentScore", "0", 48, TextAlignmentOptions.Right, AnchorPreset.TopRight, new Vector2(-40, -30), new Vector2(200, 60));
        var quarterText = CreateTmp(hudPanel.transform, "QuarterText", "Q1", 36, TextAlignmentOptions.Center, AnchorPreset.TopCenter, new Vector2(0, -24), new Vector2(160, 50));
        var timeText = CreateTmp(hudPanel.transform, "TimeText", "02:00", 32, TextAlignmentOptions.Center, AnchorPreset.TopCenter, new Vector2(0, -70), new Vector2(160, 40));
        var downText = CreateTmp(hudPanel.transform, "DownAndDistance", "1st & 10", 28, TextAlignmentOptions.Left, AnchorPreset.BottomLeft, new Vector2(40, 40), new Vector2(320, 40));
        var yardText = CreateTmp(hudPanel.transform, "YardLine", "Ball on 20", 28, TextAlignmentOptions.Center, AnchorPreset.BottomCenter, new Vector2(0, 40), new Vector2(320, 40));
        var playerTeam = CreateTmp(hudPanel.transform, "PlayerTeamName", "GOTCHI", 24, TextAlignmentOptions.Left, AnchorPreset.TopLeft, new Vector2(40, -80), new Vector2(240, 36));
        var oppTeam = CreateTmp(hudPanel.transform, "OpponentTeamName", "LICK", 24, TextAlignmentOptions.Right, AnchorPreset.TopRight, new Vector2(-40, -80), new Vector2(240, 36));

        StretchCenter(playPanel.GetComponent<RectTransform>(), new Vector2(560, 420));
        var situation = CreateTmp(playPanel.transform, "SituationText", "1st & 10", 28, TextAlignmentOptions.Center, AnchorPreset.TopCenter, new Vector2(0, -30), new Vector2(520, 40));
        var recommendation = CreateTmp(playPanel.transform, "RecommendationText", "Call a play", 22, TextAlignmentOptions.Center, AnchorPreset.TopCenter, new Vector2(0, -70), new Vector2(520, 36));
        var passBtn = CreateButton(playPanel.transform, "PassPlayButton", "PASS", new Vector2(-120, 10), new Vector2(200, 56));
        var runBtn = CreateButton(playPanel.transform, "RunPlayButton", "RUN", new Vector2(120, 10), new Vector2(200, 56));
        var fgBtn = CreateButton(playPanel.transform, "FieldGoalButton", "FIELD GOAL", new Vector2(-120, -70), new Vector2(200, 56));
        var puntBtn = CreateButton(playPanel.transform, "PuntButton", "PUNT", new Vector2(120, -70), new Vector2(200, 56));

        mainMenu.SetActive(true);
        pauseMenu.SetActive(false);
        gameOver.SetActive(false);
        quarterBreak.SetActive(false);
        hudPanel.SetActive(false);
        playPanel.SetActive(false);

        var soMenu = new SerializedObject(menuManager);
        soMenu.FindProperty("mainMenuPanel").objectReferenceValue = mainMenu;
        soMenu.FindProperty("pauseMenuPanel").objectReferenceValue = pauseMenu;
        soMenu.FindProperty("gameOverPanel").objectReferenceValue = gameOver;
        soMenu.FindProperty("quarterBreakPanel").objectReferenceValue = quarterBreak;
        soMenu.FindProperty("playButton").objectReferenceValue = playBtn;
        soMenu.FindProperty("quitButton").objectReferenceValue = quitBtn;
        soMenu.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
        soMenu.FindProperty("restartButton").objectReferenceValue = restartBtn;
        soMenu.FindProperty("mainMenuButton").objectReferenceValue = toMenuBtn;
        soMenu.FindProperty("gameOverTitleText").objectReferenceValue = goTitle;
        soMenu.FindProperty("finalScoreText").objectReferenceValue = finalScore;
        soMenu.FindProperty("playAgainButton").objectReferenceValue = playAgainBtn;
        soMenu.FindProperty("exitButton").objectReferenceValue = exitBtn;
        soMenu.FindProperty("quarterBreakText").objectReferenceValue = qbText;
        soMenu.FindProperty("continueButton").objectReferenceValue = continueBtn;
        soMenu.ApplyModifiedPropertiesWithoutUndo();

        var soHud = new SerializedObject(gameHud);
        soHud.FindProperty("playerScoreText").objectReferenceValue = playerScore;
        soHud.FindProperty("opponentScoreText").objectReferenceValue = opponentScore;
        soHud.FindProperty("quarterText").objectReferenceValue = quarterText;
        soHud.FindProperty("timeText").objectReferenceValue = timeText;
        soHud.FindProperty("downAndDistanceText").objectReferenceValue = downText;
        soHud.FindProperty("yardLineText").objectReferenceValue = yardText;
        soHud.FindProperty("playerTeamNameText").objectReferenceValue = playerTeam;
        soHud.FindProperty("opponentTeamNameText").objectReferenceValue = oppTeam;
        soHud.FindProperty("hudRoot").objectReferenceValue = hudPanel;
        soHud.ApplyModifiedPropertiesWithoutUndo();

        var soPlay = new SerializedObject(playCallingUi);
        soPlay.FindProperty("playSelectionPanel").objectReferenceValue = playPanel;
        soPlay.FindProperty("passPlayButton").objectReferenceValue = passBtn;
        soPlay.FindProperty("runPlayButton").objectReferenceValue = runBtn;
        soPlay.FindProperty("fieldGoalButton").objectReferenceValue = fgBtn;
        soPlay.FindProperty("puntButton").objectReferenceValue = puntBtn;
        soPlay.FindProperty("situationText").objectReferenceValue = situation;
        soPlay.FindProperty("recommendationText").objectReferenceValue = recommendation;
        soPlay.ApplyModifiedPropertiesWithoutUndo();
    }

    enum AnchorPreset { Center, TopLeft, TopRight, TopCenter, BottomLeft, BottomCenter }

    static void AssignDefaultFont(TextMeshProUGUI tmp)
    {
        var font = TMP_Settings.defaultFontAsset
                   ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                       "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) return;
        tmp.font = font;
        if (font.material != null) tmp.fontSharedMaterial = font.material;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions align, AnchorPreset preset, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        AssignDefaultFont(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        ApplyAnchor(rt, preset);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.12f, 0.45f, 0.9f, 1f);
        var rt = go.GetComponent<RectTransform>();
        ApplyAnchor(rt, AnchorPreset.Center);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        AssignDefaultFont(tmp);
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    static GameObject CreatePanel(Transform parent, string name, Color color, bool raycastTarget = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void StretchCenter(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    static void ApplyAnchor(RectTransform rt, AnchorPreset preset)
    {
        switch (preset)
        {
            case AnchorPreset.TopLeft: rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); break;
            case AnchorPreset.TopRight: rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f); break;
            case AnchorPreset.TopCenter: rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f); break;
            case AnchorPreset.BottomLeft: rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f); break;
            case AnchorPreset.BottomCenter: rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f); break;
            default: rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); break;
        }
    }

    static GameObject CreateEmpty(string name, params System.Type[] components)
    {
        var go = new GameObject(name);
        foreach (var type in components) go.AddComponent(type);
        return go;
    }

    static void EnsureFolders()
    {
        EnsureDir("Assets/Scenes");
        EnsureDir(PrefabsPath);
        EnsureDir("Assets/Editor");
        EnsureDir("Assets/Art/Retro");
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static void EnsureTags()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");

        // Strip built-in "Player" if an older setup inserted it into custom tags.
        for (int i = tags.arraySize - 1; i >= 0; i--)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == "Player")
                tags.DeleteArrayElementAtIndex(i);
        }

        foreach (var tag in RequiredTags)
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
            if (!exists)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
            else if (scenes[i].path.Contains("SampleScene"))
                scenes[i] = new EditorBuildSettingsScene(scenes[i].path, false);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
