using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class CrowdAnimAuthoring : MonoBehaviour
    {
        public CrowdLibraryAsset Library;
        public CrowdSpecies Species;

        class CrowdAnimBaker : Baker<CrowdAnimAuthoring>
        {
            public override void Bake(CrowdAnimAuthoring authoring)
            {
                if (authoring.Library == null)
                    return;

                Entity entity = GetEntity(TransformUsageFlags.Renderable);
                var anim = new CrowdAnim
                {
                    Fps = math.max(authoring.Library.Fps, 1f),
                    PreviousPosition = authoring.transform.position,
                    Species = (byte)authoring.Species
                };

                if (authoring.Species == CrowdSpecies.Wolf)
                {
                    authoring.Library.GetWolfRanges(
                        out anim.IdleStart, out anim.IdleCount,
                        out anim.MoveStart, out anim.MoveCount,
                        out anim.ActStart, out anim.ActCount,
                        out anim.DeathStart, out anim.DeathCount);
                }
                else
                {
                    authoring.Library.GetSheepRanges(
                        out anim.IdleStart, out anim.IdleCount,
                        out anim.MoveStart, out anim.MoveCount,
                        out anim.DeathStart, out anim.DeathCount);
                }

                AddComponent(entity, anim);
            }
        }
    }

    public enum CrowdSpecies : byte
    {
        Sheep = 0,
        Wolf = 1
    }

    public class CrowdLibraryAuthoring : MonoBehaviour
    {
        public CrowdLibraryAsset Library;
    }
}