using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateAfter(typeof(HealthSystem))]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    public partial struct WolfBrainSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var sheepQuery = SystemAPI.QueryBuilder().WithAll<SheepTag, LocalTransform>().WithDisabled<Dead, InPen>().Build();
            var sheepEntities = sheepQuery.ToEntityArray(Allocator.Temp);
            var sheepTransforms = sheepQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var healthLookup = SystemAPI.GetComponentLookup<Health>();

            foreach (var (transform, wolf, path, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<WolfState>, RefRW<WolfPath>>()
                         .WithAll<WolfTag>()
                         .WithEntityAccess())
            {
                bool dead = state.EntityManager.IsComponentEnabled<Dead>(entity);
                if (dead)
                {
                    if (wolf.ValueRO.Behavior != WolfBehavior.Dead)
                        wolf.ValueRW.Behavior = WolfBehavior.Dead;

                    wolf.ValueRW.CorpseTime -= deltaTime;
                    continue;
                }

                Entity prey = Entity.Null;
                float3 preyPosition = transform.ValueRO.Position;
                bool hasPrey = false;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < sheepEntities.Length; i++)
                {
                    float distance = math.distancesq(sheepTransforms[i].Position.xz, transform.ValueRO.Position.xz);
                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    prey = sheepEntities[i];
                    preyPosition = sheepTransforms[i].Position;
                    hasPrey = true;
                }

                wolf.ValueRW.Prey = hasPrey ? prey : Entity.Null;
                if (!hasPrey)
                {
                    wolf.ValueRW.Behavior = WolfBehavior.Chase;
                    continue;
                }

                float3 offset = preyPosition - transform.ValueRO.Position;
                offset.y = 0f;
                float distanceToPrey = math.length(offset);
                float attackRange = wolf.ValueRO.Behavior == WolfBehavior.Attack
                    ? wolf.ValueRO.AttackRange * 1.45f
                    : wolf.ValueRO.AttackRange;
                if (distanceToPrey <= attackRange)
                {
                    wolf.ValueRW.Behavior = WolfBehavior.Attack;
                    if (math.lengthsq(offset) > 0.0001f)
                        transform.ValueRW.Rotation = quaternion.LookRotationSafe(math.normalizesafe(offset), math.up());

                    wolf.ValueRW.AttackCooldown -= deltaTime;
                    if (wolf.ValueRO.AttackCooldown <= 0f)
                    {
                        wolf.ValueRW.AttackCooldown = wolf.ValueRO.AttackInterval;
                        if (healthLookup.HasComponent(prey))
                        {
                            Health preyHealth = healthLookup[prey];
                            preyHealth.CurrentHealth -= wolf.ValueRO.AttackDamage;
                            healthLookup[prey] = preyHealth;
                        }
                    }
                }
                else
                {
                    wolf.ValueRW.Behavior = WolfBehavior.Chase;
                    path.ValueRW.RepathTimer -= deltaTime;
                }
            }

            sheepEntities.Dispose();
            sheepTransforms.Dispose();
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(WolfBrainSystem))]
    public partial struct WolfPathSystem : ISystem
    {
        const int Grid = 16;
        const float Cell = 3f;
        const float Origin = -24f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PastureObstacle>(out PastureObstacle obstacle))
                obstacle = default;

            foreach (var (transform, wolf, path) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<WolfState>, RefRW<WolfPath>>()
                         .WithAll<WolfTag>()
                         .WithDisabled<Dead>())
            {
                if (wolf.ValueRO.Behavior != WolfBehavior.Chase || wolf.ValueRO.Prey == Entity.Null)
                    continue;

                float3 toWaypoint = path.ValueRO.Waypoint - transform.ValueRO.Position;
                toWaypoint.y = 0f;
                bool arrived = math.lengthsq(toWaypoint) <= 0.64f;
                if (path.ValueRO.RepathTimer > 0f && !arrived)
                    continue;

                if (!SystemAPI.HasComponent<LocalTransform>(wolf.ValueRO.Prey))
                    continue;

                float3 preyPosition = SystemAPI.GetComponent<LocalTransform>(wolf.ValueRO.Prey).Position;
                path.ValueRW.Waypoint = NextWaypoint(transform.ValueRO.Position, preyPosition, obstacle);
                path.ValueRW.RepathTimer = 0.15f;
            }
        }

        static float3 NextWaypoint(float3 from, float3 to, PastureObstacle obstacle)
        {
            if (!SegmentBlocked(from, to, obstacle))
            {
                to.y = from.y;
                return to;
            }

            int2 start = WorldToCell(from);
            int2 goal = WorldToCell(to);
            start = NearestOpen(start, obstacle);
            goal = NearestOpen(goal, obstacle);
            if (start.x == goal.x && start.y == goal.y)
            {
                to.y = from.y;
                return to;
            }

            int cellCount = Grid * Grid;
            int startIndex = Index(start);
            int goalIndex = Index(goal);
            Span<float> gScore = stackalloc float[cellCount];
            Span<int> parent = stackalloc int[cellCount];
            Span<int> open = stackalloc int[cellCount];
            Span<byte> closed = stackalloc byte[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                gScore[i] = float.MaxValue;
                parent[i] = -1;
                closed[i] = 0;
            }

            int openCount = 1;
            gScore[startIndex] = 0f;
            open[0] = startIndex;
            bool found = false;
            while (openCount > 0)
            {
                int best = 0;
                float bestScore = float.MaxValue;
                for (int i = 0; i < openCount; i++)
                {
                    int cell = open[i];
                    int2 coord = Coord(cell);
                    float score = gScore[cell] + math.abs(coord.x - goal.x) + math.abs(coord.y - goal.y);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }

                int current = open[best];
                open[best] = open[openCount - 1];
                openCount--;
                if (current == goalIndex)
                {
                    found = true;
                    break;
                }

                closed[current] = 1;
                int2 currentCoord = Coord(current);
                TryNeighbor(current, new int2(currentCoord.x + 1, currentCoord.y), obstacle, gScore, parent, open, ref openCount, closed);
                TryNeighbor(current, new int2(currentCoord.x - 1, currentCoord.y), obstacle, gScore, parent, open, ref openCount, closed);
                TryNeighbor(current, new int2(currentCoord.x, currentCoord.y + 1), obstacle, gScore, parent, open, ref openCount, closed);
                TryNeighbor(current, new int2(currentCoord.x, currentCoord.y - 1), obstacle, gScore, parent, open, ref openCount, closed);
            }

            float3 waypoint = to;
            if (found)
            {
                int step = goalIndex;
                while (parent[step] != startIndex && parent[step] >= 0)
                    step = parent[step];

                waypoint = step == goalIndex ? to : CellCenter(Coord(step));
            }

            waypoint.y = from.y;
            return waypoint;
        }

        static void TryNeighbor(int current, int2 next, PastureObstacle obstacle, Span<float> gScore, Span<int> parent, Span<int> open, ref int openCount, Span<byte> closed)
        {
            if ((uint)next.x >= Grid || (uint)next.y >= Grid || Blocked(next, obstacle))
                return;

            int nextIndex = Index(next);
            if (closed[nextIndex] != 0)
                return;

            float tentative = gScore[current] + 1f;
            if (tentative >= gScore[nextIndex])
                return;

            gScore[nextIndex] = tentative;
            parent[nextIndex] = current;
            for (int i = 0; i < openCount; i++)
            {
                if (open[i] == nextIndex)
                    return;
            }

            open[openCount] = nextIndex;
            openCount++;
        }

        static bool SegmentBlocked(float3 from, float3 to, PastureObstacle obstacle)
        {
            if (obstacle.HalfExtents.x <= 0f || obstacle.HalfExtents.y <= 0f)
                return false;

            float2 half = obstacle.HalfExtents + new float2(0.75f, 0.75f);
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                float x = math.lerp(from.x, to.x, t);
                float z = math.lerp(from.z, to.z, t);
                if (math.abs(x - obstacle.CenterXZ.x) <= half.x && math.abs(z - obstacle.CenterXZ.y) <= half.y)
                    return true;
            }

            return false;
        }

        static int2 NearestOpen(int2 cell, PastureObstacle obstacle)
        {
            if (!Blocked(cell, obstacle))
                return cell;

            for (int radius = 1; radius < Grid; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        var candidate = new int2(cell.x + dx, cell.y + dz);
                        if ((uint)candidate.x < Grid && (uint)candidate.y < Grid && !Blocked(candidate, obstacle))
                            return candidate;
                    }
                }
            }

            return cell;
        }

        static bool Blocked(int2 cell, PastureObstacle obstacle)
        {
            if (obstacle.HalfExtents.x <= 0f || obstacle.HalfExtents.y <= 0f)
                return false;

            float3 center = CellCenter(cell);
            return math.abs(center.x - obstacle.CenterXZ.x) <= obstacle.HalfExtents.x + 0.75f
                   && math.abs(center.z - obstacle.CenterXZ.y) <= obstacle.HalfExtents.y + 0.75f;
        }

        static int Index(int2 cell) => cell.y * Grid + cell.x;

        static int2 Coord(int index) => new int2(index % Grid, index / Grid);

        static int2 WorldToCell(float3 position)
        {
            int x = (int)math.floor((position.x - Origin) / Cell);
            int z = (int)math.floor((position.z - Origin) / Cell);
            return new int2(math.clamp(x, 0, Grid - 1), math.clamp(z, 0, Grid - 1));
        }

        static float3 CellCenter(int2 cell)
        {
            return new float3(Origin + (cell.x + 0.5f) * Cell, 0f, Origin + (cell.y + 0.5f) * Cell);
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(WolfPathSystem))]
    public partial struct WolfMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (transform, wolf, path) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<WolfState>, RefRO<WolfPath>>()
                         .WithAll<WolfTag>()
                         .WithDisabled<Dead>())
            {
                if (wolf.ValueRO.Behavior != WolfBehavior.Chase)
                    continue;

                float3 offset = path.ValueRO.Waypoint - transform.ValueRO.Position;
                offset.y = 0f;
                float distance = math.length(offset);
                if (distance <= 0.35f)
                    continue;

                float3 direction = math.normalizesafe(offset);
                float step = math.min(path.ValueRO.Speed * deltaTime, distance);
                transform.ValueRW.Position += direction * step;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct WaveSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<WaveGate>(out WaveGate gate) || !gate.Open)
                return;
            if (!SystemAPI.TryGetSingleton<WavesHolder>(out WavesHolder wavesHolder) || !wavesHolder.WavesReference.IsCreated)
                return;
            if (!SystemAPI.TryGetSingleton<PrefabsHolder>(out PrefabsHolder prefabs))
                return;

            ref WavesBlob wavesRoot = ref wavesHolder.WavesReference.Value;
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var spawner in SystemAPI.Query<RefRW<WaveSpawner>>())
            {
                int waveIndex = spawner.ValueRO.NextWave;
                if (waveIndex >= wavesRoot.Waves.Length)
                    continue;

                spawner.ValueRW.TimeUntilWave -= deltaTime;
                if (spawner.ValueRO.TimeUntilWave > 0f)
                    continue;

                ref Wave wave = ref wavesRoot.Waves[waveIndex];
                for (int group = 0; group < wave.Enemies.Length; group++)
                {
                    EnemyInWave enemy = wave.Enemies[group];
                    Entity prefab = enemy.EnemyType == EnemyType.Wolf ? prefabs.WolfPrefab : Entity.Null;
                    if (prefab == Entity.Null || enemy.MaxAmount <= 0)
                        continue;

                    int count = spawner.ValueRW.Random.NextInt(enemy.MinAmount, enemy.MaxAmount + 1);
                    for (int i = 0; i < count; i++)
                    {
                        float x = spawner.ValueRW.Random.NextFloat(10f, 16f);
                        float z = spawner.ValueRW.Random.NextFloat(1f, 8f);
                        Entity wolf = ecb.Instantiate(prefab);
                        ecb.SetComponent(wolf, LocalTransform.FromPositionRotationScale(
                            new float3(x, 0f, z),
                            quaternion.identity,
                            1f));
                    }
                }

                int nextWave = waveIndex + 1;
                spawner.ValueRW.NextWave = nextWave;
                if (nextWave < wavesRoot.Waves.Length)
                    spawner.ValueRW.TimeUntilWave = wavesRoot.Waves[nextWave].WaitTimeBeforeWave;
            }
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(WolfBrainSystem))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct WolfDespawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (wolf, entity) in SystemAPI.Query<RefRW<WolfState>>()
                         .WithAll<WolfTag, Dead>()
                         .WithEntityAccess())
            {
                if (wolf.ValueRO.Behavior != WolfBehavior.Dead || wolf.ValueRO.CorpseTime > 0f)
                    continue;

                wolf.ValueRW.CorpseTime = 1000f;
                ecb.DestroyEntity(entity);
            }
        }
    }
}
