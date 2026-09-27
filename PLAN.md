# Archer 2D — Release-Readiness Plan

Tracks the phased plan to take Archer 2D from prototype to release-ready: working menus/HUD, AppLovin MAX ads, and Unity IAP purchases (remove-ads, arrow/currency packs, cosmetic skins), Android only.

Full stage-by-stage implementation plan lives here; day-to-day execution notes and Claude Code session context live outside the repo. This file is the durable, project-facing source of truth for scope and progress.

## Context

An investigation via Unity MCP (firing arrows, driving signals, reading scenes live in the Editor) found this wasn't a polish job: the UI layer was non-functional scaffolding. `UIManager`/`BaseUIPanel` were well-built but zero concrete panels existed anywhere, `UIManager` wasn't placed in either scene, and `UIManager.Start()` hard-errored the instant it ran. There was no pause, no game-over trigger, no restart wiring, and `SoundManger.cs` had a stray `using static UnityEditor.PlayerSettings;` that would have broken the actual Android build. No ad/IAP SDK was installed; the Android `applicationId` was still Unity's placeholder; app icons were unassigned.

## Decisions locked in

- **Ads:** AppLovin MAX. **IAP:** Remove Ads (non-consumable), Arrow/currency packs (consumable), cosmetic bow/arrow skins (non-consumable). **Platform:** Android only.
- Arrow/currency packs grant **soft currency spendable only on cosmetics** — kept separate from the in-round, skill-based `arrowCount` (resets every game via `ScoreManager.ResetGame()`). No **paid** continue: currency can never be spent to extend a run.
- **Amended (Stage 2):** a **rewarded-ad revive** is in scope — watching an ad on the Game Over screen grants +5 arrows and resumes the current run with score intact, **once per run**. Ads cost attention, not money, so this does not reopen the paid-continue decision above; the once-per-run cap keeps the high score a measure of skill rather than ad patience. Tracked by `GameManager.HasUsedRevive`, cleared on `Restart()`/`ReturnToMenu()`.
- Lose condition is **arrow depletion only** (no lives/miss-limit system) for now — a miss-limit system is an explicit non-goal of this plan. Arrow depletion ends the run, subject to the single rewarded-ad revive above.
- Android `applicationId`: **`com.stillpoint.archer2d`**.
- Sequencing: fix core loop bugs before building panels to bind to them (Stage 1 → 2); finish `SoundManger` before Settings sliders can call it (Stage 3 → 4); build the save/economy layer before Shop UI or IAP has anywhere to persist results (Stage 4 before 6/7); ads before "Remove Ads" IAP has a switch to flip (Stage 5 before 6). Each stage is a separate, reviewable commit.

## Progress

| Stage | Status | Summary |
|---|---|---|
| 0 — Baseline hygiene & compile-blocking fixes | ✅ Done | See below |
| 1 — Core gameplay loop fixes | ✅ Done | See below |
| 2 — Activate the UI panel layer | 🟨 Partial | GameOver + PauseMenu + HUD + MainMenu + Settings shell + loading screen done; Settings panel not yet placed in GameScene |
| 3 — SoundManger completion | ⬜ Pending | Implement the 5 `NotImplementedException` stubs |
| 4 — Settings persistence + Economy foundation | ⬜ Pending | `EconomyManager`: currency, cosmetics, ads-removed, settings |
| 5 — AppLovin MAX ads integration | 🟨 Partial | MAX 8.6.6 imported, `APPLOVIN_MAX` on (Android); bottom-centre banner + rewarded wired, simulated until ad unit ids are set. Interstitial, consent flow, device test pending |
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

### Stage 2 (part 1) — Game Over + Pause panels ✅

- `GameOverPanel` (score / best / rewarded-ad revive / Restart / Home) and `PauseMenuPanel` built as the project's **first UI prefabs**, under `Assets/Prefabs/UI/`. `UIManager`, a dedicated `UI Canvas` and a HUD pause button placed in `GameScene`; the dead `Canvas (1)` proto game-over screen deleted.
- Layout is generated by `Assets/Editor/UIPanelBuilder.cs` + `UIStyle.cs` (menu: `Archer/UI/1..2`), so every anchor, pivot and colour is explicit and reviewable in a diff rather than buried in prefab YAML. Re-running a builder regenerates its prefab, discarding Inspector hand-tweaks.
- Three latent bugs fixed in the shared UI base classes, all of which blocked this work: `BaseUIPanel.Show`/`Hide` returned early **without invoking `onComplete`**, which permanently wedged `UIManager.ExecuteTransition`'s `WaitUntil` and froze all UI navigation; `BaseUIPanel.Awake` left `IsVisible` false on a panel with `hideOnStart == false`; `UIManager.InitializeUI` hard-coded an initial `MainMenu` state (now the `initialState` field, `Gameplay` in GameScene).
- Revive plumbing: new `OnRunContinued` signal (Bow re-nocks on it), `GameManager.ContinueRun()`/`ReturnToMenu()`/`HasUsedRevive`. `ReturnToMenu()` also fixes a real bug — the old Home path called `Resume()` without clearing `IsGameOver`, and `GameManager` is `DontDestroyOnLoad`, so a stale game-over state carried into the menu and the next run.
- `ScoreManager.PreviousScore` (dead, zero references) became `BestScore` on the same `PreviousScorePP` key, promoted on `OnGameOver` via `Max` so it stays correct across a revive. Promotion cannot live in `ResetGame()` — `Bow.Start()` calls that and zeroes `CurrentScore`.
- Ads seam added early (`Assets/Scripts/Manager/Ads/`): `IRewardedAdProvider`, `SimulatedRewardedAdProvider`, `MaxRewardedAdProvider` (behind `#if APPLOVIN_MAX`), `AdsConfig` Resources asset with blank ids, `AdsManager` singleton. **Stage 5 is still pending** — the MAX plugin is not imported and the ids are blank, so the simulated provider runs.
- **Verified live** in the running Editor via Unity MCP, not just compiled: zero console errors/warnings; game over → panel, `timeScale 0`, HUD hidden; revive → +5 arrows, score preserved, `timeScale 1`, **bow re-armed with a nocked arrow**; second depletion → ad button disabled, "AD USED"; 6 pause/resume toggles with `isTransitioning` releasing every time; Restart → score 0, best kept, revive re-armed; Home → MainMenu with `IsGameOver == false`. Screenshots at 16:9, 20:9, 4:3 (2048×1536) and 1024×768 all hold.
- **Known pre-existing issue newly made reachable:** leaving `GameScene` at runtime spews `MissingReferenceException: Enemy` plus `Invalid prefab or poolable type` — `EnemySpawner`/`ObjectPoolManager` are `DontDestroyOnLoad` and keep DOTween tweens and pool entries pointing at destroyed scene objects. Confirmed pre-existing (a raw `SceneManager.LoadScene` with no new code involved reproduces it), but until now nothing could leave the scene. Worth its own fix.

### Stage 2 (part 2) — Scene-reload crash fixes ✅

Wiring up Restart/Home made runtime scene reloading reachable for the first time, which exposed a set of pre-existing cross-scene leaks. Reproduced live (game over → RESTART) and fixed:

- **Root cause — `Singleton<T>.Awake` called `DontDestroyOnLoad` on scene-placed singletons.** `EnemySpawner` therefore survived every reload holding destroyed scene references (`endPoint`, all 7 `lstOfEnemies` entries — plain `fileID` scene refs, not prefab assets), and `Singleton.Awake`'s duplicate branch then **destroyed the fresh, correctly-wired spawner**, making the stale one permanently authoritative. Symptom in the log: `[Singleton] Duplicate instance of EnemySpawner destroyed`, once per load. Fix: removed `DontDestroyOnLoad` from `Awake` only. The lazy-create path in `Instance` **keeps** it — `SignalManager` is created there, and if it stopped persisting its `OnDestroy` would `ClearAllObservers()` while `ScoreManager` (cached forever in `DependencyResolver`, subscribes once in its constructor at boot) never re-subscribes, silently killing all scoring.
- **`ObjectPoolManager` pools outlived their contents.** It persists but every pooled instance is a scene object, so stacks filled with destroyed objects and the next `SpawnObject` popped a corpse. Now clears on `SceneManager.sceneUnloaded` and skips destroyed entries when popping.
- **The error handler was the crash.** `Debug.LogError($"...{prefab?.name}")` — `?.` does a *real* null check, bypassing Unity's overloaded `==` fake-null, so on a destroyed object it proceeded to call `.name` and threw. Replaced with Unity-null-safe `SafeName()`/`IsAlive()` helpers. Note `IsAlive` takes `UnityEngine.Object`, not a generic `T`: comparing a type parameter against null can compile to plain reference equality and miss fake-null entirely.
- **`VFXHandler.OnDestory` → `OnDestroy`.** The misspelled callback meant `Dinit()` never ran, so `ScoreAddingVFXHandler`/`ArrowAddingVFXHandler` never unsubscribed from the persistent `SignalManager` — every reload added another dead subscriber, and `Bow.SpawnArrow()` dispatches `OnAddArrows(-1)` on its first line, so each arrow fired invoked them all. (Deliberate deviation from CLAUDE.md's "match the typo" guidance: this is a dead Unity message, not a public signal type rename.) Their `Dinit()` then needed `SignalManager.Instance?.` guards, since it genuinely runs now.
- Also: null-guarded `Bow`'s pooled-spawn result (dereferenced 5×, and `SpawnObject` has a documented `return null`); `transform.DOKill()` on `Enemy` despawn/destroy plus guards in `DestroyEnemy`/`GotToTarget` (tweens were writing to destroyed transforms); `SignalManager.Instance?.` in `GamePlayHUD.OnDestroy`; defensive guards in `EnemySpawner.SpawnEnemy`.
- **Verified live:** 3 restart cycles + Home→menu→Play + rewarded-ad revive + exit play mode → **zero exceptions of any kind** in `Editor.log`. Observer counts stay flat across reloads (`OnAddArrows: 2`, `OnBalloonBurst: 2`), exactly one `EnemySpawner` and it lives in `GameScene` not `DontDestroyOnLoad`, `spawners=0` while sitting in the menu, and enemies still spawn after restarts (they had silently stopped in run 2+).
- **Known pre-existing, still open:** pooled objects get no per-spawn state reset (nothing overrides `OnObjectSpawn`, so a recycled `Enemy` returns with `curHealth == 0` from `Start()` running once); small balloon prefabs serialize removed `poolableType` fields with `Poolable` unset; `UIManager.Awake` hides `Singleton.Awake` instead of overriding it.

### Stage 2 (part 3) — Main Menu + Settings shell ✅

- The prototype `MainMenu` scene had two plain Play/Quit buttons wired through Inspector `onClick` bindings to `MainMenuController`, and no `UIManager`. The scene is now on the panel system: a `UI Canvas` (settings copied from the old canvas, `UI` sorting layer), `MainMenuPanel` + `SettingsPanel` prefab instances, and a `UIManager` with `initialState = MainMenu`. The old `Canvas` (and the controller on its `Main Menu` child) was removed.
- `MainMenuPanel` (`Assets/Scripts/View/Panels/`) is a full-screen landing layout with no scrim, so the mountain background stays visible. Top to bottom: the ARCHER title, a BEST pill (`ScoreManager.BestScore`, refreshed on every `OnStateEnter`), PLAY, SETTINGS, disabled SHOP / NO ADS "(SOON)" placeholders, and QUIT. Play calls `GameManager.Resume()` before `LoadScene("GameScene")` so no paused `timeScale` leaks into a run.
- **The Shop / Remove Ads placeholders are deliberately non-functional** until Stages 6-7. They sit behind `MainMenuPanel.showComingSoonButtons` (default on). Turn it off before any interim store release, because dead buttons in a shipped build draw bad reviews.
- `SettingsPanel.prefab` is the first prefab for the existing shell script: a SETTINGS title, "Audio options coming soon" and BACK. Real controls wait on Stages 3-4.
- Builder: `UIPanelBuilder` now also builds both prefabs (`Archer/UI/1`) and gained `Archer/UI/3. Wire MainMenu UI`. `CreateUiManager` takes the initial state per scene. The column is sized to about 854px, so it keeps ~60px margin on a 20:9 phone, where the 1980×1080 match-0.5 canvas is only ~980px tall.
- **Verified live** via Unity MCP: menu renders at 16:9 with the correct BEST value. Settings → Back and Settings → Escape both return to MainMenu. SHOP / NO ADS are non-interactable. Play → GameScene (`Gameplay`, `timeScale 1`, enemies spawning). A forced game over with a new high score → Home → the menu shows the updated BEST, with `IsGameOver == false` and `timeScale 1`. `Editor.log` has zero errors or exceptions across the loop. Testing note: an unfocused Editor stops ticking the player loop (`runInBackground` is false), so MCP-driven tests need `EditorApplication.Step()` while paused.
- **Open:** `GameScene` still has no `SettingsPanel` instance, so Pause → SETTINGS hits `UIManager`'s "No panel registered for state Settings" error and does nothing.

### Stage 2 (part 4) — Loading screen + layered sky ✅

- **Loading screen.** New `SceneLoader` (`Assets/Scripts/Utility/`, `Archer.Scripts.Utility`) handles every scene change: Play, Restart and Home.
  - It is lazily created and `DontDestroyOnLoad`, and must never be placed in a scene.
  - It runs `LoadSceneAsync` with activation held, keeps the overlay up for at least 0.6s, then waits 2 settle frames after activation so the new scene's `Start`/pool spawning happens under the cover. Everything runs on unscaled time.
  - A double-call guard stops a double-tapped Play from starting two loads.
  - The visuals are `Resources/UI/LoadingScreen.prefab` (`LoadingScreenView`, built by `Archer/UI/1`): its own Screen Space – Overlay canvas at sort order 1000, the sky, drifting UI clouds, the title, a progress bar and "LOADING...".
  - It is not a `UIState.Loading` panel, because `UIManager` and its panels are scene-local and die in the very load being covered.
- `GameManager.Restart()`/`ReturnToMenu()` now **stay paused during the load** and `Resume` in the loader's `onLoaded`. Otherwise the old GameScene would run live behind the overlay for about a second: balloons moving, a possible `OnGameOver`, arrows firing.
- **Bow input guard:** `Bow.Update` returns early while `GameManager.IsPaused`. Input is read raw (`Input.GetMouseButton*`), so the pause/game-over dimmer and the loading overlay never blocked it. This also fixes the existing issue where tapping RESUME drew the bow. It is **not verified live**, because `Input` can't be simulated through MCP; check it by hand.
- **Layered sky, both scenes.** `FullBG/1_Mountain.png` had 4 clouds painted in, and drifting clouds over frozen painted ones is what looked fake.
  - `Archer/Scenes/Build Layered Backgrounds` (`Assets/Editor/SceneBackgroundBuilder.cs`) swaps it for the pack's layers at the same transform: Sky `Layer_0` (-100), Mountains `Layer_2` (-70), Trees `Layer_3` (-65), Ground `Layer_4` (-60).
  - It removes GameScene's 6 hand-placed clouds and adds a `CloudField`.
  - The builder is safe to re-run.
- **`CloudField`** (`Archer.Scripts.Manager`) spawns 7 `Cloud.prefab` clones at a random depth each.
  - Far clouds are smaller, slower, higher, fainter and hazed toward the sky colour. Near clouds are bigger and faster.
  - Clouds sort at -90..-80, so low clouds pass *behind* the mountains.
  - Each cloud gets a random Cloud_1..4 sprite with random flipX, and positions are pre-spread so the sky is full on the first frame.
  - On wrap, a cloud re-rolls its depth and re-seats itself off-screen using its new width.
  - `CloudDrifter` gained `Configure`/`SetBaseY`, a sine bob and a `Wrapped` event. Unconfigured instances behave exactly as before.
- Both builders now refuse to run in play mode. In play mode `AddComponent` runs `Awake`, and `BaseUIPanel.Awake` deactivated the panels mid-build, which saved every panel prefab inactive at alpha 0. This happened once during this work and was rebuilt.
- **Verified live** via Unity MCP:
  - Menu screenshot: painted clouds gone, layers aligned.
  - Over 300 frames near clouds drift ~3× farther than far ones, with a gentle bob.
  - A forced wrap re-rolls the depth and re-seats the cloud off-screen.
  - Play (double-invoked) → one load, with the overlay screenshotted at 42% → `Gameplay`, `timeScale 1`, enemies spawning, exactly one `SceneLoader`.
  - GameScene screenshot: clouds behind balloons, bow and HUD fine.
  - 3× game over → Restart: old scene frozen mid-load (`timeScale 0`, enemy position unchanged), arrives clean, observer counts flat (`OnAddArrows 2`, `OnBalloonBurst 2`, `OnGameOver 3`).
  - Home → MainMenu clean.
  - Zero errors or exceptions in `Editor.log`.
- **Unreproduced glitch:** once during testing, after Play from the menu, a game over left `UIManager.isTransitioning` stuck. The HUD fade coroutine advanced one frame and stopped, and the UI stayed on Gameplay while paused. 4 targeted repro attempts (direct start, menu → Play, Restart, with and without screenshots) all transitioned correctly. It may be an artifact of the MCP frame-stepping harness, but that is unconfirmed. If it ever shows up in real play, suspect `UIManager.ExecuteTransition`'s `WaitUntil` on a panel coroutine that stopped.
- **Noticed, not fixed:** the HUD pause button (top-right) overlaps the "Score" label, which reads "Sco".

### Stage 5 (part 1) — MAX plugin + bottom-centre banner 🟨
- **Scope change:** a **bottom-centre banner, shown at all times including gameplay**, was added to Stage 5. The original scope listed rewarded + interstitial only. The owner accepted the accidental-tap risk of a gameplay banner, which ad networks treat as invalid traffic. Mitigations:
  - `Bow` ignores presses that start on the banner, plus 8dp padding (`AdsManager.IsPointOverBanner`).
  - The banner is a fixed 320×50dp (728×90dp on tablets). Adaptive/full-width banners are disabled.
- **Plugin:** AppLovin MAX Unity Plugin **8.6.6** (Android SDK 13.6.4), imported from the official GitHub release `.unitypackage`. It brings Google EDM4U 1.2.186.
  - The Android resolver uses Gradle templates in `Assets/Plugins/Android`: `mainTemplate.gradle` declares `com.applovin:applovin-sdk:13.6.4`, with AndroidX + Jetifier enabled.
  - No system `JAVA_HOME` is needed: Unity's bundled OpenJDK resolves the dependencies at build time. Import-time "JAVA_HOME is not set" errors came from the resolver's first pass running before the templates existed; a Force Resolve cleared them.
- **`APPLOVIN_MAX` scripting define:** set for **Android only**. Other build targets, including the Editor on Standalone, compile without MAX.
- **Code (`Assets/Scripts/Manager/Ads/`):**
  - `IBannerAdProvider`.
  - `SimulatedBannerAdProvider`: a grey placeholder on a top-most overlay canvas, parented to the `DontDestroyOnLoad` `AdsManager`.
  - `MaxBannerAdProvider` (under `#if APPLOVIN_MAX`, using `MaxSdk.AdViewConfiguration(BottomCenter) { IsAdaptive = false }`).
  - `BannerMetrics`: dp→px conversion. In the Editor the Game view is treated as a phone: 393dp short side.
  - `MaxSdkBootstrap`: initialises the SDK exactly once. The rewarded provider now waits on it instead of calling `InitializeSdk()` itself.
  - `AdsConfig.bannerAdUnitId`.
  - `AdsManager.ShowBanner/HideBanner/SetBannerAllowed/IsPointOverBanner`.
  - The banner is shown once at boot from `SplashScreenLoader`.
- **`SetBannerAllowed(false)` has no caller yet.** It is the switch the Stage 6 "Remove Ads" purchase must flip, and the rewarded revive stays available regardless.
- **Verified in Editor (simulated provider):**
  - The banner is drawn exactly where the hit test expects.
  - No HUD / Pause / Game Over button overlaps it.
  - It survives Restart (one instance).
  - The rewarded revive still resolves.
  - The project compiles with `APPLOVIN_MAX` (0 errors), and blank ids fall back to the simulated providers.
- **Not verified:** a real press on the banner in the Game view; real ad fill; device sizing; the consent dialog.
- **Still to do in Stage 5:**
  - SDK key in Integration Manager and the Android banner/rewarded ad unit ids in `AdsConfig` (human).
  - Mediation adapters.
  - MAX Terms & Privacy Policy (UMP) consent flow, which needs a hosted privacy-policy URL.
  - Interstitial placement.
  - Device test in MAX test mode / Mediation Debugger.

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
