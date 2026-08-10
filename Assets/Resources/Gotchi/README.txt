Lite Aavegotchi team skins (USDC + UNI) — 4 views each.

P1 (human) = maUSDC · P2 (opponent) = maUNI.
Skins follow the franchise, not offense/defense — you stay USDC on defense.

Team 1 / player (maUSDC):
  team_usdc_front.png
  team_usdc_left.png
  team_usdc_right.png
  team_usdc_back.png
  team_usdc.png          — alias of front

Team 2 / opponent (maUNI):
  team_uni_front.png
  team_uni_left.png
  team_uni_right.png
  team_uni_back.png
  team_uni.png           — alias of front

Compose order:
  front: shadow → body → cheek → hands → eyes → mouth (happy) → collateral
  left/right: shadow → body → cheek → SVG side hands → eyes → collateral (no mouth)
  back: shadow → body

Bake notes:
  Eyes: Haunt 1 eye-shape id09 (trait ~50, Common_2_Range_42-57) in collateral color
        front = eyes-0 (two squares); left = eyes-1; right = eyes-2 (side bar)
  Hands: SVG base-*-hands-left-9 / hands-right-10 (body-adjacent, not floating)
  Mouths: front = happy SVG only; left/right = no mouth (matches on-chain side SVG)
  Cheeks: on-chain #f696c6 blush (front both sides; left/right near-side only)
  Body/shadow/collateral: Gotchinopoly Aseprites (maUSDC / maUNI)

Facing (GotchiFacingView):
  Skin: playerOne=USDC / !playerOne=UNI
  Pre-snap / HUT: offenseSide faces drive/kick dir; defense side faces opposite
  Live: +X → right, −X → left, +Y → back, −Y → front
  Never flipX (dedicated L/R art)
  Depth: lower world Y (bottom of screen) → higher sortingOrder

Rebake: Assets/Editor/BakeGotchiTeamSprites.js (node + aseprite + sharp)
  Requires Gotchinopoly at ../Gotchinopoly and sharp via aavegotchi-game-sprites.

SVG sources (*_front/_left/_right/_back.svg) are legacy; PNGs are authoritative.
