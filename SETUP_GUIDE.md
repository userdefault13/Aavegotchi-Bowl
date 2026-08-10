# Aavegotchi Bowl — Setup Guide

## Quick start (automated)

The project includes Editor bootstraps that build a **Retro Bowl–style** match scene and the hybrid Boot → Career ↔ Match flow.

### Option A — Unity menu
1. Open `/Users/juliuswong/Dev/Aavegotchi-Bowl` in Unity Hub (Unity 6 / 6000.4+)
2. Wait for scripts to compile
3. Menu: **Aavegotchi Bowl → Setup Project** (regenerates art + `MatchScene`)
4. Menu: **Aavegotchi Bowl → Build Scene Architecture** (Boot / Career / Match + build settings)
5. Open `Assets/Scenes/BootScene.unity`
6. Press Play

### Option B — CLI
```bash
/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/juliuswong/Dev/Aavegotchi-Bowl \
  -executeMethod BowlSceneBuilder.Build

/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/juliuswong/Dev/Aavegotchi-Bowl \
  -executeMethod SceneArchitectureBuilder.Build
```

## What setup creates

- Tags: `Player`, `Receiver`, `Defender`, `Football`, `BallCarrier`
- Materials in `Assets/Materials/`
- `Assets/Prefabs/Football.prefab`
- `Assets/Scenes/MatchScene.unity` with:
  - Managers (Game, Field, Score, Team, Season, OpponentAI, Audio)
  - Field + yard lines
  - QB, 3 receivers, 5 defenders
  - Camera follow + Canvas UI (menu / HUD / play calling / pause / game over)
- `BootScene` + `CareerScene` (runtime career UI via `CareerNav`)
- Build Settings: Boot → Career → Match

## Controls

- **WASD** — move
- **Left Shift** — sprint
- **Click & hold / release** — aim & throw
- **ESC** — pause

## Re-run setup

Safe to run again; it regenerates `MatchScene.unity` and materials/prefabs.
Save any custom scene work under a different name first.
