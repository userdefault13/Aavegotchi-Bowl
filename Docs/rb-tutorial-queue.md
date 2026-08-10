# Retro Bowl tutorial screenshot queue
Pending tips to wire after main onboarding pass. Do not rip assets.


## Queued 2026-07-24 16:44 — Handoff modal
- Shot: `/Users/juliuswong/.cursor/projects/Users-juliuswong-Dev-Aavegotchi-Bowl/assets/Screenshot_2026-07-24_at_4.44.29_PM-ff454011-c9af-4905-b188-7ab88b5b3644.png`
- Modal: "Nice! At the start of each play you can hand the ball off to your Running Back by clicking on the blue circle. Use W or S whilst running to perform a side-step." + CONTINUE

## Queued 2026-07-24 16:45 — Click blue circle
- Shot: `/Users/juliuswong/.cursor/projects/Users-juliuswong-Dev-Aavegotchi-Bowl/assets/Screenshot_2026-07-24_at_4.44.45_PM-c2848969-0eaf-4580-b27b-20360ba7968e.png`
- Bubble: "Click the blue circle" with glowing blue circle under ballcarrier + direction arrow; practice dummies on field
- Advance on click of blue circle / handoff target


## Priority change 2026-07-24 16:46 — SKIP deep training for now
User skipped full Training Facility length; come back later.
Deep pass/handoff tip chain deferred (TrainingTutorialOverlay stub may remain).
Home CONTINUE / ControlsBasics OK → Week PreMatch (not TrainingFacility).

## Done 2026-07-24 — Week / PreMatch scene
- Shot: `/Users/juliuswong/.cursor/projects/Users-juliuswong-Dev-Aavegotchi-Bowl/assets/Screenshot_2026-07-24_at_4.45.16_PM-a0284b0f-71ef-428a-856c-948d4f705e87.png`
- Implemented in `CareerNav.BuildPreMatch` / `RefreshPreMatch`
- Home CONTINUE → PreMatch; PLAY → `MatchLaunchArgs.WeekGame()`
- SIM GAME stub completes week + toast → Home


## Done 2026-07-24 — Match kickoff GET READY / Receive
- Shot: `/Users/juliuswong/.cursor/projects/Users-juliuswong-Dev-Aavegotchi-Bowl/assets/Screenshot_2026-07-24_at_4.46.09_PM-48d45a33-abf6-4279-8243-664586b3d474.png`
- Week PLAY → Match: GameHUD abbrevs + `1st Qtr` / clock; stacked **GET READY!** / **Receive** bubbles (`PlayBanner.ShowKickoffIntro`) then `KickingController` kickoff via `PostScoreFlow.BeginOpeningKickoff`
- Stadium crowd/sideline polish optional later
