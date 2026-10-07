using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class WolfAuthoring : MonoBehaviour
    {
        public float Speed = 6.5f;
        public float Health = 30f;
        public float AttackRange = 1.7f;
        public float AttackDamage = 34f;
        public float AttackInterval = 0.7f;
        public float DamageTakenPerAttack = 16f;
        public float CorpseTime = 1.25f;

        class WolfBaker : Baker<WolfAuthoring>
        {
            public override void Bake(WolfAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<WolfTag>(entity);
                AddComponent(entity, new Health
                {
                    CurrentHealth = authoring.Health,
                    MaxHealth = authoring.Health
                });
                AddComponent<Dead>(entity);
                SetComponentEnabled<Dead>(entity, false);
                AddComponent(entity, new WolfState
                {
                    Behavior = WolfBehavior.Chase,
                    AttackRange = authoring.AttackRange,
                    AttackDamage = authoring.AttackDamage,
                    AttackInterval = authoring.AttackInterval,
                    DamageTakenPerAttack = authoring.DamageTakenPerAttack,
                    CorpseTime = authoring.CorpseTime
                });
                AddComponent(entity, new WolfPath
                {
                    Waypoint = authoring.transform.position,
                    Speed = authoring.Speed
                });
            }
        }
    }
}
