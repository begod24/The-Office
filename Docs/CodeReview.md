# Ревью проекта — Office Nightmare (The-Office)

**Дата:** 7 августа 2026
**Версия проекта на момент ревью:** коммит `622db33` («Music»)
**Движок:** Unity 6000.3.6f1 · URP 17.3.0 · NGO 2.13.1 · Multiplayer Services 2.3.0
**Объём проверенного:** 95 C#-файлов (~10 250 строк), 12 asmdef, 9 префабов, 4 сцены, конфиг NetworkManager, 7 наборов тестов, `GDD.md`, `Docs/Architecture.md`

**Цель ревью:** оценить пригодность архитектуры для кооп-игры и для дальнейшего масштабирования — чтобы добавление предметов, пропсов и механик оставалось дешёвым.

---

## Оглавление

1. [Общий вердикт](#1-общий-вердикт)
2. [Что сделано правильно](#2-что-сделано-правильно)
3. [Настоящие баги](#3-настоящие-баги)
4. [Что заблокирует масштабирование](#4-что-заблокирует-масштабирование)
5. [Сравнение с эталонами (Boss Room)](#5-сравнение-с-эталонами-boss-room)
6. [Как правильно делать то, что ещё не начато](#6-как-правильно-делать-то-что-ещё-не-начато)
7. [Мелочи](#7-мелочи)
8. [Приоритетный порядок работ](#8-приоритетный-порядок-работ)
9. [Источники](#9-источники)

---

## 1. Общий вердикт

**Да, подход правильный.** Это заметно выше среднего инди-уровня. Редко бывает, чтобы на этапе «есть капсула, предмет и лобби» уже существовали:

- asmdef с односторонними зависимостями и `autoReferenced: false`,
- composition root с installer-паттерном,
- data-driven контент через сетевые id вместо ссылок на ассеты,
- тесты на контракты между кодом и ассетами.

Фундамент заложен верно. Дальше в документе — в основном про то, что **сломается при росте**, а не про то, что уже хорошо, потому что именно это и было вопросом.

Три вещи, которые надо решить до того, как проект вырастет:

1. Разделить генерируемые из кода сцены и авторские сцены левел-дизайнера.
2. Распечатать `ItemDefinition` и перейти на модули — иначе оружие некуда добавлять.
3. Починить реентерабельность `EventBus`.

---

## 2. Что сделано правильно

| Решение | Почему это правильно |
|---|---|
| `autoReferenced: false` на всех asmdef | Код физически не может уехать в `Assembly-CSharp`. Апстрим-ссылка = ошибка компиляции, а не «договорённость между людьми». |
| Один `PF_WorldItem` на все предметы | Точный ответ на `ForceSamePrefabs`. Prefab-per-item — самая частая ошибка в NGO-проектах: забытая запись в реестре падает **только на удалённом клиенте**, хост её не видит. |
| `ContentDefinition.Id` вместо ссылок на ассеты | Ссылка на ассет ничего не значит на другой машине. Единое id-пространство между Item и Prop (`ItemContentBuilder.cs:108`) — тоже верно. |
| `HeldItemView` ничего не реплицирует | Выводит состояние из уже реплицированных `NetworkList` + `NetworkVariable`. Второй источник правды для того же факта — классический источник рассинхрона, и вы его избежали осознанно. |
| Split авторитета: движение — owner, инвентарь — сервер | Ровно то, что нужно кооп-хоррору. Lethal Company делает так же. Два игрока, тянущиеся к одному предмету, должны разрешаться одной машиной. |
| Scene-ready handshake перед `InRun` | Без него быстрая машина спавнит игроков в сцену, которую медленная ещё грузит. |
| `RequestInteractRpc` перепроверяет цель на сервере | Клиент только **просит**. Сервер заново резолвит `NetworkObjectReference` и меряет дистанцию от своей копии тела с допуском на интерполяцию. Это правильная модель доверия. |
| Позиция дропа считается на сервере | Иначе owner-авторитетный клиент дропал бы предметы через всю карту. |
| `ItemPlacement` — инертный маркер, а не NetworkObject | Правильный обход ограничения `EnableSceneManagement = false`. |
| Тесты `PhysicsLayersTests` / `DefinitionRegistryTests` | Тесты на **контракты между кодом и ассетами**, а не на арифметику. Правильный выбор целей: и то, и другое падает только в рантайме на удалённом клиенте. |
| `.gitattributes` с LFS + `unityyamlmerge` + `lockable` | На двоих с художником это спасёт недели. |
| Идемпотентные editor-билдеры | Сломанный префаб чинится повторным запуском MenuItem, а не разбором нечитаемого YAML-конфликта. |

---

## 3. Настоящие баги

### 3.1 EventBus: реентерабельный Publish молча теряет подписчиков ⚠️ ВАЖНО

**Файл:** `Assets/Project/Script/Core/Events/EventBus.cs:57`

`snapshot` — одно поле на весь `HandlerList<T>`, а не локальная переменная вызова:

```csharp
private sealed class HandlerList<T> : IHandlerList where T : struct
{
    private readonly List<Action<T>> handlers = new(4);
    private readonly List<Action<T>> snapshot = new(4);   // ← общий на все вызовы

    public void Invoke(in T evt)
    {
        if (handlers.Count == 0) return;

        snapshot.Clear();
        snapshot.AddRange(handlers);

        for (var i = 0; i < snapshot.Count; i++)
        {
            try { snapshot[i](evt); }
            catch (Exception e) { Debug.LogException(e); }
        }

        snapshot.Clear();      // ← вложенный вызов дошёл сюда и обнулил список
    }
}
```

**Сценарий отказа.** Подписчики `[A, B]`. Вызывается `Publish(evt1)`:

1. `snapshot = [A, B]`
2. `i = 0` → вызывается `A(evt1)`. Внутри `A` происходит `Publish(evt2)` **того же типа**.
3. Вложенный `Invoke`: `snapshot.Clear()` → `AddRange` → отработал → `snapshot.Clear()`. Теперь `snapshot.Count == 0`.
4. Возврат во внешний цикл: `i = 1`, проверка `1 < snapshot.Count` → `1 < 0` → **false** → цикл выходит.
5. **`B` никогда не получает `evt1`.**

Исключения нет, лога нет — событие просто тихо не доходит.

**Почему это выстрелит.** Сейчас событий мало. Оно сломается, когда `PowerStateChanged` начнёт триггерить `GameStateChanged`, или когда обработчик `LocalPauseChanged` опубликует `LocalPauseChanged`. Отлаживать такое долго, потому что симптом («иногда HUD не обновляется») никак не указывает на шину событий.

**Тесты это не ловят.** `EventBusTests.Handler_CanUnsubscribeItselfWhileBeingNotified` проверяет отписку изнутри обработчика, но не вложенную публикацию.

**Фикс — пул списков вместо одного поля:**

```csharp
private sealed class HandlerList<T> : IHandlerList where T : struct
{
    private readonly List<Action<T>> handlers = new(4);
    private readonly Stack<List<Action<T>>> pool = new();

    public void Invoke(in T evt)
    {
        if (handlers.Count == 0) return;

        var snapshot = pool.Count > 0 ? pool.Pop() : new List<Action<T>>(4);
        snapshot.AddRange(handlers);

        try
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                try { snapshot[i](evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
        finally
        {
            snapshot.Clear();
            pool.Push(snapshot);
        }
    }

    public void Clear()
    {
        handlers.Clear();
        pool.Clear();
    }
}
```

**Тест, который надо добавить:**

```csharp
[Test]
public void NestedPublish_StillReachesTheRemainingHandlers()
{
    var reached = false;
    var guard = false;

    bus.Subscribe<Ping>(_ =>
    {
        if (guard) return;
        guard = true;
        bus.Publish(new Ping(2));     // вложенная публикация того же типа
    });
    bus.Subscribe<Ping>(_ => reached = true);

    bus.Publish(new Ping(1));

    Assert.IsTrue(reached, "Вложенный Publish обрезал внешний список подписчиков.");
}
```

---

### 3.2 Late join во время рана — игрок остаётся без тела

**Файлы:** `Assets/Project/Script/Network/PlayerSpawner.cs:53-70`, `Assets/Project/Script/Network/SessionDirector.cs:97`

`PlayerSpawner.OnPhaseChanged` спавнит игроков **только на ребре перехода** фазы в `InRun`:

```csharp
private void OnPhaseChanged(GameState phase)
{
    // ...
    if (phase == GameState.InRun) SpawnMissingPlayers();
    else if (wasInRun) DespawnAll();
}
```

Клиент, подключившийся когда фаза **уже** `InRun`, не порождает перехода — значит `SpawnMissingPlayers()` не выполнится никогда. А `ReportRunSceneReadyRpc` в этой ситуации просто делает `return`:

```csharp
[Rpc(SendTo.Server)]
public void ReportRunSceneReadyRpc(RpcParams rpcParams = default)
{
    sceneReady.Add(rpcParams.Receive.SenderClientId);

    if (phase.Value != GameState.Generating) return;   // ← InRun уходит сюда, спавна нет
    if (sceneReady.Count < NetworkManager.ConnectedClientsIds.Count) return;

    TrySetPhase(GameState.InRun);
}
```

**Расхождение с документацией.** `Docs/Architecture.md` §9 утверждает: «Late join during a run **spawns a player** once that client reports its scene ready». Код этого не делает.

**Фикс — перенести спавн на сигнал «клиент готов», а не на ребро фазы:**

```csharp
// SessionDirector
public event Action<ulong> ClientReadyDuringRun;

[Rpc(SendTo.Server)]
public void ReportRunSceneReadyRpc(RpcParams rpcParams = default)
{
    var clientId = rpcParams.Receive.SenderClientId;
    sceneReady.Add(clientId);

    if (phase.Value == GameState.InRun)
    {
        ClientReadyDuringRun?.Invoke(clientId);   // late join
        return;
    }

    if (phase.Value != GameState.Generating) return;
    if (sceneReady.Count < NetworkManager.ConnectedClientsIds.Count) return;

    TrySetPhase(GameState.InRun);
}
```

и в `PlayerSpawner` подписаться на `ClientReadyDuringRun` → `SpawnFor(clientId)`.

---

### 3.3 Хост отвалился — клиенты зависают в ране

Во всём проекте нет ни одного обработчика `OnClientStopped` / `OnTransportFailure`:

```
grep -rn "OnClientStopped|OnTransportFailure|Shutdown()" Assets/Project/Script  →  пусто
```

GDD §15 требует: «Host disconnects — v1: Session ends for everyone, all return to lobby».

**Что происходит сейчас.** Клиент остаётся в `SCN_Sandbox` с мёртвой сессией, без UI-сообщения и без способа выйти. `MultiplayerSessionService.OnSessionDeleted` переведёт `Phase` в `Offline`, но никто на это не реагирует сменой сцены.

**Минимальный фикс в `NetworkServiceInstaller`:**

```csharp
public override void Install()
{
    // ... существующие подписки
    manager.OnClientStopped += OnClientStopped;   // срабатывает и у хоста, и у клиента
}

private void OnClientStopped(bool wasHost)
{
    if (wasHost) return;                          // хост уже сам всё разрулил

    var reason = NetworkManager.Singleton != null
        ? NetworkManager.Singleton.DisconnectReason
        : string.Empty;

    Debug.Log($"[Network] Соединение потеряно. {reason}");

    ServiceLocator.Get<IGameStateService>().SetFromAuthority(GameState.MainMenu);
    _ = ServiceLocator.Get<ISceneLoader>().SwapAsync(SceneNames.Sandbox, SceneNames.MainMenu);
}
```

Не забыть отписку в `Uninstall()`. И показать причину игроку — `NetworkManager.DisconnectReason` заполняется, если сервер её прислал.

---

### 3.4 `ConnectionApproval: 0` — лобби нельзя закрыть и версии не проверяются

**Файл:** `Assets/Project/Scenes/SCN_Boot.unity:162`

Последствия:

- лобби нельзя запереть на старте рана (`Architecture.md` §9 сам это признаёт как известный пробел);
- клиент со **старой версией контента** подключится молча и получит рассинхрон id предметов — то есть ровно тот класс багов, от которого защищает `DefinitionRegistry`;
- нельзя отклонить пятого игрока;
- нельзя передать данные о переподключении (понадобится к M4 по GDD §15).

**Фикс:**

```csharp
manager.NetworkConfig.ConnectionApproval = true;

manager.ConnectionApprovalCallback = (request, response) =>
{
    var payload = Encoding.UTF8.GetString(request.Payload);

    response.Approved = payload == ExpectedHandshake();   // версия билда + хеш реестра
    response.Reason = response.Approved ? string.Empty : "BUILD MISMATCH";
    response.CreatePlayerObject = false;                  // игроков спавним мы сами
};
```

Клиент выставляет payload перед стартом:

```csharp
manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ExpectedHandshake());
```

`ExpectedHandshake()` должен включать `Application.version` и хеш содержимого `REG_Definitions` (например, конкатенация id + имён). Дешевле сделать сейчас, чем ловить «у друга предметы не те» через полгода.

---

### 3.5 Расхождения кода и дизайн-документа

| Место | Код | GDD |
|---|---|---|
| `Data/GameplayConstants.cs:11` | `InventorySlots = 5` | §7.1 — «4 hotbar slots» |
| `ScriptableObject/Config/CFG_PlayerMovement.asset:26` | `canJump: 1` | §7.1 — прыжка нет (walk, sprint, crouch, vault) |

Второе `Architecture.md` признаёт осознанно (нужно для греибокса, надо вернуть до вертикального среза). Первое — просто рассинхрон, и его надо закрыть: 4 vs 5 слотов меняет весь дизайн «soft roles через дефицит слотов» из GDD §7.2. Четыре игрока × 4 слота = 16 слотов на группу; при 5 — 20, и специализация через дефицит работает слабее.

---

## 4. Что заблокирует масштабирование

Это важнее багов, потому что напрямую отвечает на вопрос «смогу ли я легко добавлять предметы, пропсы и механики».

### 4.1 `ItemDefinition` — `sealed`. Оружие добавить некуда 🔴

**Файлы:** `Data/Content/ItemDefinition.cs`, `Data/Content/PropDefinition.cs`

```csharp
public sealed class ItemDefinition : ContentDefinition   // и PropDefinition тоже sealed
```

Чтобы добавить оружие, нужны поля `damage`, `damageType`, `durability`, `attackRate`, `staminaCost`, `noiseRadius`. Наследование закрыто. Останется либо распечатать `sealed` и городить иерархию, либо (что случается чаще) свалить все поля оружия в `ItemDefinition` — и тогда у кофейной кружки будут поля урона и прочности.

**Наследование здесь всё равно неправильный ответ.** Смотрите GDD §8.3:

- **лазерная указка** — одновременно оружие (`Light` против Glitch-класса) и источник света;
- **огнетушитель** — тяжёлое оружие (`Blunt`) и утилита;
- **скотч** — не оружие, а иммобилайзер (`Adhesive`);
- **фонарик телефона** — источник света с батарейкой, но не оружие.

При наследовании это диамант: `WeaponDefinition` + `LightSourceDefinition` + `ConsumableDefinition` не комбинируются между собой.

**Правильно — композиция модулей.** Так устроены Boss Room (Actions как отдельные ScriptableObject), Valheim, RimWorld:

```csharp
// Office.Data — Data/Content/Modules/ItemModule.cs
public abstract class ItemModule : ScriptableObject { }
```

```csharp
[CreateAssetMenu(menuName = "Office/Modules/Melee", fileName = "MOD_Melee")]
public sealed class MeleeModule : ItemModule
{
    [SerializeField] private float damage = 12f;
    [SerializeField] private DamageType damageType = DamageType.Blunt;
    [SerializeField] private float staminaCost = 8f;
    [SerializeField] private float attackCooldown = 0.6f;

    [Tooltip("GDD §8.1: бой шумный и притягивает врагов.")]
    [SerializeField] private float noiseRadius = 12f;

    public float Damage => damage;
    public DamageType DamageType => damageType;
    public float StaminaCost => staminaCost;
    public float AttackCooldown => attackCooldown;
    public float NoiseRadius => noiseRadius;
}

[CreateAssetMenu(menuName = "Office/Modules/Light Source", fileName = "MOD_Light")]
public sealed class LightSourceModule : ItemModule
{
    [SerializeField] private float batteryDrainPerSecond = 1f;
    [SerializeField] private float range = 14f;
    [SerializeField] private float angle = 55f;

    public float BatteryDrainPerSecond => batteryDrainPerSecond;
    public float Range => range;
    public float Angle => angle;
}

[CreateAssetMenu(menuName = "Office/Modules/Durability", fileName = "MOD_Durability")]
public sealed class DurabilityModule : ItemModule
{
    [SerializeField] private int maxUses = 40;
    public int MaxUses => maxUses;
}
```

И в `ItemDefinition` (уже без `sealed`, но наследовать не придётся):

```csharp
[Header("Behaviour")]
[Tooltip("Что этот предмет умеет. Композиция вместо наследования: лазерная указка — " +
         "это Melee(Light) + LightSource, а не отдельный класс.")]
[SerializeField] private ItemModule[] modules = System.Array.Empty<ItemModule>();

public T GetModule<T>() where T : ItemModule
{
    foreach (var module in modules)
        if (module is T typed)
            return typed;

    return null;
}

public bool HasModule<T>() where T : ItemModule => GetModule<T>() != null;
```

**Результат:** «лазерная указка» = `MeleeModule(Light)` + `LightSourceModule` + `DurabilityModule` — три ассета в инспекторе, ноль кода. Это ровно то «добавление предмета — ассет и меш», которое уже сделано для визуала, распространённое на поведение.

**Важный нюанс:** модуль хранит **только данные**. Исполнение — в системах на сервере (`MeleeAttackSystem` читает `MeleeModule` из определения выбранного предмета). Иначе ScriptableObject станет носителем состояния, а он один на все экземпляры предмета — прочность конкретного степлера в руке нельзя хранить в ассете.

Состояние экземпляра (текущая прочность, заряд батареи) должно жить в `ItemStack`:

```csharp
public struct ItemStack : INetworkSerializable, IEquatable<ItemStack>
{
    public int DefinitionId;
    public int Count;
    public ushort Durability;    // 0 = «не применимо» / полное, если модуля нет
}
```

Два байта на слот, 5 слотов × 4 игрока = 40 байт — незаметно на фоне остального трафика.

---

### 4.2 `DefinitionRegistry` захардкожен под два типа

**Файлы:** `Data/Content/DefinitionRegistry.cs:22-24`, `Editor/Setup/ItemContentBuilder.cs:79`

Сейчас: два массива, два словаря, два метода `TryGetItem` / `TryGetProp`. И `RebuildRegistry` перечисляет типы руками.

Впереди по GDD: `EnemyDefinition` (§9), `RoomDefinition` (§12.3), `RecipeDefinition` (§8.4), `ObjectiveDefinition` (§10.1), `AnomalyDefinition` (§9.2). Это +5 пар полей в реестре и +5 правок билдера.

**Схлопните в один массив.** Id-пространство **уже единое** (см. `AssignIds` в `ItemContentBuilder.cs:108`), так что это чисто механическая замена:

```csharp
[CreateAssetMenu(menuName = "Office/Content/Definition Registry", fileName = "REG_Definitions")]
public sealed class DefinitionRegistry : ScriptableObject
{
    [SerializeField] private ContentDefinition[] definitions = Array.Empty<ContentDefinition>();

    private Dictionary<int, ContentDefinition> byId;

    public IReadOnlyList<ContentDefinition> All => definitions;

    private void OnEnable() => Invalidate();

    public void Invalidate() => byId = null;

    public bool TryGet<T>(int id, out T definition) where T : ContentDefinition
    {
        byId ??= BuildIndex(definitions);

        if (byId.TryGetValue(id, out var found) && found is T typed)
        {
            definition = typed;
            return true;
        }

        definition = null;
        return false;
    }
}
```

Вызовы становятся `registry.TryGet<ItemDefinition>(id, out var item)`. В билдере — `LoadAll<ContentDefinition>()` вместо перечисления типов:

```csharp
private static void RebuildRegistry()
{
    var all = LoadAll<ContentDefinition>();
    AssignIds(all);
    // одна запись массива вместо WriteArray на каждый тип
}
```

Тогда новый тип контента **вообще не требует правок реестра** — достаточно создать ассет.

---

### 4.3 Генерация сцен из кода vs ручной левелдизайн 🔴 САМЫЙ БОЛЬШОЙ РИСК

`Office/Setup/Build Sandbox Scene`, `Build Lobby Scene`, `Build Boot Scene`, `Build Main Menu Scene`, `Rebuild HUD In Open Scene` — **пересоздают сцены с нуля**.

Для двоих программистов это отличный ход: YAML-конфликты не мержатся, и «сломанный префаб чинится нажатием MenuItem». Но по GDD **человек B — левел-дизайнер и 3D-художник**, и §12.3 прямо говорит, что модульный кит уровня — приоритетный арт-деливерабл.

**В ту секунду, когда напарник расставит комнаты в `SCN_Sandbox` руками, а вы нажмёте `Build Sandbox Scene`, его работа исчезнет.** Без предупреждения, без возможности отката (сцена перезаписывается, а не мержится).

Это единственный пункт во всём ревью, который может **уничтожить чужую работу**, поэтому он первый в приоритетах.

**Введите явное разделение сейчас, пока сцена одна:**

| Категория | Владелец | Правило |
|---|---|---|
| `SCN_Boot`, `SCN_MainMenu`, `SCN_Lobby`, весь HUD, `PF_Player`, `PF_Session`, `PF_WorldItem` | код (`ProjectSetup`, `HudBuilder`, …) | **Никогда** не редактировать руками. Правка = правка билдера. |
| Уровни, `ROOM_*` префабы, модульный кит, расстановка `ItemPlacement` | человек | Билдер их не трогает. Ни одного `Build ... Scene` для них. |

**Технически:**

1. Переименуйте `SCN_Sandbox` → `SCN_GreyboxTest` (генерируемая, ваша, для тестов систем).
2. Заведите отдельную `SCN_Floor01` или префабы комнат — их не генерирует ни один MenuItem.
3. Добавьте в каждый билдер сцен защиту:

```csharp
private static bool ConfirmRegenerate(string sceneName)
{
    return EditorUtility.DisplayDialog(
        "Пересоздать сцену",
        $"'{sceneName}' генерируется из кода. Все ручные правки в ней будут потеряны.\n\n" +
        "Если в этой сцене есть работа левел-дизайнера — отмените.",
        "Пересоздать", "Отмена");
}
```

4. Запишите это правило в `Docs/Architecture.md` — оно должно быть письменным, а не устным.

---

## 5. Сравнение с эталонами (Boss Room)

Сравнивал с **Boss Room** — официальным кооп-сэмплом Unity на NGO, ближайшим по жанру и топологии (client-hosted listen server, 4 игрока, Relay).

| Аспект | Boss Room | Ваш проект | Оценка |
|---|---|---|---|
| Сборки | доменные, client/server-классы в одной сборке | доменные, одна реализация на домен | ✅ ваш вариант проще и для двоих лучше |
| DI | **VContainer**, `LifetimeScope` на сцену | статический `ServiceLocator` | ⚠️ см. ниже |
| Composition root | `ApplicationController` | `GameBootstrap` + `ServiceInstaller` | ✅ эквивалент |
| Состояние подключения | `ConnectionManager` — **машина состояний** (`StartingHostState`, `ClientConnectingState`, …) | линейный `MultiplayerSessionService` + enum `SessionPhase` | ⚠️ ваш проще, но не покрывает реконнект и часть фейлов |
| Шина сообщений | `MessageChannel` (`IPublisher`/`ISubscriber`) + `NetworkedMessageChannel` | `EventBus` | ✅ то же самое, но без сетевого варианта |
| Реконнект | карта GUID → данные игрока, восстановление состояния | нет | 🔴 GDD §15 требует к M4 |
| Спавн игрока | `PersistentPlayer` (живёт всю сессию) + `PlayerAvatar` (на ран) | только ран-объект, «сиденье» в `Dictionary` внутри спавнера | ⚠️ см. ниже |
| Абилки / действия | `Action` как ScriptableObject + `ServerActionPlayer` / `ClientActionPlayer` | нет | 🔴 см. §4.1 |
| Пул объектов | `NetworkObjectPool` через `INetworkPrefabInstanceHandler` | нет | ⚠️ понадобится для роёв |
| NetworkVariable vs RPC | NV для долгого состояния, RPC для одноразовых событий | так же | ✅ |

### 5.1 ServiceLocator vs DI — рекомендация: **не мигрировать сейчас**

Общая рекомендация индустрии — «Service Locator для прототипов, DI для масштабируемых архитектур», и Boss Room действительно на VContainer.

**Но мигрировать сейчас не стоит.** У вас `ServiceLocator` используется дисциплинированно:

- резолв только в `Awake` / `Start` / `OnNetworkSpawn`, никогда в `Update`;
- есть `TryGet` с явной обработкой отсутствия сервиса;
- есть `ServiceLocatorTests`;
- есть `ResetStatics` через `RuntimeInitializeOnLoadMethod` (важно при выключенном domain reload).

Цена миграции высокая, выигрыш на текущем размере — нулевой.

**Что стоит сделать вместо этого — зафиксировать правило письменно в `Architecture.md`:**

> Сервисы резолвятся один раз при инициализации компонента и кешируются в поле.
> `ServiceLocator.Get` в `Update`, в конструкторе или в статическом инициализаторе — запрещён.
> Новый сервис регистрируется только через `ServiceInstaller` своей сборки.

Иначе, когда `Office.Enemies` и `Office.Anomalies` наполнятся, каждый скрипт начнёт дёргать локатор откуда попало, и порядок инициализации станет неотлаживаемым. Именно этот сценарий и породил репутацию Service Locator как антипаттерна — не сам паттерн, а его недисциплинированное применение.

### 5.2 `PersistentPlayer` вам понадобится

Сейчас «сиденье» игрока живёт в `Dictionary<ulong, int> seats` внутри `PlayerSpawner.cs:26` и стирается при дисконнекте.

Но по GDD:

- §15 — отключившийся игрок должен переподключиться в тот же ран (M4);
- §7.1 — мёртвый игрок остаётся спектатором;
- §7.3.1 — голос мёртвого игрока продолжает вещать через офисную технику.

И то, и другое, и третье требует объекта, который **переживает аватара**. Заложите `PF_PersistentPlayer` — спавнится на подключении, живёт до дисконнекта, хранит seat / имя / статус (жив / downed / мёртв / спектатор) — **до** того, как появится система здоровья. После — переделывать дороже, потому что от статуса будет зависеть уже написанный код.

### 5.3 Пул сетевых объектов

GDD §9.1 прямо описывает роевых врагов:

- **Stapler** — «fast melee **swarm**»
- **Extension Cord** — «fast, low HP, **swarm**»
- **Copier** — «**spawns** weak duplicates»

Плюс проджектайлы принтера (`Printer` — «fires paper shards») и степл-ган игрока.

`Instantiate` + `Spawn` на каждый экземпляр — это GC-спайки ровно в момент боя, когда фреймтайм важнее всего. NGO даёт `INetworkPrefabInstanceHandler` именно для этого.

Сделайте пул один раз в `Office.Network` и прогоните через него `PF_WorldItem` — у вас уже единый carrier, это идеальный первый кандидат и заодно проверка механизма:

```csharp
public sealed class PooledPrefabHandler : INetworkPrefabInstanceHandler
{
    private readonly GameObject prefab;
    private readonly Queue<NetworkObject> pool = new();

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        if (pool.TryDequeue(out var pooled))
        {
            pooled.transform.SetPositionAndRotation(position, rotation);
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Object.Instantiate(prefab, position, rotation).GetComponent<NetworkObject>();
    }

    public void Destroy(NetworkObject networkObject)
    {
        networkObject.gameObject.SetActive(false);
        pool.Enqueue(networkObject);
    }
}
```

Регистрация: `NetworkManager.PrefabHandler.AddHandler(prefab, handler)`.

---

## 6. Как правильно делать то, что ещё не начато

### 6.1 Здоровье и урон — фундамент, которого нет

`DamageType` есть (`Data/DamageType.cs`), но `IDamageable` нет, и `RunState.PlayerState.Health` никем не пишется. Это следующая система по приоритету, потому что от неё зависят враги, аномалии, downed-состояние и спектатор.

**Форма — по образцу вашего же `IInteractable`** (сервер-авторитетно, клиент только просит):

```csharp
// Office.Gameplay/Combat/IDamageable.cs
public interface IDamageable
{
    bool IsAlive { get; }

    /// <summary>Только сервер. Возвращает фактически нанесённый урон.</summary>
    float ApplyDamage(in DamageInfo info);
}

public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly ulong SourceClientId;   // 0 = мир / аномалия
    public readonly Vector3 Point;
    public readonly Vector3 Direction;

    public DamageInfo(float amount, DamageType type, ulong sourceClientId,
                      Vector3 point, Vector3 direction)
    {
        Amount = amount;
        Type = type;
        SourceClientId = sourceClientId;
        Point = point;
        Direction = direction;
    }
}
```

**Ключевой момент под GDD §8.3** (вода бьёт электрических, свет — цифровых): **резисты и уязвимости — это данные, не код.** Иначе каждая пара «оружие × враг» превратится в `if` внутри системы боя, и матрица 4 оружия × 20 врагов станет нечитаемой.

Таблица на определении врага:

```csharp
[Serializable]
public struct DamageResponse
{
    public DamageType Type;
    [Min(0f)] public float Multiplier;
}

// EnemyDefinition
[Tooltip("Пусто = урон применяется как есть. Digital-класс: Blunt ×0, Light ×2.5.")]
[SerializeField] private DamageResponse[] responses;

public float MultiplierFor(DamageType type)
{
    foreach (var response in responses)
        if ((response.Type & type) != 0)
            return response.Multiplier;

    return 1f;
}
```

Тогда «цифровые сущности неуязвимы к физическому оружию» — центральный урок игры по GDD §9.2 — это `Multiplier = 0` в ассете, а не строка кода. И баланс правит дизайнер, а не программист.

**Репликация здоровья:** `NetworkVariable<float>` с write-permission Server. Boss Room делает так же для долгоживущего состояния. RPC оставляйте под одноразовые события (хит-фидбек, звук удара, вспышка экрана) — RPC дешевле по трафику и не требует подписки на изменения.

### 6.2 Враги — NavMesh только на сервере

Практика NGO: AI-персонажи существуют **только** на сервере, и `NavMeshAgent` / `NavMeshModifier` / `NavMeshObstacle` на клиентах не нужны и должны отключаться. Клиент получает только позицию через `NetworkTransform`.

```csharp
public override void OnNetworkSpawn()
{
    if (!IsServer)
    {
        agent.enabled = false;      // иначе агент дерётся с NetworkTransform
        brain.enabled = false;
        senses.enabled = false;
        return;
    }

    // серверный AI
}
```

**Важно:** `NetworkTransform` для врагов — **Server authority** (в отличие от игрока, где Owner). Иначе врага можно будет двигать с клиента.

Ещё: в сборку клиента NavMesh-данные можно вообще не класть, если генерация серверная — это экономит размер билда.

### 6.3 Генерация уровня — гибрид, не чистый seed

Здесь есть развилка, и **обе крайности неправильны**:

| Подход | Плюс | Минус |
|---|---|---|
| Чистый seed (все клиенты генерируют локально) | почти нулевой трафик | детерминизм в Unity хрупкий: разный порядок ассетов, версии физики, float-округления → **игроки видят разные стены** |
| Чистый server-spawn (каждая стена — NetworkObject) | надёжно | сотни NetworkObject на этаж, спавн-шторм при старте рана |

**Правильно для вас — гибрид, и он уже соответствует вашей архитектуре:**

1. Сервер выбирает `seed`, кладёт в `NetworkVariable<int>` (поле `RunState.FloorSeed` уже есть).
2. Каждый клиент **локально** строит геометрию из seed — стены, полы, потолки, освещение. Это обычные GameObject **без NetworkObject**, ровно как ваши `VIEW_ITM_*` префабы.
3. Всё интерактивное — двери, рубильники, предметы, враги — сервер спавнит как зарегистрированные префабы, ровно как вы уже делаете через `ItemPlacement` → `WorldItemSpawner`.
4. NavMesh пекётся **на сервере** после генерации: `NavMeshSurface.BuildNavMesh()` из `com.unity.ai.navigation` (уже в зависимостях `Office.LevelGen`).

Это буквально расширение паттерна `ItemPlacement`, который вы уже написали: сцена содержит инертные маркеры, сервер превращает их в сетевые объекты. **Ваша интуиция здесь уже правильная** — просто примените её к комнатам.

**Не забыть:** `Architecture.md` пишет, что `EnableSceneManagement` включается на Sprint 6. В этот момент правило «ничего интерактивного в сцене как NetworkObject» перестаёт быть обязательным. **Не переписывайте `ItemPlacement` тогда** — он всё равно лучше, потому что позволяет менять контент без правки сетевого реестра префабов.

### 6.4 Система питания — сделайте её сервисом сразу

GDD §10.2 называет питание «соединительной тканью» всех систем:

- свет включён → безопаснее, но враги видят дальше;
- свет выключен → враги медленнее замечают, но нужны батарейки;
- часть дверей и лифтов требует питания;
- `Electrical Outlet` и `Extension Cord` усиливаются активным питанием;
- обесточить зону — валидное тактическое решение с реальным трейд-оффом.

У вас уже есть событие `PowerStateChanged(ZoneId, IsPowered)` в `Core/Events/GameEvents.cs:19` и структура `PowerZoneState` в `RunState` — но нет сервиса, который это публикует.

**Заведите `IPowerService`** в `Office.Core` (интерфейс) + реализацию на сессионном объекте (сервер владеет `NetworkList<PowerZoneState>`). Всё остальное подписывается на `PowerStateChanged` и **ничего не знает про питание**. Тогда «выключить свет в зоне» — одна строка на сервере, а не обход всех систем.

Это тот случай, когда правильная развязка через шину событий окупается сразу: свет, двери, враги, аномалии и HUD получат событие независимо друг от друга.

---

## 7. Мелочи

### 7.1 `HudStaminaBar` сканирует сцену

**Файл:** `UI/Hud/HudStaminaBar.cs:57`

```csharp
var candidates = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
```

Выбивается из стиля проекта. У вас уже есть красивый паттерн `PlayerInventory.Local` + статическое событие `LocalChanged`. Сделайте так же для `PlayerMovement` — или, лучше, заведите один `LocalPlayer` со ссылками на все компоненты локального игрока, и HUD перестанет сканировать сцену.

### 7.2 Unity-null и интерфейсы — ловушка на будущее

**Файл:** `Gameplay/Interaction/PlayerInteractor.cs:144`

```csharp
return nearest != null && nearest.IsAvailable ? nearest : null;
```

`nearest` имеет тип `IInteractable`. Сравнение `!= null` на **интерфейсном** типе — это обычное сравнение ссылок C#, а **не** перегрузка `UnityEngine.Object.operator ==`. Уничтоженный компонент («fake null») пройдёт эту проверку.

Сейчас спасает цепочка `IsAvailable → IsSpawned`. Но с ростом числа реализаций `IInteractable` (двери, рубильники, терминалы, верстаки) это ловушка. Заведите правило:

```csharp
// либо приводить к Object перед проверкой
if (nearest is Object obj && obj != null) { ... }

// либо добавить в интерфейс явную проверку живости
public interface IInteractable
{
    bool IsAlive { get; }   // реализация: this != null && IsSpawned
    // ...
}
```

### 7.3 `WorldItemSpawner` — guard по размеру списка

**Файл:** `Gameplay/Items/WorldItemSpawner.cs:711`

```csharp
private void SpawnPlacements()
{
    if (spawned.Count > 0) return;   // ← заблокирует респавн после нештатного завершения
```

Если ран закончился аварийно и `DespawnAll` не отработал, список останется непустым и следующий ран не расставит предметы. Проверяйте фазу, а не размер списка.

### 7.4 Направление зависимости `Office.Gameplay → Office.Network`

Обратное Boss Room (там gameplay не знает про транспорт). Некритично, потому что ваш `Office.Network` — это скорее «сессия», чем «транспорт».

Но когда появятся `Office.Enemies` и `Office.Anomalies`, они через `Office.Gameplay` получат доступ к `SessionDirector` — и рано или поздно кто-нибудь вызовет оттуда `RequestEndRunRpc`. Стоит хотя бы задокументировать в `Architecture.md`:

> В `Office.Network` из вышележащих сборок обращаются только за `ILobbyService` и `ISessionService`.
> Прямые обращения к `SessionDirector`, `PlayerSpawner`, `LobbyRoster` запрещены.

### 7.5 `README.md` пустой

При двух людях в проекте README — это инструкция «как запустить два клиента и проверить сетевой код». `Architecture.md` §9 **сам ссылается на README** за ручной процедурой проверки двух клиентов, а его нет.

Минимум, что там должно быть:

- порядок первого запуска (`Office/Setup/Run All`, `Office/Content/Build All`, импорт TMP);
- правило «входить в play mode только из `SCN_Boot`»;
- как поднять Multiplayer Play Mode с двумя виртуальными игроками;
- чек-лист ручной проверки двух клиентов (роутер: хост создал → клиент вошёл по коду → оба в ростере → оба ready → старт → оба в сцене → предмет подобран у обоих).

---

## 8. Приоритетный порядок работ

| # | Что | Почему сейчас | Раздел |
|---|---|---|---|
| 1 | Разделить генерируемые и авторские сцены | Единственный пункт, способный **уничтожить чужую работу**. Дешевле всего, пока сцена одна. | §4.3 |
| 2 | Распечатать `ItemDefinition` → модули | Блокирует всё оружие и все инструменты. Переделывать после 30 предметов — больно. | §4.1 |
| 3 | Починить `EventBus` + тест | 15 минут работы. Иначе позже — сутки отладки «почему иногда не приходит событие». | §3.1 |
| 4 | `ConnectionApproval` + версия контента | Защищает то, ради чего вообще сделан реестр id. | §3.4 |
| 5 | Отвал хоста + late join | Первое, что заметят тестеры. | §3.2, §3.3 |
| 6 | Обобщить `DefinitionRegistry` | Механическая правка, снимает трение с каждого нового типа контента. | §4.2 |
| 7 | `IDamageable` + таблица резистов | Фундамент под врагов и аномалии. | §6.1 |
| 8 | `PF_PersistentPlayer` | Нужен **до** системы смерти и спектатора. | §5.2 |
| 9 | Пул сетевых объектов | До первого роя, не после. | §5.3 |
| 10 | Синхронизировать `InventorySlots` с GDD, заполнить README | Гигиена. | §3.5, §7.5 |

**Рекомендуемое расписание:**

- **Эта неделя:** пункты 1–3.
- **До первого врага:** пункты 4–6.
- **Вместе с первым врагом:** пункты 7–9.

---

## 9. Источники

- [Boss Room architecture — Netcode for GameObjects](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/samples/bossroom/architecture.html)
- [com.unity.multiplayer.samples.coop (Boss Room, GitHub)](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop)
- [Optimizing Boss Room performance](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.6/manual/samples/bossroom/optimizing-bossroom.html)
- [NetworkObject ownership — NGO docs](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/components/core/networkobject-ownership.html)
- [VContainer](https://vcontainer.hadashikick.jp/)
- [Проблемы синхронизации NavMeshAgent с Netcode — Unity Discussions](https://discussions.unity.com/t/problems-with-initial-synchronization-of-navmeshagent-with-netcode/905830)
- [Seed в мультиплеере для процедурной генерации — Unity Discussions](https://discussions.unity.com/t/seed-using-in-multiplayer-game-to-generate-procedural-level-enemies/944688)
- [Player spawning с процедурной генерацией в NGO — Unity Discussions](https://discussions.unity.com/t/player-spawning-issue-in-multiplayer-unity-ngo-with-procedural-generation-question/1637626)

---

*Документ отражает состояние репозитория на 7 августа 2026 (коммит `622db33`). Ссылки на строки кода актуальны для этого коммита.*
