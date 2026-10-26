# Практика 3. Параллельные вычисления (Jobs) и разбор изменений

Три изменённых файла в `Assets/Scripts/Herd` (`HerdVitalsSystems.cs`, `HerdMovementSystems.cs`, `FlockSpawnSystems.cs`), пространство имён `Esc.Herd`. Компоненты и authoring-скрипты первой и второй практик (`HerdComponents.cs`, `FlockComponents.cs`, `SheepAuthoring.cs`, `HerdGoalAuthoring.cs`, `PrefabHolderAuthoring.cs`, `WavesAuthoring.cs`) не менялись: данные сущностей и их запекание остались прежними, изменился способ их обработки в рантайме — с однопоточного обхода в главном потоке на многопоточные задачи (`IJobEntity` и `ScheduleParallel`).

Сцена та же: `Assets/Scenes/SampleScene.unity` и подсцена `Assets/Scenes/HerdSubScene.unity`.

---

## 1. Тематика практической работы: зачем нужны Jobs и многопоточность в ECS

По умолчанию пользовательский код в Unity выполняется на **одном главном потоке (Main Thread)** процессора. Даже если на `ISystem` висит атрибут `[BurstCompile]`, обычный цикл `foreach (var ... in SystemAPI.Query<...>())` внутри `OnUpdate` крутится последовательно на одном ядре: пока не обработается первая сущность, вторая ждёт, и так до последней.

У современных процессоров много ядер и рабочих потоков (Worker Threads). **C# Job System** в связке с **DOTS / Entities** позволяет разбить однотипную работу над множеством сущностей на пакеты (чанки) и раздать их свободным рабочим потокам параллельно:

1. **Когда параллельность нужна и даёт выигрыш:**
   Когда над множеством разных `Entity` выполняется **одна и та же независимая операция**, и расчёт одной сущности не ждёт результата другой. Например:
   - пересчитать шаг движения к цели для сотен или тысяч овец (`MoveToTargetSystem`);
   - записать координату загона в `MoveTarget` каждой живой овцы (`HerdGoalSyncSystem`);
   - восстановить запас стамины всем живым овцам (`StaminaSystem`);
   - проверить здоровье и пометить умерших компонентом `Dead` (`HealthSystem`), а затем удалить мёртвых (`DeadSheepDestroySystem`).

2. **Когда параллельность НЕ нужна (и почему не все системы переводятся на Jobs):**
   В шпаргалке к теме 3 прямо разобран антипример — **система спавна по таймеру** (`SheepWaveSpawnSystem`). Она раз в кадр прибавляет `DeltaTime` к одному таймеру волны и, когда время пришло, последовательно генерирует точки от одного генератора `Random` и создаёт сущности волны. Здесь нет тысяч независимых элементов, которые можно раздать разным ядрам: процесс строго последовательный (каждый вызов `rng.NextFloat` меняет состояние генератора для следующего шага). Поэтому `SheepWaveSpawnSystem` осознанно остаётся однопоточной.

---

## 2. Какие системы были изменены, а какие оставлены без изменений

| Система | Файл | Что сделано в Практике 3 | Почему |
|---|---|---|---|
| `HealthSystem` | `HerdVitalsSystems.cs` | Переписана на `HealthJob : IJobEntity` + `ScheduleParallel` + `EntityCommandBuffer.ParallelWriter` | Проверяет `Health <= 0` параллельно по чанкам с фильтром `[WithChangeFilter(typeof(Health))]` и включает `Dead` через параллельный писатель буфера команд |
| `StaminaSystem` | `HerdVitalsSystems.cs` | Переписана на `StaminaJob : IJobEntity` + `ScheduleParallel` | Независимо восстанавливает стамину каждой живой овце на рабочих потоках без блокировки главного потока |
| `HerdGoalSyncSystem` | `HerdMovementSystems.cs` | Переписана на `HerdGoalSyncJob : IJobEntity` + `ScheduleParallel` + `[ReadOnly] ComponentLookup<LocalTransform>` | Параллельно раздаёт позицию загона всем живым овцам, читая трансформ загона через потокобезопасный `ComponentLookup` только для чтения |
| `MoveToTargetSystem` | `HerdMovementSystems.cs` | Переписана на `MoveToTargetJob : IJobEntity` + `ScheduleParallel` | Самая тяжёлая математика кадра (векторы, длина, нормализация, поворот `LookRotationSafe`, расход стамины) теперь считается параллельно на всех потоках |
| `DeadSheepDestroySystem` | `FlockSpawnSystems.cs` | Переписана на `DeadSheepDestroyJob : IJobEntity` + `ScheduleParallel` + `EntityCommandBuffer.ParallelWriter` + добавлен `[BurstCompile]` | Параллельно собирает все сущности с включённым `Dead` и ставит их удаление в очередь через `ParallelWriter` |
| `SheepWaveSpawnSystem` | `FlockSpawnSystems.cs` | Оставлена последовательной в `OnUpdate`, добавлен `state.Dependency.Complete()` перед чтением `ComponentLookup` и `Playback` | Таймер волны и один общий `Unity.Mathematics.Random` последовательны по своей природе (как указано в шпаргалке темы 3) |

---

## 3. Ключевые механизмы темы 3, применённые в проекте

### 3.1. `IJobEntity` и метод `Execute` вместо `foreach` по `SystemAPI.Query`

В первой и второй практиках система сама обходила сущности циклом:

```csharp
foreach (var stamina in SystemAPI.Query<RefRW<Stamina>>().WithDisabled<Dead>())
{
    ...
}
```

В третьей практике логика одного шага цикла выносится в отдельную структуру-джобу с интерфейсом `IJobEntity`:

```csharp
[BurstCompile]
public partial struct StaminaJob : IJobEntity
{
    public float DeltaTime;

    [BurstCompile]
    public void Execute(ref Stamina stamina)
    {
        float restored = math.min(stamina.Max, stamina.Current + stamina.RegenPerSecond * DeltaTime);
        stamina.Current = restored;

        if (stamina.Exhausted && restored >= stamina.Max * 0.5f)
            stamina.Exhausted = false;
    }
}
```

Что изменилось в записи:

- **Нет `RefRO<T>` и `RefRW<T>`.** В параметрах метода `Execute` компоненты передаются напрямую со стандартными модификаторами C#:
  - `ref T` — чтение и запись (аналог `RefRW<T>`, без `.ValueRO` и `.ValueRW`);
  - `in T` — только чтение по константной ссылке (аналог `RefRO<T>`).
- **Нет `.WithEntityAccess()`.** Если внутри `Execute` нужен сам `Entity` (например, в `HealthJob` и `DeadSheepDestroyJob`), он просто добавляется параметром `Entity entity` в сигнатуру `Execute`.
- **`return` вместо `continue`.** В цикле `foreach` переход к следующей овце писался через `continue`. В `Execute` один вызов метода обрабатывает ровно одну сущность, поэтому досрочный выход из обработки этой сущности — это обычный `return` (см. `MoveToTargetJob`).
- **`partial struct` обязателен и для джобы.** Генератор кода Unity Entities на этапе компиляции дописывает во вторую часть структуры `IJobEntity` скрытую обвязку обхода чанков.

---

### 3.2. `state.Dependency` и `job.ScheduleParallel(...)`

Сама структура джобы ничего не выполняет, пока система её не создаст и не поставит в очередь планировщика (Job Scheduler):

```csharp
var job = new StaminaJob
{
    DeltaTime = SystemAPI.Time.DeltaTime
};
state.Dependency = job.ScheduleParallel(_staminaQuery, state.Dependency);
```

Как работает эта строка:

1. `job.ScheduleParallel(...)` **не выполняет `Execute` мгновенно** в этой точке кода. Она лишь ставит задачу в общую многопоточную очередь планировщика работ, а главный поток сразу идёт дальше.
2. `state.Dependency` — это поле системы типа `JobHandle`, в котором хранится цепочка зависимостей: какие джобы из предыдущих систем читают или пишут те же компоненты и должны завершиться перед стартом новой джобы.
3. Передавая `state.Dependency` аргументом в `ScheduleParallel(..., state.Dependency)`, мы гарантируем, что новая джоба не стартует раньше завершения предыдущих зависимых джобов.
4. Присваивая результат обратно в `state.Dependency = ...`, мы сообщаем следующим системам в кадре (например, `MoveToTargetSystem`), что `StaminaJob` встала в очередь и меняет `Stamina`, поэтому `MoveToTargetJob` автоматически дождётся её окончания.

---

### 3.3. Настройка фильтрации через `EntityQuery` и `SystemAPI.QueryBuilder()`

Параметры метода `Execute` говорят генератору только то, какие компоненты нужно читать и писать (`WithAll`), но не умеют сами по себе выразить фильтр «компонент `Dead` обязан быть выключен» (`.WithDisabled<Dead>()`).

Как показано в шпаргалке темы 3 (стр. 4), для тонкой фильтрации (`WithDisabled`, `WithNone`, `WithAll` для тегов, которые не нужны внутри `Execute`) в системе объявляется поле `EntityQuery`, которое один раз собирается в `OnCreate` через `SystemAPI.QueryBuilder()`:

```csharp
private EntityQuery _moveQuery;

[BurstCompile]
public void OnCreate(ref SystemState state)
{
    _moveQuery = SystemAPI.QueryBuilder()
        .WithAll<LocalTransform, MoveTarget, Stamina, SheepTag>()
        .WithDisabled<Dead>()
        .Build();
}
```

Затем этот `_moveQuery` передаётся первым аргументом в `job.ScheduleParallel(_moveQuery, state.Dependency)`.

Почему в `_moveQuery` перечислены не только `SheepTag`, но и `LocalTransform, MoveTarget, Stamina`? Когда в `ScheduleParallel` передаётся явный `EntityQuery`, он должен включать все компоненты, с которыми работает `Execute`, плюс дополнительные фильтры (`SheepTag`, `.WithDisabled<Dead>()`).

---

### 3.4. Атрибут `[WithChangeFilter(typeof(Health))]` над джобой

В первой практике фильтр изменений писался цепочкой в запросе: `.WithChangeFilter<Health>()`. В `IJobEntity` его прямой эквивалент из шпаргалки (стр. 4–5) — атрибут над структурой джобы:

```csharp
[BurstCompile]
[WithChangeFilter(typeof(Health))]
public partial struct HealthJob : IJobEntity
```

Планировщик пропускает целые чанки сущностей, в которых компонент `Health` не менялся с прошлого запуска `HealthSystem`. На первом кадре после запекания все чанки считаются изменёнными, поэтому `Sheep_0` с `Health = 0` сразу попадает в `HealthJob`.

---

### 3.5. Параллельная запись в буфер команд: `EntityCommandBuffer.ParallelWriter`, `[ChunkIndexInQuery]` и `state.Dependency.Complete()`

Обычный `EntityCommandBuffer` нельзя вызывать из нескольких потоков одновременно: если два потока одновременно начнут дописывать команды в один список без синхронизации, память буфера повредится, а порядок команд от запуска к запуску станет случайным.

Поэтому в `HealthSystem` и `DeadSheepDestroySystem` применён паттерн из шпаргалки (стр. 5–6):

1. Буфер создаётся с аллокатором для джобов `Allocator.TempJob`:
   ```csharp
   EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
   ```
2. В джобу передаётся не сам буфер, а его потокобезопасная обёртка `EntityCommandBuffer.ParallelWriter`, полученная вызовом `ecb.AsParallelWriter()`.
3. В сигнатуру `Execute` добавляется параметр `[ChunkIndexInQuery] int chunkIndexInQuery`:
   ```csharp
   [BurstCompile]
   public void Execute([ChunkIndexInQuery] int chunkIndexInQuery, Entity entity, in Health health)
   {
       if (health.CurrentHealth <= 0f)
           Ecb.SetComponentEnabled<Dead>(chunkIndexInQuery, entity, true);
   }
   ```
   `chunkIndexInQuery` (он же ключ сортировки `sortKey`) — порядковый номер чанка в запросе. Каждый рабочий поток пишет команды со своим номером чанка, а при `Playback` Unity сортирует записанные команды по этому индексу, чтобы результат параллельной записи был детерминированным.
4. **Обязательное ожидание перед `Playback`:**
   Поскольку `ScheduleParallel` только ставит джобу в очередь, строка после `ScheduleParallel` на главном потоке выполняется тогда, когда рабочие потоки ещё крутят `Execute`. Вызвать `ecb.Playback` в этот момент нельзя — Unity запретит воспроизводить буфер, в который ещё пишет джоба. Поэтому перед `Playback` вызывается принудительное ожидание завершения:
   ```csharp
   state.Dependency = job.ScheduleParallel(_healthQuery, state.Dependency);

   state.Dependency.Complete();
   ecb.Playback(state.EntityManager);
   ecb.Dispose();
   ```

---

### 3.6. `[ReadOnly] ComponentLookup<LocalTransform>` в `HerdGoalSyncSystem`

В `HerdGoalSyncJob` каждая овца должна узнать позицию другой сущности — загона (`GoalEntity`). Для чтения компонента произвольной сущности по её `Entity` используется `ComponentLookup<LocalTransform>`.

В параллельных джобах действуют строгие правила безопасности памяти (Safety System):
- Несколько потоков не имеют права произвольно писать в чужие сущности через `ComponentLookup`, иначе возникнет гонка данных (Data Race).
- Читать один и тот же компонент из разных потоков одновременно безопасно, но мы обязаны явно пообещать это системе безопасности Unity в двух местах (шпаргалка, стр. 6–7):
  1. При создании словаря в системе передать `true` (`isReadOnly: true`):
     ```csharp
     ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
     ```
  2. На поле внутри структуры `IJobEntity` поставить атрибут `[ReadOnly]` из `Unity.Collections`:
     ```csharp
     [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
     ```

---

## 4. Подробный разбор всех трёх изменённых файлов

### 4.1. `HerdVitalsSystems.cs`

В файле две системы и две структуры джобов:

1. **`HealthSystem` + `HealthJob`**:
   - В `OnCreate` собирает `_healthQuery` (`WithAll<Health>()`, `WithDisabled<Dead>()`).
   - В `OnUpdate` создаёт `EntityCommandBuffer(Allocator.TempJob)`, передаёт `ecb.AsParallelWriter()` в `HealthJob`, планирует её через `ScheduleParallel(_healthQuery, state.Dependency)`, дожидается завершения через `state.Dependency.Complete()`, выполняет `ecb.Playback(state.EntityManager)` и освобождает память `ecb.Dispose()`.
   - На `HealthJob` стоит `[WithChangeFilter(typeof(Health))]`. Внутри `Execute` при `health.CurrentHealth <= 0f` вызывается `Ecb.SetComponentEnabled<Dead>(chunkIndexInQuery, entity, true)`.

2. **`StaminaSystem` + `StaminaJob`**:
   - Идёт после `HealthSystem` и до `MoveToTargetSystem`.
   - В `OnCreate` собирает `_staminaQuery` (`WithAll<Stamina>()`, `WithDisabled<Dead>()`).
   - В `OnUpdate` передаёт `DeltaTime` в `StaminaJob` и планирует её:
     ```csharp
     state.Dependency = job.ScheduleParallel(_staminaQuery, state.Dependency);
     ```
   - Здесь нет `EntityCommandBuffer` и нет `state.Dependency.Complete()`: джоба выполняется полностью асинхронно на рабочих потоках, а следующая система, которой понадобится `Stamina` (`MoveToTargetSystem`), подождёт её автоматически через `state.Dependency`.

---

### 4.2. `HerdMovementSystems.cs`

В файле две системы движения и две структуры джобов:

1. **`HerdGoalSyncSystem` + `HerdGoalSyncJob`**:
   - В `OnCreate` строит `_sheepQuery` (`WithAll<MoveTarget, SheepTag>()`, `WithDisabled<Dead>()`).
   - В `OnUpdate` ищет синглтон `HerdGoal` через `SystemAPI.TryGetSingletonEntity<HerdGoal>(out Entity goalEntity)`, берёт `ComponentLookup<LocalTransform>(true)` и планирует `HerdGoalSyncJob`.
   - В `HerdGoalSyncJob` поле `[ReadOnly] public ComponentLookup<LocalTransform> TransformLookup` и `public Entity GoalEntity` позволяют каждому потоку прочитать трансформ загона и записать `goalTransform.Position` в `ref MoveTarget move` текущей овцы.

2. **`MoveToTargetSystem` + `MoveToTargetJob`**:
   - Стоит `[UpdateAfter(typeof(HerdGoalSyncSystem))]`.
   - В `OnCreate` строит `_moveQuery` (`WithAll<LocalTransform, MoveTarget, Stamina, SheepTag>()`, `WithDisabled<Dead>()`).
   - В `OnUpdate` передаёт `DeltaTime` в `MoveToTargetJob` и вызывает `state.Dependency = job.ScheduleParallel(_moveQuery, state.Dependency)`.
   - `MoveToTargetJob.Execute(ref LocalTransform transform, in MoveTarget move, ref Stamina stamina)` выполняет всю векторную математику шага, поворота и расхода стамины для каждой овцы параллельно на рабочих потоках с компиляцией Burst.

---

### 4.3. `FlockSpawnSystems.cs`

В файле две системы:

1. **`SheepWaveSpawnSystem`** (однопоточная):
   - Оставлена последовательной в точном соответствии со шпаргалкой темы 3 (стр. 1): отсчёт таймера волны и последовательная генерация случайных точек для спавна префабов не требуют и не допускают распараллеливания.
   - Перед чтением `ComponentLookup<LocalTransform>` и воспроизведением `ecb.Playback(state.EntityManager)` добавлен вызов `state.Dependency.Complete()`, чтобы гарантировать, что ни одна параллельная джоба из других систем не держит `LocalTransform` в момент спавна новых овец.

2. **`DeadSheepDestroySystem` + `DeadSheepDestroyJob`** (параллельная):
   - Теперь помечена `[BurstCompile]`.
   - В `OnCreate` строит `_deadSheepQuery` (`WithAll<SheepTag, Dead>()`). Для `IEnableableComponent` фильтр `WithAll<Dead>()` отбирает только те сущности, у которых компонент `Dead` включён.
   - В `OnUpdate` создаёт `EntityCommandBuffer(Allocator.TempJob)`, планирует `DeadSheepDestroyJob` с `Ecb = ecb.AsParallelWriter()`, вызывает `state.Dependency.Complete()`, `ecb.Playback(state.EntityManager)` и `ecb.Dispose()`.
   - В `DeadSheepDestroyJob.Execute([ChunkIndexInQuery] int chunkIndexInQuery, Entity entity)` вызывается `Ecb.DestroyEntity(chunkIndexInQuery, entity)`.

---

## 5. Как проходит один кадр после перехода на Jobs

1. **`HealthSystem`** запускает `HealthJob` по чанкам с изменившимся `Health`, дожидается её (`Complete`) и через `Playback` включает `Dead` у тех, чьё здоровье `<= 0` (на первом кадре — у `Sheep_0`).
2. **`DeadSheepDestroySystem`** (`[UpdateAfter(typeof(HealthSystem))]`) параллельно в `DeadSheepDestroyJob` собирает всех овец с включённым `Dead`, дожидается завершения и удаляет их из мира через `Playback`.
3. **`StaminaSystem`** (`[UpdateAfter(typeof(HealthSystem))]`, `[UpdateBefore(typeof(MoveToTargetSystem))]`) ставит в очередь `StaminaJob` (`ScheduleParallel`), которая на рабочих потоках восстанавливает стамину живым овцам.
4. **`SheepWaveSpawnSystem`** (`[UpdateBefore(typeof(HerdGoalSyncSystem))]`) проверяет таймер волны; когда пора спавнить волну (на 0-й секунде — 12 овец, на 4-й секунде — ещё 8 овец), создаёт их через `EntityCommandBuffer`.
5. **`HerdGoalSyncSystem`** ставит в очередь `HerdGoalSyncJob` (`ScheduleParallel`), которая параллельно через `[ReadOnly] ComponentLookup<LocalTransform>` копирует позицию загона в `MoveTarget` всех живых овец.
6. **`MoveToTargetSystem`** (`[UpdateAfter(typeof(HerdGoalSyncSystem))]`) ставит в очередь `MoveToTargetJob` (`ScheduleParallel`), которая дожидается готовности `StaminaJob` и `HerdGoalSyncJob` по цепочке `state.Dependency` и параллельно на всех потоках двигает и поворачивает овец к загону, списывая стамину.
