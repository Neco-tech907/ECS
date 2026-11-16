using Unity.Entities;

namespace Esc.Herd
{
    public static class EntityWorldAccess
    {
        public static bool TryGetSimulationWorld(out World world)
        {
            world = World.DefaultGameObjectInjectionWorld;
            return world != null && world.IsCreated;
        }

        public static bool TryGetSingleton<T>(out T value) where T : unmanaged, IComponentData
        {
            value = default;
            if (!TryGetSimulationWorld(out World world))
                return false;

            using EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.CalculateEntityCount() != 1)
                return false;

            value = query.GetSingleton<T>();
            return true;
        }

        public static bool TrySetSingleton<T>(T value) where T : unmanaged, IComponentData
        {
            if (!TryGetSimulationWorld(out World world))
                return false;

            EntityManager entityManager = world.EntityManager;
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.CalculateEntityCount() != 1)
                return false;

            entityManager.SetComponentData(query.GetSingletonEntity(), value);
            return true;
        }
    }
}
