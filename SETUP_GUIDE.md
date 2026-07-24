# Retro Bowl Unity - Quick Setup Guide

This guide will help you set up the Retro Bowl Unity MVP from scratch.

## ⚡ Quick Start (5 Minutes)

### Step 1: Open in Unity (1 min)
1. Open Unity Hub
2. Click "Add" → Select the `RetroBowlUnity` folder
3. Open the project (Unity 2022.3.10f1 or later)
4. Wait for scripts to compile

### Step 2: Import TextMeshPro (30 seconds)
1. Unity will prompt to import TMP Essentials
2. Click "Import TMP Essentials" button
3. Wait for import to complete

### Step 3: Create the Scene (3 minutes)

#### A. Create Scene
- File → New Scene → Basic (3D)
- Save as `Assets/Scenes/GameScene.unity`

#### B. Add Game Managers
Create empty GameObjects (right-click Hierarchy → Create Empty):

1. **GameManager**
   - Add Component → Scripts → Core → GameManager

2. **FieldManager**
   - Add Component → Scripts → Core → FieldManager

3. **ScoreManager**
   - Add Component → Scripts → Core → ScoreManager

4. **TeamManager**
   - Add Component → Scripts → Managers → TeamManager

5. **SeasonManager**
   - Add Component → Scripts → Managers → SeasonManager

6. **OpponentAI**
   - Add Component → Scripts → Gameplay → OpponentAI

#### C. Create Field
1. **Ground Plane**
   - GameObject → 3D Object → Plane
   - Name: "Field"
   - Scale: (10, 1, 20)
   - Create Material (green), apply to plane

2. **Ball Prefab**
   - GameObject → 3D Object → Sphere
   - Name: "Football"
   - Tag: "Football" (create tag if needed)
   - Scale: (0.3, 0.4, 0.3)
   - Add Component → Rigidbody
   - Add Component → Scripts → Gameplay → FootballBehavior
   - Drag to Assets/Prefabs folder

#### D. Create Players
1. **Quarterback**
   ```
   GameObject → 3D Object → Capsule
   Name: "Quarterback"
   Tag: "Player"
   Position: (0, 1, -10)
   Add: QuarterbackController
   Add: PlayerController
   Add: Rigidbody (Freeze Rotation: X, Y, Z)
   
   In QuarterbackController:
   - Ball Prefab: Drag Football prefab here
   ```

2. **Receivers** (Create 3)
   ```
   GameObject → 3D Object → Capsule
   Names: "Receiver1", "Receiver2", "Receiver3"
   Tag: "Receiver"
   Positions: 
     - Receiver1: (-5, 1, -8)
     - Receiver2: (5, 1, -8)
     - Receiver3: (0, 1, -6)
   Add: ReceiverController
   Add: Rigidbody (Freeze Rotation: X, Y, Z)
   ```

3. **Defenders** (Create 5)
   ```
   GameObject → 3D Object → Capsule
   Names: "Defender1" through "Defender5"
   Tag: "Defender"
   Positions: Spread across (y=1, z=5 to 15)
   Add: DefenderAI
   Add: Rigidbody (Freeze Rotation: X, Y, Z)
   Material: Red (create & apply)
   ```

#### E. Setup Camera
```
Select Main Camera:
Position: (0, 25, -30)
Rotation: (45, 0, 0)
Add Component → CameraController
Target: Drag Quarterback here
```

#### F. Create UI (The Tedious Part)
1. **Create Canvas**
   - GameObject → UI → Canvas
   - Canvas Scaler: Scale With Screen Size
   - Reference Resolution: 1920 x 1080

2. **Add UI Manager**
   - Select Canvas
   - Add Component → Scripts → UI → MenuManager
   - Add Component → Scripts → UI → GameHUD
   - Add Component → Scripts → UI → PlayCallingUI

3. **Create Panels** (Right-click Canvas → UI → Panel)
   
   **MainMenuPanel:**
   ```
   - Panel (rename to MainMenuPanel)
     - Text (TMP): "RETRO BOWL"
     - Button: "PLAY"
       - Child Text: "Play"
     - Button: "QUIT"
       - Child Text: "Quit"
   ```

   **HUDPanel:**
   ```
   - Panel (rename to HUDPanel)
     - Text (TMP): "PlayerScore" (Anchor: Top-Left)
     - Text (TMP): "OpponentScore" (Anchor: Top-Right)
     - Text (TMP): "QuarterText" (Anchor: Top-Center)
     - Text (TMP): "TimeText" (Anchor: Top-Center, below Quarter)
     - Text (TMP): "DownAndDistance" (Anchor: Bottom-Left)
     - Text (TMP): "YardLine" (Anchor: Bottom-Center)
   ```

   **PlayCallingPanel:**
   ```
   - Panel (rename to PlayCallingPanel)
     - Button: "PassPlay"
     - Button: "RunPlay"
     - Button: "FieldGoal"
     - Button: "Punt"
   ```

   **PauseMenuPanel:**
   ```
   - Panel (rename to PauseMenuPanel)
     - Text: "PAUSED"
     - Button: "Resume"
     - Button: "Restart"
     - Button: "Main Menu"
   ```

   **GameOverPanel:**
   ```
   - Panel (rename to GameOverPanel)
     - Text (TMP): "GameOverTitle"
     - Text (TMP): "FinalScore"
     - Button: "Play Again"
     - Button: "Exit"
   ```

4. **Connect UI References**
   - Select Canvas
   - In MenuManager component:
     - Drag all panels to corresponding fields
     - Drag all buttons to corresponding fields
   - In GameHUD component:
     - Drag all text elements to corresponding fields
   - In PlayCallingUI component:
     - Drag PlayCallingPanel and buttons

### Step 4: Configure Tags (30 seconds)
Make sure these tags exist (Edit → Project Settings → Tags):
- Player
- Receiver
- Defender
- Football
- BallCarrier

### Step 5: Play! (10 seconds)
- Press Play button
- Click "PLAY" in main menu
- Test the game!

## 🎮 Controls Reminder
- **WASD** - Move
- **Left Shift** - Sprint
- **Click & Hold** - Aim throw
- **Release** - Throw ball
- **ESC** - Pause

## 🐛 Common Issues

### Problem: Scripts don't compile
**Solution:** Make sure you're using Unity 2022.3 or later

### Problem: UI doesn't show
**Solution:** 
- Check Canvas is set to Screen Space - Overlay
- Verify Canvas Scaler settings
- Make sure EventSystem exists in scene

### Problem: Ball doesn't throw
**Solution:**
- Check QuarterbackController has Ball Prefab assigned
- Verify Football prefab has Rigidbody and FootballBehavior

### Problem: Players don't move
**Solution:**
- Verify Rigidbody components are attached
- Check that Rigidbody has rotation constraints frozen
- Make sure tags are set correctly

### Problem: AI doesn't work
**Solution:**
- Verify all managers are in the scene
- Check FieldManager has Ball Transform assigned
- Ensure tags are set on all objects

## ✅ Verification Checklist

Before playing, verify:
- [ ] All manager scripts in scene
- [ ] Tags configured correctly
- [ ] Rigidbody on all moving objects
- [ ] Rotation frozen on all Rigidbodies
- [ ] UI panels connected in MenuManager
- [ ] Ball prefab assigned to QB
- [ ] Camera has target assigned
- [ ] Canvas has EventSystem

## 🎨 Optional Enhancements

### Make it Look Better (5-10 minutes)
1. **Colors**
   - Create materials for teams
   - Player team: Blue/White
   - Opponent team: Red/Black

2. **Field Markers**
   - Create cubes for yard lines
   - Scale: (0.1, 0.1, field width)
   - Place every 10 yards

3. **Skybox**
   - Window → Rendering → Lighting
   - Add a skybox material
   - Try "Default-Skybox"

4. **Lighting**
   - Select Directional Light
   - Adjust intensity and color
   - Try warm afternoon lighting

## 📝 Next Steps

Once basic game works:
1. Add sound effects (drop .wav/.mp3 files in Assets/Audio)
2. Create better materials and textures
3. Add particle effects for touchdowns
4. Enhance UI with better styling
5. Add player names and stats display
6. Create a proper stadium environment

## 💡 Pro Tips

- **Save Often**: Ctrl+S to save scene
- **Test Frequently**: Press Play often to catch bugs early
- **Use Console**: Window → General → Console for error messages
- **Duplicate Objects**: Ctrl+D to duplicate (useful for creating multiple defenders)
- **Prefabs**: Make prefabs of players for easy reuse

## 📚 Need Help?

Check the main README.md for:
- Detailed component descriptions
- Game rules and mechanics
- Customization options
- Troubleshooting guide

---

**Total Setup Time: ~10 minutes**

Happy developing! 🏈
