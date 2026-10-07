using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class StoneProjectile : MonoBehaviour
    {
        public float Damage = 40f;
        public float Radius = 1.8f;
        public float Lifetime = 4f;

        float age;
        bool reported;

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
                Report(transform.position);
        }

        void OnCollisionEnter(Collision collision)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Report(point);
        }

        void Report(Vector3 point)
        {
            if (reported)
                return;

            reported = true;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                EntityManager manager = world.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(StoneImpact));
                if (!query.IsEmpty)
                {
                    Entity holder = query.GetSingletonEntity();
                    DynamicBuffer<StoneImpact> impacts = manager.GetBuffer<StoneImpact>(holder);
                    impacts.Add(new StoneImpact
                    {
                        Position = new float3(point.x, point.y, point.z),
                        Damage = Damage,
                        Radius = Radius
                    });
                }
            }

            Destroy(gameObject);
        }
    }
}
