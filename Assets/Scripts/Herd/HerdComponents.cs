using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

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

    // Маркер единственного загона. Живая позиция — LocalTransform этой entity.
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

    public enum WolfBehavior : byte
    {
        Chase = 0,
        Attack = 1,
        Dead = 2
    }

    public struct WolfTag : IComponentData
    {
    }

    public struct WolfState : IComponentData
    {
        public WolfBehavior Behavior;
        public float AttackRange;
        public float AttackDamage;
        public float AttackInterval;
        public float AttackCooldown;
        public float DamageTakenPerAttack;
        public float CorpseTime;
        public Entity Prey;
    }

    public struct WolfPath : IComponentData
    {
        public float3 Waypoint;
        public float Speed;
        public float RepathTimer;
    }

    public struct PastureObstacle : IComponentData
    {
        public float2 CenterXZ;
        public float2 HalfExtents;
    }

    public enum EnemyType : byte
    {
        Wolf = 0
    }

    public struct WavesBlob
    {
        public BlobArray<Wave> Waves;
    }

    public struct Wave
    {
        public float WaitTimeBeforeWave;
        public BlobArray<EnemyInWave> Enemies;
    }

    public struct EnemyInWave
    {
        public EnemyType EnemyType;
        public int MinAmount;
        public int MaxAmount;
    }

    public struct WavesHolder : IComponentData
    {
        public BlobAssetReference<WavesBlob> WavesReference;
    }

    public struct WaveSpawner : IComponentData
    {
        public float TimeUntilWave;
        public int NextWave;
        public Random Random;
    }

    public struct WaveGate : IComponentData
    {
        public bool Open;
    }

    public struct PrefabsHolder : IComponentData
    {
        public Entity SheepPrefab;
        public Entity WolfPrefab;
    }

    [InternalBufferCapacity(8)]
    public struct StoneImpact : IBufferElementData
    {
        public float3 Position;
        public float Damage;
        public float Radius;
    }

    public struct InPen : IComponentData, IEnableableComponent
    {
    }

    public struct PenZone : IComponentData
    {
        public float Radius;
    }

    public struct Shepherd : IComponentData
    {
        public float3 Position;
        public bool Calling;
    }

    public struct SheepFlockSpawner : IComponentData
    {
        public int Count;
        public float2 AreaMin;
        public float2 AreaMax;
        public Random Random;
        public bool Spawned;
    }

    public struct CrowdAnim : IComponentData
    {
        public float Time;
        public float Fps;
        public float3 PreviousPosition;
        public int IdleStart;
        public int IdleCount;
        public int MoveStart;
        public int MoveCount;
        public int ActStart;
        public int ActCount;
        public int DeathStart;
        public int DeathCount;
        public byte Species;
        public byte Clip;
        public int MaterialId;
    }

    [InternalBufferCapacity(48)]
    public struct CrowdMeshIdElement : IBufferElementData
    {
        public int Value;
    }

    public struct CrowdLibraryTag : IComponentData
    {
        public int SheepMaterialId;
        public int WolfMaterialId;
        public bool Ready;
    }
}
