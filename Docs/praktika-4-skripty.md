# Практика 4. Физика в DOTS (Unity Physics, CollisionWorld и события коллизий)

Изменения четвёртой практической работы в проекте (`Assets/Scripts/Herd`, пространство имён `Esc.Herd`):

- установлен пакет **`com.unity.physics` (`1.4.7`)** в `Packages/manifest.json`;
- настроены физические компоненты в подсцене `Assets/Scenes/HerdSubScene.unity` и префабе `Assets/Prefabs/Sheep.prefab`;
- переписаны системы движения в `HerdMovementSystems.cs` на использование **`CollisionWorld` (`OverlapSphere` и `CastRay`) внутри параллельных `IJobEntity`**, а также физического перемещения через **`PhysicsVelocity`** и стабилизации осей вращения через **`PhysicsMass.InverseInertia`**;
- добавлен новый файл `HerdPhysicsEventsSystem.cs` с системой обработки событий столкновений через **`ICollisionEventsJob`** и **`SimulationSingleton`**.

---

## 1. Тематика практической работы: как устроена физика в DOTS (`Unity Physics`)

Стандартная объектно-ориентированная физика Unity (`PhysX` на `GameObject`) хранит состояние в управляемых компонентах и вызывает методы `OnCollisionEnter` / `OnTriggerEnter` у классов `MonoBehaviour` на главном потоке. Для тысяч ECS-сущностей такой подход не подходит: он не компилируется через Burst и не масштабируется на рабочие потоки Job System.

Пакет **Unity Physics (`com.unity.physics`)** — это безсостоятельный (stateless) DOTS-движок физики, полностью написанный на C# поверх `IJob` и `Burst`:

1. **Запекание (Baking) стандартных компонентов в ECS-физику:**
   В редакторе на объекты подсцены и префабы по-прежнему вешаются привычные `Rigidbody` и `BoxCollider` / `CapsuleCollider` / `SphereCollider`. При запекании подсцены бейкеры Unity Physics автоматически превращают их в неуправляемые ECS-компоненты:
   - `PhysicsCollider` — хранит `BlobAssetReference<Collider>` с геометрией формы и фильтром слоёв `CollisionFilter`;
   - `PhysicsVelocity` — линейная (`Linear`) и угловая (`Angular`) скорости тела в мировом пространстве;
   - `PhysicsMass` — масса, центр масс и обратный тензор инерции (`InverseInertia`);
   - `PhysicsDamping` — коэффициенты затухания линейной и угловой скорости;
   - `PhysicsWorldIndex` — номер физического мира (по умолчанию `0`).
   Если на объекте есть только коллайдер без `Rigidbody` (как у пола `Pasture`, загона `Pen` и камня `Rock`), сущность запекается как **статическое физическое тело** (`PhysicsCollider` без `PhysicsVelocity` и `PhysicsMass`). Если есть и `Rigidbody`, и коллайдер (как у овец `Sheep`), сущность становится **динамическим физическим телом**.

2. **Конфигурация мира через синглтон `Physics Step`:**
   Чтобы физический мир в подсцене работал с нужными параметрами (тип симуляции `Unity Physics`, вектор гравитации `(0, -9.81, 0)`, многопоточный режим `Multi Threaded`), на объект-синглтон в подсцене (`PrefabHolder`) добавляется компонент `PhysicsStepAuthoring` (`Physics Step`).

3. **Два способа взаимодействия с физикой в системах:**
   - **Пространственные запросы к `CollisionWorld` внутри параллельных `IJobEntity`:**
     Через синглтон `PhysicsWorldSingleton` система берёт структуру `CollisionWorld` и передаёт её с атрибутом `[ReadOnly]` в параллельную джобу `IJobEntity`. Внутри `Execute` рабочие потоки параллельно вызывают `OverlapSphere`, `CastRay`, `BoxCast` и т.д., отбирая нужные объекты битовой маской `CollisionFilter` (`BelongsTo`, `CollidesWith`, `GroupIndex`).
   - **Реакция на случившиеся столкновения и триггеры через `ICollisionEventsJob` / `ITriggerEventsJob`:**
     Вместо `OnCollisionEnter` / `OnTriggerEnter` после шага симуляции физики (`[UpdateInGroup(typeof(PhysicsSystemGroup))]`, `[UpdateAfter(typeof(PhysicsSimulationGroup))]`) запускается специальная джоба `ICollisionEventsJob` или `ITriggerEventsJob`, принимающая `SimulationSingleton`. Для тех коллайдеров, чьи касания нужно отслеживать, в инспекторе включается галочка **`Provides Contacts`**.

---

## 2. Что изменено в сцене `HerdSubScene.unity` и префабе `Sheep.prefab`

1. **`PrefabHolder` (в `HerdSubScene.unity`):**
   - Добавлен компонент **`Physics Step` (`PhysicsStepAuthoring`)** — синглтон-конфигурация физического мира DOTS.
2. **`Pasture` (пол пастбища в `HerdSubScene.unity`):**
   - Добавлен статический `BoxCollider` (`center = (0, -0.5, 0)`, `size = (10, 1, 10)` при масштабе объекта `(4, 1, 4)`), создающий твёрдую поверхность `40 × 40` метров с верхней гранью ровно на высоте `y = 0`, чтобы динамические овцы с гравитацией стояли на земле.
3. **`Pen` (загон в `HerdSubScene.unity`):**
   - Объект переведён на **6-й слой (`Layer = 6`)**, поэтому его коллайдер при запекании получает битовую маску `BelongsTo = 1 << 6` (ровно как в шпаргалке темы 4 на стр. 3–5).
   - Добавлен `BoxCollider` (`center = (0, 3, 0)`, `size = (1, 8, 1)`, в мировом масштабе `3 × 1.2 × 3` м) с включённой опцией **`Provides Contacts = true`**, чтобы столкновения овец с загоном поступали в `ICollisionEventsJob`.
4. **`Rock` (камень в `HerdSubScene.unity`):**
   - Добавлен статический `BoxCollider` (`size = (1, 1, 1)`, `Provides Contacts = true`), чтобы камень был физическим препятствием на пастбище.
5. **`Assets/Prefabs/Sheep.prefab` и объекты `Sheep_0`…`Sheep_6` в подсцене:**
   - Добавлен `Rigidbody` (`Mass = 1`, `Use Gravity = true`, `Is Kinematic = false`).
   - Добавлен `BoxCollider` по габаритам меша овцы (`center = (0, 0.48, 0)`, `size = (0.37, 0.95, 1.24)`, `Provides Contacts = true`).

---

## 3. Разбор всех механизмов темы 4 в коде проекта

### 3.1. Запрет опрокидывания через `PhysicsMass.InverseInertia` (стр. 2 шпаргалки)

В классическом `Rigidbody` галочки `Freeze Rotation X / Z` не дают капсуле или коробке персонажа упасть на бок при движении по полу. При запекании в ECS-компоненты `Constraints` автоматически не переносятся.

Как показано на стр. 2 шпаргалки, ограничение физического поворота по осям X и Z в DOTS делается обнулением соответствующих компонент вектора `InverseInertia` в `PhysicsMass`:

```csharp
physicsMass.InverseInertia = new float3(0f, physicsMass.InverseInertia.y, 0f);
```

Когда `InverseInertia.x = 0` и `InverseInertia.z = 0`, физический солвер не может наклонить овцу вперёд-назад или на бок — она всегда остаётся строго вертикальной, сохраняя возможность поворачиваться вокруг вертикальной оси `Y`.

---

### 3.2. Физическое движение через `PhysicsVelocity.Linear` вместо записи в `LocalTransform.Position` (стр. 3 шпаргалки)

В практиках 1–3 шаг овцы перезаписывал координаты напрямую:

```csharp
// Старый нефизичный вариант:
transform.Position += direction * step;
```

Если двигать физическое тело прямой подменой `LocalTransform.Position`, оно будет телепортироваться сквозь коллайдеры и ломать расчёт контактов. Поэтому в `MoveToTargetJob` движение переведено на `PhysicsVelocity` (стр. 3 шпаргалки):

```csharp
physicsVelocity.Linear = new float3(direction.x * move.Speed, physicsVelocity.Linear.y, direction.z * move.Speed);
physicsVelocity.Angular = float3.zero;
transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
```

Обратите внимание: вертикальная составляющая `physicsVelocity.Linear.y` сохраняется от физического движка, чтобы гравитация и опора на пол `Pasture` продолжали работать естественно, а по горизонтали `(X, Z)` задаётся вектор скорости `direction * move.Speed`. Когда овца устала (`Exhausted`) или дошла до цели, горизонтальная скорость сбрасывается в ноль:

```csharp
physicsVelocity.Linear = new float3(0f, physicsVelocity.Linear.y, 0f);
physicsVelocity.Angular = float3.zero;
```

---

### 3.3. Фильтр слоёв `CollisionFilter`: `BelongsTo`, `CollidesWith`, `GroupIndex` (стр. 3 шпаргалки)

В Unity Physics фильтрация столкновений и пространственных запросов строится на структуре `CollisionFilter`:

```csharp
CollisionFilter filter = new CollisionFilter
{
    BelongsTo = 1u << 0,
    CollidesWith = 1u << 6,
    GroupIndex = 0
};
```

- **`BelongsTo`** — битовая маска слоёв, к которым принадлежит сам запрос или коллайдер (`1u << 0` — нулевой слой `Default`, на котором находятся овцы).
- **`CollidesWith`** — битовая маска слоёв, с которыми разрешено пересечение (`1u << 6` — 6-й слой, на котором стоит загон `Pen`). Благодаря этому запрос видит только загон и игнорирует пол `Pasture`, камень `Rock` и соседних овец.
- **`GroupIndex`** — индекс группы (`0` означает, что специальная группировка отключена; положительное совпадение форсирует столкновение, отрицательное совпадение запрещает).

---

### 3.4. Пункт 1 задания: `CollisionWorld.OverlapSphere` и `CollisionWorld.CastRay` внутри параллельных `IJobEntity` (стр. 4–5 шпаргалки)

В проекте использованы **оба** метода из шпаргалки внутри параллельных систем `IJobEntity`:

#### А) `CollisionWorld.OverlapSphere` в `HerdGoalSyncSystem` (`HerdGoalSyncJob : IJobEntity`)

Вместо чтения синглтона `HerdGoal` напрямую система получает `PhysicsWorldSingleton`, достаёт из него `CollisionWorld` и передаёт в параллельную джобу `HerdGoalSyncJob`:

```csharp
if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out PhysicsWorldSingleton physicsWorld))
    return;

CollisionWorld collisionWorld = physicsWorld.CollisionWorld;
ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

var job = new HerdGoalSyncJob
{
    CollisionWorld = collisionWorld,
    TransformLookup = transformLookup,
    SearchRadius = 60f
};
state.Dependency = job.ScheduleParallel(_sheepQuery, state.Dependency);
```

Внутри `HerdGoalSyncJob.Execute` (полный аналог `TowerAttackTargetSelectJob` со стр. 4 шпаргалки) каждая овца на своём рабочем потоке выделяет временный список попаданий `NativeList<DistanceHit>(Allocator.TempJob)`, вызывает `CollisionWorld.OverlapSphere` с фильтром на 6-й слой (`CollidesWith = 1u << 6`), выбирает ближайший объект по минимальному `hit.Distance`, записывает его позицию в `move.Position` и освобождает список `hits.Dispose()`:

```csharp
NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.TempJob);
CollisionFilter filter = new CollisionFilter
{
    BelongsTo = 1u << 0,
    CollidesWith = 1u << 6,
    GroupIndex = 0
};

if (CollisionWorld.OverlapSphere(transform.Position, SearchRadius, ref hits, filter))
{
    float minDist = float.PositiveInfinity;
    Entity foundEntity = Entity.Null;

    foreach (var hit in hits)
    {
        if (hit.Distance < minDist)
        {
            minDist = hit.Distance;
            foundEntity = hit.Entity;
        }
    }

    if (foundEntity != Entity.Null && TransformLookup.TryGetComponent(foundEntity, out LocalTransform goalTransform))
        move.Position = goalTransform.Position;
}

hits.Dispose();
```

#### Б) `CollisionWorld.CastRay` в `MoveToTargetSystem` (`MoveToTargetJob : IJobEntity`)

В `MoveToTargetJob.Execute` (по шаблону со стр. 5 шпаргалки) каждая овца дополнительно пускает луч `RaycastInput` от своей позиции по направлению движения к цели на 6-м слое (`CollisionWorld.CastRay(raycastInput, out RaycastHit hit)`) и уточняет вектор подхода к ближайшей точке поверхности коллайдера загона `hit.Position`:

```csharp
RaycastInput raycastInput = new RaycastInput
{
    Start = transform.Position + new float3(0f, 0.5f, 0f),
    End = transform.Position + new float3(0f, 0.5f, 0f) + direction * 50f,
    Filter = filter
};
if (CollisionWorld.CastRay(raycastInput, out RaycastHit hit))
{
    float3 toHit = hit.Position - transform.Position;
    toHit.y = 0f;
    if (math.lengthsq(toHit) > 0.0001f)
        direction = math.normalizesafe(toHit);
}
```

Поле `[ReadOnly] public CollisionWorld CollisionWorld` помечено атрибутом `[ReadOnly]`, так как все рабочие потоки только читают дерево коллайдеров и не меняют его.

---

### 3.5. Пункт 2 задания: `ICollisionEventsJob` и `SimulationSingleton` в `HerdPhysicsEventsSystem.cs` (стр. 5–7 шпаргалки)

В новом файле `Assets/Scripts/Herd/HerdPhysicsEventsSystem.cs` реализованы система `HerdPhysicsEventsSystem` и джоба `HerdCollisionEventJob : ICollisionEventsJob`:

```csharp
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[BurstCompile]
public partial struct HerdPhysicsEventsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<SimulationSingleton>(out SimulationSingleton simulation))
            return;

        var job = new HerdCollisionEventJob
        {
            GoalLookup = SystemAPI.GetComponentLookup<HerdGoal>(true),
            SheepLookup = SystemAPI.GetComponentLookup<SheepTag>(true),
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false)
        };

        state.Dependency = job.Schedule(simulation, state.Dependency);
    }
}
```

Разбор важных деталей для защиты:

1. **Группы обновления `[UpdateInGroup(typeof(PhysicsSystemGroup))]` и `[UpdateAfter(typeof(PhysicsSimulationGroup))]`:**
   События столкновений формируются внутри `PhysicsSimulationGroup` во время шага физики. Поэтому система, читающая эти события, обязана лежать в `PhysicsSystemGroup` и запускаться строго после `PhysicsSimulationGroup`.
2. **`SimulationSingleton` и `job.Schedule(simulation, state.Dependency)`:**
   В отличие от `IJobEntity` (где вызывается `ScheduleParallel`), `ICollisionEventsJob` — это специализированная однопоточная джоба физического пайплайна, которая запускается методом `job.Schedule(simulation, state.Dependency)`.
3. **Проверка `EntityA` и `EntityB` в обе стороны через `ComponentLookup`:**
   В `CollisionEvent` приходят два участника контакта — `collisionEvent.EntityA` и `collisionEvent.EntityB`. Порядок, кто из них окажется `EntityA`, а кто `EntityB`, заранее не гарантирован (стр. 7 шпаргалки). Поэтому проверка через `GoalLookup` и `SheepLookup` выполняется симметрично в обе стороны:
   - если `EntityA` — загон (`HerdGoal`), а `EntityB` — овца (`SheepTag`), меняем `Health` у `EntityB`;
   - если `EntityB` — загон (`HerdGoal`), а `EntityA` — овца (`SheepTag`), меняем `Health` у `EntityA`.
4. **Запись через `HealthLookup.TryGetRefRW` без гонки данных:**
   Поскольку `ICollisionEventsJob` выполняется последовательно в одном потоке, в ней разрешено использовать `ComponentLookup<Health>` с правами на запись (`isReadOnly: false`) и менять здоровье через `TryGetRefRW`.
5. **Связь со старыми системами (`HealthSystem` и `DeadSheepDestroySystem`):**
   Когда овца доходит до загона `Pen` и касается его коллайдера (у которого включён `Provides Contacts`), `HerdCollisionEventJob` вычитает у неё здоровье. Как только `CurrentHealth` падает до `<= 0`, срабатывает фильтр `[WithChangeFilter(typeof(Health))]` в `HealthSystem`, овца получает включённый компонент `Dead`, и `DeadSheepDestroySystem` удаляет дошедшую до загона овцу со сцены.
