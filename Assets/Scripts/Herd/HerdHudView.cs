using UnityEngine;

namespace Esc.Herd
{
    public class HerdHudView : MonoBehaviour
    {
        private string _status = "Овцы идут в загон. Удерживайте E — пойдут за пастухом.";

        private void OnEnable()
        {
            HerdEvents.OnFlockCleared += HandleFlockCleared;
        }

        private void OnDisable()
        {
            HerdEvents.OnFlockCleared -= HandleFlockCleared;
        }

        private void OnGUI()
        {
            const int pad = 16;
            GUIStyle box = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 12, 12)
            };
            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };

            GUI.Box(new Rect(pad, pad, 480, 150), GUIContent.none, box);
            GUI.Label(new Rect(pad + 12, pad + 8, 456, 134), BuildHudText(), label);
        }

        private string BuildHudText()
        {
            if (!EntityWorldAccess.TryGetSingleton(out HerdHudData hud))
                return "HUD: мир Entity ещё не готов.\n" + _status;

            string nearest = hud.HasNearest
                ? $"Ближайшая овца: HP {hud.NearestHealth:0}  stamina {hud.NearestStamina:0.0}  dist {hud.NearestDistance:0.0} м"
                : "Ближайшая овца: нет живых";

            return $"Живых овец: {hud.AliveCount}\n{nearest}\n{_status}";
        }

        private void HandleFlockCleared()
        {
            _status = "Событие из ECS: стадо рассеяно (волны закончились, живых овец 0).";
        }
    }
}
