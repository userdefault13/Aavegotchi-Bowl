# Aavegotchi Bowl (Gotchi Bowl)

Unity arcade football project inspired by Retro Bowl — gameplay + season management, headed toward an Aavegotchi-skinned experience.

## 🎮 Features

### Core Gameplay
- **Quarter-based Football** - 4 quarters with time management
- **Pass & Run Plays** - Control the quarterback to throw passes or hand off to running backs
- **Field Goals & Punts** - Strategic special teams plays
- **Down & Distance System** - Realistic football progression mechanics
- **AI Opponents** - Adaptive AI that makes strategic play decisions

### Team Management
- **Generated Rosters** - Automatic player generation with stats
- **Player Positions** - QB, RB, WR, TE, OL, DL, LB, CB, S
- **Team Ratings** - Overall team rating based on player stats
- **Season Mode** - 17-week season with win/loss tracking

### Game Systems
- **Score Tracking** - Touchdowns (6 pts), Field Goals (3 pts), Extra Points (1 pt)
- **Game States** - Menu, Playing, Paused, Quarter Break, Game Over
- **Field Management** - 100-yard field with yardline tracking
- **Stats System** - Track passing, rushing, touchdowns, turnovers

## 🚀 Getting Started

### Prerequisites
- **Unity 6000.4+** (Unity 6)
- TextMesh Pro (included under `Assets/TextMesh Pro`)

### Installation

1. Open `Aavegotchi-Bowl` in Unity Hub
2. Open `Assets/Scenes/BootScene.unity` (Career + Match via SceneFlow)
3. Press Play (or run **Aavegotchi Bowl → Setup Project** / **Build Scene Architecture** if scenes need rebuilding)

See `SETUP_GUIDE.md` / `GETTING_STARTED.md` for details.

### Project Structure

```
RetroBowlUnity/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # Core game systems
│   │   │   ├── GameManager.cs
│   │   │   ├── FieldManager.cs
│   │   │   └── ScoreManager.cs
│   │   ├── Gameplay/          # Player & gameplay mechanics
│   │   │   ├── PlayerController.cs
│   │   │   ├── QuarterbackController.cs
│   │   │   ├── ReceiverController.cs
│   │   │   ├── FootballBehavior.cs
│   │   │   ├── DefenderAI.cs
│   │   │   └── OpponentAI.cs
│   │   ├── Managers/          # Game management
│   │   │   ├── TeamManager.cs
│   │   │   └── SeasonManager.cs
│   │   ├── Data/              # Data structures
│   │   │   ├── PlayerData.cs
│   │   │   └── TeamData.cs
│   │   └── UI/                # User interface
│   │       ├── GameHUD.cs
│   │       ├── MenuManager.cs
│   │       └── PlayCallingUI.cs
│   ├── Scenes/
│   ├── Prefabs/
│   ├── Materials/
│   └── Sprites/
└── ProjectSettings/
```

## 🎯 How to Build a Scene

Since this is an MVP with scripts only, you'll need to create the scene manually:

### 1. Create Main Scene

1. **Create new scene**: File → New Scene → Basic (3D)
2. **Save scene**: `Assets/Scenes/MatchScene.unity`

### 2. Setup Game Managers

Create empty GameObjects and attach scripts:

```
Hierarchy:
├── GameManager (GameManager.cs)
├── FieldManager (FieldManager.cs)
├── ScoreManager (ScoreManager.cs)
├── TeamManager (TeamManager.cs)
├── SeasonManager (SeasonManager.cs)
└── OpponentAI (OpponentAI.cs)
```

### 3. Create the Football Field

1. **Create Plane** (Ground): GameObject → 3D Object → Plane
   - Scale: (10, 1, 20)
   - Position: (0, 0, 0)
   - Add green material

2. **Create Ball Object**: GameObject → 3D Object → Sphere
   - Name: "Football"
   - Scale: (0.3, 0.4, 0.3)
   - Tag: "Football"
   - Add `FootballBehavior.cs`
   - Add Rigidbody component

### 4. Setup Players

**Quarterback:**
```
GameObject → 3D Object → Capsule
- Name: "Quarterback"
- Tag: "Player"
- Add: QuarterbackController.cs
- Add: PlayerController.cs
- Add: Rigidbody (freeze rotation)
```

**Receivers (create 3):**
```
GameObject → 3D Object → Capsule
- Name: "Receiver1", "Receiver2", "Receiver3"
- Tag: "Receiver"
- Add: ReceiverController.cs
- Add: Rigidbody (freeze rotation)
- Position them spread across the field
```

**Defenders (create 5-7):**
```
GameObject → 3D Object → Capsule
- Name: "Defender1", "Defender2", etc.
- Tag: "Defender"
- Add: DefenderAI.cs
- Add: Rigidbody (freeze rotation)
- Give different colored material (red)
```

### 5. Setup Camera

```
Main Camera:
- Position: (0, 25, -30)
- Rotation: (45, 0, 0)
- Projection: Perspective
```

### 6. Create UI Canvas

1. **Create Canvas**: GameObject → UI → Canvas
   - Canvas Scaler: Scale with Screen Size
   - Reference Resolution: 1920x1080

2. **Add UI Scripts**:
   - GameHUD.cs
   - MenuManager.cs
   - PlayCallingUI.cs

3. **Create UI Elements**:

**HUD Panel:**
```
Canvas/
├── HUD Panel
│   ├── PlayerScore (TextMeshProUGUI)
│   ├── OpponentScore (TextMeshProUGUI)
│   ├── QuarterText (TextMeshProUGUI)
│   ├── TimeText (TextMeshProUGUI)
│   ├── DownAndDistance (TextMeshProUGUI)
│   └── YardLine (TextMeshProUGUI)
```

**Main Menu Panel:**
```
Canvas/
├── MainMenuPanel
│   ├── Title (TextMeshProUGUI)
│   ├── PlayButton (Button)
│   └── QuitButton (Button)
```

**Play Calling Panel:**
```
Canvas/
├── PlayCallingPanel
│   ├── PassPlayButton (Button)
│   ├── RunPlayButton (Button)
│   ├── FieldGoalButton (Button)
│   └── PuntButton (Button)
```

**Pause Menu Panel:**
```
Canvas/
├── PauseMenuPanel
│   ├── ResumeButton (Button)
│   ├── RestartButton (Button)
│   └── MainMenuButton (Button)
```

**Game Over Panel:**
```
Canvas/
├── GameOverPanel
│   ├── GameOverTitle (TextMeshProUGUI)
│   ├── FinalScore (TextMeshProUGUI)
│   ├── PlayAgainButton (Button)
│   └── ExitButton (Button)
```

## 🎮 Controls

### Gameplay
- **WASD / Arrow Keys** - Move player
- **Left Shift** - Sprint
- **Left Mouse Button** - Start aiming throw
- **Hold & Release Mouse** - Throw ball
- **ESC** - Pause game

### Menu Navigation
- **Mouse** - Navigate menus
- **Left Click** - Select options

## 🎨 Customization

### Adjust Game Settings

**GameManager.cs:**
- `quartersPerGame` - Number of quarters (default: 4)
- `QUARTER_DURATION` - Length of each quarter in seconds (default: 120)

**FieldManager.cs:**
- `fieldLength` - Length of field (default: 100)
- `fieldWidth` - Width of field (default: 53.3)
- `startingYardLine` - Where drives start (default: 20)

**OpponentAI.cs:**
- `difficulty` - AI skill level 0-1 (default: 0.5)
- `playCallingDelay` - Seconds between AI plays (default: 2)

**PlayerStats:**
- Modify ranges in `PlayerData.cs` constructor
- Adjust stat bonuses by position

## 🏈 Game Rules

### Scoring
- **Touchdown**: 6 points (advance to opponent's endzone)
- **Extra Point**: 1 point (automatic after TD, 95% success)
- **Field Goal**: 3 points (kick through uprights)
- **Safety**: 2 points (tackle opponent in their endzone)

### Downs System
- 4 downs to advance 10 yards
- First down resets to 10 yards or distance to endzone
- 4th down options: Go for it, Punt, or Field Goal

### Game Flow
1. Each team starts with the ball at their 20-yard line
2. Offense has 4 downs to gain 10 yards
3. Scoring changes possession
4. Game ends after 4 quarters
5. Winner determined by final score

## 🔧 Technical Details

### Key Components

**GameManager** - Central game state controller
- Manages game states (Menu, Playing, Paused, etc.)
- Tracks score and quarter time
- Handles game flow and transitions

**FieldManager** - Football field logic
- Tracks ball position and yard lines
- Manages downs and distance
- Handles touchdowns, turnovers, punts, FGs

**TeamManager** - Team and roster management
- Generates teams with players
- Manages player stats and ratings
- Handles team selection

**QuarterbackController** - Player throwing mechanics
- Aiming system with trajectory preview
- Ball throwing physics
- Receiver targeting

**ReceiverController** - Receiver AI and catching
- Route running AI
- Catch attempt system with success chance
- Player control after catch

**DefenderAI** - Defensive AI behavior
- Ball carrier pursuit
- Receiver coverage
- Tackling mechanics

**OpponentAI** - AI play calling
- Strategic play selection based on down/distance
- Difficulty-based performance
- Automated offense

## 🐛 Known Limitations

This is an MVP (Minimum Viable Product), so some features are simplified:

- No physics-based tackling (collision-based)
- Simplified AI pathfinding
- Basic graphics (primitives only)
- No sound effects or music
- No advanced animations
- Single game mode (no playoffs yet)
- No save/load system
- No player progression/training

## 🚧 Future Enhancements

Potential features to add:

1. **Graphics**
   - Retro pixel art sprites
   - Animated players
   - Particle effects

2. **Gameplay**
   - More play types (screen pass, play action, etc.)
   - Advanced defensive formations
   - Special teams (kickoffs, returns)
   - Weather conditions

3. **Management**
   - Draft system
   - Player injuries
   - Training & progression
   - Salary cap
   - Trades

4. **Modes**
   - Career mode
   - Playoffs
   - Super Bowl
   - Challenge mode

5. **Polish**
   - Sound effects
   - Music
   - Better animations
   - Replay system
   - Statistics screen

## 📝 License

This is a fan project inspired by Retro Bowl. All rights to the original game belong to New Star Games.

## 🤝 Contributing

This is an MVP educational project. Feel free to fork and enhance it!

## 💡 Tips for Development

### Testing the Game
1. Create a simple test scene first
2. Add debug logs to verify systems work
3. Use Unity's Console for error checking
4. Test each component individually

### Debugging
- Use `Debug.Log()` extensively
- Check Rigidbody constraints (freeze rotation)
- Verify tags are set correctly
- Ensure UI references are connected

### Performance
- Keep AI simple for smooth gameplay
- Limit number of active defenders (5-7)
- Use object pooling for repeated objects
- Optimize physics calculations

### Making it Look Better
1. Add materials with team colors
2. Create simple stadium environment
3. Add yard line markers
4. Create scoreboard UI
5. Add particle effects for celebrations

## 📚 Resources

- [Unity Documentation](https://docs.unity3d.com/)
- [Unity Learn](https://learn.unity.com/)
- [TextMeshPro Guide](https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest)
- [Original Retro Bowl](https://www.retrobowl.com/)

---

**Made with Unity 🎮**

Enjoy building your Retro Bowl MVP!
