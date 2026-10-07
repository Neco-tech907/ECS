using System.Collections.Generic;
using UnityEngine;

namespace Esc.Herd
{
    public class CrowdLibraryAsset : ScriptableObject
    {
        public Mesh[] SheepIdle = System.Array.Empty<Mesh>();
        public Mesh[] SheepWalk = System.Array.Empty<Mesh>();
        public Mesh[] SheepDeath = System.Array.Empty<Mesh>();
        public Mesh[] WolfIdle = System.Array.Empty<Mesh>();
        public Mesh[] WolfRun = System.Array.Empty<Mesh>();
        public Mesh[] WolfAttack = System.Array.Empty<Mesh>();
        public Mesh[] WolfDeath = System.Array.Empty<Mesh>();
        public Material SheepMaterial;
        public Material WolfMaterial;
        public float Fps = 12f;

        public Mesh[] BuildMeshList()
        {
            var meshes = new List<Mesh>();
            meshes.AddRange(SheepIdle);
            meshes.AddRange(SheepWalk);
            meshes.AddRange(SheepDeath);
            meshes.AddRange(WolfIdle);
            meshes.AddRange(WolfRun);
            meshes.AddRange(WolfAttack);
            meshes.AddRange(WolfDeath);
            return meshes.ToArray();
        }

        public void GetSheepRanges(out int idleStart, out int idleCount, out int moveStart, out int moveCount, out int deathStart, out int deathCount)
        {
            idleStart = 0;
            idleCount = SheepIdle.Length;
            moveStart = idleCount;
            moveCount = SheepWalk.Length;
            deathStart = moveStart + moveCount;
            deathCount = SheepDeath.Length;
        }

        public void GetWolfRanges(out int idleStart, out int idleCount, out int moveStart, out int moveCount, out int actStart, out int actCount, out int deathStart, out int deathCount)
        {
            GetSheepRanges(out _, out int sheepIdle, out _, out int sheepWalk, out _, out int sheepDeath);
            int offset = sheepIdle + sheepWalk + sheepDeath;
            idleStart = offset;
            idleCount = WolfIdle.Length;
            moveStart = idleStart + idleCount;
            moveCount = WolfRun.Length;
            actStart = moveStart + moveCount;
            actCount = WolfAttack.Length;
            deathStart = actStart + actCount;
            deathCount = WolfDeath.Length;
        }
    }
}