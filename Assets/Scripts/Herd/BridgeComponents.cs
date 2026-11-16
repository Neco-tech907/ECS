using Unity.Entities;
using Unity.Mathematics;

namespace Esc.Herd
{
    public struct ShepherdInput : IComponentData
    {
        public float3 Position;
        public bool FollowHeld;
    }

    public struct HerdHudData : IComponentData
    {
        public int AliveCount;
        public float NearestHealth;
        public float NearestStamina;
        public float NearestDistance;
        public bool HasNearest;
    }

    public struct FlockCleared : IComponentData
    {
        public bool Fired;
    }
}
