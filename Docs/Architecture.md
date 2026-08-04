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
| `[DevUI]` | `DevSessionPanel` |

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
| `SCN_Boot` | Composition root, NetworkManager | Build index 0, never unloads |
| `SCN_Sandbox` | Greybox test space | Additively by `GameBootstrap.firstScene` |
| `SCN_Lobby` | Placeholder, empty | Not yet used |
| `SCN_Main` | Legacy template scene | To be deleted |

`NetworkConfig.EnableSceneManagement` is **off**. Every client loads `SCN_Sandbox` itself at
boot, so letting NGO also synchronise scenes would load the same geometry twice on a joining
client. This flips back on in Sprint 6, when the floor generator owns scene flow — that is a
required change, not an optional one.

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
| Everything else | Not implemented yet |

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
| `Office/Setup/Build Sandbox Scene` | Regenerates `SCN_Sandbox` |
| `Office/Setup/Build Boot Scene` | Regenerates `SCN_Boot` |
| `Office/Setup/Configure Build Settings` | Scene list, `SCN_Boot` at index 0 |
| `Office/Tests/Run EditMode Tests` | Runs the suite, logs a one-line summary |

All of it is idempotent. Prefabs and scenes are YAML that two people cannot merge, so anything
that can be regenerated from code is — a broken prefab is fixed by re-running a menu item rather
than by resolving an unreadable conflict.

---

## 8. What is deliberately not here yet

Interaction, inventory, damage, enemies, level generation, power, voice, audio service, HUD,
the PS1 render pipeline. Each has an empty assembly waiting for it.
