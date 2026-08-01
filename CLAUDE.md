# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Archer 2D is a Unity 2D mobile-style archery game: drag-to-draw a bow, fire arrows at balloon enemies that float toward an end point, score points, and manage a limited arrow supply that regenerates on hit streaks.

- Engine: Unity **6000.5.6f1** (Unity 6). Open the project with this exact editor version via Unity Hub — mismatched versions will trigger a reimport and can churn `.meta`/serialized scene data.
- There is no CLI build/test pipeline in this repo (no Makefile, package.json, or CI config). All building, playing, and testing happens inside the Unity Editor:
  - Play the game: open `Assets/Scenes`, load the main scene, press Play.
  - Build: Unity Editor `File > Build Settings`.
  - Tests: `com.unity.test-framework` is installed as a package dependency. `Assets/Scripts/Tests/Archer.Scripts.Tests.asmdef` is an EditMode test assembly, currently empty (no test scripts yet). **It cannot reference gameplay types (`ScoreManager`, `GameManager`, etc.) yet** — `Assets/Scripts/` has no `.asmdef` of its own, so those scripts compile into the implicit `Assembly-CSharp`, which a custom assembly definition can't reference. Adding a runtime `.asmdef` for `Assets/Scripts/` (or an equivalent restructure) is a prerequisite the first time a test needs to touch gameplay code — do this deliberately, not as a side effect of adding a test. Run tests via `Window > General > Test Runner` (EditMode/PlayMode).
- `Packages/manifest.json` includes `com.coplaydev.unity-mcp` (MCP for Unity, pinned to `v9.7.3`), a Unity Editor bridge. It's registered with this Claude Code session as the `UnityMCP` MCP server (local scope, via `uvx --from mcpforunityserver==9.7.3 mcp-for-unity`) — its tools give live Editor control (scenes, GameObjects, console, play mode, etc.) as a separate tool surface, but only work while the Unity Editor has this project open (the bridge runs inside the Editor).

## Namespace convention

`Assets/Editor/NamespaceAdder.cs` is a custom `AssetModificationProcessor` that auto-inserts a namespace (rooted at `Archer`, mirroring folder structure, e.g. `Archer.Scripts.View.Manager`) into any **newly created** script under `Assets/`, excluding `Plugins`/`ThirdParty`/`External`/`Libraries`/`Packages` folders and `*Test(s).cs` files.

In practice this is not applied retroactively, so the codebase is a mix:
- Namespaced: `GameManager`, `UIManager`, `BaseUIPanel`, `IUIPanel`, `SplashScreenLoader` (under `Archer.Scripts.*`).
- Global namespace: most gameplay/utility classes — `Bow`, `Arrow`, `Enemy`, `EnemySpawner`, `ScoreManager`, `SoundManger`, `SignalManager`, `Singleton<T>`, `DependencyResolver`, `DependencyInjectionManager`, `ObjectPoolManager`, `PoolableObject`, all signal/event classes.

When adding new scripts, match whatever convention the sibling files in that folder already use rather than "fixing" it project-wide.

## Core architecture

The codebase leans on four hand-rolled infrastructure systems (all in `Assets/Scripts/Utility/` unless noted) that most gameplay code touches:

**`Singleton<T>` (`Utility/Singleton.cs`)** — generic `MonoBehaviour` base providing a lazy, `DontDestroyOnLoad` global instance (`SignalManager`, `SoundManger`, `ObjectPoolManager`, `EnemySpawner`, `UIManager` all derive from it). Auto-creates a GameObject if no instance exists in the scene.

**`SignalManager` (`Utility/SignalManager.cs`)** — a global, thread-safe pub/sub event bus and the primary way decoupled systems communicate (gameplay code rarely calls across systems directly). `SignalManager.Instance.AddObserver<TSignal>(handler)` / `DispatchSignal<TSignal>(data)`, where `TSignal` is a plain C# class (not a `MonoBehaviour`/`ScriptableObject`). Signal classes live in `Assets/Scripts/Models/` (gameplay: `OnBalloonBurst`, `OnAddArrows`, `OnArrowsAdded`, `OnArrowDestoryed`, `OnUpdateScore`, `OnUpdateStreak`) and `Assets/Scripts/View/Events/` (UI: `OnUIStateChanged`, `OnUITransitionStarted`, `OnUITransitionCompleted`). When adding a new cross-system notification, add a small signal-data class rather than a direct method call/reference.

**`DependencyResolver` / `DependencyInjectionManager` (`Utility/`)** — a minimal custom DI container for plain (non-`MonoBehaviour`) services. `DependencyInjectionManager.RegisterDependencies()` is the single place new services get registered (currently just `ScoreManager`); `Initialize()` + `InitializeServices()` are invoked once at boot from `SplashScreenLoader.LoadDataInRunTime()` (a `[RuntimeInitializeOnLoadMethod]`). Consumers call `DependencyResolver.Resolve<T>()` (e.g. `Bow`, `GamePlayHUD` both resolve `ScoreManager` this way instead of a scene reference).

**`ObjectPoolManager` (`Utility/ObjectPooling/`) + `PoolableObject` + `PoolableTypes`** — generic pooling keyed by a `PoolableTypes` ScriptableObject asset (not a C# type), so pool identity is data-driven per prefab. `Enemy` and `Arrow` both derive from `PoolableObject` and override `OnObjectSpawn`/`OnObjectDespawn`. Spawn via `ObjectPoolManager.Instance.SpawnObject(prefab, pos, rot)`, return via `DespawnObject(obj)`.

**`UIManager` (`View/Manager/UIManager.cs`) + `IUIPanel`/`BaseUIPanel` (`View/`)** — a `UIState`-driven navigation system (`MainMenu`, `Gameplay`, `PauseMenu`, `GameOver`, `Settings`, `Loading`). Panels auto-register from the scene on `Awake` (any `BaseUIPanel` found). Transitions are queued and executed one at a time via coroutine (`Fade`/`Scale`/`Instant`), dispatching `OnUIStateChanged`/`OnUITransitionStarted`/`OnUITransitionCompleted` signals. New UI screens should subclass `BaseUIPanel`, set a `panelState` in the inspector, and override `Initialize`/`OnStateEnter`/`OnStateExit`/`OnStateUpdate` rather than wiring visibility by hand.

## Gameplay flow

`Bow` (`Manager/Bow.cs`) reads mouse drag input, computes launch force/angle from drag distance (clamped by `BowData`, a ScriptableObject), and renders the bowstring via `LineRenderer`. On release it hands the pooled `Arrow` off (`isFired = true`, sets `Rigidbody2D` velocity) and immediately spawns the next arrow from the pool if `ScoreManager.arrowCount >= 1` (arrows are consumed via `OnAddArrows(-1)` signals, not decremented directly).

`Arrow` (`Manager/Arrow.cs`) is a `PoolableObject`; on trigger collision with an `Enemy` it deals damage, dispatches `OnUpdateStreak`, and plays a hit sound. It despawns itself (`DisableArrow`) after impact/miss and dispatches `OnArrowDestoryed`, which `Bow` listens for to spawn the next arrow.

`Enemy` (`Manager/BallonEnemies/Enemy.cs`) is a `PoolableObject` that DOTween-moves (`transform.DOMove`) toward `EnemySpawner.endPoint`; on death it triggers a burst animation, dispatches `OnBalloonBurst` (score payload), and returns itself to the pool after a delay. The per-color subclasses (`RedBalloonEnemy`, `BlueBalloonEnemy`, `GreenBalloonEnemy`, etc. in the same folder) are empty — they exist purely so different prefabs/colors can be distinguished by type for pooling/spawning, not for behavior differences.

`EnemySpawner` (Singleton) picks a random spawn point and random enemy prefab from `lstOfEnemies` on an interval, spawns from the pool, and sends it toward `endPoint`.

`ScoreManager` (plain C# class, not a `MonoBehaviour` — resolved via DI) subscribes to `OnBalloonBurst`/`OnAddArrows`/`OnUpdateStreak` in its constructor and is the single source of truth for score/arrow/streak state. Score persists via `PlayerPrefs` (`CurrentScorePP`/`PreviousScorePP`); `arrowCount`/`streakCount` are in-memory only. A maintained hit streak (`streakCount > 1`) grants a bonus arrow.

`GamePlayHUD` and VFX handlers (`Manager/VFX/`) are purely reactive — they listen for `OnUpdateScore`/`OnArrowsAdded`/etc. and update text or play effects, with no gameplay logic of their own.

`SoundManger` (Singleton) loads a `BowSoundManager` ScriptableObject from a hardcoded `Resources.Load` path (`Scriptables/Sounds/BowSoundData`) and exposes simple `PlayXSound()` wrappers. Several of its methods (`PlaySFX`, `SetSFXEnabled`, `SetMusicVolume`, `SetSFXVolume`, `SetMusicEnabled`) are stubs that `throw new NotImplementedException()` — check before assuming they're wired up.

## Known rough edges to be aware of

- `Bow.Update()` contains a large commented-out block duplicating arrow-respawn logic that now happens via the `OnArrowDestoryed` signal handler — leave it alone unless asked to clean it up.
- `GameManager` (`Manager/GameManager.cs`) is an empty stub.
- Enemy/pooling classes have inconsistent typo'd naming inherited from the original code (`BallonEnemies` folder, `OnArrowDestoryed`, `destoryTime`, `OnDestory` instead of `OnDestroy` in a couple of places) — match existing spelling in a file rather than "fixing" it in isolation, since renaming public signal/event types is a cross-file change.
