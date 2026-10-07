using Unity.Entities;
using UnityEngine;

namespace Esc.Herd
{
    public class HerdGoalAuthoring : MonoBehaviour
    {
        public float Radius = 2.4f;

        class HerdGoalBaker : Baker<HerdGoalAuthoring>
        {
            public override void Bake(HerdGoalAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<HerdGoal>(entity);
                AddComponent(entity, new PenZone { Radius = authoring.Radius });
            }
        }
    }
}
