using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    public partial struct HealthSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>()
                         .WithDisabled<Dead>()
                         .WithEntityAccess()
                         .WithChangeFilter<Health>())
            {
                if (health.ValueRO.CurrentHealth <= 0f)
                    state.EntityManager.SetComponentEnabled<Dead>(entity, true);
            }
        }
    }

    [BurstCompile]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    [UpdateAfter(typeof(HealthSystem))]
    public partial struct StaminaSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var stamina in SystemAPI.Query<RefRW<Stamina>>().WithDisabled<Dead>())
            {
                float restored = math.min(stamina.ValueRO.Max, stamina.ValueRO.Current + stamina.ValueRO.RegenPerSecond * deltaTime);
                stamina.ValueRW.Current = restored;

                if (stamina.ValueRO.Exhausted && restored >= stamina.ValueRO.Max * 0.5f)
                    stamina.ValueRW.Exhausted = false;
            }
        }
    }
}
