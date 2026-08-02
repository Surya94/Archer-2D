# Archer 2D — Release-Readiness Plan

Tracks the phased plan to take Archer 2D from prototype to release-ready: working menus/HUD, AppLovin MAX ads, and Unity IAP purchases (remove-ads, arrow/currency packs, cosmetic skins), Android only.

Full stage-by-stage implementation plan lives here; day-to-day execution notes and Claude Code session context live outside the repo. This file is the durable, project-facing source of truth for scope and progress.

## Context

An investigation via Unity MCP (firing arrows, driving signals, reading scenes live in the Editor) found this wasn't a polish job: the UI layer was non-functional scaffolding. `UIManager`/`BaseUIPanel` were well-built but zero concrete panels existed anywhere, `UIManager` wasn't placed in either scene, and `UIManager.Start()` hard-errored the instant it ran. There was no pause, no game-over trigger, no restart wiring, and `SoundManger.cs` had a stray `using static UnityEditor.PlayerSettings;` that would have broken the actual Android build. No ad/IAP SDK was installed; the Android `applicationId` was still Unity's placeholder; app icons were unassigned.

## Decisions locked in

- **Ads:** AppLovin MAX. **IAP:** Remove Ads (non-consumable), Arrow/currency packs (consumable), cosmetic bow/arrow skins (non-consumable). **Platform:** Android only.
- Arrow/currency packs grant **soft currency spendable only on cosmetics** — kept separate from the in-round, skill-based `arrowCount` (resets every game via `ScoreManager.ResetGame()`). No pay-to-continue-a-round design.
- Lose condition is **arrow depletion only** (no lives/miss-limit system) for now — a miss-limit system is an explicit non-goal of this plan.
- Android `applicationId`: **`com.stillpoint.archer2d`**.
- Sequencing: fix core loop bugs before building panels to bind to them (Stage 1 → 2); finish `SoundManger` before Settings sliders can call it (Stage 3 → 4); build the save/economy layer before Shop UI or IAP has anywhere to persist results (Stage 4 before 6/7); ads before "Remove Ads" IAP has a switch to flip (Stage 5 before 6). Each stage is a separate, reviewable commit.

## Progress

| Stage | Status | Summary |
|---|---|---|
| 0 — Baseline hygiene & compile-blocking fixes | ✅ Done | See below |
| 1 — Core gameplay loop fixes | ✅ Done | See below |
| 2 — Activate the UI panel layer | ⬜ Pending | MainMenu/Gameplay/PauseMenu/GameOver/Settings panels |
| 3 — SoundManger completion | ⬜ Pending | Implement the 5 `NotImplementedException` stubs |
| 4 — Settings persistence + Economy foundation | ⬜ Pending | `EconomyManager`: currency, cosmetics, ads-removed, settings |
| 5 — AppLovin MAX ads integration | ⬜ Pending | Needs AppLovin account/ad units (human step) |
| 6 — Unity IAP integration | ⬜ Pending | Needs Play Console app/products (human step) |
| 7 — Shop / cosmetics UI | ⬜ Pending | `ShopPanel`, skin ScriptableObjects, equip logic |
| 8 — Release/store readiness polish | ⬜ Pending | Icons, manifest, keystore, Play Console checklist |
| 9 — Regression pass & final QA | ⬜ Pending | Consolidate tests, full device smoke test |

### Stage 0 — Baseline hygiene & compile-blocking fixes ✅

Commits: `9fbe624` (DOTween plugin update), `2124274` (retire unused `SplashScreenHandler.cs`), `a366b24` (baseline WIP + Stage 0 fixes).

- Fixed the release-blocking `using static UnityEditor.PlayerSettings;` reference in `Assets/Scripts/Manager/SoundManger.cs` (would have failed to compile in an actual Android Player build) — verified via forced recompile, zero console errors.
- Set Android `applicationId` to `com.stillpoint.archer2d` in `ProjectSettings/ProjectSettings.asset`.
- `AndroidTargetSdkVersion` left on **Automatic** rather than pinned — no Android SDK platforms were resolvable in this environment to verify a specific number against; revisit at Stage 8 against Play Console's actual current requirement.
- Added `Assets/Scripts/Tests/Archer.Scripts.Tests.asmdef` (EditMode test assembly). **Known gap:** it can't yet reference gameplay types (`ScoreManager`, `GameManager`, etc.) — `Assets/Scripts/` has no runtime `.asmdef` of its own, so those scripts compile into the implicit `Assembly-CSharp`, which a custom assembly definition can't reference. A runtime `.asmdef` needs to be added deliberately before the first real test lands (Stage 1).
- `.gitignore`d the unused 469MB `Assets/Feel/` third-party asset pack (More Mountains Feel + Nice Vibrations, not referenced anywhere in game code).
- Untracked `Logs/AssetImportWorker0.log`, `Logs/Packages-Update.log`, `UserSettings/EditorUserSettings.asset` — were tracked despite matching existing `.gitignore` rules.
- Reconciled the Unity Editor version discrepancy (last commit said `6000.0.24f1`, `CLAUDE.md` said `6000.3.5f1`, actual running Editor is `6000.5.6f1`) — `6000.5.6f1` confirmed correct and now documented in `CLAUDE.md`.
- Deleted debug screenshots from an earlier MCP play-testing session (`Assets/Screenshots/`) — not project assets.

### Stage 1 — Core gameplay loop fixes ✅

- Added `Assets/Scripts/Models/OnGameOver.cs` and dispatch it from `Bow.SpawnArrow()`'s no-arrows-left branch, replacing the dead-end `Debug.Log("No Arrows Left")`.
- `GameManager.cs` (was an empty stub) is now `Singleton<GameManager>`: subscribes to `OnGameOver`, owns `Pause()`/`Resume()` (`Time.timeScale`), exposes `Restart()` (reloads the active scene). Its creation is forced eagerly from `SplashScreenLoader.LoadDataInRunTime()` (the existing boot hook) since nothing else referenced `GameManager.Instance` to trigger the lazy-singleton path.
- Fixed `GamePlayHUD.OnDestory` → `OnDestroy` typo (signal observers were never unsubscribing).
- Fixed the HUD stale-init issue: added `SetArrowCount()`, called from `Start()` so the arrow count reads correctly on the first frame instead of waiting on the first `OnArrowsAdded` signal.
- **Verified live** via Unity MCP in the running Editor (not just compiled): forced a recompile with zero console errors/warnings, then in Play Mode drove `ScoreManager.arrowCount` to 0 and invoked `Bow.SpawnArrow()` directly — confirmed `OnGameOver` fired and `GameManager` responded (`IsGameOver=true`, `IsPaused=true`, `Time.timeScale=0`), then confirmed `Resume()` restores `Time.timeScale=1`. HUD text was already correctly populated (`': 9'`) from the `Start()`-time fix rather than stale/blank.
- Known follow-up, not blocking Stage 1: `GameManager` is created lazily rather than placed explicitly in the scene as a GameObject — matches the `Singleton<T>` fallback design, but the project's convention (per the Stage 0 investigation that found `UIManager` missing from both scenes) leans toward explicit scene placement for manager singletons. Worth placing explicitly in Stage 2 once panels give it Inspector-exposed fields to justify a scene presence.

## Remaining stages (summary)

See the full per-stage breakdown (files touched, verification steps) in the working plan; short version:

- **Stage 2:** Build concrete `BaseUIPanel` subclasses (`MainMenuPanel`, `GameplayHUDPanel`, `PauseMenuPanel`, `GameOverPanel`, `SettingsPanel`), place `UIManager` in both scenes, first UI prefabs the project has ever had.
- **Stage 3:** Implement `SoundManger`'s `PlaySFX`/`SetSFXEnabled`/`SetMusicVolume`/`SetSFXVolume`/`SetMusicEnabled`, fix the `PlayButtonClickSound` copy-paste bug.
- **Stage 4:** New `EconomyManager` (music/SFX settings, currency, owned cosmetics, ads-removed flag), wire `SettingsPanel` for real.
- **Stage 5:** AppLovin MAX SDK import, `AdsManager`, interstitial/rewarded placements, Android manifest, UMP consent flow.
- **Stage 6:** Unity IAP (`com.unity.purchasing`), `IAPManager` with the full product catalog, restore-purchases flow.
- **Stage 7:** `ShopPanel`, `BowSkinData`/`ArrowSkinData`, equip logic.
- **Stage 8:** App icons, manifest finalization, keystore, Play Console checklist (Data Safety, age rating, privacy policy, ads.txt, store listing).
- **Stage 9:** Regression pass, final device build smoke test.

## Human-required steps (outside Claude Code)

- AppLovin account, app registration, ad units, SDK key; Integration Manager run + per-network adapters (Stage 5).
- Google Play Console app, keystore custody, in-app product creation, internal testing track, license testers (Stage 6).
- Final app icon/brand art, privacy policy hosting, Data Safety declaration, age rating, ads.txt, store listing assets (Stage 8).
