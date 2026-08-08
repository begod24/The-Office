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
| `[Bootstrap]` | `GameBootstrap`, `UIEventSystemInstaller`, `NetworkServiceInstaller` |
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
| `DefinitionRegistry` | `REG_Definitions` | `GameBootstrap` |
| `ISessionService` | `MultiplayerSessionService` | `NetworkServiceInstaller` |
| `ILobbyService` | `LobbyService` | `NetworkServiceInstaller` |

`UIEventSystemInstaller` registers no service — it owns the one `EventSystem` the application
uses. **No scene may ship its own.** Scene flow is additive and overlapping: the loader brings
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
├── PlayerInteractor         camera probe, server-validated interact request
├── PlayerInventory          NetworkList of slots, owner-selected index
├── HeldItemView             draws the selected item at the socket
├── Body (capsule)           hidden from the owner
├── FacingMarker (cube)      so facing is readable in greybox
├── Socket                   (0.256, 1.251, 0.437) — where a carried item hangs
└── CameraPivot              y 1.62 standing, 0.92 crouched
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
| `Office/Setup/Build Lobby Scene` | Regenerates `PF_LobbyRow` and `SCN_Lobby` |
| `Office/Setup/Build Boot Scene` | Regenerates `SCN_Boot` |
| `Office/Setup/Configure Build Settings` | Scene list, `SCN_Boot` at index 0 |
| `Office/Content/Build All` | Everything below, in order |
| `Office/Content/Build Sample Items` | Greybox item definitions, view prefabs and icons |
| `Office/Content/Build World Item Prefab` | Regenerates `PF_WorldItem` and registers it |
| `Office/Content/Rebuild Definition Registry` | Scans for definitions, hands out ids, writes `REG_Definitions` |
| `Office/Tests/Run EditMode Tests` | Runs the suite, logs a one-line summary |

All of it is idempotent. Prefabs and scenes are YAML that two people cannot merge, so anything
that can be regenerated from code is — a broken prefab is fixed by re-running a menu item rather
than by resolving an unreadable conflict.

### 7.1 Who owns which asset

Regeneration is destructive and silent. `Build ... Scene` opens an empty scene and saves it over
the existing file; there is no merge and no undo. `SaveScene` also clears the read-only bit
first — and that bit is exactly what `.gitattributes` sets on `*.unity` and `*.prefab` through
LFS `lockable`. **A generated scene cannot be protected by locking it**, so the split below is
the only thing standing between a menu click and someone's lost afternoon.

| Owned by code — never edit by hand | Owned by a person — no builder touches it |
|---|---|
| `SCN_Boot`, `SCN_Lobby`, `SCN_MainMenu`, `SCN_Sandbox` | Level scenes |
| The HUD, the pause menu, the settings panel | Room prefabs and the modular kit |
| `PF_Player`, `PF_Session`, `PF_WorldItem`, `PF_LobbyRow` | `ItemPlacement` layouts inside level scenes |
| Wrong in one of these? Fix the builder, then re-run it. | Wrong in one of these? Fix it in the editor. |

**Starting a level scene: duplicate `SCN_Sandbox`, then delete its `Greybox` object.** Do not
start from an empty scene. The copy inherits `PlayerSpawnPoints`, the HUD, the pause menu, the
post-process volume and the lighting rig — of those, only the HUD and the pause menu have their
own `Rebuild ... In Open Scene` menu item, and the rest exist solely inside `BuildSandboxScene`.
An empty scene silently spawns players at the world origin with no HUD.

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

`DefinitionRegistryTests` fails the build on a definition without an id, on two definitions
sharing one, and on an item that exists but is missing from the registry — each of those would
otherwise surface only on a remote client, at runtime.

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
so indices stay stable, and `HudBuilder` generates exactly that many hotbar cells from the same
constant. `ServerAdd` tops up matching stacks before opening a new slot, returns whatever did
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

## 12. What is deliberately not here yet

Enemies, level generation, power, voice, audio service, the PS1 render pipeline. Each has an
empty assembly waiting for it. Props are defined but no prop behaviour exists yet — the first
door will need `PropDefinition`, a `PropPlacement` marker and a component implementing
`IInteractable`, all of which the item path already demonstrates.

Combat exists but has no consumers yet: nothing reads `MeleeModule.NoiseRadius` because there
is nothing that hears, nothing reads `LightSourceModule` because held lights are not built, and
`DurabilityModule` is authored but not spent. The numbers are resolved and ready so that the
systems which need them do not also have to invent them.

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
