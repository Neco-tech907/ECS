# Практика 5. Взаимодействие мира Entity и мира GameObject

Изменения пятой практической работы в проекте (`Assets/Scripts/Herd`, пространство имён `Esc.Herd`):

- в подсцене на `PrefabHolder` запекается синглтон-мост `ShepherdInput` + `HerdHudData` + `FlockCleared` (`BridgeAuthoring.cs`);
- пастух `PlayerArmature` в `SampleScene` пишет ввод в ECS через `ShepherdInputController`;
- HUD `HerdHud` читает уже посчитанные данные Entity и подписан на редкое managed-событие;
- системы моста живут в `HerdBridgeSystems.cs`.

Задание: реализовать **не меньше двух** сценариев обмена между мирами и обосновать выбор направления для каждого.

В проекте сделаны **три** сценария, чтобы закрыть все три типовых примера шпаргалки темы 5:

| № | Сценарий | Направление | Почему так |
|---|---|---|---|
| 1 | Пастух свистит (`E`) — овцы идут за ним | GameObject → Entity | Событие рождается в Input System на `MonoBehaviour`. В ECS уходит уже чистая структура |
| 2 | HUD: число живых овец и HP ближайшей | Entity → GameObject (готовые данные) | Поиск ближайшей среди тысяч овец должен остаться в Burst-системе |
| 3 | «Стадо рассеяно», когда волны кончились и живых 0 | Entity → GameObject (`Action`) | Редкое событие, логически случается в ECS; Burst на этом вызове не нужен |

---

## 1. Тематика: два мира и два направления обмена

Мир **GameObject** — это управляемые классы (`MonoBehaviour`, UI, Input System, `Transform`). Мир **Entity** — неуправляемые `IComponentData`, `ISystem`, Burst и Jobs. Напрямую они не видят друг друга.

Шпаргалка даёт два основных способа обмена:

1. **Из ECS-системы обратиться к managed-памяти** (синглтон `MonoBehaviour`, `Action.Invoke`). Система теряет Burst и Jobs.
2. **Из `MonoBehaviour` обратиться к `EntityManager`**: `World.DefaultGameObjectInjectionWorld.EntityManager`, `CreateEntityQuery`, `GetComponentData` / `SetComponentData`.

Что выбирать, зависит от того, **где возникает событие**, **как часто** оно происходит и **какой объём данных** нужно обработать.

---

## 2. Сценарий 1. Ввод пастуха: GameObject → Entity

Аналог примера 1 шпаргалки (WASD → `PlayerMovementData`).

### Почему направление такое

Нажатие `E` и позиция пастуха живут в мире GameObject: `PlayerArmature` двигается `ThirdPersonController` + Input System. Овцы — Entity. Логично:

1. `MonoBehaviour` собирает ввод в чистую структуру;
2. записывает её в синглтон-компонент;
3. Burst-система читает синглтон и меняет `MoveTarget` овец.

Если бы ECS-система сама ходила в `Keyboard.current`, `ShepherdFollowSystem` потеряла бы `[BurstCompile]`.

### Что куда повешено

- `Assets/Scenes/SampleScene.unity` → `PlayerArmature` → `ShepherdInputController`.
- `Assets/Scenes/HerdSubScene.unity` → `PrefabHolder` → `BridgeAuthoring` (бейкер синглтона).

### Код записи (мир GameObject)

`ShepherdInputController` каждый кадр кладёт позицию и флаг `FollowHeld` в синглтон `ShepherdInput` через `EntityManager.SetComponentData` (обёртка `EntityWorldAccess.TrySetSingleton`). Пока подсцена не загрузилась, синглтона нет — метод просто возвращает `false`, без исключения.

Клавиша — `Keyboard.current.eKey.isPressed` (проект на новом Input System).

### Код чтения (мир Entity)

`ShepherdFollowSystem` стоит `[UpdateAfter(typeof(HerdGoalSyncSystem))]` и `[UpdateBefore(typeof(MoveToTargetSystem))]`: сначала загон как обычно пишется в `MoveTarget`, затем при зажатом `E` цель перезаписывается на позицию пастуха. Сама джоба Burst-параллельная и managed-памяти не касается:

```csharp
ShepherdInput input = SystemAPI.GetSingleton<ShepherdInput>();
if (!input.FollowHeld)
    return;
// ShepherdFollowJob: move.Position = ShepherdPosition;
```

Отпустил `E` — следующий кадр снова ведёт овец в загон.

---

## 3. Сценарий 2. HUD: Entity считает, GameObject только рисует

Аналог примера 3 шпаргалки (HP ближайшего врага).

### Почему не искать овец в `Update` у HUD

Обход всех `SheepTag` с `math.distancesq` — работа для Burst. В `MonoBehaviour` этот цикл был бы на главном потоке и без Jobs. Поэтому:

1. `HerdHudGatherSystem` (Burst) считает `AliveCount`, HP/стамину ближайшей к пастуху овцы и дистанцию;
2. пишет результат в синглтон `HerdHudData`;
3. `HerdHudView.OnGUI` только читает этот синглтон.

`HerdHud` — обычный GameObject в `SampleScene`, не в подсцене.

### Как выглядит на экране

```
Живых овец: 18
Ближайшая овца: HP 100  stamina 20.0  dist 12.3 м
Овцы идут в загон. Удерживайте E — пойдут за пастухом.
```

---

## 4. Сценарий 3. Редкое событие через `Action`

Аналог примера 2 шпаргалки (ачивка за 1000 убийств).

Событие «волны закончились и живых овец не осталось» случается **максимум один раз за сессию** и логически рождается в ECS (курсор волн + счётчик живых). Потенциальная цена перехода unmanaged → managed ничтожна.

`FlockClearedSystem` **без** `[BurstCompile]`:

```csharp
if (!cursor.Finished || hud.AliveCount > 0)
    return;

HerdEvents.OnFlockCleared?.Invoke();
flag.ValueRW.Fired = true;
state.Enabled = false;
```

`HerdHudView` в `OnEnable` подписывается на `HerdEvents.OnFlockCleared` и меняет строку статуса. После срабатывания система выключает себя (`state.Enabled = false`), повторных вызовов нет.

Почему не проверять то же самое каждый кадр в `Update` HUD: архитектурно чище вызвать делегат **там, где условие стало истинным**. Для частого события шпаргалка советует наоборот — пусть GameObject сам читает компонент. Здесь частота = 1 раз, поэтому `Action`.

В текущей сцене овцы из практики 4 теряют 25 HP о загон и в итоге умирают. После второй волны HUD показывает: «Событие из ECS: стадо рассеяно…».

---

## 5. Компоненты моста (`BridgeComponents.cs`)

```csharp
public struct ShepherdInput : IComponentData
{
    public float3 Position;
    public bool FollowHeld;
}

public struct HerdHudData : IComponentData
{
    public int AliveCount;
    public float NearestHealth;
    public float NearestStamina;
    public float NearestDistance;
    public bool HasNearest;
}

public struct FlockCleared : IComponentData
{
    public bool Fired;
}
```

Все три кладутся на одну entity `PrefabHolder` бейкером `BridgeAuthoring`. `PrefabHolderAuthoring` по-прежнему печёт `PrefabsHolder` на ту же сущность: два бейкера на одном GameObject сходятся в один Entity.

Синглтон нельзя плодить в рантайме «на всякий случай»: `GetSingleton` падает, если сущностей две. GameObject-код только пишет/читает, если `CalculateEntityCount() == 1`.

---

## 6. Файлы и объекты сцены

| Файл | Роль |
|---|---|
| `BridgeComponents.cs` | Три unmanaged-компонента моста |
| `BridgeAuthoring.cs` | Бейкер синглтона в подсцене |
| `HerdEvents.cs` | Статический `Action OnFlockCleared` |
| `EntityWorldAccess.cs` | Безопасный доступ GO → `EntityManager` |
| `ShepherdInputController.cs` | Сценарий 1: ввод → `ShepherdInput` |
| `HerdHudView.cs` | Сценарии 2 и 3: HUD + подписка на `Action` |
| `HerdBridgeSystems.cs` | `ShepherdFollowSystem`, `HerdHudGatherSystem`, `FlockClearedSystem` |

Сцена:

- `SampleScene` / `PlayerArmature` — `ShepherdInputController`;
- `SampleScene` / `HerdHud` — `HerdHudView`;
- `HerdSubScene` / `PrefabHolder` — `BridgeAuthoring` рядом с `PrefabHolderAuthoring` и `Physics Step`.

Порядок систем кадра (добавленное к практикам 1–4):

`HerdGoalSyncSystem` → **`ShepherdFollowSystem`** → `MoveToTargetSystem` → **`HerdHudGatherSystem`** → **`FlockClearedSystem`**.

---

## 7. Как защищать на сдаче

1. **Два мира не смешиваются внутри Burst.** Ввод и UI — классы. Массовый поиск и движение овец — `ISystem` + Burst.
2. **Частое (каждый кадр) идёт через синглтон-компонент**, а не через `Action` из системы: переход unmanaged → managed на каждую овцу был бы дорогим.
3. **Редкое событие** вызывается из ECS в момент истины, система сразу выключается.
4. **Данные для UI считаются в Entity** и приходят в GameObject уже сжатыми до нескольких чисел.
5. Показать в Play Mode: овцы идут в загон; зажать `E` — разворачиваются к пастуху; в HUD меняются счётчик и HP ближайшей; когда все погибли о загон — строка «стадо рассеяно».
