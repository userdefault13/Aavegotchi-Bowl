# Bidirectional drives (`driveDirection`)

Absolute field model used by MatchScene gameplay.

## Field

| Absolute yard | Meaning |
|---|---|
| 0 | Left endzone |
| 100 | Right endzone |

`FieldManager.currentYardLine` is always the **absolute** LOS / ball yard.
World X = `YardToWorldX(currentYardLine)` (no fake `100 − abs` remap of world position).

## Drive direction

| `driveDirection` | Offense attacks | Offense lined up | Score by reaching |
|---|---|---|---|
| `+1` | toward yard 100 (+X) | smaller-X side of LOS | yard 100 |
| `−1` | toward yard 0 (−X) | larger-X side of LOS | yard 0 |

- **Own yard (HUD):** `dir > 0 ? abs : 100 − abs` → `OwnYardLine`
- **Advances:** `currentYardLine += yardsGained * driveDirection`
- **First-down stick:** absolute yard `currentYardLine + yardsToGo * driveDirection`

## Kickoff

| Situation | `kickDirection` | Tee (abs) | After resolve |
|---|---|---|---|
| Opening KO | `+1` (L→R) | 35 | Receiving offense `driveDirection = −1` |
| After score | scorer’s `LastScorerDriveDirection` | own 35 → abs 35 or 65 | Receiving offense `driveDirection = −kickDirection` |

Return runs opposite kick flight (`ReturnDirX = −KickDirX`). Touchback spots receiving own **35** without flipping world X (bounce out the back of the EZ / sideline OOB in the EZ, or kneel).

## Change of possession

Keep absolute spot; flip `driveDirection` so the new offense attacks the opposite endzone.

## XP / FG

Kick toward the **attack** endzone uprights (posts at `±uprightPostWorldX` from midfield). Tee stays near the scoring endzone (XP abs 85 or 15).
