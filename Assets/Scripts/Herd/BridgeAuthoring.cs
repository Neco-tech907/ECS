using Unity.Entities;
using UnityEngine;

namespace Esc.Herd
{
    public class BridgeAuthoring : MonoBehaviour
    {
        class BridgeBaker : Baker<BridgeAuthoring>
        {
            public override void Bake(BridgeAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ShepherdInput());
                AddComponent(entity, new HerdHudData());
                AddComponent(entity, new FlockCleared());
            }
        }
    }
}
