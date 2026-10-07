using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Esc.Herd.Editor
{
    public static class CrowdPoseBaker
    {
        const string OutputFolder = "Assets/Models/Baked";
        const string LibraryPath = "Assets/Models/Baked/CrowdLibrary.asset";

        public static string BakeAll()
        {
            Directory.CreateDirectory(OutputFolder);
            var sheep = BakeCharacter(
                "Assets/UrsaAnimation/LOW POLY CUBIC - Goat and Sheep Pack/Prefabs_URP/SK_Sheep_white.prefab",
                "Sheep",
                0.95f,
                new[]
                {
                    ("Idle", "Assets/UrsaAnimation/LOW POLY CUBIC - Goat and Sheep Pack/Animation/Goat&Sheep/GoatSheep_Idle.fbx", 4),
                    ("Walk", "Assets/UrsaAnimation/LOW POLY CUBIC - Goat and Sheep Pack/Animation/Goat&Sheep/GoatSheep_Walk_Forward.fbx", 8),
                    ("Death", "Assets/UrsaAnimation/LOW POLY CUBIC - Goat and Sheep Pack/Animation/Goat&Sheep/GoatSheep_Death.fbx", 6)
                });
            var wolf = BakeCharacter(
                "Assets/Polygonal Wolf/Prefabs/Polygonal Wolf Brown.prefab",
                "Wolf",
                1.15f,
                new[]
                {
                    ("Idle", "Assets/Polygonal Wolf/FBX/Polygonal Wolf@Idle.FBX", 4),
                    ("Run", "Assets/Polygonal Wolf/FBX/Polygonal Wolf@Walk Forward WO Root.FBX", 8),
                    ("Attack", "Assets/Polygonal Wolf/FBX/Polygonal Wolf@Bite Attack.FBX", 6),
                    ("Death", "Assets/Polygonal Wolf/FBX/Polygonal Wolf@Die.FBX", 6)
                });

            var library = AssetDatabase.LoadAssetAtPath<CrowdLibraryAsset>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CrowdLibraryAsset>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.SheepIdle = sheep.Clips[0];
            library.SheepWalk = sheep.Clips[1];
            library.SheepDeath = sheep.Clips[2];
            library.WolfIdle = wolf.Clips[0];
            library.WolfRun = wolf.Clips[1];
            library.WolfAttack = wolf.Clips[2];
            library.WolfDeath = wolf.Clips[3];
            library.SheepMaterial = sheep.Material;
            library.WolfMaterial = wolf.Material;
            library.Fps = 12f;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return "sheep=" + sheep.Summary + " wolf=" + wolf.Summary;
        }

        struct BakedCharacter
        {
            public Mesh[][] Clips;
            public Material Material;
            public string Summary;
        }

        static BakedCharacter BakeCharacter(string prefabPath, string prefix, float targetHeight, (string Name, string ClipPath, int Frames)[] clips)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = Object.Instantiate(prefab);
            instance.name = prefix + "_Bake";
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;

            float scale = 1f;
            var probeClip = LoadClip(clips[0].ClipPath);
            if (probeClip != null)
            {
                Sample(instance, probeClip, 0f, 1f);
                var probe = CombineSkinned(instance);
                float height = probe.bounds.size.y;
                if (height > 0.001f)
                    scale = targetHeight / height;
                Object.DestroyImmediate(probe);
            }

            var material = FirstMaterial(instance);
            var bakedClips = new Mesh[clips.Length][];
            var summary = new System.Text.StringBuilder();
            for (int c = 0; c < clips.Length; c++)
            {
                var clip = LoadClip(clips[c].ClipPath);
                int frames = clips[c].Frames;
                bakedClips[c] = new Mesh[frames];
                for (int f = 0; f < frames; f++)
                {
                    float time = clip == null || frames == 1 ? 0f : clip.length * f / frames;
                    if (clip != null)
                        Sample(instance, clip, time, scale);
                    var mesh = CombineSkinned(instance);
                    PlantFeet(mesh);
                    mesh.name = prefix + "_" + clips[c].Name + "_" + f;
                    string path = OutputFolder + "/" + mesh.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing != null)
                        AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(mesh, path);
                    bakedClips[c][f] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                }

                var bounds = bakedClips[c][0].bounds.size;
                summary.Append(clips[c].Name).Append(bounds.x.ToString("0.00")).Append("x").Append(bounds.y.ToString("0.00")).Append(" ");
            }

            Object.DestroyImmediate(instance);
            return new BakedCharacter { Clips = bakedClips, Material = material, Summary = summary.ToString() };
        }

        static void Sample(GameObject root, AnimationClip clip, float time, float scale)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * scale;
            clip.SampleAnimation(root, time);
        }

        static Mesh CombineSkinned(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var combines = new List<CombineInstance>();
            var temps = new List<Mesh>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh == null)
                    continue;

                var baked = new Mesh();
                renderer.BakeMesh(baked);
                temps.Add(baked);
                combines.Add(new CombineInstance
                {
                    mesh = baked,
                    transform = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                });
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            if (combines.Count > 0)
                mesh.CombineMeshes(combines.ToArray(), true, true);
            foreach (var temp in temps)
                Object.DestroyImmediate(temp);
            return mesh;
        }

        static void PlantFeet(Mesh mesh)
        {
            var bounds = mesh.bounds;
            var shift = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= shift;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        static Material FirstMaterial(GameObject root)
        {
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        static AnimationClip LoadClip(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            AnimationClip fallback = null;
            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview"))
                    continue;
                if (fallback == null || clip.length > fallback.length)
                    fallback = clip;
            }

            return fallback;
        }
    }
}
