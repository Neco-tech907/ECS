using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class ObstacleAuthoring : MonoBehaviour
    {
        public Vector2 HalfExtents = new Vector2(2f, 3f);

        class ObstacleBaker : Baker<ObstacleAuthoring>
        {
            public override void Bake(ObstacleAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new PastureObstacle
                {
                    CenterXZ = new float2(authoring.transform.position.x, authoring.transform.position.z),
                    HalfExtents = new float2(authoring.HalfExtents.x, authoring.HalfExtents.y)
                });
            }
        }
    }
}
