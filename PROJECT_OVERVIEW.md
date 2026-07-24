# Retro Bowl Unity - Project Overview

## 🎯 Project Summary

A Unity MVP (Minimum Viable Product) recreation of Retro Bowl featuring core football gameplay mechanics including passing, running, field management, AI opponents, and team management systems.

## 📊 Project Status: MVP Complete ✅

### What's Included
- ✅ Core game loop with quarter-based gameplay
- ✅ Player controls (QB, Receivers)
- ✅ Ball physics and throwing mechanics
- ✅ AI opponents with strategic play calling
- ✅ Defensive AI with pursuit and coverage
- ✅ Field management (downs, yards, scoring)
- ✅ Team generation with player stats
- ✅ Season mode with W/L tracking
- ✅ Complete UI system (HUD, menus, play calling)
- ✅ Score tracking and game stats
- ✅ Camera system with follow and zoom

## 🏗️ Architecture

### Core Systems (Scripts/Core/)
**GameManager.cs** - Central game controller
- Manages game states (Menu, Playing, Paused, GameOver, QuarterBreak)
- Tracks score and time
- Handles quarter progression
- Controls game flow

**FieldManager.cs** - Football field logic
- Ball position tracking (0-100 yard line)
- Down and distance system
- First down calculations
- Touchdown detection
- Turnover handling
- Punt and field goal mechanics

**ScoreManager.cs** - Scoring system
- Touchdown scoring (6 pts)
- Field goal scoring (3 pts)
- Extra point attempts (1 pt)
- Safety scoring (2 pts)
- Game statistics tracking
- Passing/rushing stats

**AudioManager.cs** - Sound management (structure only)
- Music playback
- Sound effect system
- Volume controls
- Placeholder for audio clips

### Gameplay Systems (Scripts/Gameplay/)
**PlayerController.cs** - Basic player movement
- WASD/Arrow key movement
- Sprint functionality
- Ball carrying
- Collision detection
- Tackle response

**QuarterbackController.cs** - QB-specific controls
- Throwing mechanics
- Aim targeting system
- Trajectory visualization
- Mouse-based throw aiming
- Receiver detection

**ReceiverController.cs** - Receiver AI and control
- Route running AI (3 route types)
- Catch attempt mechanics
- Post-catch player control
- Ball carrier behavior
- Tackle detection

**FootballBehavior.cs** - Ball physics
- Throw physics with arc
- In-air state management
- Catch detection
- Ground collision
- Interception handling

**DefenderAI.cs** - Individual defender AI
- Target detection (ball carrier, ball, receivers)
- Three AI states: Patrolling, Pursuing, Covering
- Smart pursuit logic
- Coverage positioning
- Tackle attempts

**OpponentAI.cs** - Offensive AI play calling
- Strategic play selection
- Down/distance awareness
- Field position logic
- Difficulty scaling
- Automated offense execution

**CameraController.cs** - Dynamic camera system
- Player following
- Smooth camera movement
- Boundary constraints
- Zoom functionality
- Target switching

### Management Systems (Scripts/Managers/)
**TeamManager.cs** - Team management
- Player team and opponent team
- Roster generation
- Team rating calculation
- Opponent generation
- Position-based player retrieval

**SeasonManager.cs** - Season progression
- 17-week season structure
- Schedule generation
- Win/loss tracking
- Season completion logic
- Next season advancement

### Data Systems (Scripts/Data/)
**PlayerData.cs** - Player information
- Player attributes (name, number, position)
- Stats system (speed, strength, agility, throwing, catching, awareness)
- Position-based stat bonuses
- Overall rating calculation
- 9 player positions

**TeamData.cs** - Team information
- Team identity (name, city, colors)
- Full roster management (22+ players)
- Team rating calculation
- Roster generation with proper distribution
- Win/loss record

### UI Systems (Scripts/UI/)
**GameHUD.cs** - In-game display
- Score display (player/opponent)
- Quarter and time display
- Down and distance
- Yard line position
- Team name display

**MenuManager.cs** - Menu system
- Main menu
- Pause menu
- Game over screen
- Quarter break screen
- Button management
- State-based panel display

**PlayCallingUI.cs** - Play selection
- Pass/Run/Field Goal/Punt buttons
- Situation display
- AI recommendations
- Button state management
- Play execution

## 🎮 Game Flow

```
Start Game
    ↓
Main Menu (Press Play)
    ↓
Initialize Teams & Field
    ↓
Game Playing State
    ↓
┌─────────────────────┐
│   Player on Offense │
│   - Choose Play     │
│   - Execute Play    │
│   - Advance Ball    │
└─────────────────────┘
    ↓
┌─────────────────────┐
│   AI on Offense     │
│   - AI Calls Play   │
│   - Auto Execute    │
│   - Advance Ball    │
└─────────────────────┘
    ↓
(Repeat until Quarter Ends)
    ↓
Quarter Break (Continue)
    ↓
Next Quarter (or Game Over)
    ↓
Game Over Screen
    ↓
Play Again or Main Menu
```

## 🔧 Key Features Explained

### Throwing Mechanics
1. Player holds left mouse button
2. Trajectory line shows throw arc
3. Player aims at desired location
4. Release mouse to throw
5. Ball follows physics-based arc
6. Receivers attempt to catch

### Down System
- Start at 20-yard line with 1st & 10
- Gain yards toward opponent endzone
- 4 downs to advance 10 yards
- First down resets to 10 yards (or less if near endzone)
- 4th down options: Go for it, Punt, Field Goal

### AI Behavior
**Defender AI:**
- Detects ball carrier → Pursues
- Ball in air → Attempts interception
- No immediate threat → Covers receivers

**Opponent AI:**
- Analyzes down/distance/field position
- Selects appropriate play
- Difficulty affects success rates
- Strategic 4th down decisions

### Scoring
- **Touchdown**: Cross goal line (0-yard line)
- **Field Goal**: Kick attempt from any distance (success rate based on distance)
- **Punt**: Strategic 4th down option to flip field position
- **Turnover**: Failed 4th down conversion

## 📈 Stat Tracking

### Player Stats (per position)
- **QB**: Throwing +20, Awareness +15
- **RB**: Speed +15, Agility +15
- **WR**: Speed +20, Catching +15
- **TE**: Strength +15, Catching +10
- **OL**: Strength +20, Awareness +10

### Game Stats Tracked
- Pass attempts/completions
- Passing yards
- Rush attempts
- Rushing yards
- Touchdowns
- Field goals
- Turnovers
- Completion percentage
- Yards per attempt

## 🎨 Visual Structure

```
Game Scene Hierarchy:
├── GameManager
├── FieldManager
├── ScoreManager
├── TeamManager
├── SeasonManager
├── OpponentAI
├── AudioManager
├── Main Camera (CameraController)
├── Directional Light
├── Field (Plane)
├── Quarterback
│   ├── QuarterbackController
│   └── PlayerController
├── Receiver1-3
│   └── ReceiverController
├── Defender1-7
│   └── DefenderAI
└── Canvas
    ├── MenuManager
    ├── GameHUD
    ├── PlayCallingUI
    ├── MainMenuPanel
    ├── HUDPanel
    ├── PlayCallingPanel
    ├── PauseMenuPanel
    └── GameOverPanel
```

## 🔄 State Machine

### Game States
1. **Menu** - Title screen, game not started
2. **Playing** - Active gameplay
3. **Paused** - Game paused (ESC key)
4. **QuarterBreak** - Between quarters
5. **GameOver** - Final score, winner determined

### Defender States
1. **Patrolling** - Random movement, no threat
2. **Pursuing** - Chasing ball carrier or ball
3. **Covering** - Shadowing receiver

## 💾 Data Flow

```
GameManager → Controls overall state
    ↓
FieldManager → Tracks ball position
    ↓
ScoreManager → Records stats/scoring
    ↓
TeamManager → Provides team/player data
    ↓
SeasonManager → Tracks season progress
```

## 🚀 Getting Started Priority

**Critical Components** (Must have):
1. GameManager
2. FieldManager
3. At least 1 QB with QuarterbackController
4. At least 1 Receiver with ReceiverController
5. Football prefab with FootballBehavior
6. Basic UI with GameHUD
7. Canvas with MenuManager

**Nice to Have** (Can add later):
- Multiple receivers
- Defenders with AI
- OpponentAI system
- SeasonManager
- Full menu system
- PlayCallingUI

## 🧪 Testing Strategy

### Phase 1: Core Mechanics
- [ ] Game starts and shows menu
- [ ] Can start a game
- [ ] QB can move with WASD
- [ ] Ball can be thrown

### Phase 2: Gameplay Loop
- [ ] Ball physics work correctly
- [ ] Receivers run routes
- [ ] Catching works
- [ ] Down system advances
- [ ] Scoring works

### Phase 3: AI Systems
- [ ] Defenders pursue
- [ ] Opponent AI calls plays
- [ ] Turnovers happen correctly
- [ ] Field position logic works

### Phase 4: Polish
- [ ] UI displays correctly
- [ ] Game flow is smooth
- [ ] Camera follows properly
- [ ] Menus function

## 📦 File Organization

```
Assets/
├── Scripts/
│   ├── Core/           (Game systems)
│   ├── Gameplay/       (Player mechanics)
│   ├── Managers/       (High-level management)
│   ├── Data/           (Data structures)
│   └── UI/             (User interface)
├── Scenes/
│   └── GameScene.unity (Main game scene)
├── Prefabs/
│   └── Football.prefab (Ball prefab)
├── Materials/
│   ├── PlayerMaterial.mat
│   ├── DefenderMaterial.mat
│   └── FieldMaterial.mat
└── Sprites/
    (Future: UI sprites, pixel art)
```

## 🎯 MVP Goals: ACHIEVED ✅

- [x] Playable football game loop
- [x] Pass and run mechanics
- [x] AI opponent
- [x] Score tracking
- [x] Down system
- [x] Team management
- [x] Basic UI
- [x] Game states
- [x] Season structure

## 🔮 Post-MVP Roadmap

### Phase 1: Polish (Visual)
- Retro pixel art sprites
- Animated players
- Stadium environment
- Crowd system
- Better UI graphics

### Phase 2: Enhanced Gameplay
- Running play control (switch to RB)
- Defensive play calling
- Special teams (kickoffs, returns)
- More play types
- Play editor

### Phase 3: Management Depth
- Draft system
- Player training/progression
- Injuries
- Salary cap
- Trades
- Multiple teams/leagues

### Phase 4: Modes
- Playoffs
- Super Bowl
- Career mode with legacy
- Challenge modes
- Online multiplayer (ambitious)

## 🤝 Code Style Guide

**Naming Conventions:**
- Classes: PascalCase (GameManager)
- Methods: PascalCase (StartNewGame)
- Variables: camelCase (currentQuarter)
- Constants: UPPER_SNAKE_CASE (QUARTER_DURATION)
- Private fields: camelCase (playerScore)
- Public fields: camelCase (moveSpeed)

**Organization:**
- Singleton pattern for managers
- Component-based for gameplay
- Data classes separate from logic
- UI separated from game logic

## 📝 Notes

This MVP focuses on **core functionality over polish**. The goal is to have a working, playable game with all essential systems in place. Visual polish, advanced features, and optimization come in post-MVP phases.

The codebase is structured to be:
- **Modular** - Easy to add features
- **Readable** - Clear naming and structure
- **Extensible** - Built for future growth
- **Educational** - Good learning resource

## 🎓 Learning Resources

This project demonstrates:
- Unity game architecture
- State machine implementation
- AI behavior trees
- Physics-based gameplay
- UI system design
- Data management
- Component communication

Good for learning:
- Unity fundamentals
- C# programming
- Game design patterns
- Sports game mechanics
- AI programming

---

**Version**: 1.0.0 (MVP)  
**Unity**: 2022.3.10f1+  
**Language**: C#  
**Platform**: PC (Windows/Mac/Linux)  
**Status**: Ready for Implementation  

Built with ❤️ for football and game development!
