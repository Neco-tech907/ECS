using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Esc.Herd
{
    public class ShepherdBridge : MonoBehaviour
    {
        Entity shepherdEntity;

        void Update()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                Shepherd current = Read(world.EntityManager);
                current.Calling = !current.Calling;
                Write(world.EntityManager, current);
            }

            EntityManager manager = world.EntityManager;
            Shepherd shepherd = Read(manager);
            shepherd.Position = transform.position;
            Write(manager, shepherd);
        }

        Shepherd Read(EntityManager manager)
        {
            if (manager.Exists(shepherdEntity) && manager.HasComponent<Shepherd>(shepherdEntity))
                return manager.GetComponentData<Shepherd>(shepherdEntity);

            shepherdEntity = Entity.Null;
            return default;
        }

        void Write(EntityManager manager, Shepherd shepherd)
        {
            if (!manager.Exists(shepherdEntity))
                shepherdEntity = manager.CreateEntity(typeof(Shepherd));

            manager.SetComponentData(shepherdEntity, shepherd);
        }
    }
}
