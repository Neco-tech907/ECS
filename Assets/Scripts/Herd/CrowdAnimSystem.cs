using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esc.Herd
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CrowdAnimSystem : SystemBase
    {
        Entity libraryEntity;
        bool ready;

        protected override void OnUpdate()
        {
            if (!ready && !TryRegister())
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            var ids = EntityManager.GetBuffer<CrowdMeshIdElement>(libraryEntity);
            var library = EntityManager.GetComponentData<CrowdLibraryTag>(libraryEntity);

            foreach (var (anim, info, transform, entity) in SystemAPI
                         .Query<RefRW<CrowdAnim>, RefRW<MaterialMeshInfo>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                bool dead = EntityManager.HasComponent<Dead>(entity) && EntityManager.IsComponentEnabled<Dead>(entity);
                bool moving = math.distancesq(transform.ValueRO.Position, anim.ValueRO.PreviousPosition) > 0.0004f;
                bool attack = EntityManager.HasComponent<WolfState>(entity)
                              && EntityManager.GetComponentData<WolfState>(entity).Behavior == WolfBehavior.Attack;

                byte clip = 0;
                int start = anim.ValueRO.IdleStart;
                int count = anim.ValueRO.IdleCount;
                bool hold = false;
                if (dead && anim.ValueRO.DeathCount > 0)
                {
                    clip = 3;
                    start = anim.ValueRO.DeathStart;
                    count = anim.ValueRO.DeathCount;
                    hold = true;
                }
                else if (attack && anim.ValueRO.ActCount > 0)
                {
                    clip = 2;
                    start = anim.ValueRO.ActStart;
                    count = anim.ValueRO.ActCount;
                }
                else if (moving && anim.ValueRO.MoveCount > 0)
                {
                    clip = 1;
                    start = anim.ValueRO.MoveStart;
                    count = anim.ValueRO.MoveCount;
                }

                if (count <= 0)
                    continue;

                if (anim.ValueRO.Clip != clip)
                {
                    anim.ValueRW.Clip = clip;
                    anim.ValueRW.Time = 0f;
                }

                float fps = anim.ValueRO.Fps;
                float duration = count / fps;
                float time = anim.ValueRO.Time + deltaTime;
                if (hold)
                    time = math.min(time, math.max(duration - 0.0001f, 0f));
                else if (duration > 0f)
                    time %= duration;

                anim.ValueRW.Time = time;
                int frame = math.clamp((int)(time * fps), 0, count - 1);
                int meshIndex = start + frame;
                if ((uint)meshIndex >= (uint)ids.Length)
                    continue;

                int materialId = anim.ValueRO.Species == (byte)CrowdSpecies.Wolf
                    ? library.WolfMaterialId
                    : library.SheepMaterialId;
                anim.ValueRW.MaterialId = materialId;
                info.ValueRW = new MaterialMeshInfo(
                    new BatchMaterialID { value = (uint)materialId },
                    new BatchMeshID { value = (uint)ids[meshIndex].Value });
                anim.ValueRW.PreviousPosition = transform.ValueRO.Position;
            }
        }

        bool TryRegister()
        {
            var host = Object.FindAnyObjectByType<CrowdLibraryAuthoring>();
            if (host == null || host.Library == null)
                return false;

            var graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
                return false;

            var meshes = host.Library.BuildMeshList();
            if (meshes.Length == 0 || host.Library.SheepMaterial == null || host.Library.WolfMaterial == null)
                return false;

            libraryEntity = EntityManager.CreateEntity(typeof(CrowdLibraryTag));
            var buffer = EntityManager.AddBuffer<CrowdMeshIdElement>(libraryEntity);
            for (int i = 0; i < meshes.Length; i++)
            {
                BatchMeshID meshId = graphics.RegisterMesh(meshes[i]);
                buffer.Add(new CrowdMeshIdElement { Value = (int)meshId.value });
            }

            EntityManager.SetComponentData(libraryEntity, new CrowdLibraryTag
            {
                SheepMaterialId = (int)graphics.RegisterMaterial(host.Library.SheepMaterial).value,
                WolfMaterialId = (int)graphics.RegisterMaterial(host.Library.WolfMaterial).value,
                Ready = true
            });
            ready = true;
            return true;
        }
    }
}