using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    public partial struct HealthSystem : ISystem
    {
        private EntityQuery _healthQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthQuery = SystemAPI.QueryBuilder()
                .WithAll<Health>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            var job = new HealthJob
            {
                Ecb = ecb.AsParallelWriter()
            };
            state.Dependency = job.ScheduleParallel(_healthQuery, state.Dependency);

            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [WithChangeFilter(typeof(Health))]
    public partial struct HealthJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        [BurstCompile]
        public void Execute([ChunkIndexInQuery] int chunkIndexInQuery, Entity entity, in Health health)
        {
            if (health.CurrentHealth <= 0f)
                Ecb.SetComponentEnabled<Dead>(chunkIndexInQuery, entity, true);
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    [UpdateAfter(typeof(HealthSystem))]
    public partial struct StaminaSystem : ISystem
    {
        private EntityQuery _staminaQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _staminaQuery = SystemAPI.QueryBuilder()
                .WithAll<Stamina>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new StaminaJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            state.Dependency = job.ScheduleParallel(_staminaQuery, state.Dependency);
        }
    }

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
}
