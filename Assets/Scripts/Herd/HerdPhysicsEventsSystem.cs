using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Esc.Herd
{
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

    [BurstCompile]
    public partial struct HerdCollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<HerdGoal> GoalLookup;
        [ReadOnly] public ComponentLookup<SheepTag> SheepLookup;
        public ComponentLookup<Health> HealthLookup;

        [BurstCompile]
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            if (GoalLookup.TryGetComponent(entityA, out _) && SheepLookup.TryGetComponent(entityB, out _))
            {
                if (HealthLookup.TryGetRefRW(entityB, out RefRW<Health> sheepHealth))
                {
                    sheepHealth.ValueRW.CurrentHealth -= 25f;
                }
            }

            if (GoalLookup.TryGetComponent(entityB, out _) && SheepLookup.TryGetComponent(entityA, out _))
            {
                if (HealthLookup.TryGetRefRW(entityA, out RefRW<Health> sheepHealth))
                {
                    sheepHealth.ValueRW.CurrentHealth -= 25f;
                }
            }
        }
    }
}
