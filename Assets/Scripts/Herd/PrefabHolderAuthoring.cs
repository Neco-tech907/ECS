using Unity.Entities;
using UnityEngine;

namespace Esc.Herd
{
    public class PrefabHolderAuthoring : MonoBehaviour
    {
        public GameObject SheepPrefab;
        public GameObject WolfPrefab;

        class PrefabHolderBaker : Baker<PrefabHolderAuthoring>
        {
            public override void Bake(PrefabHolderAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PrefabsHolder
                {
                    SheepPrefab = GetEntity(authoring.SheepPrefab, TransformUsageFlags.Dynamic),
                    WolfPrefab = GetEntity(authoring.WolfPrefab, TransformUsageFlags.Dynamic)
                });
                AddBuffer<StoneImpact>(entity);
            }
        }
    }
}
