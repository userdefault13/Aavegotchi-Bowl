# Phase 0 — Retro Bowl Feel Audit

Fill this while playing the **local Retro Bowl reference**. Goal: capture timing and feel notes so later Unity phases can match the original arcade loop — not copy art or ship New Star IP.

---

## How to open / play local Retro Bowl

Reference repo: `/Users/juliuswong/Dev/Retro-Bowl`  
(Same inode as the old `retro-bowl` path on mac — layout replaced with the current local package.)

### Local package (no remote ad host)

This workspace is an unofficial redistrib (8bitCAP / CAPPRIME-style). Root `index.html` iframes the local game under `rb/` — **OK to use** (it is no longer a broken remote ad host).

Old `play.html` / `production.html` entry points are **obsolete** — do not use those paths.

| Entry | Path | Use? |
|-------|------|------|
| **Preferred** | `/Users/juliuswong/Dev/Retro-Bowl/rb/index.html` | Yes — local GameMaker build |
| Also OK | `/Users/juliuswong/Dev/Retro-Bowl/index.html` | Yes — local iframe → `rb/` |

Requires local assets under `rb/` (textures/audio the canvas loads at runtime).

**Start a static server (required — avoid `file://`):**

```bash
cd /Users/juliuswong/Dev/Retro-Bowl
python3 -m http.server 8080
```

Then open:

**http://localhost:8080/rb/index.html**

(`http://localhost:8080/` also works — root iframe into `rb/`.)

You should see the GameMaker canvas load (splash → menu). If the canvas stays blank, confirm assets under `http://localhost:8080/rb/` return 200.

**Do not** rip sprites, audio, or shipping assets from this package into the Unity project. Observation and feel notes only.

---

## License / teaching note

- The Echo / 3kh0 HTML packaging of Retro Bowl is commonly redistributed under MIT-style packaging terms for the **wrapper**, but that **does not** grant rights to New Star Games’ Retro Bowl game IP, art, audio, or design as a product.
- Use this build as a **teaching / feel reference** only: timings, control cadence, readability, and flow.
- Aavegotchi Bowl should reimplement systems and ship original (or properly licensed) assets — never a asset rip.

---

## Confirmed GameRules baseline (Phase 0 freeze)

Verified in `Assets/Scripts/Core/GameRules.cs` — **no code changes required** (defaults already match Retro Bowl baseline):

| Flag | Value | Intent |
|------|-------|--------|
| `EnablePlayerDefense` | `false` | Defense is AI/sim; no player defender cycling |
| `EnableAiOffenseWhenDefending` | `true` | Opponent drives keep the game moving |
| `EnableContactBattle` | `false` | No universal O↔D mash |
| `EnableTackleBattleQte` | `false` | No dive/hard-hit break-meter QTE |
| `EnableStaminaSprint` | `false` | No mash sprint boost |
| `EnableLineEngageBattles` | `false` | No OL soft-engage ContactBattle |

Custom extras stay frozen off until a later phase explicitly re-enables them.

---

## Recommended play loop (tonight)

Do this once with a phone timer or rough stopwatch; jot blanks below.

1. **1 full offensive drive** — call mix of run + pass; note snap → pocket → throw/scramble → tackle/dive.
2. **1 defensive series** — watch AI offense; note CB trail vs break, pressure, tackle frequency (you are not controlling defense in baseline RB).
3. **1 FG attempt** — kick cone + power meter feel.
4. **1 TD → PAT / 2PT choice** — post-score UI flow and kick timing.
5. **1 kickoff** (receive or kick if offered) — fielding / coverage pace.

Optional: 1 punt if you reach 4th and long.

---

## Observation checklist

For each row: play Retro Bowl, fill blanks, leave Unity comparison for later phases.

### 1. Snap cadence (READY / SET / HUT spacing)

| | |
|--|--|
| **Retro script hints** | `s_set_up_play`, cadence UI / hut loop in match scripts (`s_set_anim` nearby) |
| **Unity compare later** | `Assets/Scripts/Gameplay/SnapCadence.cs` |
| **What good looks like** | Distinct READY → SET → HUT rhythm; click to snap feels intentional, not accidental; huts keep pulsing until snap |

**Notes**

- READY hold (estimate sec): ________
- SET hold (estimate sec): ________
- HUT interval (estimate sec): ________
- Click-to-snap feel (too early / just right / sticky): ________
- Free-form: _______________________________________________

---

### 2. Aim hold → throw release feel

| | |
|--|--|
| **Retro script hints** | `s_aiming`, `s_get_aim_direction`, `s_can_throw`, `s_sound_throw` |
| **Unity compare later** | `Assets/Scripts/Gameplay/QuarterbackController.cs`, `ThrowingArc.cs` |
| **What good looks like** | Hold aim reads clearly; release commits a readable throw; no double-tap throw from the snap click |

**Notes**

- Aim grace after snap (feels needed?): ________
- Hold → release latency (snappy / mushy): ________
- Arc / target readability: ________
- Free-form: _______________________________________________

---

### 3. Scramble cancel (aim proximity / pass lock at LOS)

| | |
|--|--|
| **Retro script hints** | `s_aiming`, `s_can_throw`, ball-carrier transition / `s_set_ball_down` |
| **Unity compare later** | `QuarterbackController.cs`, `GameManager.cs` (LOS / scramble), `PlayBanner.cs` |
| **What good looks like** | Pulling aim back near QB cancels pass into scramble; once past a LOS threshold, pass is locked / run mode is clear |

**Notes**

- How close must aim be to cancel?: ________
- When does pass lock relative to LOS?: ________
- Banner / feedback on scramble?: ________
- Free-form: _______________________________________________

---

### 4. Pocket life / DL pressure ramp

| | |
|--|--|
| **Retro script hints** | `s_set_position_defense`, `s_set_up_play`, DL chase toward QB |
| **Unity compare later** | `DefenderAI.cs`, `DefensePlayDirector.cs`, `LineBattle.cs`, `OffensiveBlocker.cs` |
| **What good looks like** | Early pocket is usable; pressure ramps so you feel urgency without instant sack every snap |

**Notes**

- Seconds until first real pressure: ________
- Sack frequency (rare / fair / constant): ________
- OL hold vs collapse feel: ________
- Free-form: _______________________________________________

---

### 5. CB trail vs break-on-ball

| | |
|--|--|
| **Retro script hints** | coverage AI near receivers; `s_intercept_object`, `s_check_tipped` |
| **Unity compare later** | `DefenderAI.cs`, `ReceiverController.cs`, `PassCollisionGate.cs` |
| **What good looks like** | CBs trail routes; when ball is in air they break toward it — contest readable, not random teleport |

**Notes**

- Trail tightness (loose / sticky): ________
- Break-on-ball reaction (late / on time / early): ________
- INT vs tip frequency feel: ________
- Free-form: _______________________________________________

---

### 6. Tackle frequency / shed / stiff-arm presence

| | |
|--|--|
| **Retro script hints** | `s_sound_tackle`, `s_tackle_failure`, contact resolve on ball carrier |
| **Unity compare later** | `TackleBattle.cs` (off), `ContactBattle.cs` (off), `ArcadeMove.cs`, `PlayerController.cs` |
| **What good looks like** | Tackles resolve quickly; occasional shed/broken tackle; stiff-arm is rare spice, not a mash minigame |

**Notes**

- Tackle stickiness: ________
- Shed / broken tackle frequency: ________
- Stiff-arm present? (Y/N + when): ________
- Free-form: _______________________________________________

---

### 7. Dive / slide timing and safe-spot behavior

| | |
|--|--|
| **Retro script hints** | `s_check_dive`, `s_sound_dive`, sprites `spr_dive` / `spr_dive_ball` |
| **Unity compare later** | `OffenseDiveSlide.cs` |
| **What good looks like** | Dive/slide is a deliberate commit near contact or sideline; “safe” spots feel earned, not spammy |

**Notes**

- Input timing window: ________
- Safe-spot / slide-out-of-bounds feel: ________
- Dive vs auto-tackle clarity: ________
- Free-form: _______________________________________________

---

### 8. Catch window / tip / INT readability

| | |
|--|--|
| **Retro script hints** | `s_check_tipped`, `s_tip_continue`, `s_intercept_object`, `s_sound_throw` |
| **Unity compare later** | `FootballBehavior.cs`, `PassCollisionGate.cs`, `ReceiverController.cs`, `PlayBanner.cs` |
| **What good looks like** | Catch / tip / INT outcomes are instantly readable; incomplete bounce/spot is clear |

**Notes**

- Catch window size feel: ________
- Tip vs catch vs INT clarity: ________
- Incomplete feedback (banner / spot): ________
- Free-form: _______________________________________________

---

### 9. Kick cone + power meter (FG / punt)

| | |
|--|--|
| **Retro script hints** | `s_draw_kick_cone`, `s_draw_kicking_power`, `s_get_kick_direction`, `s_kick_ball`, `s_set_up_fieldgoal`, `s_punt`, `s_sound_kick` |
| **Unity compare later** | `FieldManager.cs` (punt/FG hooks), kick UI TBD / `PlayCallingUI.cs` |
| **What good looks like** | Cone shows aim error; power meter has a clear sweet spot; FG and punt share readable rules |

**Notes**

- Cone width / drift: ________
- Power meter pace (slow / fair / frantic): ________
- FG vs punt differences: ________
- Free-form: _______________________________________________

---

### 10. PAT / 2PT post-TD flow

| | |
|--|--|
| **Retro script hints** | `btn_response_2pt`, XP / PAT kick path, `s_set_up_fieldgoal` (short), `s_kick_ball` |
| **Unity compare later** | `GameManager.cs`, `ScoreManager.cs`, `PlayCallingUI.cs`, `PlayBanner.cs` |
| **What good looks like** | After TD, clear PAT vs 2PT choice; PAT is a short kick; 2PT is a mini play from short yardage |

**Notes**

- Choice UI clarity: ________
- PAT kick timing vs FG: ________
- 2PT play feel: ________
- Free-form: _______________________________________________

---

### 11. Kickoff behavior

| | |
|--|--|
| **Retro script hints** | `s_kick_off`, `s_get_kicker`, coverage chase after catch |
| **Unity compare later** | `FieldManager.cs`, `GameManager.cs`, formation / `FormationRoster.cs` |
| **What good looks like** | Kickoff lands with a readable return lane; coverage closes without chaos; touchback rules feel fair |

**Notes**

- Hang time / return start: ________
- Coverage speed feel: ________
- Touchback / out-of-bounds: ________
- Free-form: _______________________________________________

---

### 12. Commentary / banners / crowd beats

| | |
|--|--|
| **Retro script hints** | `s_update_commentary`, `s_draw_commentary`, `s_do_banner_draft` (meta), crowd/audio cues |
| **Unity compare later** | `PlayBanner.cs`, `GameHUD.cs`, `AudioManager.cs` |
| **What good looks like** | Big outcomes get a short banner/line; crowd swells on big plays; never blocks the next snap for long |

**Notes**

- Banner duration / click-to-continue?: ________
- Commentary density: ________
- Crowd on score / big play: ________
- Free-form: _______________________________________________

---

## Phase 0 done when…

- [ ] Local Retro Bowl opened and playable (`index.html` or `production.html`, ideally via `python3 -m http.server`).
- [ ] License/teaching note understood (study feel only; no asset rip).
- [ ] `GameRules` baseline confirmed frozen (table above; extras off).
- [ ] Recommended play loop completed at least once (drive, D series watch, FG, TD→PAT).
- [ ] Each of the 12 checklist sections has at least rough timing/feel notes filled.
- [ ] No Phase 1+ Unity gameplay retune started yet (audit first).

---

## After Phase 0

Hand filled notes to the next phase owner. Use blanks as acceptance targets when tuning Unity scripts listed above. Do not re-enable `GameRules` extras until the Retro baseline feel is close.
