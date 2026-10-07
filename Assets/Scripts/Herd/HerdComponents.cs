using Unity.Entities;
using Unity.Mathematics;

namespace Esc.Herd
{
    public struct SheepTag : IComponentData
    {
    }

    public struct MoveTarget : IComponentData
    {
        public float3 Position;
        public float Speed;
        public float StopDistance;
    }

    public struct HerdGoal : IComponentData
    {
    }

    public struct Health : IComponentData
    {
        public float CurrentHealth;
        public float MaxHealth;
    }

    public struct Dead : IComponentData, IEnableableComponent
    {
    }

    public struct Stamina : IComponentData
    {
        public float Current;
        public float Max;
        public float RegenPerSecond;
        public float DrainPerSecond;
        public bool Exhausted;
    }
}
