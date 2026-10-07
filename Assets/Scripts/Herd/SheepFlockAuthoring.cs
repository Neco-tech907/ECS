using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class SheepFlockAuthoring : MonoBehaviour
    {
        public int Count = 1000;
        public Vector2 AreaMin = new Vector2(-14f, -8f);
        public Vector2 AreaMax = new Vector2(14f, 5f);
        public uint Seed = 17;

        class SheepFlockBaker : Baker<SheepFlockAuthoring>
        {
            public override void Bake(SheepFlockAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SheepFlockSpawner
                {
                    Count = math.max(authoring.Count, 0),
                    AreaMin = new float2(authoring.AreaMin.x, authoring.AreaMin.y),
                    AreaMax = new float2(authoring.AreaMax.x, authoring.AreaMax.y),
                    Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed == 0 ? 1u : authoring.Seed)
                });
            }
        }
    }
}
