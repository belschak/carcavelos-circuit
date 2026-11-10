using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // One NovaMesh per material. At the end every mesh becomes one static GameObject (few draw calls on the Web).
    public class NovaBatch
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<NovaMesh> meshes = new List<NovaMesh>();
        private readonly string name;
        private readonly bool withCollider;
        private readonly bool castShadows;

        public NovaBatch(string name, bool withCollider, bool castShadows)
        {
            this.name = name;
            this.withCollider = withCollider;
            this.castShadows = castShadows;
        }

        public NovaMesh For(Material material)
        {
            int index = materials.IndexOf(material);
            if (index < 0)
            {
                materials.Add(material);
                meshes.Add(new NovaMesh());
                index = materials.Count - 1;
            }
            return meshes[index];
        }

        // Creates the GameObjects under "parent" and saves each mesh as an asset (scenes cannot hold big generated meshes well).
        public void Flush(Transform parent, string meshFolder)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            for (int i = 0; i < materials.Count; i++)
            {
                if (meshes[i].VertexCount == 0)
                {
                    continue;
                }
                string meshName = name + "_" + materials[i].name;
                Mesh mesh = meshes[i].ToMesh(meshName);
                AssetDatabase.CreateAsset(mesh, meshFolder + meshName + ".asset");

                GameObject go = new GameObject(meshName);
                go.transform.SetParent(group.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = materials[i];
                renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (withCollider)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
