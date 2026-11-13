using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals.Editor
{
    // V26: lighter meshes for the Web build (Role 3), from the performance audit of 13 Nov (Web .data 175 MB, 52 MB meshes).
    // Runs at the end of CampusWorldBuilder.BuildScene, after the hall, the fence-free cut and the grass tufts were made,
    // so it never changes how those builders work, only how their meshes are stored:
    // - generated meshes lose the vertices no triangle uses any more (the fence trim and the hall cut only drop triangles),
    //   colours are stored as half floats (16 -> 8 bytes; the tuft tint goes above 1, so Color32 would clip it),
    //   and indices are 16 bit when the mesh has fewer than 65536 vertices;
    // - the grass tufts also store both UV sets as half floats (all values 0..1) and drop the CPU copy (nothing reads them);
    // - objects the builders switched off for good (old building block, old atrium furniture and railings, the hall's
    //   lilac rails) are tagged EditorOnly: they stay in the editor scene but no longer ship in the player.
    // Noah's hall model is imported without tangents: its materials have no normal maps.
    public static class WebMeshBudget
    {
        private const string Root = "Assets/_Project/Visuals/CampusWorld";
        private const string HallModel = Root + "/Models/NovaHallWorld.fbx";
        private const string GrassMeshes = Root + "/Models/GrassTufts.asset";
        private const string CutMeshDir = Root + "/Generated/NovaHallCuts";
        private const string EditorOnlyTag = "EditorOnly";

        // Names the builders switch off for good (NovaHallInsert: BuildingId, Passages, HallRails, RailingProto, DressingRoot).
        private const string OldBlock = "w665606788";
        private const string OldPassages = "Interior_Passages";
        private const string HallRails = "Hall_Track_Rails";
        private const string RailingProto = "Prop_RailingSegment";
        private const string DressingRoot = "Dressing";

        // Called first in CampusWorldBuilder.Build, before the hall is placed.
        public static void ConfigureHallImporter()
        {
            if (!(AssetImporter.GetAtPath(HallModel) is ModelImporter mi))
            {
                return;
            }
            bool changed = mi.importTangents != ModelImporterTangents.None || mi.importAnimation || mi.importCameras ||
                           mi.importLights || mi.importBlendShapes || mi.animationType != ModelImporterAnimationType.None;
            if (!changed)
            {
                return;
            }
            mi.importTangents = ModelImporterTangents.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.SaveAndReimport();
        }

        // Called in CampusWorldBuilder.BuildScene just before the world prefab is saved. Returns a line for the build log.
        public static string Finish(GameObject worldRoot)
        {
            int editorOnly = TagSwitchedOff(worldRoot);
            long before = 0, after = 0;
            int meshes = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { CutMeshDir }))
            {
                foreach (Mesh m in MeshesAt(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    before += Bytes(m);
                    Repack(m, false);
                    after += Bytes(m);
                    meshes++;
                }
            }
            foreach (Mesh m in MeshesAt(GrassMeshes))
            {
                before += Bytes(m);
                Repack(m, true);
                after += Bytes(m);
                SetReadable(m, false);
                meshes++;
            }
            AssetDatabase.SaveAssets();
            return "WebMeshBudget: " + meshes + " generated meshes " + (before / 1024) + " KB -> " + (after / 1024) + " KB, " +
                   editorOnly + " switched-off objects tagged EditorOnly";
        }

        private static int TagSwitchedOff(GameObject worldRoot)
        {
            int tagged = 0;
            foreach (Transform t in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.activeSelf || t.CompareTag(EditorOnlyTag))
                {
                    continue;
                }
                bool oldBlock = t.name.Contains(OldBlock) || t.name == OldPassages;
                bool rails = t.name == HallRails;
                bool oldRailing = t.parent != null && t.parent.name == RailingProto;
                bool oldProp = t.parent != null && t.parent.parent != null && t.parent.parent.name == DressingRoot;
                if (oldBlock || rails || oldRailing || oldProp)
                {
                    t.gameObject.tag = EditorOnlyTag;
                    tagged++;
                }
            }
            return tagged;
        }

        private static IEnumerable<Mesh> MeshesAt(string path)
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Mesh m)
                {
                    yield return m;
                }
            }
        }

        // Approximate stored size: vertex buffer plus index buffer.
        public static long Bytes(Mesh m)
        {
            long vb = 0;
            for (int s = 0; s < m.vertexBufferCount; s++)
            {
                vb += (long)m.GetVertexBufferStride(s) * m.vertexCount;
            }
            long ib = 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                ib += (long)m.GetSubMesh(s).indexCount * (m.indexFormat == IndexFormat.UInt16 ? 2 : 4);
            }
            return vb + ib;
        }

        // Rebuilds the mesh in place (same asset, so every reference stays) with only the used vertices and a lean layout.
        public static void Repack(Mesh m, bool halfUv)
        {
            int n = m.vertexCount;
            int subs = m.subMeshCount;
            var indices = new int[subs][];
            var topology = new MeshTopology[subs];
            var remap = new int[n];
            for (int i = 0; i < n; i++)
            {
                remap[i] = -1;
            }
            int used = 0;
            for (int s = 0; s < subs; s++)
            {
                indices[s] = m.GetIndices(s);
                topology[s] = m.GetTopology(s);
                foreach (int i in indices[s])
                {
                    if (remap[i] < 0)
                    {
                        remap[i] = used++;
                    }
                }
            }

            Vector3[] pos = m.vertices;
            Vector3[] nor = m.HasVertexAttribute(VertexAttribute.Normal) ? m.normals : null;
            Vector4[] tan = m.HasVertexAttribute(VertexAttribute.Tangent) ? m.tangents : null;
            Color[] col = m.HasVertexAttribute(VertexAttribute.Color) ? m.colors : null;
            var uvs = new List<Vector4>[4];
            var uvDim = new int[4];
            for (int ch = 0; ch < 4; ch++)
            {
                VertexAttribute a = VertexAttribute.TexCoord0 + ch;
                if (m.HasVertexAttribute(a))
                {
                    uvDim[ch] = m.GetVertexAttributeDimension(a);
                    uvs[ch] = new List<Vector4>();
                    m.GetUVs(ch, uvs[ch]);
                }
            }

            var attrs = new List<VertexAttributeDescriptor> { new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3) };
            if (nor != null)
            {
                attrs.Add(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3));
            }
            if (tan != null)
            {
                attrs.Add(new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4));
            }
            if (col != null)
            {
                attrs.Add(new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4));
            }
            var uvHalf = new bool[4];
            for (int ch = 0; ch < 4; ch++)
            {
                if (uvs[ch] == null)
                {
                    continue;
                }
                uvHalf[ch] = halfUv && uvDim[ch] % 2 == 0;           // half floats come in pairs
                attrs.Add(new VertexAttributeDescriptor(VertexAttribute.TexCoord0 + ch,
                    uvHalf[ch] ? VertexAttributeFormat.Float16 : VertexAttributeFormat.Float32, uvDim[ch]));
            }
            int stride = 0;
            foreach (VertexAttributeDescriptor a in attrs)
            {
                stride += a.dimension * (a.format == VertexAttributeFormat.Float16 ? 2 : 4);
            }

            // One interleaved stream in the order of attrs (the order Unity requires: position, normal, tangent, colour, uv0..).
            var data = new byte[used * stride];
            for (int i = 0; i < n; i++)
            {
                int j = remap[i];
                if (j < 0)
                {
                    continue;
                }
                int o = j * stride;
                o = Floats(data, o, pos[i].x, pos[i].y, pos[i].z);
                if (nor != null)
                {
                    o = Floats(data, o, nor[i].x, nor[i].y, nor[i].z);
                }
                if (tan != null)
                {
                    o = Floats(data, o, tan[i].x, tan[i].y, tan[i].z, tan[i].w);
                }
                if (col != null)
                {
                    o = Halves(data, o, col[i].r, col[i].g, col[i].b, col[i].a);
                }
                for (int ch = 0; ch < 4; ch++)
                {
                    if (uvs[ch] == null)
                    {
                        continue;
                    }
                    Vector4 u = uvs[ch][i];
                    float[] c = { u.x, u.y, u.z, u.w };
                    var part = new float[uvDim[ch]];
                    Array.Copy(c, part, uvDim[ch]);
                    o = uvHalf[ch] ? Halves(data, o, part) : Floats(data, o, part);
                }
            }

            Bounds bounds = m.bounds;
            m.Clear();
            m.indexFormat = used <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            m.SetVertexBufferParams(used, attrs.ToArray());
            m.SetVertexBufferData(data, 0, 0, data.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
            m.subMeshCount = subs;
            for (int s = 0; s < subs; s++)
            {
                int[] idx = indices[s];
                var mapped = new int[idx.Length];
                for (int k = 0; k < idx.Length; k++)
                {
                    mapped[k] = remap[idx[k]];
                }
                m.SetIndices(mapped, topology[s], s, false);
            }
            m.RecalculateBounds();
            if (m.vertexCount == 0)
            {
                m.bounds = bounds;
            }
            EditorUtility.SetDirty(m);
        }

        private static void SetReadable(Mesh m, bool readable)
        {
            var so = new SerializedObject(m);
            SerializedProperty p = so.FindProperty("m_IsReadable");
            if (p != null && p.boolValue != readable)
            {
                p.boolValue = readable;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static int Floats(byte[] data, int o, params float[] values)
        {
            foreach (float f in values)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(f), 0, data, o, 4);
                o += 4;
            }
            return o;
        }

        private static int Halves(byte[] data, int o, params float[] values)
        {
            foreach (float f in values)
            {
                ushort h = Mathf.FloatToHalf(f);
                data[o] = (byte)(h & 0xff);
                data[o + 1] = (byte)(h >> 8);
                o += 2;
            }
            return o;
        }
    }
}
