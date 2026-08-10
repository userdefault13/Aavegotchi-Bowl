#!/usr/bin/env node
/**
 * Rebake Assets/Resources/Gotchi/team_{usdc,uni}_{front,left,right,back}.png
 *
 * Needs: aseprite CLI, sharp (run from aavegotchi-game-sprites or npm i sharp),
 *        Gotchinopoly sibling at ../Gotchinopoly (or GOTCHINOPOLY_ROOT).
 *
 * Usage:
 *   node Assets/Editor/BakeGotchiTeamSprites.js
 */
const sharp = require('sharp');
const path = require('path');
const fs = require('fs');
const { execSync } = require('child_process');

const ROOT = path.resolve(__dirname, '../..');
const ASE = process.env.ASEPRITE || '/Users/juliuswong/.local/bin/aseprite';
const OUT = path.join(ROOT, 'Assets/Resources/Gotchi');
const TMP = path.join(OUT, '_bake');
const GROOT = process.env.GOTCHINOPOLY_ROOT
  || path.resolve(ROOT, '../Gotchinopoly');
const GBASE = path.join(GROOT, 'Assets/Resources/Aavegotchi/Aseprites/Aavegotchi');
const SVG = path.join(GROOT, 'Assets/Resources/Aavegotchi/SVGs');
const CHEEK = { r: 0xf6, g: 0x96, b: 0xc6 }; // on-chain #f696c6

fs.rmSync(TMP, { recursive: true, force: true });
fs.mkdirSync(TMP, { recursive: true });

function ase(src, dest) {
  execSync(`"${ASE}" -b "${src}" --save-as "${dest}"`, { stdio: 'pipe' });
}
async function svgPng(svgPath, dest) {
  await sharp(fs.readFileSync(svgPath))
    .resize(64, 64, { fit: 'fill', kernel: 'nearest' })
    .png()
    .toFile(dest);
}
async function writeRaw(buf, w, h, dest) {
  await sharp(buf, { raw: { width: w, height: h, channels: 4 } }).png().toFile(dest);
}
async function compose(layers, dest) {
  await sharp(layers[0])
    .ensureAlpha()
    .composite(layers.slice(1).map(input => ({ input, blend: 'over' })))
    .png()
    .toFile(dest);
}

/** Official on-chain cheek blush paths from body SVG. */
async function makeCheek(dest, face) {
  const w = 64, h = 64;
  const out = Buffer.alloc(w * h * 4, 0);
  const rects =
    face === 'front' ? [[21, 32, 2, 2], [41, 32, 2, 2]] :
    face === 'left'  ? [[22, 32, 2, 2]] :
    face === 'right' ? [[40, 32, 2, 2]] : [];
  for (const [x0, y0, rw, rh] of rects) {
    for (let y = y0; y < y0 + rh; y++) {
      for (let x = x0; x < x0 + rw; x++) {
        const i = (y * w + x) * 4;
        out[i] = CHEEK.r; out[i + 1] = CHEEK.g; out[i + 2] = CHEEK.b; out[i + 3] = 255;
      }
    }
  }
  await writeRaw(out, w, h, dest);
}

const teams = [
  { slug: 'usdc', col: 'maUSDC', svgCol: 'mausdc' },
  { slug: 'uni', col: 'maUNI', svgCol: 'mauni' },
];

(async () => {
  for (const { slug, col, svgCol } of teams) {
    const base = path.join(GBASE, col);
    const eyeDir = path.join(SVG, 'Eyes', svgCol, 'Common', 'Common_2_Range_42-57');

    {
      const p = path.join(TMP, `${slug}_front`);
      ase(`${base}/shadow/shadow_00_${col}.aseprite`, `${p}_shadow.png`);
      ase(`${base}/body/body_front_${col}.aseprite`, `${p}_body.png`);
      await makeCheek(`${p}_cheek.png`, 'front');
      ase(`${base}/hands/hands_down_open_${col}.aseprite`, `${p}_hands.png`);
      ase(`${base}/collateral/collateral_front_${col}.aseprite`, `${p}_collateral.png`);
      await svgPng(path.join(eyeDir, 'eyes-common-eyes-0.svg'), `${p}_eyes.png`);
      await svgPng(path.join(SVG, 'Base', `base-${svgCol}`, `base-${svgCol}-mouth-happy-22.svg`), `${p}_mouth.png`);
      await compose(
        [`${p}_shadow.png`, `${p}_body.png`, `${p}_cheek.png`, `${p}_hands.png`, `${p}_eyes.png`, `${p}_mouth.png`, `${p}_collateral.png`],
        path.join(OUT, `team_${slug}_front.png`));
      fs.copyFileSync(path.join(OUT, `team_${slug}_front.png`), path.join(OUT, `team_${slug}.png`));
      console.log('front', slug);
    }

    {
      const p = path.join(TMP, `${slug}_back`);
      ase(`${base}/shadow/shadow_00_${col}.aseprite`, `${p}_shadow.png`);
      const backBody = `${base}/body/body_back_${col}.aseprite`;
      if (fs.existsSync(backBody)) ase(backBody, `${p}_body.png`);
      else ase(`${base}/body/body_front_${col}.aseprite`, `${p}_body.png`);
      await compose([`${p}_shadow.png`, `${p}_body.png`], path.join(OUT, `team_${slug}_back.png`));
      console.log('back', slug);
    }

    for (const face of ['left', 'right']) {
      const p = path.join(TMP, `${slug}_${face}`);
      ase(`${base}/shadow/shadow_00_${col}.aseprite`, `${p}_shadow.png`);
      ase(`${base}/body/body_${face}_${col}.aseprite`, `${p}_body.png`);
      await makeCheek(`${p}_cheek.png`, face);
      const handSvg = path.join(SVG, 'Base', `base-${svgCol}`,
        `base-${svgCol}-hands-${face}-${face === 'left' ? 9 : 10}.svg`);
      await svgPng(handSvg, `${p}_hands.png`);
      ase(`${base}/collateral/collateral_${face}_${col}.aseprite`, `${p}_collateral.png`);
      await svgPng(path.join(eyeDir, `eyes-common-eyes-${face === 'left' ? 1 : 2}.svg`), `${p}_eyes.png`);
      // Official on-chain side views have no mouth (front only).
      await compose(
        [`${p}_shadow.png`, `${p}_body.png`, `${p}_cheek.png`, `${p}_hands.png`, `${p}_eyes.png`, `${p}_collateral.png`],
        path.join(OUT, `team_${slug}_${face}.png`));
      console.log(face, slug);
    }
  }
  fs.rmSync(TMP, { recursive: true, force: true });
  console.log('Bake complete →', OUT);
})().catch(err => { console.error(err); process.exit(1); });
