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
- `Assets/Sound/Generated/*.wav` - five effects are still synthesized by the builder, because
  the Kenney packs have nothing that fits: `pop`, `pop_big`, `lightning_zap`, `game_over`,
  `new_best`. Any slot in `Assets/Resources/Scriptables/Sounds/BowSoundData.asset` can be
  pointed at a recorded clip, and the builder leaves a slot alone once it no longer points at
  a generated file.

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
