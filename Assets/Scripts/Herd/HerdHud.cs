using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Esc.Herd
{
    public class HerdHud : MonoBehaviour
    {
        Text arrivedLabel;
        Text deadLabel;
        EntityQuery sheepQuery;
        World queryWorld;

        void Awake()
        {
            var canvasObject = new GameObject("HerdCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrivedLabel = CreateLabel(canvasObject.transform, font, new Vector2(24f, -24f), "В загоне: 0");
            deadLabel = CreateLabel(canvasObject.transform, font, new Vector2(24f, -78f), "Погибли: 0");
        }

        void OnDestroy()
        {
            if (queryWorld != null && queryWorld.IsCreated)
                sheepQuery.Dispose();
        }

        void Update()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            if (queryWorld != world)
            {
                if (queryWorld != null && queryWorld.IsCreated)
                    sheepQuery.Dispose();

                sheepQuery = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(SheepTag), typeof(InPen), typeof(Dead) },
                    Options = EntityQueryOptions.IgnoreComponentEnabledState
                });
                queryWorld = world;
            }

            var entities = sheepQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            int arrived = 0;
            int dead = 0;
            var manager = world.EntityManager;
            for (int i = 0; i < entities.Length; i++)
            {
                if (manager.IsComponentEnabled<Dead>(entities[i]))
                    dead++;
                else if (manager.IsComponentEnabled<InPen>(entities[i]))
                    arrived++;
            }

            entities.Dispose();
            arrivedLabel.text = "В загоне: " + arrived;
            deadLabel.text = "Погибли: " + dead;
        }

        static Text CreateLabel(Transform parent, Font font, Vector2 offset, string message)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(520f, 48f);
            var text = labelObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 36;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = message;
            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}
