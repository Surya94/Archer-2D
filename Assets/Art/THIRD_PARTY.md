# Third-party assets added in the UI/UX polish pass

| Asset | Files | Licence | Source |
|---|---|---|---|
| Lilita One (display font) | `UI/Fonts/LilitaOne-Regular.ttf`, baked into `UI/Fonts/LilitaOne SDF.asset` | SIL Open Font License 1.1 - `UI/Fonts/LilitaOne-OFL.txt` | https://github.com/google/fonts/tree/main/ofl/lilitaone |
| Material Icons Round (icon font) | `UI/Fonts/MaterialIconsRound-Regular.otf`, baked into `UI/Fonts/MaterialIconsRound SDF.asset` | Apache License 2.0 - `UI/Fonts/MaterialIcons-LICENSE.txt` | https://github.com/google/material-design-icons |

| Kenney UI Audio | `Assets/Audio/kenney_ui-audio/` | CC0 1.0 - `License.txt` in the folder | https://kenney.nl |
| Kenney RPG Audio | `Assets/Audio/kenney_rpg-audio/` | CC0 1.0 - `License.txt` in the folder | https://kenney.nl |
| Kenney Impact Sounds | `Assets/Audio/kenney_impact-sounds/` | CC0 1.0 - `License.txt` in the folder | https://kenney.nl |

All of these allow use in a commercial game; the Kenney packs are public domain (credit optional). The OFL asks that the licence text ship with
the font files (it does, beside them); neither requires in-game credit.

## Generated in-project (no third-party licence)

Built by `Assets/Editor/FeelAssetBuilder.cs` (`Archer/FX/Build Feel Assets`):

- `UI/Kit/*.png` - rounded rect, pill, shadow, circle, glow, ring, shard sprites.
- `FX/Pop*.mat` and `Assets/Prefabs/PopBurst.prefab` - balloon pop particles.
- `Assets/Sound/Generated/*.wav` - `pop` and `pop_big` are still synthesized by the builder and
  in use. `lightning_zap`, `game_over` and `new_best` are still generated there too, but their
  slots now point at the Ludo clips below. Any slot in `Assets/Resources/Scriptables/Sounds/BowSoundData.asset` can be
  pointed at a recorded clip, and the builder leaves a slot alone once it no longer points at
  a generated file.

## Generated with Ludo.ai (Indie plan, owned by the project)

- `Assets/Sound/Music/ForestAdventureLoop.wav` - the `music` slot.
- `Assets/Sound/Ludo/lightning_zap.mp3`, `game_over.mp3`, `new_best.mp3` - replace the synthesized
  clips in the `lightningZapSound`, `gameOverSound` and `newBestSound` slots.
- `Assets/Art/Bonus/clock.png`, `bolt.png`, `snowflake.png`, `shuriken.png` - overwrite the
  code-drawn icons (same paths and GUIDs; `BonusBalloonBuilder` only draws them when missing).
  `glow.png` and `frost.png` are still code-drawn.
- `Assets/Art/UI/Logo.png` - stacked "BALLOON ARCHER" title logo; `UIPanelBuilder.CreateTitle`
  uses it and falls back to TMP text when it is missing.
- `Assets/Art/Weapons/Bow.png` (string-free red-brown recurve, 168.6 PPU so it is 5.93 units
  tall like the old bow) and `Assets/Art/Weapons/Arrow.png` (level, pointing right, 272.4 PPU so
  nock-to-tip matches the old arrow). Used by `Bow.prefab`, `Arrow.prefab`, the HUD arrow icon
  and `Adding Arrow VFX.prefab`. The original `Elf Archer` bow and arrow are kept but unused.
- `Assets/Art/Background/Sky.png` (twilight scene: indigo-to-peach dusk sky, misty hills, a
  village with glowing windows; the full painted image, 2:1 at 52.8 PPU = 40x20 units),
  `Landscape.png` (the same image with the sky cut out, so clouds drift between the sky and the
  hills) and `Clouds/Cloud_1..4.png` (soft peach and lavender dusk clouds, 100 PPU).
  `SceneBackgroundBuilder` also sets the CloudField haze to a dusk lavender. Built into MainMenu and GameScene by
  `SceneBackgroundBuilder`, and used by the loading screen. The "Free 2D Cartoon Parallax
  Background" pack is no longer referenced.
- `Assets/Art/UI/AppIcon.png` - default app icon (Player Settings): a drawn bow in the bottom-left
  aiming at a bunch of five balloons in the top-right, on a light sky with faint clouds. Three
  separate upscaled Ludo images (bow and balloon cut-outs, sky) laid out by ffmpeg, with the bow
  rotated 45 degrees; corners are square.

## Which Kenney clip is in which slot

Set by `Archer/FX/Assign Kenney Sounds` (the table in `FeelAssetBuilder.cs`), which also
imports the used clips as mono and normalised:

| Slot | Clip |
|---|---|
| UI click | `kenney_ui-audio/Audio/click1.ogg` |
| UI back | `kenney_ui-audio/Audio/switch2.ogg` |
| Settings toggle | `kenney_ui-audio/Audio/switch7.ogg` |
| Arrow gained | `kenney_rpg-audio/Audio/drawKnife3.ogg` |
| Bonus collected | `kenney_rpg-audio/Audio/handleCoins2.ogg` |
| Streak lost | `kenney_impact-sounds/Audio/impactSoft_medium_000.ogg` |
| Streak callout | `kenney_impact-sounds/Audio/impactBell_heavy_004.ogg` |
