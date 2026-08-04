# Office Nightmare — Technical Plan

**Version:** 0.1
**Engine:** Unity 6000.3.6f1, Universal Render Pipeline
**Team:** A — systems and gameplay code. B — level design and 3D art.
**Companion documents:** `GDD.md`, `GIT_SETUP.md`, `CONTRIBUTING.md`

---

## 0. Decision Log

| # | Decision | Value | Impact on this plan |
|---|---|---|---|
| 1 | Netcode | **NGO** | §2 finalised, no alternatives evaluated further |
| 2 | Multiplayer mode | **Online only** | Single camera, single input context, no viewport splitting |
| 3 | Level kit grid | **2 m module, 0.25 m sub-grid** | §6 connector contract sized to 2 m |
| 4 | Proximity voice | **In scope, core mechanic** | New §2.6, middleware selection required by M1 |
| 5 | Player roles | **Soft roles** | No class system, no per-class balancing |
| 6 | Meta-progression | **None** | Persistence layer removed from scope entirely |
| 7 | Release timer | **Score modifier only** | Pressure moved to director and scarcity systems |
| 8 | Dead player voice | **Distorted via office equipment** | Requires voice-pipeline access — drives §2.6 middleware choice |
| 9 | Host migration | **Deferred to M4, prepared from M0** | New §2.7, `RunState` discipline mandatory from first commit |

---

## 1. The Decision That Determines Everything

This is a cooperative game for 1–4 players. **Multiplayer is not a feature, it is the substrate.** Every system must be authored networked from the first commit.

The most common way projects of this exact shape die is: build the shooter singleplayer, get it feeling good, then attempt to add co-op. Retrofitting authority, replication, and ownership into finished gameplay code is not a refactor — it is a rewrite. Assume that any system written singleplayer will be thrown away.

**Rule:** if a system touches game state, it does not get written until its authority model is decided.

---

## 2. Networking Architecture

### 2.1 Topology

**Client-hosted listen server.** One player is host and authoritative; the others are clients. No dedicated server infrastructure, no hosting costs.

This is the correct choice for a friends-only 4-player co-op game. Dedicated servers would add cost, deployment complexity, and an operations burden that a two-person team cannot carry.

Consequence: the host has a latency advantage and could cheat. For a co-op game played with friends, this is acceptable. Do not spend engineering effort on anti-cheat.

### 2.2 Stack

| Layer | Choice | Rationale |
|---|---|---|
| Netcode | **Netcode for GameObjects (NGO) 2.x** | First-party, ships with Unity 6, integrates with Multiplayer Play Mode |
| Transport (development) | Unity Transport + Unity Relay | Works immediately, no port forwarding, free tier sufficient for two developers |
| Transport (shipping) | Steam Sockets transport | Steam handles NAT traversal and friend invites; players expect a Steam invite flow |
| Testing | **Multiplayer Play Mode** package | Run 2–4 virtual players from one editor. This will save more time than any other single decision here. |
| Lobby | Steam lobbies at ship, Relay join codes during development | |

Alternatives considered: FishNet (excellent, more mature server-authoritative tooling, free — a legitimate choice if NGO proves limiting) and Photon Fusion 2 (best-in-class prediction, but costs money at scale and is heavier than this project needs).

**CONFIRMED: NGO.** This is now a fixed dependency. No system may be written against an abstraction layer "in case we switch" — such layers cost real time and are never used.

### 2.3 Authority model

| Domain | Authority | Notes |
|---|---|---|
| Player movement | **Client-authoritative** | Owner moves, host replicates. Friends-only game; cheating is not a threat. Avoids implementing prediction and reconciliation, which is weeks of work. |
| Player look direction | Client-authoritative | |
| Damage calculation | **Server** | Client sends "I attacked", server decides hit and damage |
| Enemy AI and movement | **Server** | Clients see replicated transforms only |
| Enemy spawning | Server | |
| Level generation | Server generates seed, clients build deterministically | See section 6 |
| Objectives and mission state | Server | |
| Power system | Server | |
| Item pickup and inventory | Server-validated | Client requests, server confirms — prevents duplication across clients |
| Doors, breakers, interactables | Server | |
| UI, VFX, audio | Local, triggered by replicated state | Never replicate presentation |

### 2.4 Replication rules

- **NetworkVariable** for continuous or late-joiner-relevant state: health, door open/closed, power on/off, objective progress. Late joiners receive it automatically.
- **RPC** for one-shot events: attack impact, sound cue, VFX spawn. Never for state a late joiner needs.
- **Nothing is replicated that can be derived locally.** Muzzle flashes, footstep sounds, camera shake, screen effects are all local reactions to replicated state changes.
- Every `NetworkBehaviour` must be safe to disable and re-enable. Do not assume `Start()` runs before spawn — use `OnNetworkSpawn()`.

### 2.5 Network object budget

Target: **fewer than 60 active NetworkObjects** at any moment. Static geometry, props, and decoration are never NetworkObjects. If a chair is not interactable, it is local geometry on every client.

Pooled network objects use `INetworkPrefabInstanceHandler` — see section 8.6.

---

## 2.6 Voice Architecture

Proximity voice is a locked core mechanic (GDD §7.3). It runs on a **separate channel from NGO** — its own connection, its own lifecycle, its own failure modes.

### 2.6.1 Middleware selection

| Option | Positional audio | Access to the audio pipeline | Cost | Verdict |
|---|---|---|---|---|
| **Dissonance Voice Chat** | Yes | **Yes** — custom DSP on the voice stream | Paid asset, one-time | **Recommended** |
| Unity Vivox | Yes | No — closed service | Free tier | Rejected |
| Steam Voice API | No, manual | Yes, raw PCM | Free | Fallback, more work |

The deciding factor is decision #8. Routing a dead player's voice through a filter and re-emitting it from an in-world speaker requires reading and processing the voice stream before playback. Vivox does not expose this. Dissonance does, and ships NGO integration.

`[DECISION REQUIRED BY M1]` Confirm Dissonance and budget the licence.

### 2.6.2 Channel model

| Channel | Participants | Spatialisation |
|---|---|---|
| `Proximity` | Living players within range | 3D, positional, wall-occluded |
| `Radio` | Holders of an active walkie-talkie | Non-positional, emitted from the device with a band-pass filter |
| `Equipment` | Dead players → living players | 3D, positional **at the emitter**, heavily filtered |
| `Lobby` | Everyone, pre-run | Non-positional |

### 2.6.3 The equipment channel

The technically distinctive system in this project. Implementation shape:

```
Player dies
  → Voice moves from Proximity to the Equipment channel
  → Server tracks a set of active emitters: monitors, PA grilles, desk phones, printers
  → For each living listener, the server selects the emitter nearest to them
  → The dead player's stream is spatialised at that emitter's position
  → A DSP chain is applied: band-pass, bit-crush, light distortion, slight delay
  → The emitter object plays a visual state: flicker, static, speaker cone movement
```

Requirements that follow:
- Emitters are placed by the level generator, not hand-authored per room
- Emitter selection must be per-listener, not global — two living players in different rooms hear the same dead teammate from different speakers
- Occlusion is intentionally poor here; degraded intelligibility is the design goal, not a defect
- Enemies subscribe to emitter activity: a transmitting speaker is an audio event that draws attention
- The AI antagonist can publish to this channel to spoof a dead player. Gate this behind a run counter so it never happens on a first playthrough.

### 2.6.4 Non-negotiable constraints

- Voice session lifecycle is bound to the NGO session but must fail independently. A voice server outage degrades to a silent game, never to a crashed game.
- Push-to-talk, per-player volume, mute, and a global voice disable are **shipping requirements**, not options. Any game with open-mic voice needs them on day one.
- Mute state is client-local and never replicated.
- Voice chat triggers content-moderation obligations on Steam and affects age rating. Budget for reporting flow before store submission.

---

## 2.7 Host Migration

**Decision: desired, deferred to M4.** In v1, host disconnect ends the session.

### 2.7.1 Why it is expensive

NGO provides no host migration. A full implementation requires: serialising all run state, electing a new host, starting a server on that client, respawning every NetworkObject with restored state, reconnecting all remaining clients, re-establishing the voice session, and handling failure at every one of those steps. Realistically several weeks, plus a permanent tax — every new system must be serialisable and restorable, forever.

### 2.7.2 What is done from M0 instead

The cheap discipline that makes M4 implementation feasible rather than a rewrite: **all authoritative run state lives in one serialisable structure**, not scattered across component fields.

```csharp
[Serializable]
public sealed class RunState
{
    public int FloorSeed;
    public int FloorIndex;
    public float ElapsedSeconds;
    public GameState Phase;
    public List<PlayerState> Players = new();
    public List<EnemyState> Enemies = new();
    public List<InteractableState> Interactables = new();
    public List<PowerZoneState> PowerZones = new();
    public List<ObjectiveState> Objectives = new();
}
```

Rules enforced from the first commit:

- Any state that must survive a host change lives in `RunState`, never only in a component field
- Components read from and write to `RunState`; they do not own authoritative data
- Every new system adds its own serialisable block here as part of being written, not later
- A PlayMode test snapshots `RunState`, rebuilds the world from it, and asserts equivalence. Written in M1, run every milestone.

That test is what turns host migration from a rewrite into a feature. It costs an afternoon now.

### 2.7.3 Mitigation in v1

Runs are 25–40 minutes and there is no meta-progression (decision #6), so a lost run costs nothing but time. On host disconnect: show a clear message, return everyone to the lobby with the party intact, and let them requeue in one click. Most of the pain of a lost session is friction in getting back together, and that part is cheap to solve.

---

## 3. Project Architecture

### 3.1 Layer model

```
┌─────────────────────────────────────────────┐
│  Presentation   UI, VFX, audio, animation    │  ← reads state, never writes it
├─────────────────────────────────────────────┤
│  Gameplay       weapons, enemies, objectives │  ← game rules
├─────────────────────────────────────────────┤
│  Core           services, events, pooling    │  ← engine-adjacent infrastructure
├─────────────────────────────────────────────┤
│  Data           ScriptableObject configs     │  ← no logic, no references upward
└─────────────────────────────────────────────┘
```

Dependencies point downward only. This is enforced physically by assembly definitions — not by discipline, because discipline fails at 2 a.m. before a deadline.

### 3.2 Assembly definitions

| Assembly | References | Contents |
|---|---|---|
| `Office.Data` | — | ScriptableObject definitions, enums, plain structs |
| `Office.Core` | Data | Service locator, event bus, pooling, extensions, interfaces |
| `Office.Network` | Core, Data | Network bootstrap, session management, spawn management |
| `Office.Gameplay` | Core, Data, Network | Player, weapons, interaction, inventory, damage |
| `Office.Enemies` | Core, Data, Network, Gameplay | AI, perception, behaviour states |
| `Office.LevelGen` | Core, Data, Network | Procedural assembly, navmesh baking |
| `Office.Anomalies` | Core, Data, Network, Gameplay | Digital entity effects |
| `Office.UI` | Core, Data, Gameplay | HUD, menus |
| `Office.Audio` | Core, Data | Audio service, mixer control |
| `Office.Tests.EditMode` | all | Unit tests |
| `Office.Tests.PlayMode` | all | Integration and network tests |

Benefits beyond architecture: compile time after a script change drops from tens of seconds to a few seconds. Over a year that is weeks of recovered work.

### 3.3 Scene structure

| Scene | Content | Loaded |
|---|---|---|
| `Boot` | Composition root, service registration, network manager | Always, index 0 |
| `MainMenu` | Menu UI | Additive |
| `Lobby` | Pre-run room, player spawns, readiness | Additive |
| `Run_Base` | Run-scoped managers: director, objectives, spawner | Additive during run |
| `Floor_XX` | Generated geometry container | Additive per floor |

`Boot` never unloads. It is owned by A and changes rarely — which is exactly what makes the git workflow in `CONTRIBUTING.md` viable. Level scenes are owned by B and contain almost nothing except prefab references.

**Critical rule:** `Boot` holds no direct references to level content. Systems find each other through the service locator and events, never through inspector references across scenes. Violating this is what causes "B edited the level and A's scene broke".

---

## 4. Composition Root and Service Access

The team asked specifically about the Singleton pattern. Here is the honest senior position on it.

### 4.1 Why classic singletons are wrong here

```csharp
public static GameManager Instance { get; private set; }
```

In a networked project this pattern causes concrete, expensive problems:

- Multiplayer Play Mode runs several virtual players; static state can bleed between them in the editor
- Domain reload is disabled for fast iteration, so statics survive between play sessions and hold stale references
- Access order becomes implicit — nothing declares what depends on what
- Anything can mutate global state from anywhere, so a bug's origin is unfindable
- Untestable: no way to substitute a fake

### 4.2 What to use instead

A **composition root** in `Boot` that constructs services explicitly, plus a `ServiceLocator` for access.

```csharp
public sealed class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private AudioService audioService;
    [SerializeField] private PoolService poolService;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ServiceLocator.Register<IAudioService>(audioService);
        ServiceLocator.Register<IPoolService>(poolService);
        ServiceLocator.Register<IEventBus>(new EventBus());
        ServiceLocator.Register<IGameConfig>(config);
    }

    private void OnDestroy() => ServiceLocator.Clear();
}
```

```csharp
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new();

    public static void Register<T>(T service) where T : class
        => services[typeof(T)] = service;

    public static T Get<T>() where T : class
        => services.TryGetValue(typeof(T), out var s)
            ? (T)s
            : throw new InvalidOperationException($"{typeof(T).Name} not registered");

    public static void Clear() => services.Clear();
}
```

This keeps one access point, but registers interfaces rather than concrete types, makes lifetime explicit, and allows a test to register a fake.

If the codebase grows past roughly fifteen services, migrate to **VContainer** — a fast, Unity-native dependency injection container. Do not adopt it on day one; it is a solution to a problem the project does not have yet.

`[NOTE]` `NetworkManager.Singleton` from NGO is a legitimate exception. It is engine infrastructure with a managed lifecycle, not game state.

---

## 5. Design Patterns — Applied, Not Decorative

Patterns are worth using only where they solve a real problem in this project. Each entry below names the problem first.

### 5.1 Polymorphism

**Problem:** twenty enemy types and a dozen weapons need uniform handling by damage, pooling, and AI systems.

```csharp
public interface IDamageable
{
    void TakeDamage(in DamageInfo info);
    bool IsAlive { get; }
}

public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly Vector3 Point;
    public readonly ulong SourceClientId;

    public DamageInfo(float amount, DamageType type, Vector3 point, ulong sourceClientId)
    {
        Amount = amount;
        Type = type;
        Point = point;
        SourceClientId = sourceClientId;
    }
}

[Flags]
public enum DamageType
{
    None = 0,
    Blunt = 1 << 0,
    Cutting = 1 << 1,
    Water = 1 << 2,
    Electric = 1 << 3,
    Adhesive = 1 << 4,
    Light = 1 << 5
}
```

`DamageType` as flags directly implements the GDD's elemental system. A digital entity declares `Light` as its only vulnerability; the damage resolver reads that and returns zero for everything else. No `if (enemy is Glitch)` anywhere in the codebase.

### 5.2 Encapsulation

**Problem:** health desynchronising because six systems write to it.

```csharp
public sealed class Health : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    private readonly NetworkVariable<float> current = new();

    public float Current => current.Value;
    public float Normalized => current.Value / maxHealth;
    public bool IsAlive => current.Value > 0f;

    public event Action<float> Changed;
    public event Action<DamageInfo> Died;

    public override void OnNetworkSpawn()
    {
        if (IsServer) current.Value = maxHealth;
        current.OnValueChanged += OnCurrentChanged;
    }

    public override void OnNetworkDespawn() => current.OnValueChanged -= OnCurrentChanged;

    public void TakeDamage(in DamageInfo info)
    {
        if (!IsServer || !IsAlive) return;

        current.Value = Mathf.Max(0f, current.Value - info.Amount);
        if (current.Value <= 0f) Died?.Invoke(info);
    }

    private void OnCurrentChanged(float previous, float value) => Changed?.Invoke(value);
}
```

The `NetworkVariable` is private. The server-only write guard lives in one place. No caller can bypass it.

### 5.3 Observer

**Problem:** the power system must notify lights, doors, enemies, audio, and UI, without knowing any of them exist.

Two mechanisms, used for different scopes:

**C# events** for local, object-scoped notifications — as in `Health` above. Always unsubscribe in `OnDestroy` or `OnNetworkDespawn`; leaked subscriptions on pooled objects are the most common source of "it works the first time" bugs.

**Event bus** for cross-system, global notifications:

```csharp
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : struct;
    void Unsubscribe<T>(Action<T> handler) where T : struct;
    void Publish<T>(in T evt) where T : struct;
}

public readonly struct PowerStateChanged
{
    public readonly int ZoneId;
    public readonly bool IsPowered;
    public PowerStateChanged(int zoneId, bool isPowered) { ZoneId = zoneId; IsPowered = isPowered; }
}
```

Events are structs to avoid allocation. The bus is server-published and client-published independently — a network event is replicated first via `NetworkVariable` or RPC, and each client raises its own local bus event in response. **The bus never crosses the network.**

Discipline rule: the event bus is for cross-system notification, not for hiding spaghetti. If tracing a bug requires searching for publishers of five different event types, the design is wrong, not the pattern.

### 5.4 SOLID

| Principle | Concrete application in this project |
|---|---|
| **S** — Single responsibility | `PlayerMovement`, `PlayerLook`, `PlayerInteraction`, `PlayerInventory` are separate components. No `PlayerController` doing everything. |
| **O** — Open/closed | New enemies are new ScriptableObject configs plus a behaviour component. No existing file is edited to add one. |
| **L** — Liskov substitution | Any `IDamageable` can be handed to the damage resolver without special cases. If a subclass needs an exception, the abstraction is wrong. |
| **I** — Interface segregation | `IDamageable`, `IInteractable`, `IPoolable`, `IPowerConsumer` are separate. A door is interactable and power-consuming but not damageable. |
| **D** — Dependency inversion | Gameplay depends on `IAudioService`, not on `FMODAudioService`. Swapping the audio backend touches one registration line. |

### 5.5 State Machine

**Problem:** enemy behaviour written as nested booleans becomes unmaintainable by the fourth enemy.

```csharp
public interface IEnemyState
{
    void Enter(EnemyContext ctx);
    void Tick(EnemyContext ctx, float deltaTime);
    void Exit(EnemyContext ctx);
}

public sealed class EnemyStateMachine
{
    private IEnemyState current;
    private readonly EnemyContext context;

    public EnemyStateMachine(EnemyContext context) => this.context = context;

    public void ChangeState(IEnemyState next)
    {
        if (ReferenceEquals(current, next)) return;
        current?.Exit(context);
        current = next;
        current.Enter(context);
    }

    public void Tick(float deltaTime) => current?.Tick(context, deltaTime);
}
```

States: `Idle`, `Patrol`, `Investigate`, `Chase`, `Attack`, `Stunned`, `Dead`. Runs **server-only**. Clients receive position and an animation state index.

### 5.6 Strategy

**Problem:** weapons differ in behaviour, not just numbers.

```csharp
public interface IWeaponBehaviour
{
    void OnPrimary(WeaponContext ctx);
    void OnSecondary(WeaponContext ctx);
}
```

`MeleeSwingBehaviour`, `ProjectileBehaviour`, `SprayBehaviour`, `BeamBehaviour`. A weapon is a ScriptableObject config plus a behaviour reference. Adding the glue gun means writing one behaviour class and authoring one asset — no existing weapon code is touched.

### 5.7 Object Pooling

**Problem:** instantiating projectiles, staples, impact VFX, and enemies during play causes garbage collection spikes, which in a horror game read as the game stuttering when something scary happens.

**Local (non-networked) pooling** — VFX, decals, audio sources, UI elements. Use `UnityEngine.Pool.ObjectPool<T>`, which ships with the engine:

```csharp
public sealed class VfxPool : MonoBehaviour
{
    [SerializeField] private ParticleSystem prefab;
    [SerializeField] private int defaultCapacity = 32;
    [SerializeField] private int maxSize = 128;

    private ObjectPool<ParticleSystem> pool;

    private void Awake()
    {
        pool = new ObjectPool<ParticleSystem>(
            createFunc: () => Instantiate(prefab, transform),
            actionOnGet: ps => ps.gameObject.SetActive(true),
            actionOnRelease: ps => ps.gameObject.SetActive(false),
            actionOnDestroy: ps => Destroy(ps.gameObject),
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);
    }

    public ParticleSystem Get() => pool.Get();
    public void Release(ParticleSystem ps) => pool.Release(ps);
}
```

**Networked pooling** — enemies, projectiles that require server authority. NGO destroys and respawns network objects by default, which is expensive. Implement `INetworkPrefabInstanceHandler` so NGO takes from the pool instead:

```csharp
public sealed class NetworkObjectPool : MonoBehaviour, INetworkPrefabInstanceHandler
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int prewarmCount = 16;

    private readonly Queue<NetworkObject> available = new();

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var obj = available.Count > 0 ? available.Dequeue() : CreateNew();
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Destroy(NetworkObject networkObject)
    {
        networkObject.gameObject.SetActive(false);
        available.Enqueue(networkObject);
    }

    private NetworkObject CreateNew()
        => Instantiate(prefab, transform).GetComponent<NetworkObject>();
}
```

**Critical rule for pooling:** every pooled object must fully reset its state on release. Health, status effects, event subscriptions, coroutines, particle emission, physics velocity. Implement `IPoolable` with a `ResetState()` method and call it unconditionally. Ninety percent of pooling bugs are missing resets, and they present as impossible-looking behaviour: an enemy that spawns already dead, a flashlight that starts empty.

### 5.8 ScriptableObject-driven data (Flyweight)

**Problem:** balancing numbers live in prefabs, which are binary-ish YAML files owned by different people, which causes merge conflicts and unreviewable diffs.

```csharp
[CreateAssetMenu(menuName = "Office/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private DamageType vulnerabilities = DamageType.Blunt;
    [SerializeField] private DamageType immunities = DamageType.None;

    public string DisplayName => displayName;
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float DetectionRadius => detectionRadius;
    public DamageType Vulnerabilities => vulnerabilities;
    public DamageType Immunities => immunities;
}
```

Every tunable number in the game lives in a ScriptableObject. Benefits: designers tune without touching prefabs, diffs are readable, one shared instance serves all copies of an enemy, and balancing changes never conflict with art changes.

### 5.9 Command

**Problem:** networked interactions need validation, and some need to be undone or queued.

Interactions travel as commands from client to server: `InteractCommand { targetNetworkObjectId, interactionType }`. The server validates distance, line of sight, and state before executing. This also gives a natural place to add lag tolerance later.

---

## 6. Procedural Level Generation

The GDD requires an office whose layout changes. This is the highest-risk system in the project.

### 6.1 Approach

**Seeded graph-based assembly of handcrafted rooms.** Not wave function collapse, not cellular automata — those produce spaces that feel generated. Handcrafted rooms assembled procedurally produce spaces that feel designed but unfamiliar, which is exactly what horror needs.

```
1. Server generates a seed
2. Seed is replicated to all clients before floor load
3. Every client runs the identical deterministic generator
4. Result: identical geometry everywhere, near-zero network traffic
```

Determinism requirement: the generator uses its own `System.Random` instance seeded explicitly. It must never call `UnityEngine.Random`, never depend on `Time`, `Update` order, or physics results, and never iterate an unordered collection. A single violation produces desynchronised floors, which manifests as players walking through walls that exist only for them — a nightmare to debug.

### 6.2 Algorithm

```
Pick a floor template (defines room count, depth, required rooms)
  → Place the entry room
  → Walk a graph, attaching rooms at connector sockets
  → Guarantee required rooms exist (server room, breaker room, exit)
  → Close unused connectors with wall caps
  → Validate: is every required room reachable?
  → Place props, loot spawn points, enemy spawn points
  → Bake NavMesh at runtime
```

Rooms are prefabs with `RoomConnector` components on each doorway, declaring direction and size class. The generator matches sockets. This is the contract between A's generator and B's kit — it must be defined and frozen before B builds twenty rooms.

### 6.3 Navigation

Procedural geometry means the NavMesh cannot be baked offline. Use the **AI Navigation** package with runtime `NavMeshSurface.BuildNavMesh()` after generation completes.

Budget: runtime baking of a full floor costs on the order of one to several seconds. Hide it behind the floor transition. Bake on **all** clients — enemy movement is server-driven, but clients need the NavMesh for local prediction and for any client-side agent.

### 6.4 Lighting

The unavoidable cost of procedural levels: no baked global illumination. Mitigations:

- Per-room baked light probes stored in the room prefab
- Limited realtime lights with tight ranges, budget of 4–6 visible at once
- Emissive materials for monitors, exit signs, and screens
- Heavy fog doing most of the atmospheric work
- Vertex lighting where the PS1 aesthetic permits it — historically accurate and nearly free

### 6.5 Risk

If this system proves too costly, the fallback is **handcrafted floors with procedural variation**: fixed layout, randomised room contents, blocked routes, loot, and enemy placement. Ninety percent of the perceived variety for twenty percent of the engineering. Decide by the end of Milestone 2, not later.

---

## 7. Core System Specifications

### 7.1 Game state machine

`Boot → MainMenu → Lobby → Generating → InRun → FloorTransition → RunComplete/RunFailed → Lobby`

Server-authoritative, replicated as a `NetworkVariable<GameState>`. Every system subscribes and reacts. No system infers the current phase from anything else.

### 7.2 Player

| Component | Responsibility |
|---|---|
| `PlayerMovement` | Character controller, walk/sprint/crouch, stamina |
| `PlayerLook` | Camera, sensitivity, view bob |
| `PlayerInteraction` | Raycast for `IInteractable`, prompt, command dispatch |
| `PlayerInventory` | Four slots, held item, drop and pickup |
| `PlayerHealth` | `Health` component, downed state, revive |
| `PlayerFlashlight` | Battery, toggle, light cookie |
| `PlayerAudio` | Footsteps by surface, breathing tied to stamina and health |

Each component is separately testable and separately ownable. No shared "player god object".

### 7.3 Interaction

```csharp
public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract(PlayerInteraction source);
    void Interact(PlayerInteraction source);
}
```

Two-player interactions (simultaneous breakers) are modelled as an interactable that tracks a set of currently-holding clients server-side and fires when the required count is met.

### 7.4 Anomaly system

Digital entities affect game systems rather than dealing damage. This needs a dedicated service so effects are centralised, time-limited, and guaranteed to clean up:

```csharp
public interface IAnomalyService
{
    void ApplyEffect(AnomalyEffect effect, ulong targetClientId, float duration);
    void ClearAll(ulong targetClientId);
    bool HasEffect(AnomalyEffect effect, ulong targetClientId);
}
```

Effects: `HudDisabled`, `VisionCorrupted`, `AudioDistorted`, `StaminaDrained`, `MovementSlowed`, `FalseProjection`.

**Hard constraint from the GDD:** anomalies may never take control of player input. Removing agency reads as a bug and destroys trust in the game. Every effect must leave the player able to act.

Every effect must have a guaranteed expiry and a forced cleanup on run end, player death, and disconnect. An effect that leaks past its lifetime is a permanently broken session.

### 7.5 Power system

Zones with a powered/unpowered `NetworkVariable`. Lights, doors, elevators, and electrical enemies are `IPowerConsumer` and subscribe to their zone. Publishing `PowerStateChanged` on the local event bus drives all presentation.

### 7.6 Director

A server-side pacing system in the tradition of *Left 4 Dead*: tracks time since the last threat, player stress proxies (health, proximity, recent combat), and objective progress, then modulates spawn rate and intensity. Without one, procedural horror settles into a flat rhythm within two minutes.

Not required for the vertical slice. Required before the game is fun.

---

## 8. Rendering and Performance

### 8.1 PS1 pipeline

| Effect | Implementation |
|---|---|
| Vertex jitter | Custom shader; snap clip-space position to a low-resolution grid |
| Affine texture mapping | `noperspective` interpolation of UVs in the shader |
| Low internal resolution | Render to a 320×240 or 480×360 target, upscale point-filtered |
| Colour depth reduction | Full-screen pass with ordered dithering |
| VHS layer | Full-screen pass: scanlines, chromatic aberration, tracking noise |

In Unity 6, URP custom passes use **Render Graph**. The old compatibility mode is deprecated — write against Render Graph from the start rather than porting later.

### 8.2 Performance budget

| Metric | Target |
|---|---|
| Frame rate | 60 FPS on a GTX 1060-class GPU |
| Frame time | 16.6 ms — CPU main thread under 10 ms |
| Draw calls | Under 400 per frame |
| Active NetworkObjects | Under 60 |
| GC allocation during gameplay | **Zero per frame** |
| Runtime NavMesh bake | Under 2 s, hidden behind transition |
| Peak memory | Under 3 GB |

The zero-allocation target is the one that matters most. In a horror game, a GC spike during a scare is a ruined moment. Enforce it with the Profiler's allocation view, not with hope: no LINQ in `Update`, no string concatenation in the HUD, no `GetComponent` per frame, no closures capturing in hot paths.

### 8.3 Profiling cadence

Profile at the end of every milestone, not at the end of the project. Use the Profiler, Frame Debugger, and Memory Profiler on a **build**, not in the editor — editor numbers are fiction.

---

## 9. Data and Asset Pipeline

- All tunable values in ScriptableObjects under `Assets/_Project/Data/`
- Enemy, weapon, room, and floor definitions each in their own folder
- **Addressables** deferred to Milestone 4. Direct references are fine until load times become a real problem. Adopting Addressables early is a classic premature optimisation that costs weeks.
- Blender sources in `_Source/`, outside `Assets/`. Unity receives exported FBX only. Reason: importing `.blend` directly requires a matching Blender install on both machines and triggers reimports on every pull.
- Naming: `PascalCase`, type suffix — `Chair_Office_01.fbx`, `SM_Wall_Corner.fbx`, `T_Carpet_Grey_D.png`

---

## 10. Testing Strategy

| Layer | Tool | Coverage target |
|---|---|---|
| Pure logic — damage resolution, inventory, crafting, generator determinism | Unity Test Framework, EditMode | High. These are cheap to test and expensive to debug. |
| Systems — spawning, objectives, power | PlayMode tests | Moderate |
| Network behaviour | Multiplayer Play Mode, manual | Manual, checklist-driven |
| Performance | Profiler on build, per milestone | Threshold-based |

The single most valuable test in this project: **generator determinism**. Same seed must produce a byte-identical room graph across a hundred runs. Write it in week one and never remove it.

Manual network checklist, run every milestone: host disconnect, client disconnect and rejoin, two clients interacting with the same object simultaneously, a client joining mid-generation, high latency simulation.

---

## 11. Coding Standards

- C# naming: `PascalCase` for types and methods, `camelCase` for fields, `_camelCase` avoided in favour of plain `camelCase` with explicit `this` where ambiguous
- `private` by default. `[SerializeField] private` instead of `public` fields — always
- `sealed` on classes not designed for inheritance
- `readonly struct` for data passed by value; `in` parameters for structs above 16 bytes
- No `public` mutable state on `MonoBehaviour`
- No `GameObject.Find`, no `SendMessage`, no `Invoke("MethodName")`
- No logic in `Update` that could be event-driven
- One class per file, filename matches the class
- `.editorconfig` in the repository root; both machines use the same formatting rules

---

## 12. Milestones

### M0 — Foundations (2–3 weeks)

- [x] Repository, LFS, gitattributes, workflow — complete
- [ ] Unity project configured: URP, input system, assembly definitions, folder structure
- [ ] `Boot` scene, composition root, service locator, event bus
- [ ] NGO integrated, host/join over Relay working
- [ ] Multiplayer Play Mode configured for two virtual players
- [ ] Networked player: movement, look, replicated, two clients see each other move
- [ ] `.editorconfig`, coding standards agreed
- [ ] `RunState` structure defined and wired for player and session data (§2.7.2)
- [ ] Room connector contract written down and frozen (§14, item 3)
- [ ] B: modular kit on the 2 m grid, first ten pieces built against the frozen contract

**Exit criterion:** two players connect and walk around a static grey-box office. Nothing else.

### M1 — Core Interactions (3–4 weeks)

- [ ] Interaction system, `IInteractable`
- [ ] Inventory, pickup, drop, four slots
- [ ] Health, damage, `DamageType` matrix
- [ ] Two weapons: one melee, one ranged
- [ ] Object pooling for VFX and projectiles
- [ ] One enemy with a full state machine, server-authoritative
- [ ] Basic HUD
- [ ] Voice middleware integrated: proximity channel, wall occlusion, push-to-talk, mute, volume
- [ ] `RunState` snapshot-and-restore PlayMode test (§2.7.2)

**Exit criterion:** two players pick up weapons and kill an enemy together, with no desync, while hearing each other positionally.

### M2 — Level Generation (4–6 weeks)

- [ ] Room prefab contract, `RoomConnector`
- [ ] Deterministic seeded generator, with the determinism test
- [ ] Runtime NavMesh baking
- [ ] Loot and enemy spawn point system
- [ ] Power system with zones
- [ ] B: twelve rooms built to the contract

**Exit criterion:** two players explore a generated floor and see identical geometry. Generator fallback decision made here.

### M3 — Vertical Slice (6–8 weeks)

- [ ] Three enemies including one digital entity
- [ ] Four weapons with elemental interactions
- [ ] Restore Power objective, end to end
- [ ] Elevator escape
- [ ] Full PS1 render pipeline
- [ ] Audio layer: ambience, enemy audio, AI voice
- [ ] Downed, revive, and spectator state
- [ ] Equipment voice channel — dead players heard through office speakers (§2.6.3)
- [ ] Director v1 — the real pressure system, since the timer no longer provides it
- [ ] Consumable scarcity and weapon durability
- [ ] Run success and failure flow

**Exit criterion:** a complete, frightening, replayable 15-minute run. **This is the decision point for whether the project continues.**

### M4 — Content and Systems (ongoing)

Remaining floors, enemy roster, mini-bosses, crafting, director, anomaly variety, Steam integration, Addressables, settings and accessibility.

### M5 — Polish and Ship

Performance pass, bug triage, store page, trailer, playtesting with people who are not the developers.

---

## 12.1 Realistic Expectations

The GDD as written — four floors, twenty enemies, three bosses, crafting, procedural generation, a director, full audio — is a multi-year project for two part-time developers. This is not pessimism; it is the reason the milestone plan front-loads a vertical slice.

The plan above is designed so that stopping after M3 still yields something shippable and interesting. Design every milestone that way.

---

## 13. Risk Register

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Multiplayer retrofitted rather than built in | Project death | Low if this plan is followed | Network from commit one, no exceptions |
| Generator non-determinism | Severe, hard to debug | High | Determinism test in week one; forbid `UnityEngine.Random` in generation code |
| Scope exceeds team capacity | Project death | **Very high** | MVP in GDD §16; hard gate at M3 |
| Modular kit grid changed after rooms are built | Weeks of rework | Medium | Freeze the grid and connector contract in M0 |
| Procedural lighting looks flat | Quality | Medium | Probe-per-room strategy; fallback to handcrafted floors |
| GC spikes during scares | Quality | Medium | Zero-allocation budget, pooling, profile on build |
| LFS quota exhaustion | Workflow disruption | Medium | Weekly audit; $5/month data pack when needed |
| Trademark issue with title or in-game brands | Legal, storefront removal | Medium | Rename before any public build |
| Voice middleware cannot deliver the equipment channel | Loss of the signature mechanic | Low with Dissonance, high with Vivox | Prototype the DSP chain in M1 before committing content to it |
| `RunState` discipline decays, host migration becomes a rewrite | Weeks of rework at M4 | Medium | Snapshot-restore test enforced every milestone |
| Voice moderation obligations discovered late | Store submission blocked | Medium | Mute, report, and disable flows scoped before M4 |
| Timer has no teeth, runs feel aimless | Core loop failure | Medium | Director and scarcity systems in M3; explicit playtest check |
| One person loses motivation | Project death | High, in every two-person project | Short milestones, playable builds often, ship the slice |

---

## 14. Decisions Required Before Milestone 1

Nine decisions are locked in §0. These remain open.

| # | Decision | Owner | Blocks | Needed by |
|---|---|---|---|---|
| 1 | Voice middleware — confirm Dissonance, budget the licence | A | All voice work, §2.6 | M1 |
| 2 | Client-authoritative movement accepted? | A | Player controller | M0 |
| 3 | Room connector contract — socket sizes on the 2 m grid | A + B | Every room prefab ever built | M0, before B builds room 2 |
| 4 | Target run length: 25 / 30 / 40 min | Both | Floor count, pacing | M2 |
| 5 | Proximity voice range and rolloff | Both | Level sight and sound lines | M2 playtest |
| 6 | Emitter density for the equipment channel | A + B | Room kit contents | M2 |
| 7 | Final trademark-safe title | Both | Store page, repository name | Before any public build |
| 8 | Release model and date | Both | Milestone planning | After M3 |

Item 3 is now the expensive one. The 2 m module is locked, but the **connector contract** — socket dimensions, doorway width, ceiling height, floor pivot convention — is not, and B cannot safely build the kit until it exists. This is the first thing to resolve in M0.

Recommended starting contract, to be confirmed:

| Property | Value |
|---|---|
| Module | 2 m × 2 m footprint |
| Sub-grid | 0.25 m for props and detail |
| Ceiling height | 3 m (2 m base wall + 1 m header section) |
| Doorway width | 1 m, centred on a module edge |
| Pivot | Bottom corner of the floor footprint, never centred |
| Socket classes | `Corridor`, `Door`, `Wide` (double door), `Vent` |
| Forward axis | +Z out of the room, consistent on every connector |
