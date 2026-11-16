using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Esc.Herd
{
    public class ShepherdInputController : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool followHeld = keyboard != null && keyboard.eKey.isPressed;

            EntityWorldAccess.TrySetSingleton(new ShepherdInput
            {
                Position = (float3)transform.position,
                FollowHeld = followHeld
            });
        }
    }
}
