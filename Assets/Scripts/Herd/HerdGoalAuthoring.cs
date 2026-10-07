using Unity.Entities;
using UnityEngine;

namespace Esc.Herd
{
    public class HerdGoalAuthoring : MonoBehaviour
    {
        class HerdGoalBaker : Baker<HerdGoalAuthoring>
        {
            public override void Bake(HerdGoalAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<HerdGoal>(entity);
            }
        }
    }
}
