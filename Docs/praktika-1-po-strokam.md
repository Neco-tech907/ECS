# Пять скриптов первой практики, по строкам

Ниже полный текст каждого файла. Справа от строки комментарий `//` говорит, что эта строка делает. Сами скрипты в `Assets` не менялись: комментарии только в этом разборе.

Порядок такой же, как на списке для защиты:

1. [HerdComponents.cs](Assets/Scripts/Herd/HerdComponents.cs)
2. [SheepAuthoring.cs](Assets/Scripts/Herd/SheepAuthoring.cs)
3. [HerdGoalAuthoring.cs](Assets/Scripts/Herd/HerdGoalAuthoring.cs)
4. [HerdMovementSystems.cs](Assets/Scripts/Herd/HerdMovementSystems.cs)
5. [HerdVitalsSystems.cs](Assets/Scripts/Herd/HerdVitalsSystems.cs)

## HerdComponents.cs

```csharp
using Unity.Entities; // Пакет ECS. Отсюда IComponentData, IEnableableComponent, IBufferElementData, Entity, BlobArray и BlobAssetReference.
using Unity.Mathematics; // Типы координат float2 и float3. Их можно читать внутри Burst-систем, в отличие от Vector3.
using Random = Unity.Mathematics.Random; // Имя Random в этом файле означает генератор из Mathematics. Так оно не сталкивается с UnityEngine.Random.

namespace Esc.Herd // Все структуры ниже лежат в пространстве имён Esc.Herd. Системы в той же папке видят их без приставки.
{ // Начало пространства имён.
    public struct SheepTag : IComponentData // Публичная структура-компонент. IComponentData разрешает повесить её на сущность. Полей нет: это метка «это овца».
    { // Тело метки пустое.
    } // Конец SheepTag.

    public struct MoveTarget : IComponentData // Компонент цели движения. Система читает его и сдвигает сущность к Position.
    { // Начало полей цели.
        public float3 Position; // Точка, к которой идёт сущность: x, y, z. Для овцы сюда каждый кадр записывается позиция загона.
        public float Speed; // Скорость в метрах в секунду.
        public float StopDistance; // На каком расстоянии до цели шаг прекращается. Ближе этого сущность стоит.
    } // Конец MoveTarget.

    // HerdGoal — пустая метка единственного загона. Координата загона лежит в LocalTransform той же сущности, а не в этом компоненте.
    public struct HerdGoal : IComponentData // Компонент-метка. По запросу HerdGoal система находит одну сущность загона.
    { // Тело метки пустое.
    } // Конец HerdGoal.

    public struct Health : IComponentData // Компонент здоровья.
    { // Начало полей здоровья.
        public float CurrentHealth; // Сколько здоровья сейчас. Система смерти смотрит, не стало ли это значение меньше или равно нулю.
        public float MaxHealth; // Верхняя граница здоровья.
    } // Конец Health.

    public struct Dead : IComponentData, IEnableableComponent // Метка «мертва». IEnableableComponent позволяет включить и выключить её, не добавляя и не снимая компонент.
    { // Тело пустое: важен сам факт, включена метка или нет.
    } // Конец Dead. Выключенный Dead означает «жива».

    public struct Stamina : IComponentData // Компонент запаса сил. От него зависит, делает ли овца шаг в этом кадре.
    { // Начало полей стамины.
        public float Current; // Сколько стамины осталось сейчас.
        public float Max; // Потолок, выше которого восстановление не поднимает запас.
        public float RegenPerSecond; // Сколько единиц прибавляется за одну секунду, пока сущность жива.
        public float DrainPerSecond; // Сколько единиц вычитается за одну секунду, пока сущность идёт.
        public bool Exhausted; // true — сущность истощена и шаг ей запрещён, пока запас не вернётся хотя бы до половины максимума.
    } // Конец Stamina.

    public enum WolfBehavior : byte // Перечисление состояния волка. byte хранит его в одном байте.
    { // Начало значений.
        Chase = 0, // Волк идёт к добыче.
        Attack = 1, // Волк в радиусе атаки и кусает.
        Dead = 2 // Волк мёртв и лежит до удаления.
    } // Конец перечисления.

    public struct WolfTag : IComponentData // Пустая метка «эта сущность — волк». Запросы волков фильтруются по ней.
    { // Тело пустое.
    } // Конец WolfTag.

    public struct WolfState : IComponentData // Текущее поведение одного волка и числа его атаки.
    { // Начало полей поведения.
        public WolfBehavior Behavior; // Какое из трёх состояний сейчас активно.
        public float AttackRange; // Дистанция, с которой волк переходит в атаку.
        public float AttackDamage; // Сколько здоровья снимается овце за один укус.
        public float AttackInterval; // Пауза между укусами в секундах.
        public float AttackCooldown; // Сколько секунд осталось до следующего укуса.
        public float DamageTakenPerAttack; // Поле урона по самому волку за укус. Текущая система укуса его не вычитает.
        public float CorpseTime; // Сколько секунд труп ещё лежит, прежде чем его удалят.
        public Entity Prey; // Ссылка на сущность-добычу, ближайшую живую овцу вне загона.
    } // Конец WolfState.

    public struct WolfPath : IComponentData // Точка, к которой волк идёт на этом отрезке пути.
    { // Начало полей пути.
        public float3 Waypoint; // Ближайшая промежуточная точка. Если камень не мешает, сюда пишется сама добыча.
        public float Speed; // Скорость волка в метрах в секунду.
        public float RepathTimer; // Через сколько секунд путь можно пересчитать. Ноль и меньше разрешают новый расчёт.
    } // Конец WolfPath.

    public struct PastureObstacle : IComponentData // Препятствие на пастбище, серый камень. Путь волка обходит его.
    { // Начало полей препятствия.
        public float2 CenterXZ; // Центр камня на плоскости земли: x и z, без высоты.
        public float2 HalfExtents; // Половина ширины и половины глубины камня. Прямоугольник препятствия строится от центра на эти величины.
    } // Конец PastureObstacle.

    public enum EnemyType : byte // Тип врага в статической таблице волн. Сейчас в ней один вариант.
    { // Начало значений.
        Wolf = 0 // Группа волны состоит из волков. По этому значению спавн берёт префаб волка.
    } // Конец EnemyType.

    public struct WavesBlob // Корень неизменяемой таблицы волн. Это не компонент: так уложены данные внутри Blob.
    { // Начало корня.
        public BlobArray<Wave> Waves; // Массив волн фиксированной длины. Длину задают один раз при запекании.
    } // Конец WavesBlob.

    public struct Wave // Одна волна внутри Blob.
    { // Начало описания волны.
        public float WaitTimeBeforeWave; // Сколько секунд ждать перед этой волной. У первой волны в сцене это 0.4, дальше 8.
        public BlobArray<EnemyInWave> Enemies; // Вложенный массив групп врагов этой волны. У нас в каждой волне одна группа.
    } // Конец Wave.

    public struct EnemyInWave // Одна группа врагов внутри волны.
    { // Начало группы.
        public EnemyType EnemyType; // Кого спавнить, сейчас Wolf.
        public int MinAmount; // Минимальное число существ в группе. Генератор случайных чисел берёт не меньше этого.
        public int MaxAmount; // Максимальное число. Верхняя граница включается при вызове NextInt.
    } // Конец EnemyInWave.

    public struct WavesHolder : IComponentData // Компонент-держатель таблицы. На сущности хранится только ссылка, сами волны лежат в Blob.
    { // Начало держателя.
        public BlobAssetReference<WavesBlob> WavesReference; // Ссылка на запечённую таблицу. Через .Value система читает Waves.
    } // Конец WavesHolder.

    public struct WaveSpawner : IComponentData // Живое состояние спавнера. Оно меняется каждый кадр, поэтому это обычный компонент, а не Blob.
    { // Начало состояния спавнера.
        public float TimeUntilWave; // Сколько секунд осталось до следующей волны.
        public int NextWave; // Индекс волны в Blob, которую спавнить следующей. После последней рост индекса останавливает спавн.
        public Random Random; // Генератор этой сущности. От него зависят размер волны и точка появления волка.
    } // Конец WaveSpawner.

    public struct WaveGate : IComponentData // Выключатель волн. Пока Open равен false, спавн волков сразу выходит.
    { // Начало выключателя.
        public bool Open; // true после того, как пастух вошёл в площадку-триггер.
    } // Конец WaveGate.

    public struct PrefabsHolder : IComponentData // Единственное хранилище запечённых префабов. Спавн читает его через GetSingleton.
    { // Начало хранилища.
        public Entity SheepPrefab; // Запечённый префаб овцы. Instantiate копирует эту сущность.
        public Entity WolfPrefab; // Запечённый префаб волка.
    } // Конец PrefabsHolder.

    [InternalBufferCapacity(8)] // До восьми элементов списка лежат прямо в чанке сущности. Девятый и дальше уходят в отдельную память.
    public struct StoneImpact : IBufferElementData // Один элемент динамического списка попаданий камня. На сущности таких элементов несколько.
    { // Начало элемента.
        public float3 Position; // Точка удара в мире.
        public float Damage; // Сколько здоровья снять.
        public float Radius; // Волк получает урон, если по земле он не дальше этого радиуса от точки.
    } // Конец StoneImpact.

    public struct InPen : IComponentData, IEnableableComponent // Метка «овца уже в загоне». Её включают, а не добавляют заново.
    { // Тело пустое.
    } // Конец InPen. Выключенная метка значит «ещё снаружи». Движение и выбор добычи такую овцу пропускают, когда метка включена.

    public struct PenZone : IComponentData // Размер круга загона. Висит на той же сущности, что и HerdGoal.
    { // Начало зоны.
        public float Radius; // Радиус в метрах. Овца внутри этого круга по земле получает включённый InPen. В сцене стоит 2.4.
    } // Конец PenZone.

    public struct Shepherd : IComponentData // Данные пастуха, которые обычный объект игрока записывает в ECS каждый кадр.
    { // Начало данных пастуха.
        public float3 Position; // Где пастух стоит сейчас.
        public bool Calling; // true — свисток включён, и овцы вне загона получают целью эту позицию вместо загона.
    } // Конец Shepherd.

    public struct SheepFlockSpawner : IComponentData // Настройки разового создания стада. Префаб овцы здесь не хранится: он лежит в PrefabsHolder.
    { // Начало настроек.
        public int Count; // Сколько овец создать. В сцене это 1000.
        public float2 AreaMin; // Ближний угол прямоугольника спавна на земле: x и z.
        public float2 AreaMax; // Дальний угол того же прямоугольника.
        public Random Random; // Генератор точек внутри прямоугольника. Сид задаётся при запекании.
        public bool Spawned; // true после первого создания. Система больше не проходит цикл спавна.
    } // Конец SheepFlockSpawner.

    public struct CrowdAnim : IComponentData // Состояние смены запечённых мешей у одной овцы или одного волка.
    { // Начало состояния анимации.
        public float Time; // Секунды, прошедшие с начала текущего ролика.
        public float Fps; // Сколько кадров ролика проигрывается за секунду.
        public float3 PreviousPosition; // Позиция в прошлом кадре. По сдвигу система выбирает шаг вместо покоя.
        public int IdleStart; // Индекс первого кадра покоя в общем списке мешей.
        public int IdleCount; // Сколько кадров в ролике покоя.
        public int MoveStart; // Индекс первого кадра шага или бега.
        public int MoveCount; // Длина ролика движения.
        public int ActStart; // Индекс первого кадра укуса. У овцы этот диапазон пустой.
        public int ActCount; // Длина ролика действия.
        public int DeathStart; // Индекс первого кадра смерти.
        public int DeathCount; // Длина ролика смерти.
        public byte Species; // 0 — овца, 1 — волк. От этого зависит, какой материал подставлять.
        public byte Clip; // Какой диапазон выбран сейчас: покой, движение, действие или смерть.
        public int MaterialId; // Номер материала в библиотеке отрисовки.
    } // Конец CrowdAnim.

    [InternalBufferCapacity(48)] // До 48 номеров мешей хранятся прямо на сущности библиотеки.
    public struct CrowdMeshIdElement : IBufferElementData // Один номер уже зарегистрированного меша.
    { // Начало элемента.
        public int Value; // Идентификатор меша, который отрисовка понимает как кадр.
    } // Конец CrowdMeshIdElement.

    public struct CrowdLibraryTag : IComponentData // Метка сущности, на которой собрана библиотека мешей и материалов.
    { // Начало метки библиотеки.
        public int SheepMaterialId; // Номер материала овец в системе отрисовки.
        public int WolfMaterialId; // Номер материала волков.
        public bool Ready; // true после того, как меши и материалы зарегистрированы и список номеров заполнен.
    } // Конец CrowdLibraryTag.
} // Конец пространства имён Esc.Herd.
```

## SheepAuthoring.cs

```csharp
using Unity.Entities; // Baker, Entity, AddComponent, GetEntity, SetComponentEnabled.
using Unity.Mathematics; // Функция math.max для потолка здоровья и стамины.
using UnityEngine; // MonoBehaviour: этот класс живёт на объекте сцены и виден в инспекторе.

namespace Esc.Herd // Тот же namespace, что у компонентов, поэтому SheepTag и остальные типы доступны по короткому имени.
{ // Начало namespace.
    public class SheepAuthoring : MonoBehaviour // Обычный компонент Unity на объекте овцы. Сам он в игре не двигает овцу: из него Baker делает сущность.
    { // Начало класса, поля ниже сериализуются и видны в инспекторе.
        public float Speed = 3.5f; // Скорость по умолчанию, 3.5 метра в секунду. Буква f задаёт тип float.
        public float StopDistance = 1.5f; // Дистанция остановки по умолчанию. На префабе стада в сцене она переопределена на 1.2.
        public float Health = 100f; // Здоровье в момент запекания. У Sheep_0 в подсцене сюда записан 0, поэтому она сразу мёртвая.
        public float MaxHealth = 100f; // Максимум здоровья по умолчанию.
        public float Stamina = 3f; // Начальный запас сил. У семи овец подсцены остаётся 3, у префаба стада в инспекторе стоит 20.
        public float MaxStamina = 3f; // Потолок стамины по умолчанию.
        public float StaminaRegenPerSecond = 1f; // Восстановление: одна единица в секунду.
        public float StaminaDrainPerSecond = 2f; // Расход на ходу: две единицы в секунду. На префабе стада расход снижен до 0.4, поэтому тысяча овец успевает дойти.

        class SheepBaker : Baker<SheepAuthoring> // Вложенный запекатель. Unity вызывает его для каждого объекта с SheepAuthoring, когда подсцена превращается в сущности.
        { // Начало baker.
            public override void Bake(SheepAuthoring authoring) // Вызывается один раз при запекании. authoring — тот объект, чьи поля из инспектора сейчас читаются.
            { // Начало переноса данных в сущность.
                Entity entity = GetEntity(TransformUsageFlags.Dynamic); // Сущность этого объекта. Dynamic значит: позиция будет меняться в игре, поэтому сущности выдают LocalTransform.
                AddComponent<SheepTag>(entity); // Вешает пустую метку овцы. Движение и здоровье фильтруют запросы по ней.
                AddComponent(entity, new MoveTarget // Вешает компонент цели и сразу заполняет поля.
                { // Начало инициализатора MoveTarget.
                    Position = authoring.transform.position, // Стартовая цель — место самой овцы. В первом кадре синхронизация заменит её на позицию загона.
                    Speed = authoring.Speed, // Копирует скорость из инспектора.
                    StopDistance = authoring.StopDistance // Копирует дистанцию остановки.
                }); // Конец MoveTarget и вызова AddComponent.
                AddComponent(entity, new Health // Вешает здоровье.
                { // Начало инициализатора Health.
                    CurrentHealth = authoring.Health, // Текущее здоровье из инспектора.
                    MaxHealth = math.max(authoring.MaxHealth, authoring.Health) // Потолок не ниже текущего. Если в инспекторе максимум случайно меньше здоровья, берётся здоровье.
                }); // Конец Health.
                AddComponent<Dead>(entity); // Вешает метку смерти. Наличие метки нужно, чтобы её потом можно было включить.
                SetComponentEnabled<Dead>(entity, false); // Сразу выключает её: запечённая овца жива. Исключение по данным — Sheep_0, у неё здоровье 0, и в игре система включит Dead.
                AddComponent<InPen>(entity); // Вешает метку «в загоне», тоже выключенной.
                SetComponentEnabled<InPen>(entity, false); // Овца начинает снаружи загона и поэтому попадает в запрос движения.
                AddComponent(entity, new Stamina // Вешает стамину.
                { // Начало инициализатора Stamina.
                    Current = authoring.Stamina, // Начальный запас из инспектора.
                    Max = math.max(authoring.MaxStamina, 0.01f), // Потолок хотя бы 0.01, чтобы у восстановления был ненулевой предел и половина максимума не равнялась нулю.
                    RegenPerSecond = authoring.StaminaRegenPerSecond, // Скорость восстановления.
                    DrainPerSecond = authoring.StaminaDrainPerSecond // Скорость расхода. Поле Exhausted не записано и остаётся false: овца начинает неуставшей.
                }); // Конец Stamina.
            } // Конец Bake.
        } // Конец класса SheepBaker.
    } // Конец класса SheepAuthoring.
} // Конец namespace.
```

## HerdGoalAuthoring.cs

```csharp
using Unity.Entities; // Baker, Entity, AddComponent, GetEntity.
using UnityEngine; // MonoBehaviour для объекта загона в подсцене.

namespace Esc.Herd // Общий namespace с компонентами HerdGoal и PenZone.
{ // Начало namespace.
    public class HerdGoalAuthoring : MonoBehaviour // Висит на цилиндре загона. В игре логику не выполняет: только отдаёт радиус запекателю.
    { // Начало класса.
        public float Radius = 2.4f; // Радиус круга загона в метрах. Это значение по умолчанию, в инспекторе подсцены тоже стоит 2.4.

        class HerdGoalBaker : Baker<HerdGoalAuthoring> // Запекатель объекта, на котором висит HerdGoalAuthoring.
        { // Начало baker.
            public override void Bake(HerdGoalAuthoring authoring) // Вызывается при запекании подсцены. authoring даёт доступ к Radius и к трансформу объекта.
            { // Начало Bake.
                Entity entity = GetEntity(TransformUsageFlags.Dynamic); // Сущность загона с подвижным LocalTransform. Позиция цилиндра в игре и есть центр цели.
                AddComponent<HerdGoal>(entity); // Метка, по которой система движения находит единственный загон.
                AddComponent(entity, new PenZone { Radius = authoring.Radius }); // Круг загона. Radius копируется из инспектора в компонент PenZone.
            } // Конец Bake.
        } // Конец HerdGoalBaker.
    } // Конец HerdGoalAuthoring.
} // Конец namespace.
```

## HerdMovementSystems.cs

```csharp
using Unity.Burst; // Атрибут BurstCompile: метод компилируется в машинный код без сборщика мусора.
using Unity.Entities; // ISystem, SystemAPI, SystemState, запросы по компонентам.
using Unity.Mathematics; // float3, math.length, math.normalizesafe, quaternion.
using Unity.Transforms; // LocalTransform: позиция, поворот и масштаб сущности.

namespace Esc.Herd // Системы видят MoveTarget, HerdGoal, Stamina и остальные компоненты из этого namespace.
{ // Начало namespace.
    [BurstCompile] // Весь тип системы допускается к компиляции Burst.
    public partial struct HerdGoalSyncSystem : ISystem // Система синхронизации цели. partial нужен генератору ECS: он дописывает код запросов. ISystem — интерфейс системы.
    { // Начало системы.
        [BurstCompile] // Этот метод тоже компилируется Burst.
        public void OnUpdate(ref SystemState state) // Вызывается каждый кадр. state — доступ системы к миру, времени и запросам. ref передаёт её без копии.
        { // Начало кадра этой системы.
            if (!SystemAPI.TryGetSingletonEntity<HerdGoal>(out Entity goalEntity)) // Ищет единственную сущность с меткой HerdGoal и кладёт её в goalEntity. Если загона нет или их больше одного, условие истинно.
                return; // Загона нет — в этом кадре цели не обновляются, метод заканчивается.

            ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true); // Таблица для чтения LocalTransform по ссылке на сущность. true запрещает запись через эту таблицу.
            if (!transformLookup.TryGetComponent(goalEntity, out LocalTransform goalTransform)) // Достаёт трансформ загона. Если у сущности его нет, выходит.
                return; // Без трансформы позицию цели взять неоткуда.

            float3 goalPosition = goalTransform.Position; // Копия координат загона в локальную переменную.
            if (SystemAPI.TryGetSingleton<Shepherd>(out Shepherd shepherd) && shepherd.Calling) // Есть ровно один компонент пастуха, и у него включён свисток.
                goalPosition = shepherd.Position; // Тогда цель на этот кадр — позиция пастуха, а не загона.

            foreach (var move in SystemAPI.Query<RefRW<MoveTarget>>().WithAll<SheepTag>().WithDisabled<InPen>()) // Цикл по овцам вне загона. RefRW разрешает запись в MoveTarget. WithAll требует метку SheepTag. WithDisabled берёт тех, у кого InPen есть и выключен.
                move.ValueRW.Position = goalPosition; // Записывает одну и ту же цель во все подошедшие овцы. ValueRW — доступ на запись.
        } // Конец OnUpdate у синхронизации цели.
    } // Конец HerdGoalSyncSystem.

    [BurstCompile] // Система движения компилируется Burst.
    [UpdateAfter(typeof(HerdGoalSyncSystem))] // В том же кадре эта система идёт после синхронизации, поэтому шаг использует уже обновлённую цель.
    public partial struct MoveToTargetSystem : ISystem // Система шага к цели. Она же тратит стамину и не двигает истощённых.
    { // Начало системы движения.
        [BurstCompile] // OnUpdate компилируется Burst.
        public void OnUpdate(ref SystemState state) // Один вызов за кадр.
        { // Начало кадра движения.
            float deltaTime = SystemAPI.Time.DeltaTime; // Длительность этого кадра в секундах. Скорость умножается на неё, чтобы шаг не зависел от частоты кадров.

            foreach (var (transform, move, stamina) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveTarget>, RefRW<Stamina>>() // Цикл. transform можно менять, move только читать, stamina можно менять. В запрос входят сущности, у которых есть все три компонента.
                         .WithAll<SheepTag>() // Оставляет только овец.
                         .WithDisabled<Dead, InPen>()) // Оба компонента должны существовать и быть выключены. Мёртвые и уже зашедшие в загон в цикл не попадают.
            { // Тело цикла для одной овцы.
                if (stamina.ValueRO.Exhausted || stamina.ValueRO.Current <= 0f) // Овца помечена истощённой или запас уже на нуле. ValueRO читает значение.
                { // В этом кадре шага не будет.
                    stamina.ValueRW.Exhausted = true; // Подтверждает флаг, в том числе если запас кончился без флага.
                    stamina.ValueRW.Current = math.max(0f, stamina.ValueRO.Current); // Не даёт запасу остаться отрицательным: берётся ноль либо текущее значение, что больше.
                    continue; // Переходит к следующей овце, позицию этой не трогает.
                } // Конец ветки истощения.

                float3 offset = move.ValueRO.Position - transform.ValueRO.Position; // Вектор от овцы к цели.
                offset.y = 0f; // Обнуляет вертикаль. Овца сходится с целью только по земле, высота остаётся прежней.

                float distance = math.length(offset); // Длина вектора по земле, в метрах.
                if (distance <= move.ValueRO.StopDistance) // Овца уже достаточно близко к цели.
                    continue; // Шага нет, стамина в этом кадре не тратится. Управление переходит к следующей овце.

                float3 direction = math.normalizesafe(offset); // Вектор направления длиной 1. normalizesafe при нулевой длине возвращает ноль и не делит на ноль.
                float step = math.min(move.ValueRO.Speed * deltaTime, distance - move.ValueRO.StopDistance); // Шаг за кадр: скорость на время, но не дальше чем оставшееся расстояние до границы остановки.
                transform.ValueRW.Position += direction * step; // Прибавляет шаг к позиции. Запись идёт в LocalTransform.
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up()); // Поворачивает овцу лицом по направлению шага. math.up — мировая ось Y, «верх».

                float left = stamina.ValueRO.Current - stamina.ValueRO.DrainPerSecond * deltaTime; // Запас после расхода за этот кадр. Расход пропорционален длительности кадра.
                if (left <= 0f) // Расход съел весь запас или ушёл ниже нуля.
                { // Овца истощена в конце этого шага. Сам шаг выше уже сделан.
                    stamina.ValueRW.Current = 0f; // Запас ровно ноль, без отрицательного остатка.
                    stamina.ValueRW.Exhausted = true; // Со следующего кадра первая проверка в цикле остановит её.
                } // Конец ветки опустошения.
                else // Запас после расхода ещё положительный.
                { // Овца продолжит идти в следующем кадре.
                    stamina.ValueRW.Current = left; // Записывает уменьшенный запас.
                } // Конец ветки обычного расхода.
            } // Конец тела цикла по овцам.
        } // Конец OnUpdate движения.
    } // Конец MoveToTargetSystem.
} // Конец namespace.
```

## HerdVitalsSystems.cs

```csharp
using Unity.Burst; // BurstCompile для обеих систем этого файла.
using Unity.Entities; // ISystem, SystemAPI, EntityManager, атрибуты порядка систем.
using Unity.Mathematics; // math.min для потолка восстановленной стамины.

namespace Esc.Herd // Общий namespace с Health, Dead и Stamina.
{ // Начало namespace.
    [BurstCompile] // Систему смерти можно компилировать Burst.
    [UpdateBefore(typeof(MoveToTargetSystem))] // В кадре она выполняется раньше шага. Овца, у которой здоровье упало до нуля, в этом же кадре уже не идёт.
    public partial struct HealthSystem : ISystem // Система реакции на здоровье. partial и ISystem — обычная пара для системы ECS.
    { // Начало HealthSystem.
        [BurstCompile] // Метод кадра компилируется Burst.
        public void OnUpdate(ref SystemState state) // Один проход за кадр. state нужен, чтобы включить компонент через EntityManager.
        { // Начало кадра проверки здоровья.
            foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>() // Цикл по здоровью. RefRO: система его здесь только читает. entity — ссылка на саму сущность, она нужна, чтобы включить Dead.
                         .WithDisabled<Dead>() // Берёт сущности, у которых Dead есть и выключен, то есть ещё живых.
                         .WithEntityAccess() // Добавляет в цикл ссылку Entity рядом с компонентом.
                         .WithChangeFilter<Health>()) // Пропускает тех, у кого Health не менялся с прошлого просмотра этой системой. Запись нового здоровья снова их включает.
            { // Тело для одной живой сущности с изменившимся здоровьем.
                if (health.ValueRO.CurrentHealth <= 0f) // Текущее здоровье меньше или равно нулю. Это условие из задания.
                    state.EntityManager.SetComponentEnabled<Dead>(entity, true); // Включает метку Dead на этой сущности. Движение её больше не выбирает, потому что фильтр WithDisabled<Dead> такую овцу отсекает.
            } // Конец цикла. Если здоровье положительное, тело условия ничего не делает.
        } // Конец OnUpdate здоровья.
    } // Конец HealthSystem.

    [BurstCompile] // Система стамины компилируется Burst.
    [UpdateBefore(typeof(MoveToTargetSystem))] // Восстановление происходит раньше расхода в шаге, в том же кадре.
    [UpdateAfter(typeof(HealthSystem))] // Идёт после проверки смерти. Мёртвая в этот запрос уже может не попасть, если Dead успели включить.
    public partial struct StaminaSystem : ISystem // Система плавного восстановления запаса сил.
    { // Начало StaminaSystem.
        [BurstCompile] // Метод кадра компилируется Burst.
        public void OnUpdate(ref SystemState state) // Один проход за кадр. Параметр state у этого метода не читается: время берётся через SystemAPI.
        { // Начало кадра восстановления.
            float deltaTime = SystemAPI.Time.DeltaTime; // Длина кадра в секундах. Прибавка за кадр равна скорости восстановления, умноженной на это число.

            foreach (var stamina in SystemAPI.Query<RefRW<Stamina>>().WithDisabled<Dead>()) // Цикл по живым сущностям со стаминой. RefRW нужен, потому что Current и Exhausted здесь записываются. Мёртвые отфильтрованы.
            { // Тело для одной живой сущности.
                float restored = math.min(stamina.ValueRO.Max, stamina.ValueRO.Current + stamina.ValueRO.RegenPerSecond * deltaTime); // Новый запас: старый плюс восстановление за кадр, но не выше Max.
                stamina.ValueRW.Current = restored; // Записывает восстановленное значение обратно в компонент.

                if (stamina.ValueRO.Exhausted && restored >= stamina.ValueRO.Max * 0.5f) // Сущность была истощена, и запас дошёл хотя бы до половины потолка.
                    stamina.ValueRW.Exhausted = false; // Снимает запрет шага. Со следующего прохода MoveToTargetSystem она снова может идти.
            } // Конец цикла. Если флаг Exhausted выключен, второе условие ничего не меняет: запас просто растёт до максимума.
        } // Конец OnUpdate стамины.
    } // Конец StaminaSystem.
} // Конец namespace.
```
