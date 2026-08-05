# Architecture — current state

Living document. Describes what exists in the repository right now, not the target design.
For the target design read `TECHNICAL_PLAN.md`; for the schedule read `MVP_PLAN.md`.

**Engine:** Unity 6000.3.6f1 · URP 17.3.0 · NGO 2.13.1 · Multiplayer Services 2.3.0

---

## 1. Assemblies

Dependencies point downward only, enforced physically by assembly definitions rather than by
discipline. Adding an upward reference is a compile error, which is the point.

```
Office.Data          (no references)
   ↑
Office.Core          → Data
   ↑
Office.Network       → Core, Data, NGO, Services.{Core,Authentication,Multiplayer}
   ↑
Office.Gameplay      → Core, Data, Network, NGO, InputSystem
   ↑                        ↑
Office.Enemies       Office.Anomalies      → + Gameplay
Office.LevelGen      → Core, Data, Network, AI.Navigation
Office.UI            → Core, Data, Network, Gameplay, InputSystem, TMP, uGUI
Office.Audio         → Core, Data
Office.Rendering     → Core, Data, URP
Office.Editor        → everything (Editor platform only)
Office.Tests.EditMode / .PlayMode → everything
```

Two assemblies were added beyond Technical Plan §3.2: `Office.Rendering` (URP render features
for the PS1 pipeline in Sprint 9 — it must reference URP, and nothing else should) and
`Office.Editor` (setup tooling, which must never ship in a build).

`Office.Rendering`, `Office.Enemies`, `Office.LevelGen`, `Office.Anomalies`, `Office.Audio` and
`Office.Tests.PlayMode` are empty. They exist so that the first file written into each one lands
in the right place instead of in `Assembly-CSharp`.

Every asmdef sets `autoReferenced: false` — no project code may live outside an assembly.

---

## 2. Composition root

`SCN_Boot` is build index 0 and never unloads. It contains three objects and no gameplay:

| Object | Components |
|---|---|
| `NetworkManager` | `NetworkManager`, `UnityTransport` |
| `[Bootstrap]` | `GameBootstrap`, `NetworkServiceInstaller` |
| `[DevUI]` | `DevSessionPanel` (hidden, F1) |

The session itself is **not** in this scene. `NetworkServiceInstaller` spawns `PF_Session`
(`SessionRoot`, `LobbyRoster`, `SessionDirector`, `PlayerSpawner`, `RunSceneFlow`) when the
server starts, and every instance moves itself to `DontDestroyOnLoad`.

`GameBootstrap` runs at `[DefaultExecutionOrder(-10000)]`, registers the core services, then runs
every `ServiceInstaller` in ascending `Order`. Teardown runs installers in reverse.

Services registered today:

| Interface | Implementation | Installed by |
|---|---|---|
| `IEventBus` | `EventBus` | `GameBootstrap` |
| `ISceneLoader` | `SceneLoader` | `GameBootstrap` |
| `IGameStateService` | `GameStateMachine` | `GameBootstrap` |
| `RunState` | `RunState` | `GameBootstrap` |
| `ISessionService` | `MultiplayerSessionService` | `NetworkServiceInstaller` |
| `ILobbyService` | `LobbyService` | `NetworkServiceInstaller` |

**The installer pattern is why this works.** `Office.Core` may not reference `Office.Network`,
so the composition root cannot construct a session service directly. Instead it knows only the
abstract `ServiceInstaller` MonoBehaviour, and each higher assembly supplies a subclass that
registers its own services. Dependencies still point downward.

**Known ordering trap, already hit once:** `NetworkManager.Singleton` is null inside
`GameBootstrap.Awake` because the bootstrap deliberately runs first. `NetworkServiceInstaller`
therefore holds a serialized same-scene reference to the NetworkManager. Do not replace it with
`.Singleton`.

---

## 3. Scenes

| Scene | Role | Loaded |
|---|---|---|
| `SCN_Boot` | Composition root, NetworkManager, session | Build index 0, never unloads |
| `SCN_Lobby` | Pre-run room: roster, ready, start | Additively at boot, and on return from a run |
| `SCN_Sandbox` | Greybox test space | Additively when the run starts |
| `SCN_Main` | Legacy template scene | To be deleted |

`NetworkConfig.EnableSceneManagement` is **off**. Each client drives its own scene flow from the
replicated phase, so letting NGO also push scenes would load the same geometry twice on a
joining client. This flips back on in Sprint 6, when the floor generator owns scene flow — that
is a required change, not an optional one.

**This setting has a consequence that cost a debugging session.** With scene management off, NGO
cannot resolve **in-scene placed NetworkObjects** on a remote client: it sends them as ordinary
spawns, the client looks for a matching entry in its prefab registry, finds none, and logs
`NetworkPrefab could not be found`. The host never sees it, because the host already has the
object. Anything networked and persistent must therefore be a **registered prefab that the server
spawns**, not an object sitting in a scene — until scene management is turned on.

`SCN_Boot` holds no reference to level content. Systems find each other through the service
locator and the event bus, never through inspector references across scenes.

---

## 4. Networking

**Topology:** client-hosted listen server over Unity Relay. No dedicated servers.

**Session stack:** the Unity Multiplayer Services SDK (`com.unity.services.multiplayer`) wraps
Relay, Lobby and the NGO handshake behind a single `CreateSessionAsync(...).WithRelayNetwork()`
call. This is a deviation from Technical Plan §2.2, which described wiring Relay and Lobby
separately; the Sessions API is the supported path in Unity 6 and removes roughly a hundred
lines of allocation and polling code. It is still Unity Lobby underneath.

`MultiplayerSessionService` never throws at its caller. Failures land in `Phase` and `LastError`.

**Authentication profiles.** Each editor process signs in under `editor{processId}`. Multiplayer
Play Mode virtual players are separate processes sharing one project folder; without distinct
profiles they all authenticate as the same anonymous player and the second one evicts the first.

**Authority today:**

| Domain | Authority |
|---|---|
| Player movement and look | Owner (client-authoritative) |
| Spawn position | Server picks, owner applies |
| Session phase, lobby roster, ready flags | Server |
| Player object creation | Server |
| Everything else | Not implemented yet |

**Network prefab registry.** `Assets/DefaultNetworkPrefabs.asset` holds `PF_Player` and
`PF_Session`, and is referenced by the NetworkManager. `ForceSamePrefabs` is on, so client and
server must carry identical lists — after adding any spawnable prefab, both machines need the
updated asset. `Office/Setup/Build Session Prefab` registers entries explicitly rather than
relying on Unity's auto-add editor preference, because a missing entry only fails on a remote
client.

### 4.1 Session phase and the run loop

`SessionDirector` owns a `NetworkVariable<GameState>`. The server is the only thing that decides
a transition; it validates against `GameStateMachine.IsLegal` — the same table the local machine
uses — and writes the variable. Every client, host included, applies what arrives through
`IGameStateService.SetFromAuthority`. One decision point, one code path, and the local machine
stays a mirror rather than a second source of truth.

```
Lobby ──(host presses Start, everyone ready)──▶ Generating
                                                    │
              every client loads SCN_Sandbox, unloads SCN_Lobby,
                    then calls ReportRunSceneReadyRpc
                                                    │
                        all clients reported ───────▶ InRun
                                                    │       (server spawns player objects)
              host presses End Run ──▶ RunFailed ──▶ Lobby
                                                    │
                    clients reload SCN_Lobby, players despawn, ready flags cleared
```

The scene-ready handshake is not optional: without it a fast machine spawns players into a scene
a slow machine has not finished loading.

`GameState` has no direct `InRun → Lobby` edge, so aborting a run passes through `RunFailed`.
Adding a shortcut would let a run end without ever reaching a terminal state.

`NetworkTransform` runs in `AuthorityModes.Owner` with scale sync disabled. Non-owner instances
have their `CharacterController` disabled by `PlayerRig` so it cannot fight the replicated
transform.

---

## 5. Player prefab

`Assets/Project/Prefab/Player/PF_Player.prefab`, generated by `Office/Setup/Build Player Prefab`.

```
PF_Player                    layer: Player
├── CharacterController      h 1.8, r 0.32, step 0.35, skin 0.03
├── NetworkObject
├── NetworkTransform         AuthorityMode = Owner, scale sync off
├── PlayerInputReader        disabled in prefab; PlayerRig enables it for the owner
├── PlayerMovement           walk / sprint / crouch / stamina
├── PlayerLook               yaw on body, pitch on pivot, view bob
├── PlayerRig                owner-vs-remote split, cursor lock
├── PlayerSpawnAnchor        server picks a spawn point, owner teleports
├── Body (capsule)           hidden from the owner
├── FacingMarker (cube)      so facing is readable in greybox
└── CameraPivot              y 1.62 standing, 0.92 crouched
    └── PlayerCamera         Camera + AudioListener, owner only
```

Every tunable number lives in `CFG_PlayerMovement` and `CFG_PlayerLook`, never in the prefab.

`NetworkConfig.PlayerPrefab` is **null** on purpose. NGO's automatic player spawning fires the
instant a client connects, which would drop a capsule into the lobby where there is no floor.
`PlayerSpawner` creates player objects when the run begins and despawns them when it ends.

`canJump` is **true** in the current config for probing greybox geometry. GDD §7.1 lists walk,
sprint, crouch and vault — not jump. Set it back to false before the vertical slice.

---

## 6. Physics layers

| Index | Layer | Purpose |
|---|---|---|
| 8 | `Player` | Player capsules |
| 9 | `Enemy` | Enemy colliders |
| 10 | `Interactable` | Anything with `IInteractable` |
| 11 | `LevelGeometry` | Walls, floors, kit pieces |
| 12 | `Projectile` | Staples, sprays |
| 13 | `Prop` | Physics props, not interactable |
| 14 | `VoiceEmitter` | Equipment voice channel emitters |
| 15 | `ViewModel` | First-person hands and held items |

Mirrored in `Office.Data.PhysicsLayers`, which `PhysicsLayersTests` verifies against
TagManager.asset — renaming a layer without updating the constant makes raycasts miss silently.

Collision matrix, configured by `Office/Setup/Configure Collision Matrix`:

- `ViewModel` collides with nothing
- `VoiceEmitter` collides with nothing
- `Projectile` × `Projectile` off
- `Player` × `Player` off — a teammate must never block a doorway during a chase

---

## 7. Editor tooling

| Menu item | Effect |
|---|---|
| `Office/Setup/Run All` | Everything below, in order |
| `Office/Setup/Configure Collision Matrix` | Writes the matrix into DynamicsManager.asset |
| `Office/Setup/Create Config Assets` | Creates the player config ScriptableObjects |
| `Office/Setup/Build Player Prefab` | Regenerates `PF_Player` from code |
| `Office/Setup/Build Session Prefab` | Regenerates `PF_Session` and registers both network prefabs |
| `Office/Setup/Import TextMeshPro Essentials` | One-time TMP resource import, needed before the lobby |
| `Office/Setup/Build Sandbox Scene` | Regenerates `SCN_Sandbox` |
| `Office/Setup/Build Lobby Scene` | Regenerates `PF_LobbyRow` and `SCN_Lobby` |
| `Office/Setup/Build Boot Scene` | Regenerates `SCN_Boot` |
| `Office/Setup/Configure Build Settings` | Scene list, `SCN_Boot` at index 0 |
| `Office/Tests/Run EditMode Tests` | Runs the suite, logs a one-line summary |

All of it is idempotent. Prefabs and scenes are YAML that two people cannot merge, so anything
that can be regenerated from code is — a broken prefab is fixed by re-running a menu item rather
than by resolving an unreadable conflict.

**Trap worth knowing.** A reference to an asset created moments earlier goes stale as soon as
the AssetDatabase reimports it, and `EditorSceneManager.NewScene` triggers exactly that reimport.
Assigning the stale wrapper to a `SerializedProperty` writes a silent null. Always reload a
freshly created prefab from its path after a scene swap. `Wire` now logs an error on a null
value so this fails loudly instead of producing an empty player list at runtime.

---

## 8. What is deliberately not here yet

Interaction, inventory, damage, enemies, level generation, power, voice, audio service, HUD,
the PS1 render pipeline. Each has an empty assembly waiting for it.

Known gaps in what does exist:

- **Two-client behaviour is not machine-verified.** The single-client path is exercised end to
  end, but a second client appearing in the roster and its ready flag replicating has only been
  reasoned about, not tested. Multiplayer Play Mode has no scriptable API for activating a
  virtual player, and NGO 2.13 does not ship its integration-test helpers. Verify by hand
  (README) until there is a way to automate it.

  This gap has already produced one shipped bug: the session object was originally placed in
  the Boot scene, which works for a host and fails for every client. Treat anything that only
  a remote client exercises as unverified until two machines have run it.
- **Late join during a run** spawns a player once that client reports its scene ready, but the
  case is untested and the lobby does not lock.
- **The lobby look is placeholder.** GDD §14 wants a retro terminal HUD; that pass belongs with
  the PS1 render pipeline in Sprint 9.
