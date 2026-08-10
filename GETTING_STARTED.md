# Getting Started — Aavegotchi Bowl

Unity 6 arcade football MVP (Gotchi Bowl), with managers, AI, season/team data, and UI scripts.

## Open the project

1. Unity Hub → **Add** → `Dev/Aavegotchi-Bowl`
2. Open with **Unity 6000.4.10f1** (or later Unity 6)
3. Open scene: `Assets/Scenes/BootScene.unity` (build order: Boot → Career → Match)
4. Press **Play** → Choose Team (first run) → career Home → **PLAY WEEK** / **PRACTICE**

If Match is missing, run **Aavegotchi Bowl → Setup Project**, then **Aavegotchi Bowl → Build Scene Architecture**.

## Layout

```
Assets/
├── Editor/                      # BowlSceneBuilder + SceneArchitectureBuilder
├── Scripts/
│   ├── App/                     # BootLoader, SceneFlow, CareerNav, SaveService
│   ├── Core/                    # GameManager, Field, Score, Audio
│   ├── Gameplay/                # QB, receivers, defenders, ball, camera
│   ├── Managers/                # Team + Season
│   ├── Data/                    # Player/Team structs
│   └── UI/                      # Menu, HUD, play calling, CareerUiKit
├── Scenes/BootScene.unity
├── Scenes/CareerScene.unity
├── Scenes/MatchScene.unity
├── Prefabs/Football.prefab
├── Materials/
└── TextMesh Pro/
```

## Docs

| File | Use |
|------|-----|
| `SETUP_GUIDE.md` | Bootstrap / rebuild scenes |
| `README.md` | Features & systems |
| `PROJECT_OVERVIEW.md` | Architecture notes |
