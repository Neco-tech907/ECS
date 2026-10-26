using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    public partial struct HerdGoalSyncSystem : ISystem
    {
        private EntityQuery _sheepQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sheepQuery = SystemAPI.QueryBuilder()
                .WithAll<MoveTarget, LocalTransform, SheepTag>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
        }
    }

    [BurstCompile]
    public partial struct HerdGoalSyncJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld CollisionWorld;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        public float SearchRadius;

        [BurstCompile]
        public void Execute(ref MoveTarget move, in LocalTransform transform)
        {
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
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(HerdGoalSyncSystem))]
    public partial struct MoveToTargetSystem : ISystem
    {
        private EntityQuery _moveQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _moveQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, PhysicsVelocity, PhysicsMass, MoveTarget, Stamina, SheepTag>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out PhysicsWorldSingleton physicsWorld))
                return;

            CollisionWorld collisionWorld = physicsWorld.CollisionWorld;

            var job = new MoveToTargetJob
            {
                CollisionWorld = collisionWorld,
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            state.Dependency = job.ScheduleParallel(_moveQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct MoveToTargetJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld CollisionWorld;
        public float DeltaTime;

        [BurstCompile]
        public void Execute(
            ref LocalTransform transform,
            ref PhysicsVelocity physicsVelocity,
            ref PhysicsMass physicsMass,
            in MoveTarget move,
            ref Stamina stamina)
        {
            physicsMass.InverseInertia = new float3(0f, physicsMass.InverseInertia.y, 0f);

            if (stamina.Exhausted || stamina.Current <= 0f)
            {
                stamina.Exhausted = true;
                stamina.Current = math.max(0f, stamina.Current);
                physicsVelocity.Linear = new float3(0f, physicsVelocity.Linear.y, 0f);
                physicsVelocity.Angular = float3.zero;
                return;
            }

            float3 offset = move.Position - transform.Position;
            offset.y = 0f;

            float distance = math.length(offset);
            if (distance <= 0.1f)
            {
                physicsVelocity.Linear = new float3(0f, physicsVelocity.Linear.y, 0f);
                physicsVelocity.Angular = float3.zero;
                return;
            }

            float3 direction = math.normalizesafe(offset);

            CollisionFilter filter = new CollisionFilter
            {
                BelongsTo = 1u << 0,
                CollidesWith = 1u << 6,
                GroupIndex = 0
            };
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

            physicsVelocity.Linear = new float3(direction.x * move.Speed, physicsVelocity.Linear.y, direction.z * move.Speed);
            physicsVelocity.Angular = float3.zero;
            transform.Rotation = quaternion.LookRotationSafe(direction, math.up());

            float left = stamina.Current - stamina.DrainPerSecond * DeltaTime;
            if (left <= 0f)
            {
                stamina.Current = 0f;
                stamina.Exhausted = true;
            }
            else
            {
                stamina.Current = left;
            }
        }
    }
}
