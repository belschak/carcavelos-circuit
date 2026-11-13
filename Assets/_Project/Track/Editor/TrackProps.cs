using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // V22w (Luc's playtest of 13 Nov 2026): the track props made by tools/props/make_track_props.py
    // (tyre stack, Jersey barrier, block wall + its orange cap, kerb strip). They are our own low-poly models,
    // all drawn with one material and one 256 px texture (Track/Materials/TrackProps.mat, Track/Models/TrackProps.png).
    //  - Setup() gives the models and the texture their import settings and hooks the material up. Run it once
    //    after the Blender script made new files (menu "Carcavelos Circuit/Set Up Track Props").
    //  - TyreWall is the row of tyre stacks that WhiteGuardrailBuilder puts along a barrier line instead of the wall.
    public static class TrackProps
    {
        public const string Folder = "Assets/_Project/Track/Models";
        // In Track/Materials, because CampusWorldBuilder's self-test only accepts world materials from there.
        private const string MaterialPath = "Assets/_Project/Track/Materials/TrackProps.mat";
        private const string TexturePath = Folder + "/TrackProps.png";
        private const string TyreStackPath = Folder + "/TyreStack.fbx";
        private static readonly string[] Models = { "TyreStack", "JerseyBarrier", "BlockWall", "BlockWallCap", "KerbStrip" };

        // TUNING (metres) of the tyre wall.
        public const float TyreRadius = 0.35f;      // of one tyre (as in the Blender script)
        private const float TyreSpacing = 0.58f;    // from stack to stack: well under a tyre (0.70), so no paving shows between them
        public const float TyreInward = 0.12f;      // the stacks stand this far road-side of the barrier line (clear of the U-turn boards behind it)
        private const float TyreStagger = 0.04f;    // every second stack stands this far back, the others this far forward
        private const float TyreSink = 0.01f;       // into the paving, so no stack floats

        [MenuItem("Carcavelos Circuit/Set Up Track Props (Role 3)")]
        public static void Setup()
        {
            TextureImporter texture = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (texture == null)
            {
                Debug.LogError("TrackProps: " + TexturePath + " not found. Run tools/props/make_track_props.py with Blender first.");
                return;
            }
            texture.maxTextureSize = 256;
            texture.mipmapEnabled = true;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;   // the tyre tread runs once round the whole width
            texture.textureCompression = TextureImporterCompression.Uncompressed; // flat colours with hard edges, 256 px: 0.3 MB
            texture.SaveAndReimport();

            Material material = Material();
            int done = 0;
            foreach (string name in Models)
            {
                ModelImporter model = AssetImporter.GetAtPath(Folder + "/" + name + ".fbx") as ModelImporter;
                if (model == null)
                {
                    Debug.LogWarning("TrackProps: " + name + ".fbx not found.");
                    continue;
                }
                model.importCameras = false;
                model.importLights = false;
                model.importAnimation = false;
                model.animationType = ModelImporterAnimationType.None;
                model.importBlendShapes = false;
                model.importTangents = ModelImporterTangents.None;   // no normal map
                model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                // The model's own material "TrackProps" is replaced by our material file.
                model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "TrackProps"), material);
                model.SaveAndReimport();
                done++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("TrackProps: " + done + " of " + Models.Length + " models set up with " + MaterialPath + ".");
        }

        // Created once, then only updated, so its GUID stays the same (the models point at it).
        public static Material Material()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // A row of tyre stacks: copies of the TyreStack model, all in one mesh (one draw call, like the walls).
        public class TyreWall
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private Vector3[] modelVertices, modelNormals;
            private Vector2[] modelUvs;
            private int[] modelTriangles;

            public int VertexCount
            {
                get { return vertices.Count; }
            }

            public int Stacks { get; private set; }

            // Stacks along the line through these points (their feet), evenly spread from end to end.
            // Returns false if the model is missing (then nothing is added).
            public bool AddLine(Vector3[] line)
            {
                if (!LoadModel())
                {
                    return false;
                }
                float length = 0f;
                for (int k = 0; k + 1 < line.Length; k++)
                {
                    length += Vector3.Distance(line[k], line[k + 1]);
                }
                int count = Mathf.Max(1, Mathf.RoundToInt(length / TyreSpacing));
                float step = length / count;
                int piece = 0;
                float pieceStart = 0f; // metres along the line where the current piece starts
                for (int s = 0; s < count; s++)
                {
                    float at = (s + 0.5f) * step;
                    float pieceLength = Vector3.Distance(line[piece], line[piece + 1]);
                    while (piece + 2 < line.Length && at > pieceStart + pieceLength)
                    {
                        pieceStart += pieceLength;
                        piece++;
                        pieceLength = Vector3.Distance(line[piece], line[piece + 1]);
                    }
                    Vector3 along = line[piece + 1] - line[piece];
                    Vector3 foot = Vector3.Lerp(line[piece], line[piece + 1], (at - pieceStart) / Mathf.Max(0.001f, pieceLength));
                    Vector3 sideways = Vector3.Cross(Vector3.up, new Vector3(along.x, 0f, along.z).normalized);
                    foot += sideways * (s % 2 == 0 ? TyreStagger : -TyreStagger) - sideways * TyreInward + Vector3.down * TyreSink; // road = left of the line (outside of a left turn)
                    AddStack(foot, s * 37f); // each stack turned a bit further, so no two look the same
                }
                return true;
            }

            private void AddStack(Vector3 foot, float turnDegrees)
            {
                Quaternion turn = Quaternion.Euler(0f, turnDegrees, 0f);
                int first = vertices.Count;
                for (int k = 0; k < modelVertices.Length; k++)
                {
                    vertices.Add(foot + turn * modelVertices[k]);
                    normals.Add(turn * modelNormals[k]);
                    uvs.Add(modelUvs[k]);
                }
                for (int k = 0; k < modelTriangles.Length; k++)
                {
                    triangles.Add(first + modelTriangles[k]);
                }
                Stacks++;
            }

            // The model's mesh in the model's own space (the file may carry a turn or a scale on its object).
            private bool LoadModel()
            {
                if (modelVertices != null)
                {
                    return true;
                }
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TyreStackPath);
                MeshFilter filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogWarning("TrackProps: " + TyreStackPath + " not found, no tyre wall. Run tools/props/make_track_props.py with Blender.");
                    return false;
                }
                Mesh mesh = filter.sharedMesh;
                Matrix4x4 place = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                modelVertices = mesh.vertices;
                modelNormals = mesh.normals;
                modelUvs = mesh.uv;
                modelTriangles = mesh.triangles;
                if (place.determinant < 0f)
                {
                    System.Array.Reverse(modelTriangles); // a mirrored model would show its inside
                }
                for (int k = 0; k < modelVertices.Length; k++)
                {
                    modelVertices[k] = place.MultiplyPoint3x4(modelVertices[k]);
                    modelNormals[k] = place.MultiplyVector(modelNormals[k]).normalized;
                }
                return true;
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh();
                mesh.name = name;
                mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
