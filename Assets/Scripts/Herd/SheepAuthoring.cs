using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class SheepAuthoring : MonoBehaviour
    {
        public float Speed = 3.5f;
        public float StopDistance = 1.5f;
        public float Health = 100f;
        public float MaxHealth = 100f;
        public float Stamina = 3f;
        public float MaxStamina = 3f;
        public float StaminaRegenPerSecond = 1f;
        public float StaminaDrainPerSecond = 2f;

        class SheepBaker : Baker<SheepAuthoring>
        {
            public override void Bake(SheepAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<SheepTag>(entity);
                AddComponent(entity, new MoveTarget
                {
                    Position = authoring.transform.position,
                    Speed = authoring.Speed,
                    StopDistance = authoring.StopDistance
                });
                AddComponent(entity, new Health
                {
                    CurrentHealth = authoring.Health,
                    MaxHealth = math.max(authoring.MaxHealth, authoring.Health)
                });
                AddComponent<Dead>(entity);
                SetComponentEnabled<Dead>(entity, false);
                AddComponent(entity, new Stamina
                {
                    Current = authoring.Stamina,
                    Max = math.max(authoring.MaxStamina, 0.01f),
                    RegenPerSecond = authoring.StaminaRegenPerSecond,
                    DrainPerSecond = authoring.StaminaDrainPerSecond
                });
            }
        }
    }
}
