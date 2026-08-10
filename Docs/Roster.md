# Roster architecture

There are **two different “rosters”** in this project. They are not the same data, and only loosely related today.

## 1. Career / team roster (players + stats)

**What it is:** Franchise players — names, jersey numbers, positions, rating stats. Used by career UI and team labels.

**Where it lives (code today):**

| Piece | Path |
|-------|------|
| Team + `List<PlayerData> roster` | [`Assets/Scripts/Data/TeamData.cs`](../Assets/Scripts/Data/TeamData.cs) |
| Player fields / stats / positions | [`Assets/Scripts/Data/PlayerData.cs`](../Assets/Scripts/Data/PlayerData.cs) |
| Seeds defaults / opponents | [`Assets/Scripts/Managers/TeamManager.cs`](../Assets/Scripts/Managers/TeamManager.cs) |
| Career UI reads `playerTeam.roster` | [`Assets/Scripts/App/CareerNav.cs`](../Assets/Scripts/App/CareerNav.cs) |

**How it is filled:** `TeamData.GenerateRoster()` builds a fixed position mix in C# and **randomizes names + stats** (`GeneratePlayerName()`, `PlayerStats` constructor). `TeamManager.EnsureDefaultTeamsIfNeeded()` creates New York Thunder / LA Raptors on first run; career choose-team and `GenerateNewOpponent()` call `GenerateRoster()` again.

**Not loaded from disk:** there is no `Assets/Resources/Rosters/*.json` yet. Save/career paths can regenerate an empty roster via `GenerateRoster()` if needed.

### Target shape (editable JSON)

Natural home for data-driven editing:

```
Assets/Resources/Rosters/default_player_team.json
Assets/Resources/Rosters/default_opponent_team.json   # or league templates
```

Example:

```json
{
  "cityName": "New York",
  "teamName": "Thunder",
  "primaryColor": "#1A4DCC",
  "secondaryColor": "#FFFFFF",
  "players": [
    {
      "playerName": "Tom Rivers",
      "jerseyNumber": 12,
      "position": "Quarterback",
      "stats": {
        "speed": 62,
        "strength": 55,
        "agility": 60,
        "throwing": 88,
        "catching": 40,
        "awareness": 80
      }
    }
  ]
}
```

**Load path (planned):**

1. Keep `PlayerData` / `TeamData` JSON-friendly (`[Serializable]`, plain fields).
2. Store colors as hex (or `r,g,b`) — avoid raw Unity `Color` in JSON if using `JsonUtility`.
3. Replace (or gate) `GenerateRoster()` with Resources load + deserialize.
4. Keep random generation as fallback or as an editor “export to JSON” tool.

## 2. On-field formation slots (GameObject names)

**What it is:** Match-scene unit IDs used for placement, kickoff walls, AI, tags, and control handoffs. These are **not** career `PlayerData` rows.

**Where it lives:** [`Assets/Scripts/Gameplay/FormationRoster.cs`](../Assets/Scripts/Gameplay/FormationRoster.cs)

Hardcoded name groups (examples):

- Offense: `Quarterback`, `OL_LT`…`OL_RT`, `WR_Top`, `WR_Bot`, `TE`, `RB`
- Defense: `DL_1`…`DL_4`, `LB_1`, `LB_2`, `CB_Top`, `CB_Bot`, `S`

Gameplay finds units with `GameObject.Find` / inactive-name search. Kickoff receive walls, returner select (`RB` / `WR_Top` / …), and defense cycle rosters all key off these strings.

Putting formation slot names in the same career JSON only helps if you also add a **map from `PlayerData` → on-field unit** (e.g. starting RB → GameObject `"RB"`). That bridge does not exist yet.

## Mental model

```
Career JSON / TeamData.roster     →  names, ratings, FO UI
        │
        │  StatBridge.ApplyMatchSides / ApplyKickoffSides
        ▼
UnitRuntimeStats on field units   →  speed / strength / stamina / skill
        │
        ▼
Controllers (move, catch, throw, tackle, sprint)
```

### Live match wiring (Retro Bowl–style)

[`StatBridge`](../Assets/Scripts/Gameplay/StatBridge.cs) maps roster → GameObject names after every `FormationRoster.PlaceOnly` / kickoff place:

| Roster attr (1–99) | Runtime |
|--------------------|---------|
| Speed | `moveSpeed` / `sprintSpeed` on receivers, QB, defenders |
| Strength | SoftTackle / TecmoContact break & wrap power |
| Stamina (`agility`) | `StaminaSprint` max + drain |
| Catching | `catchChance` / catch radius |
| Throwing | QB `throwPower` / aim click radius |
| Key skill (pos) | Defender `tackling` / wrap skill |

Star display: `PlayerStats.StarRating` / `UnitRuntimeStats.StarRating` (0.5–5).

## When editing what

| Goal | Edit |
|------|------|
| Player names, numbers, OVR stats, team identity | Career roster (`TeamData` / future JSON) |
| Who lines up where on the field, KO walls, returner candidates | `FormationRoster` (+ scene objects named to match) |
| Live speed / tackle / catch feel | Roster stats (auto-applied) or `StatBridge` scalars |

## Related

- Team labels / abbrevs: `TeamManager.PlayerTeamLabel()`, `GetAbbreviation()`
- Gotchi/sprites presentation is separate (`GotchiTeamSprites`, `RetroLookApplier`)
- Component on units: `UnitRuntimeStats`
