using UnityEngine;
using UnityEngine.InputSystem;

namespace Esc.Herd
{
    public class StoneThrower : MonoBehaviour
    {
        public float Speed = 16f;
        public float Cooldown = 0.7f;
        public float Damage = 40f;
        public float Radius = 1.8f;

        float nextThrowTime;

        void Update()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (!pressed && Mouse.current != null)
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
            if (pressed)
                Throw();
        }

        public void Throw()
        {
            if (Time.time < nextThrowTime)
                return;

            nextThrowTime = Time.time + Cooldown;
            Transform aim = Camera.main != null ? Camera.main.transform : transform;
            Vector3 direction = aim.forward;
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Stone";
            stone.transform.position = transform.position + Vector3.up * 1.3f + direction * 0.5f;
            stone.transform.localScale = Vector3.one * 0.22f;
            var body = stone.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = direction * Speed + Vector3.up * 2.5f;
            var projectile = stone.AddComponent<StoneProjectile>();
            projectile.Damage = Damage;
            projectile.Radius = Radius;
        }
    }
}
