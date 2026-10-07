using Unity.Entities;
using UnityEngine;

namespace Esc.Herd
{
    public class WolfWaveTrigger : MonoBehaviour
    {
        Entity gateEntity;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null && other.GetComponentInParent<ShepherdBridge>() == null)
                return;

            Open();
        }

        public void Open()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager manager = world.EntityManager;
            if (!manager.Exists(gateEntity))
                gateEntity = manager.CreateEntity(typeof(WaveGate));

            manager.SetComponentData(gateEntity, new WaveGate { Open = true });
        }
    }
}
