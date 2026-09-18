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

Two assemblies were added beyond Technical Plan §3.2: `Office.Rendering` (URP render features —
it must reference URP, and nothing else should) and `Office.Editor` (setup tooling, which must
never ship in a build).

`Office.LevelGen`, `Office.Anomalies` and `Office.Tests.PlayMode` are empty. They exist so that
the first file written into each one lands in the right place instead of in `Assembly-CSharp`.

Every asmdef sets `autoReferenced: false` — no project code may live outside an assembly.

**`Office.Network` is reached through its service interfaces only.** Assemblies above it use
`ILobbyService` and `ISessionService`. Reaching directly into `SessionDirector`, `PlayerSpawner`
or `LobbyRoster` is not allowed — the compiler cannot stop it, because the reference points the
right way. Once `Office.Enemies` and `Office.Anomalies` fill up, a direct call to something like
`RequestEndRunRpc` from enemy code is the kind of thing nobody finds until it fires in a build.

---

## 2. Composition root

`SCN_Boot` is build index 0 and never unloads. It contains three objects and no gameplay:

| Object | Components |
|---|---|
| `NetworkManager` | `NetworkManager`, `UnityTransport` |
| `[Bootstrap]` | `GameBootstrap`, `UIEventSystemInstaller`, `AudioServiceInstaller`, `NetworkServiceInstaller`, `GameplayServiceInstaller` |
| `[DevUI]` | `DevSessionPanel` (hidden, F1) |
| `[LoadingScreen]` | `LoadingScreen` (covers a load into a session) |

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
| `ISettingsService` | `GameSettingsService` | `GameBootstrap` |
| `RunState` | `RunState` | `GameBootstrap` |
| `DefinitionRegistry` | `REG_Definitions` | `GameBootstrap` |
| `ISessionService` | `MultiplayerSessionService` | `NetworkServiceInstaller` |
| `ILobbyService` | `LobbyService` | `NetworkServiceInstaller` |

`ISettingsService` is constructed in `GameBootstrap` rather than by an installer because it
applies the stored resolution in its constructor and everything an installer creates — the music
among it — reads a volume out of it in its own `Awake`. §13 has the rest.

`UIEventSystemInstaller` and `AudioServiceInstaller` register no service — they own the one
`EventSystem` and the one music source the application uses. **No scene may ship its own.** Scene flow is additive and overlapping: the loader brings
the next scene up before it drops the previous one, so a per-scene EventSystem means two are
live for the whole length of every load. Unity warns about that once per frame, and the real
cost is worse than the noise — both systems raise events, so the outgoing screen keeps taking
clicks while the incoming one is already on screen. The installer destroys any stray it finds
when a scene loads and says which scene needs regenerating.

The consequence: entering play mode straight into a UI scene leaves it without an EventSystem,
so mouse input is dead there. That is the same rule the rest of the composition root already
enforces — start from `SCN_Boot`.

**The installer pattern is why this works.** `Office.Core` may not reference `Office.Network`,
so the composition root cannot construct a session service directly. Instead it knows only the
abstract `ServiceInstaller` MonoBehaviour, and each higher assembly supplies a subclass that
registers its own services. Dependencies still point downward.

**Known ordering trap, already hit once:** `NetworkManager.Singleton` is null inside
`GameBootstrap.Awake` because the bootstrap deliberately runs first. `NetworkServiceInstaller`
therefore holds a serialized same-scene reference to the NetworkManager. Do not replace it with
`.Singleton`.

### 2.1 How services may be resolved

The locator stays a locator only while these hold. They are cheap now and unenforceable later:

- Resolve once, in `Awake`, `Start` or `OnNetworkSpawn`, and cache the result in a field.
- Never call `ServiceLocator.Get` from `Update`, a constructor or a static initializer.
- Register a new service only through the `ServiceInstaller` of its own assembly.

Service Locator earned its reputation as an anti-pattern from undisciplined use, not from the
pattern itself: once every script pulls dependencies from anywhere at any time, initialization
order stops being reviewable. Migrating to a DI container is deliberately **not** planned — the
cost is real and the win at this size is zero.

---

## 3. Scenes

| Scene | Role | Loaded |
|---|---|---|
| `SCN_Boot` | Composition root, NetworkManager, session | Build index 0, never unloads |
| `SCN_Lobby` | Pre-run room: roster, ready, start | Additively at boot, and on return from a run |
| `SCN_MainMenu` | Terminal main menu, first scene after boot | Additively at boot, and on leaving a session |
| `SCN_Sandbox` | Greybox test space, regenerated from code | Additively when the run starts |

Level scenes are added next to `SCN_Sandbox`, never in place of it: `SCN_Sandbox` stays the
programmer's throwaway space for testing systems, and authored levels live in their own scenes
that no builder regenerates. Which one the run loads is a single constant in `SceneNames` plus
the build settings list. See §7 for who owns what.

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

**This is also why nothing interactive is placed in a scene as a NetworkObject.** Level content
is authored as plain marker components (`ItemPlacement`), and the server turns them into
spawned, registered prefabs when the run starts. See §8.2.

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

**Connection approval.** `ConnectionApproval` is on, and the payload is a handshake:
`Application.version` plus a fingerprint of every definition id and the name behind it
(`ConnectionHandshake`). A client whose content disagrees with the host is turned away with a
reason instead of joining successfully and then quietly seeing different items — the failure it
prevents has no symptom on the wire, which is exactly what makes it expensive to debug later.

The fingerprint uses a hand-written FNV-1a rather than `string.GetHashCode`, which is not stable
across runtimes and, on some, not across processes. A hash that disagreed for reasons unrelated
to content would reject two machines running the identical build — a worse failure than the one
being guarded against, and `ConnectionHandshakeTests` pins it to the published vectors.

`CreatePlayerObject` is false: bodies are spawned by `PlayerSpawner` when the run starts, not by
NGO on connection, because the lobby has no bodies in it.

**Losing the host.** `NetworkServiceInstaller` handles `OnClientStopped` and returns a
non-host client to the main menu, per GDD §15. It lives in the boot scene deliberately — the
session object is despawned by then, so nothing that rides on it can react. The scene teardown
goes through `ISceneLoader.ReturnToAsync`, which unloads everything except boot rather than
naming a scene: which run scene was loaded depends on the level, and the network layer has no
business knowing the level list.

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

### 4.2 The player record

A body is not a player. Bodies are despawned at the end of every run and built again at the
start of the next, and three things in the GDD ask a question about a player whose body does not
exist: §15 reconnecting into the run you dropped out of, §7.1 a dead player who stays on as a
spectator, and §7.3.1 a dead player's voice still carrying through the office equipment.

So `PF_PersistentPlayer` — one per connected client, spawned by `PersistentPlayerSpawner` on
PF_Session the moment they connect, despawned when they drop. It carries no art, no collider and
no transform: it is a row in a table that happens to be a `NetworkObject`.

| Field | Written by | Read by |
|---|---|---|
| `Seat` | `SeatRegistry`, at spawn | Which character prefab the body uses |
| `DisplayName` | `SeatRegistry`, at spawn | Rosters, nameplates, the spectator list |
| `Status` | `PlayerStatusReporter` on the body | Anything that needs to know the player is down or dead when their body is gone |

It calls `DontDestroyOnLoad` on itself for the same reason `SessionRoot` does. A record of the
player that dies with the scene the player left is not a record.

**Status is pushed, not pulled.** `Office.Network` cannot see `Office.Gameplay` — §1 keeps the
dependency pointing the other way — so `PlayerStatusReporter` sits on the body, watches `Health`
and writes the result onto the record. The record needs no idea what a body is, which is the
whole point of it outliving one.

**Spectating is not a status.** A dead player watching a teammate is what `Dead` looks like from
inside their own camera, and `SpectatorCamera` already derives it from the same vitals. Two
sources for one fact is how the two come to disagree.

### 4.3 Seats

`SeatRegistry` is a static, server-side table of client id to seat, and the display name is
derived from the seat rather than stored beside it. Three callers need a seat — the lobby
roster, the persistent player and the body spawner — and each asks from its own connection
callback, in whatever order those happen to be subscribed. A registry that assigns on first ask
is the only version of this where the order cannot produce two different answers.

It replaces a `Dictionary<ulong, int>` that lived inside `PlayerSpawner` and died with the run,
and it fixes a naming bug on the way: the roster used to name a joining player from its own
list length, so the player who joined after a middle seat emptied wore a name the player in the
last seat still had. Seats are released on disconnect and not before — until GDD §15
reconnection lands there is nothing to come back to, and holding a seat open would leave a
four-player lobby with three usable seats after one person's network hiccup.

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
├── PlayerInteractor         camera probe, server-validated interact request
├── PlayerInventory          NetworkList of slots, owner-selected index
├── HeldItemView             draws the selected item at the socket
├── Health                   server writes; vitals, resistances, bleed-out (§10.2)
├── PlayerAttacker           swing, server-validated (§10)
├── CombatFeedback           impact effects on every machine
├── PlayerFlashlight         the beam under CameraPivot, battery from MOD_Light
├── DownedPlayer             IInteractable; a standing teammate revives this one
├── SpectatorCamera          owner only; takes over when its own Health dies
├── PlayerStatusReporter     server only; mirrors Health onto the player record (§4.2)
├── Body (capsule)           hidden from the owner
├── FacingMarker (cube)      so facing is readable in greybox
├── Socket                   (0.256, 1.251, 0.437) — where a carried item hangs
└── CameraPivot              y 1.62 standing, 0.92 crouched
    ├── Flashlight           Spot light, off in the prefab
    └── PlayerCamera         Camera + AudioListener, owner only
```

`Socket` hangs off the body, not the camera. A carried item therefore sits in one place for
the holder and for everyone watching them, rather than swinging with the holder's pitch — the
same object, seen from two angles, instead of a first-person view model plus a separate
third-person prop that could disagree.

Every tunable number lives in `CFG_PlayerMovement` and `CFG_PlayerLook`, never in the prefab.

`Body` uses `MAT_Cylinder`; `Office/Setup/Build Player Prefab` reuses that material when it
exists rather than creating a fresh grey one, so regenerating the prefab keeps the look.

**Which prefab actually spawns.** `PlayerSpawner` holds two prefab slots and alternates them by
seat, falling back to the first whenever the second is empty. Today both seats resolve to
`PF_Player`: the second slot is deliberately null. `PF_Player_Man` and `PF_Player_Woman` — prefab
variants carrying the rigged FBX models, humanoid animator controllers and `OwnerNetworkAnimator`
— are built and registered as network prefabs, but nothing spawns them until someone runs
`Office/Setup/Player Prefab/Use Character Models`. Switching is one menu item in either
direction; `Build Character Players` no longer changes the choice as a side effect.

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
| `Office/Setup/Build Session Prefab` | Regenerates `PF_Session`, points the spawner at `PF_Player`, registers both network prefabs |
| `Office/Setup/Build Character Players` | Builds the animator controllers and the Man / Woman prefab variants |
| `Office/Setup/Player Prefab/Use Greybox Capsule (PF_Player)` | Every seat spawns the capsule — the current setting |
| `Office/Setup/Player Prefab/Use Character Models` | Seats alternate between the Man and Woman variants |
| `Office/Setup/Import TextMeshPro Essentials` | One-time TMP resource import, needed before the lobby |
| `Office/Setup/Build Sandbox Scene` | Regenerates `SCN_Sandbox` |
| `Office/Setup/Rebuild HUD In Open Scene` | Replaces `[HUD]` in whatever scene is open |
| `Office/Setup/Rebuild Inventory In Open Scene` | Replaces `[Inventory]` in whatever scene is open |
| `Office/Setup/Rebuild Pause Menu In Open Scene` | Replaces `[PauseMenu]` in whatever scene is open |
| `Office/Setup/Build Lobby Scene` | Regenerates `PF_LobbyRow` and `SCN_Lobby` |
| `Office/Setup/Build Main Menu Scene` | Regenerates `SCN_MainMenu` |
| `Office/Setup/Build Boot Scene` | Regenerates `SCN_Boot`, including the music clip on `AudioServiceInstaller` |
| `Office/Setup/Configure Build Settings` | Scene list, `SCN_Boot` at index 0 |
| `Office/Content/Build All` | Everything below, in order |
| `Office/Content/Build Sample Items` | Greybox item definitions, view prefabs and icons |
| `Office/Content/Build World Item Prefab` | Regenerates `PF_WorldItem` and registers it |
| `Office/Content/Rebuild Definition Registry` | Scans for definitions, hands out ids, writes `REG_Definitions` |
| `Office/Tests/Run EditMode Tests` | Runs the suite, logs a one-line summary |

All of it is idempotent. Prefabs and scenes are YAML that two people cannot merge, so anything
that can be regenerated from code is — a broken prefab is fixed by re-running a menu item rather
than by resolving an unreadable conflict.

The settings panel is generated by `SettingsPanelBuilder` into both the main menu and the pause
menu, so changing a row means editing that one file and re-running **both** hosts: `Build Main
Menu Scene`, and `Rebuild Pause Menu In Open Scene` in every scene that carries one — including
authored levels, which no builder regenerates on its own.

### 7.1 Who owns which asset

Regeneration is destructive and silent. `Build ... Scene` opens an empty scene and saves it over
the existing file; there is no merge and no undo. `SaveScene` also clears the read-only bit
first — and that bit is exactly what `.gitattributes` sets on `*.unity` and `*.prefab` through
LFS `lockable`. **A generated scene cannot be protected by locking it**, so the split below is
the only thing standing between a menu click and someone's lost afternoon.

| Owned by code — never edit by hand | Owned by a person — no builder touches it |
|---|---|
| `SCN_Boot`, `SCN_Lobby`, `SCN_MainMenu`, `SCN_Sandbox` | Level scenes |
| The HUD, the inventory screen, the pause menu, the settings panel | Room prefabs and the modular kit |
| `PF_Player`, `PF_Session`, `PF_WorldItem`, `PF_LobbyRow` | `ItemPlacement` layouts inside level scenes |
| Wrong in one of these? Fix the builder, then re-run it. | Wrong in one of these? Fix it in the editor. |

**Starting a level scene: duplicate `SCN_Sandbox`, then delete its `Greybox` object.** Do not
start from an empty scene. The copy inherits `PlayerSpawnPoints`, the HUD, the inventory screen,
the pause menu, the post-process volume and the lighting rig — of those, only the HUD, the
inventory screen and the pause menu have their own `Rebuild ... In Open Scene` menu item, and the
rest exist solely inside `BuildSandboxScene`. An empty scene silently spawns players at the world
origin with no HUD.

**Trap worth knowing.** A reference to an asset created moments earlier goes stale as soon as
the AssetDatabase reimports it, and `EditorSceneManager.NewScene` triggers exactly that reimport.
Assigning the stale wrapper to a `SerializedProperty` writes a silent null. Always reload a
freshly created prefab from its path after a scene swap. `Wire` now logs an error on a null
value so this fails loudly instead of producing an empty player list at runtime.

---

## 8. Content, interaction, inventory

### 8.1 Definitions and ids

Content is authored as ScriptableObjects deriving from `ContentDefinition`: `ItemDefinition`
today, `PropDefinition` waiting for the first door. Each carries a display name, a **view
prefab** — a plain mesh and collider, nothing networked — and an icon.

**An asset reference means nothing on the other machine, so a definition never travels.** Its
`Id` does, and both ends resolve it through `REG_Definitions`, registered as a service by
`GameBootstrap`. Ids are handed out once by `Office/Content/Rebuild Definition Registry` and
then left alone: renaming or moving an asset must not renumber it, or a connected client would
be holding an id that now means something else. Id `0` is reserved for "nothing", which is why
`default(ItemStack)` reads as an empty slot.

**The registry holds one array of `ContentDefinition`, not one per type**, and resolution goes
through `registry.TryGet<ItemDefinition>(id, out var item)`. The id space was always shared, so
per-type arrays bought nothing while costing a field, a dictionary, a `TryGet` and a builder
edit for every new kind of content — and GDD still has enemies, rooms, recipes, objectives and
anomalies to come. Now a new type is an asset and nothing else.

The type parameter is a guard, not a cast convenience: an id that belongs to a prop must fail
to resolve as an item rather than come back as something the caller will misuse.

`DefinitionRegistryTests` fails the build on a definition without an id, on two definitions
sharing one, on a definition that exists but is missing from the registry, and on an id that
resolves as the wrong type — each of those would otherwise surface only on a remote client, at
runtime.

### 8.2 One prefab for every item

`PF_WorldItem` is the only network prefab items will ever need. It carries a `NetworkObject`
and a `WorldItem` holding a `NetworkVariable<ItemStack>`; every machine instantiates the
definition's view prefab locally as an ordinary child and forces it onto the `Interactable`
layer.

This is the answer to `ForceSamePrefabs`. A per-item network prefab would mean a registry entry
per item, and a forgotten entry fails only on the client that did not add it. With one carrier,
**adding an item is an asset plus a mesh** — no netcode, no registry edit, no risk.

It carries no `NetworkTransform`: NGO already ships position and rotation in the spawn payload
while `SynchronizeTransform` is on, and a floor item never moves. Physics props will need one.

### 8.3 Who decides what

| Domain | Authority |
|---|---|
| What the player is looking at | Owner — it is the owner's aim, so only the owner can probe |
| Whether an interaction happens | Server, after re-resolving the target and re-checking reach |
| Inventory contents | Server |
| Selected hotbar slot | Owner — it is cosmetic, and a round trip to move a highlight is not worth paying |

`PlayerInteractor` sphere-casts from the owner's camera along `PhysicsLayers.InteractionMask`,
which includes `LevelGeometry` on purpose: a wall between the player and an item has to win. It
publishes prompt changes on the event bus — only on change, never per frame — and `HudScreen`
draws them under the crosshair.

`RequestInteractRpc` is untrusted by construction. It checks that the sender owns the
interactor, re-resolves the target from its `NetworkObjectReference`, and measures reach from
the **server's** copy of the body with `ServerRangeTolerance` applied, because owner-authoritative
movement means the server's copy trails the owner by the interpolation window. Dropping works
the same way: the position is computed server-side, never sent by the client.

`Player × Interactable` collisions are off in the matrix. A stapler on the floor must not shove
a running player, and physics queries take a layer mask rather than the matrix, so the probe
still finds it. Anything that should physically block — a closed door — puts its blocking
collider on `LevelGeometry` and keeps only its interaction collider on `Interactable`.

### 8.4 Slots

`PlayerInventory` holds a `NetworkList<ItemStack>` pre-filled to `GameplayConstants.InventorySlots`
so indices stay stable.

**The hand and the backpack are one list, split by index.** The first
`GameplayConstants.HotbarSlots` entries — four, per GDD §7.1 — are the hand: the number keys
address them, `HudBuilder` draws exactly that many hotbar cells, and `Select` refuses anything
past them, because selecting into the backpack would put the hotbar highlight on a cell the HUD
does not draw. The remaining four are storage. Only what is in the hand can be held or swung.

One list rather than two, because every alternative is a second source of truth for the same
slots: moving an item between them would be a transfer needing its own RPC and its own failure
mode, instead of the `RequestMove` a drag already goes through. It also keeps the scarcity GDD
§7.2 builds the soft roles on — the backpack does not loosen it, because reaching into one means
opening the screen and standing still in front of everyone, so it stores options rather than
answers. `InventoryCapacityTests` pins the two constants against each other and against the grid
width; every way of getting them wrong is silent.

A pickup fills the hand first, and that is not a rule anywhere — `ItemStacking.Distribute` walks
the list in order and the hand is the front of it. The test says so out loud, because reversing
that loop would leave a player who just picked something up unable to swing it, with nothing
logged. `ServerAdd` tops up matching stacks before opening a new slot, returns whatever did
not fit, and writes back only the entries that actually moved — an unchanged element still
costs a delta. A full inventory hands the whole stack back untouched, which is how `WorldItem`
knows to leave the item on the floor instead of deleting it.

The arithmetic lives in `ItemStacking`, free of NGO, so `ItemStackingTests` can exercise it
without a running session.

### 8.5 The item in your hand

`HeldItemView` sends and receives nothing. Both facts it needs are already replicated — the
slots as a server-written `NetworkList`, the selected index as an owner-written
`NetworkVariable` — so every peer works out what every player is holding from state it already
has, and runs identically on the owner and on remote instances. That is what makes the holder
and everyone else see the same object. Replicating the held item separately would be a second
source of truth for the same fact, and the two would disagree the first time a pickup and a
slot change landed in the same tick.

The held instance goes on the `ViewModel` layer with its colliders disabled. `ViewModel`
collides with nothing and is absent from `InteractionMask`, so a carried item can neither shove
its holder nor block their own interaction probe.

Where each item sits in the hand is per-item, not per-rig: `ItemDefinition.heldOffset` and
`heldEulerAngles` — a cup wants its base at the socket, a stapler its middle.

`ItemViewFactory` is the one place a definition id becomes a mesh, for both the floor and the
hand. The layer is the reason it is shared: getting it wrong makes an item silently unreachable
rather than visibly broken.

### 8.6 The inventory screen

Tab opens `InventoryScreen`, generated by `InventoryBuilder` into every run scene alongside the
HUD. It is a **second view of the same slots the hotbar draws** — the replicated `NetworkList`
and the owner-written selected index. `Equip` calls `PlayerInventory.Select`, which the owner
already owns; drop and rearrange go through `RequestDrop` and `RequestMove`. Neither is a second
authority: both RPCs re-check the sender, and the drop still computes its landing spot from the
server's copy of the body.

**Opening it pauses nothing.** The run is co-op, so the body stands there in front of everyone
while the player reads. What the screen does take is the cursor and this player's input, and it
says so on the bus as `LocalInventoryChanged`. Three things listen, none of which knows the
screen exists: `PlayerRig` frees the cursor and mutes the input reader, `HudScreen` hides the
HUD, and `PauseScreen` stands down from Escape so the two overlays cannot both consume the same
key press in one frame.

`LocalInventoryChanged` is deliberately not `LocalPauseChanged`. Both free the cursor, but a
listener has to be able to tell them apart — the pause menu suppresses its own hotkey for one
and not the other, and either overlay closing must not re-lock a cursor the other one is still
using. `PlayerRig` and `HudScreen` therefore track two flags rather than one bool.

**Keys are read from the device, not through `PlayerInputReader`.** Freeing the cursor disables
the Player action map, so a Tab binding living in that map would open the screen and then never
close it. `PauseScreen` reads Escape for the same reason.

**Dragging a cell moves the slot, not the item.** `InventoryCell` carries Unity's four drag
handlers because Unity only raises them on a component that implements them, but it decides
nothing: it hands the screen a source and a target, and the screen asks the server. There is no
prediction — a slot the client rearranged optimistically would have to be un-rearranged when the
server disagreed, and the one case where it disagrees (the server having just dropped a pickup
into that slot) is where the flicker would be worst. The ghost that follows the cursor lives
outside the panel and is never a raycast target: an icon under the pointer would be what every
drop landed on.

What a drop means is in `ItemStacking.Move` — merge onto the same item, swap with anything else
— pure, static and tested, the same split as `Distribute` next to it. Swapping rather than
refusing is what makes an occupied target work at all: the item already there has to go
somewhere, and the slot just emptied is the only place that does not invent capacity. **The
selection does not follow a dragged item.** A slot is a place in the hand, not a label on an
object, so dragging the equipped item elsewhere leaves the player holding whatever now sits in
the selected slot — which is what pressing the same number key would give them.

The grid is drawn at a fixed 4×2 and only the first `GameplayConstants.InventorySlots` cells are
live; any drawn past that are locked and the cursor never reaches one. Today the constant is 8
and every cell is live. It cannot exceed the drawn cells — `InventoryBuilder` refuses to build
and says to add a row, because the failure is silent otherwise: the hotbar would still address a
slot by number that this screen never draws.

**The grid is four wide and the hand is four slots, so the top row is the hand and the bottom is
the backpack** — the split needs no divider, only for those two numbers to agree, which
`InventoryCapacityTests` requires. Cells carry a number only where a number key reaches them, and
a legend over the grid says which row is which rather than leaving a player to infer it. Equipping
means two different things by row: a hand cell is selected, and a backpack cell is *moved* into
the selected hand slot — the same `RequestMove` a drag uses, so it swaps what was held into the
bag and adds no second path through the server. The cursor arithmetic is a pure static class,
`InventoryGrid`, tested without a scene, and for the same reason as the rest: every failure mode
is a cursor sitting on a cell that does not exist, which reads as a screen that will not respond
rather than as a bug.

Item cards read their numbers through `WeaponResolver.TryResolve`, which is `Resolve` minus the
unarmed fallback. `Resolve` answers "what happens when this player attacks", so an empty hand and
a coffee mug both come back armed — correct for the attack path, and a lie printed next to a mug.
Sharing the resolution is what stops a card disagreeing with the swing it describes.

---

## 9. Item modules

An item's identity is fields on `ItemDefinition`; what it *does* is a list of `ItemModule`
assets on it. `MeleeModule` makes it swingable, `LightSourceModule` makes it glow,
`DurabilityModule` gives it a finite life.

**Composition, not inheritance, because the content does not form a tree.** GDD §8.3 has a
laser pointer that is a weapon *and* a light source, a fire extinguisher that is a weapon *and*
a utility, and tape that is neither. Subclassing produces a diamond the first time two of those
meet: `WeaponDefinition` and `LightSourceDefinition` cannot be combined. A list can.

```
ITM_LaserPointer → [ MOD_Melee(Light), MOD_Light, MOD_Durability ]
```

Three assets, no code. `definition.GetModule<MeleeModule>()` is how any system asks.

**A module is data only.** It is a ScriptableObject, so one asset is shared by every instance
of that item — the charge left in *this* flashlight cannot live there. Per-instance state goes
in `ItemStack`; the systems that read modules keep the state.

Nothing asks "is this a weapon". An item with no `MeleeModule` swings with the unarmed numbers
from `CFG_Combat`, which is why a coffee cup needs no special case.

---

## 10. Combat

### 10.1 Trust

`PlayerAttacker` is shaped exactly like `PlayerInteractor`, for the same reason: movement is
owner-authoritative, so the client's aim is the only aim that exists and the probe must run on
the owner. That makes every request untrusted, so the server re-derives everything that matters:

| Claim | Who decides |
|---|---|
| What was hit | Server re-resolves the `NetworkObjectReference` and re-checks reach from its own copy of the body |
| Which weapon | Server reads the selected slot out of its own authoritative `PlayerInventory` |
| How often | Server keeps the cooldown clock, with a tolerance so honest jitter does not cost swings |
| How much | Server multiplies the weapon's damage by the *target's* resistance table |
| Stamina | The owner, deliberately — see below |

Stamina is the one thing the client is trusted with, because stamina already is owner state
throughout `PlayerMovement` and there is no server copy to check against. The worst a modified
client buys is swinging while tired; reach, rate and damage are all server-side.

### 10.2 Vitals

`Health` owns replication and authority; `Vitals` owns the rules and knows nothing about NGO,
the same split as `PlayerInventory` and `ItemStacking`. The rules are unit tested.

`VitalsState` travels as one `NetworkVariable`, not three, so a client can never observe health
at zero while the downed flag is still in flight. Downed is **derived** from health rather than
stored — a stored flag admits states that cannot happen.

**How the HUD reads it.** `Health` keeps a static list of every *spawned player's* instance —
`SpawnedPlayerList` — and `HudScreen` binds a squad row to each and listens to its `Changed`.
Nothing is copied: every machine already holds a `Health` for every player and the
`NetworkVariable` on it arrives on its own, so the list is a way of finding them rather than a
second source of truth. It is players only, because enemies and breakable props carry this
component too and a HUD that swept the scene for it would bind a row to a filing cabinet.

The event bus cannot do this job: `LocalVitalsChanged` carries no client id and cannot grow one
without `Office.Core` learning what a player is. That mismatch is why the wiring was missing for
so long — `HudScreen` had `SetHealth` and `SetDowned` and nothing ever called them, so damage
changed the numbers on the wire while the HUD went on drawing full bars.

Per GDD §7.1 and §15: zero health is downed, not dead; a teammate has 60 seconds; then
spectator. Damage to a downed player does nothing, because the revive window is a flat timer
and the alternative rewards standing over a body. GDD leaves that open (§16, question 5) — it
is one line in `Vitals.ApplyDamage` and the tests will say what else moves.

### 10.3 Resistances are data

GDD §8.3 pairs damage types against enemy classes, and GDD §9.2 makes "digital entities are
immune to physical weapons" the lesson of the game. As code that is an `if` per pair, and a
4 × 20 matrix of those is unreadable and cannot be balanced by a designer. Instead every target
carries a `DamageResponseTable`.

The matching rule is exact, and both halves matter:

- **The strongest matching row wins.** A laser pointer is `Blunt | Light`; against a digital
  enemy it deals ×2.5, because immunity to being hit with a stick must not cancel a weakness to
  light.
- **Rows that do not match are not considered at all.** A wet mop is `Blunt | Water`; against
  that same enemy it deals ×0, because Water is not a listed weakness and must not drag the
  result back to neutral.

Averaging or multiplying instead would let any weapon launder its way past an immunity by
carrying a second damage type. `DamageResponseTests` pins both cases down.

---

## 11. Object pooling

`INetworkObjectPool` reuses networked instances instead of creating and destroying them. The
point is the frame it saves, not the memory: GDD §9.1 is built on swarms, and instantiating
those at the moment they appear puts a GC spike exactly where frame time matters most.

NGO owns creation of networked objects, so pooling is only possible through
`INetworkPrefabInstanceHandler`. Both ends register it, for opposite reasons — the server so
its own spawns recycle, the client so an arriving spawn message does not instantiate. Server
code calls `Acquire`; clients never do.

**Registration happens on network start, not at boot.** `NetworkManager.PrefabHandler` does not
exist until the manager initialises, so `NetworkServiceInstaller` registers from
`OnServerStarted` and `OnClientStarted`. A host fires both, which is why registration is
idempotent.

**Parked instances are moved to `DontDestroyOnLoad`, not reparented.** A pooled object is a real
GameObject sitting inactive in whatever scene it was created in, and runs end by unloading the
run scene — so something has to move it out. The obvious move, parenting it under a tidy `[Pool]`
root, does not work: NGO watches `OnTransformParentChanged` to replicate hierarchy changes and
rejects them on an unspawned object, logging `NetworkObject can only be re-parented after being
spawned` and then **reverting** the change. The object stays in the doomed scene and the queue
fills with Unity-null entries that look fine to `Count`. `DontDestroyOnLoad` changes the scene
without touching the parent, which is the part that was actually needed.

`PF_WorldItem` is the first prefab through it: it is already the single carrier every item
shares, so a run exercises the pool constantly.

---

## 12. Settings and audio

### 12.1 One owner for what the player chose

`ISettingsService` holds the settings, applies the display and writes both back to
`PlayerPrefs`. Two screens read it — the main menu's settings column and the pause menu's, which
are the same panel built by the same builder into two scenes — and neither keeps a copy. A screen
that remembered its own volume would be a second answer to a question the service already
answers, and the two would disagree the first time the other screen moved something.

Changes travel as `SettingsChanged` on the bus, carrying the whole `GameSettings` rather than the
field that moved: everything that listens reads more than one field, so a delta would make each
listener keep the rest.

Three seams, each earning its place:

| Seam | Why it is not called directly |
|---|---|
| `ISettingsStore` | An EditMode test that wrote through `PlayerPrefs` would change the editor it runs in |
| `IDisplayDevice` | Lets a test drive a monitor that reports nothing, and gives `ChangesTakeEffect` somewhere honest to live |
| `VolumeCurve`, `ResolutionCatalogue`, `DisplayModes` | Pure and in `Office.Data`, so the awkward cases are pinned down without a display attached |

**Volume applies as it is dragged; the display waits for Apply.** The player is listening to what
the volume slider sets, so a round trip through a button would be a worse way to choose one. A
resolution is the opposite: the wrong one is recoverable only while the screen is still readable,
so nothing may set one on the way past.

**`Screen.SetResolution` does nothing in the editor** — the Game view owns the size in play mode.
The picker therefore says so on screen rather than letting the only person who ever tests it
conclude it is broken. `DisplayChangesTakeEffect` is that fact, and it is `false` exactly when
`Application.isEditor` is true.

Volumes are stored as **slider positions**, never gain. Gain is `position²`, which approximates
the decibel curve a player expects; storing the gain instead would move every saved slider the
first time that curve is tuned.

### 12.2 Music that outlives scenes

The menu track was a GameObject inside `SCN_MainMenu`. That made it restart on every walk back
into the menu and left the lobby — which has no music object and no camera — completely silent.
A scene is the wrong owner for something meant to outlive scenes, so `AudioServiceInstaller`
builds `[Audio]` in the composition root instead: one `AudioSource`, one `MasterOutput`, one
`AudioListenerGuard`, all on `DontDestroyOnLoad`. Same move as `UIEventSystemInstaller` and the
one EventSystem.

`MusicDirector` fades the track out wherever the player is inside the office and back in
wherever they are not. **Which scenes those are is `SceneNames.IsFrontEnd`'s answer, and the
question is asked from the front-end side on purpose:** boot, menu and lobby are a closed set the
builders own, while levels are authored, named by whoever adds them and arrive without a code
change. A list of gameplay scenes would be stale the first time someone added a floor.

The fade is driven by the **active scene**, not by the game state: the phase reaches `Generating`
while the loading screen is still up and the player is still looking at the front end, whereas
the scene going active is the moment the office appears — which is the moment the music has to be
gone by. Coming back from a run unpauses the same track rather than restarting it.

`AudioListenerGuard` keeps exactly one listener alive: its own, and only when nothing else
supplies one. A run wants the listener at the player's ears, the sandbox has a fallback camera
holding one until a body spawns, and the lobby has neither. Unity's answer to two listeners is a
warning and an undefined winner; its answer to none is silence with nothing logged, which is the
failure worth guarding against because it looks exactly like a music bug. It re-checks on scene
load, scene unload and `LocalPlayerSpawned` — never per frame.

Master volume is applied here, not in `Office.Core`: the service stores what the player chose,
and this assembly decides how a stored number becomes sound. When a mixer arrives, `MasterOutput`
points at an exposed parameter and nothing else in the project changes.

---

## 13. Enemies

One registered prefab, `PF_Enemy`, is every enemy there will ever be — the third use of the
arrangement `WorldItem` and `DamageableTarget` already share. An `EnemyDefinition` id rides the
spawn payload, each machine builds the definition's view prefab locally, and `Health` is
configured from the definition before the object spawns. A new enemy is an asset and a mesh.

| Component | Runs on | Owns |
|---|---|---|
| `Enemy` | everyone | The definition, the view, the capsule, the agent's dimensions, the corpse timer |
| `EnemyBrain` | server only | Sight, hearing, patrol, the state machine, the attack |
| `Health` | server writes | Damage, resistances, death — `canBeDowned` is off, so zero is dead |

**How one reaches the world.** `EnemySpawner` on `PF_Session` is the third subclass of
`RunScopedSpawner`, next to the items and the practice targets: it reads the level's
`EnemyPlacement` markers on the InRun edge, pushes each marker's definition and health into
the carrier before `Spawn()`, and takes everything back when the run ends. A marker is a spawn
point, not a spawner — one marker, one enemy, once per run; waves and the escalating director
belong to whatever reads the markers later. The sandbox authors two stapler markers in its far
corners, deliberately outside sight of the spawn area.

**Hearing.** Every swing the server rules valid — hit or miss — publishes `NoiseRaised` on the
event bus, carrying the position and the weapon's `NoiseRadius`. The event is server-side only:
it is published where attacks are ruled on and consumed where brains run, so it never crosses
the wire, and what a client sees is the replicated behaviour state changing. An enemy hears a
noise when the distance clears **the smaller of** the noise's radius and its own
`HearingRadius` — the quieter of the two decides, so both numbers stay worth authoring. A heard
noise sends a targetless enemy to the point at chase speed (`Investigating`); it lingers there
for `MemorySeconds`, then forgets and falls back into patrol. Sight always wins: the moment
anything is seen the noise is dropped, and a noise never interrupts a chase or a committed
swing.

**Patrol.** With nothing seen and nothing heard, an enemy walks a circle around where it spawned
(`Patrolling`). It draws a point inside `PatrolRadius` of its spawn anchor, puts it through
`NavMesh.SamplePosition` against **the agent's own area mask** — an enemy barred from an area
must not patrol through it either — walks there at `PatrolSpeed`, and stands for `PatrolPause`
jittered half either side before drawing the next. Two of the same enemy spawned in one room
fall out of step with each other within a couple of points.

The circle is centred on the spawn anchor rather than on the enemy, which gives the walk home
for free: one that gave up a chase three rooms away draws its next point near the anchor and
walks back to its patch. The anchor is taken on the first frame the agent reports itself on the
mesh, not at spawn — `Enemy` snaps the transform onto the mesh during its own spawn and nothing
orders the two, so a position read at spawn can be the marker's rather than the one the agent
actually stands on.

`PatrolRadius` of zero holds station on the marker: GDD §9.1 #11 is a stationary hazard, and a
socket that strolls is not one. `Idle` is what the brain is in whenever it is not walking — the
pause between points, a marker off the mesh, a definition that holds station — so `Idle` now
means *standing still* rather than *has not noticed anything*.

A marker sealed in by geometry has no point to draw. Six draws per pick, then a one-second
back-off instead of resampling every frame, and a warning naming the definition on the third
consecutive failure: the only other symptom is an enemy standing there, which is exactly what a
patrol at rest looks like.

**The brain disables itself everywhere but the server**, and so does the `NavMeshAgent`. An
agent left live on a client fights the replicated transform for the same object and wins about
half the frames, which arrives as an enemy that stutters only for the people not hosting.

**What crosses the wire is one `EnemyBehaviourState` and the transform.** Nothing else — no
`NetworkAnimator`, no target id, no path. That is what makes a view's animation free to be
procedural: it is a function of a byte and a position, computed identically on every machine
from data that was already being sent. GDD §9.1 is built on swarms, and a swarm cannot afford
an animator sync per member.

**The collider is on the carrier, not in the art.** One prefab serves every enemy, so the
capsule a swing connects with and the agent that walks are both sized from the definition and
have to agree. Views are built with their colliders stripped, which leaves an artist free to
hand over a mesh with whatever collision it came with.

### 13.1 Navigation

Unity's built-in Humanoid agent is half a metre wide, and a bake erodes the mesh by the agent's
radius on both sides of every obstacle — so nothing survives in a doorway narrower than a metre.
Real office doors are 0.8–0.9 m and the greybox partition's is exactly one. Baking with Humanoid
produces a sandbox with no connection through its only door, and **the failure is silent**: the
enemy simply stands there.

So the project owns an agent type. `Office`, id **1**, radius 0.25, height 1.8, climb 0.35 —
written by `Office/Setup/Create Navigation Agent Type` into `NavMeshAreas.asset`. The id is a
fixed constant rather than the hash the Navigation window hands out, because a surface baked
against one id and an agent asking for another produce a mesh nothing can stand on, and nothing
is logged.

One type, not one per enemy: every agent takes its own radius and height from its definition,
but they all walk the same mesh, and the mesh is baked for the tightest thing that uses it. A
crawling enemy that wants to go under a desk needs a second type and a second bake.

`SCN_Sandbox` bakes as part of `Build Sandbox Scene`; `Office/Setup/Bake Navigation In Open
Scene` re-bakes without regenerating everything else, the same escape hatch the HUD and the
inventory screen have. The mesh is written beside the scenes, in
`Assets/Project/Scenes/Navigation/`, and both entry points get the path from
`NavigationSetup.DataPathFor` so they cannot write to two files.

Edit-time baking is right while levels are hand-built. Procedural floors cannot be baked before
their shape is known, so `Office.LevelGen` will bake on the server at run start instead — the
agent type is the part both paths share.

---

## 14. Rendering

Stock URP, with no project-owned shader or render pass anywhere in it. `PC_RPAsset` renders at
full scale with the automatic upscaling filter, and the only feature on `PC_Renderer` is URP's
own screen-space ambient occlusion. Post-processing is a generated volume profile — tonemapping,
colour grading, bloom, vignette and film grain, every one of them a stock URP component —
authored by `Office/Setup/Build Post Process Profile` (§7.1).

**The PS1 screen layer was removed on 20 August 2026.** The custom pass (`PixelArtFeature`), its
shader (`S_PixelArt`) and the builder that wired the two together (`PixelRenderBuilder`, menu
item `Office/Setup/Build Pixel Render`) are gone from the project; commit `8f5cc2a` is the last
one that carries them, and `Office.Rendering` is an empty assembly again.

One thing the removal left behind on purpose: the volume profile is still graded the way it was
tuned to sit *underneath* that pass — exposure up, contrast and vignette low, so the palette
quantisation had readable mid-tones to work with. Grading is stock URP, so it was left alone
rather than reverted. If the picture now reads as too flat or too bright, the darker pre-effect
numbers are in `PostProcessBuilder` at commit `072d658`.

---

## 15. What is deliberately not here yet

Level generation, voice, SFX and ambience. Each has an empty assembly waiting for it —
`Office.Audio` now holds the music and the settings-to-sound binding, and no sound effect,
stinger or ambience system yet.

Power has its first piece: `PowerSwitch` is a prop implementing `IInteractable`, placed from
`PowerSwitchPlacement` markers by `PowerSwitchSpawner`, which hands each one the `RunOutcome`
it reports to — so a run can now be won. What is not there is a power *state* — nothing in the building is dark because a
switch is off, and nothing else asks whether it is. The service GDD §6 wants still has to be
written; the switches are its objective, not its implementation.

`LightSourceModule` is read by the flashlight and the item card, but a held item that glows —
a laser pointer lighting the wall it points at — is not built. The numbers are resolved and
ready so that the system which needs them does not also have to invent them.

Known gaps in what does exist:

- **Two-client behaviour is not machine-verified.** The single-client path is exercised end to
  end, but a second client appearing in the roster and its ready flag replicating has only been
  reasoned about, not tested. Multiplayer Play Mode has no scriptable API for activating a
  virtual player, and NGO 2.13 does not ship its integration-test helpers. Verify by hand
  (README) until there is a way to automate it.

  This gap has already produced one shipped bug: the session object was originally placed in
  the Boot scene, which works for a host and fails for every client. Treat anything that only
  a remote client exercises as unverified until two machines have run it.
- **Late join during a run** spawns a player once that client reports its scene ready.
  `SessionDirector` raises `ClientReadyDuringRun` for it, because a late joiner produces no
  phase transition and spawning otherwise hangs off the `InRun` edge alone. Untested with two
  machines, and the lobby still does not lock — connection approval could refuse a mid-run join
  outright, which may turn out to be the better answer than spawning one.
- **The lobby look is placeholder.** GDD §14 wants a retro terminal HUD, and no render layer
  will deliver one: screen-space UI is composited after URP entirely. That pass is its own piece
  of work, in the UI rather than in rendering.
- **An enemy is silent and has no animation of its own.** It patrols, hears, chases and swings,
  and does all of it without a footstep, a servo or an idle. The procedural walker moves the
  legs; nothing else about it reads as alive. For a co-op horror this is the largest remaining
  hole in what does exist: a player cannot react to a thing they cannot hear coming, and GDD
  §2's "the building is the antagonist" is not testable until they can.
- **`SCN_Level_1` has no baked NavMesh and no lights.** It is in build settings and it is a
  floor plan, not a level: `m_NavMeshData` is empty, so nothing that walks can path in it, and
  every enemy spawned there will log the snap warning and stand still. The only baked mesh in
  the project is `NavMesh_SCN_Sandbox`.
