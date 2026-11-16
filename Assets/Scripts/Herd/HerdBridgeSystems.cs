using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateAfter(typeof(HerdGoalSyncSystem))]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    public partial struct ShepherdFollowSystem : ISystem
    {
        private EntityQuery _sheepQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShepherdInput>();
            _sheepQuery = SystemAPI.QueryBuilder()
                .WithAll<MoveTarget, SheepTag>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ShepherdInput input = SystemAPI.GetSingleton<ShepherdInput>();
            if (!input.FollowHeld)
                return;

            var job = new ShepherdFollowJob
            {
                ShepherdPosition = input.Position
            };
            state.Dependency = job.ScheduleParallel(_sheepQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ShepherdFollowJob : IJobEntity
    {
        public float3 ShepherdPosition;

        [BurstCompile]
        public void Execute(ref MoveTarget move)
        {
            move.Position = ShepherdPosition;
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(MoveToTargetSystem))]
    public partial struct HerdHudGatherSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HerdHudData>();
            state.RequireForUpdate<ShepherdInput>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ShepherdInput input = SystemAPI.GetSingleton<ShepherdInput>();

            int alive = 0;
            float nearestDistSq = float.MaxValue;
            float nearestHealth = 0f;
            float nearestStamina = 0f;
            bool hasNearest = false;

            foreach (var (health, stamina, transform) in SystemAPI
                         .Query<RefRO<Health>, RefRO<Stamina>, RefRO<LocalTransform>>()
                         .WithAll<SheepTag>()
                         .WithDisabled<Dead>())
            {
                alive++;
                float distSq = math.distancesq(input.Position, transform.ValueRO.Position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestHealth = health.ValueRO.CurrentHealth;
                    nearestStamina = stamina.ValueRO.Current;
                    hasNearest = true;
                }
            }

            SystemAPI.SetSingleton(new HerdHudData
            {
                AliveCount = alive,
                NearestHealth = nearestHealth,
                NearestStamina = nearestStamina,
                NearestDistance = hasNearest ? math.sqrt(nearestDistSq) : 0f,
                HasNearest = hasNearest
            });
        }
    }

    [UpdateAfter(typeof(HerdHudGatherSystem))]
    public partial struct FlockClearedSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HerdHudData>();
            state.RequireForUpdate<FlockCleared>();
            state.RequireForUpdate<WaveCursor>();
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<FlockCleared> flag = SystemAPI.GetSingletonRW<FlockCleared>();
            if (flag.ValueRO.Fired)
            {
                state.Enabled = false;
                return;
            }

            HerdHudData hud = SystemAPI.GetSingleton<HerdHudData>();
            WaveCursor cursor = SystemAPI.GetSingleton<WaveCursor>();
            if (!cursor.Finished || hud.AliveCount > 0)
                return;

            HerdEvents.OnFlockCleared?.Invoke();
            flag.ValueRW.Fired = true;
            state.Enabled = false;
        }
    }
}
