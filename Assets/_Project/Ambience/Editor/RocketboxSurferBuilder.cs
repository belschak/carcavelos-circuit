using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Ambience.Editor
{
    // Turns the downloaded Microsoft Rocketbox avatars (MIT licence, files in Ambience/ThirdParty/Rocketbox, row in
    // THIRD_PARTY.md) into surfer prefabs and puts them into AmbienceSettings > Surfer Models.
    // Per avatar: keeps one detail level of the body (see KeepOneDetailLevel), paints a
    // wetsuit onto its body picture (slate suit, a coloured chest and shoulder panel, bare hands and feet; the
    // colour of each spot is chosen by the bone that moves it), makes URP materials, and adds a SurferModelPuppet
    // with its bones. The Rocketbox import script from the download is NOT used: this is our own code.
    // Called by AmbienceBuilder (so Role4SceneBuilder.BuildAll keeps everything in place); builds only what is
    // missing. Rebuild() makes everything again (e.g. after changing a colour here).
    public static class RocketboxSurferBuilder
    {
        public const string Folder = "Assets/_Project/Ambience/ThirdParty/Rocketbox";

        // One entry per avatar: folder (= file name), texture name start, wetsuit and panel colour.
        // Only the two barefoot "Sports" avatars: the others wear football boots or trainers.
        private static readonly string[] Avatars = { "Sports_Male_01", "Sports_Female_01" };
        private static readonly string[] TexturePrefix = { "m021", "f021" };
        private static readonly Color[] SuitColours = { new Color(0.24f, 0.29f, 0.35f), new Color(0.2f, 0.27f, 0.4f) };
        private static readonly Color[] PanelColours = { new Color(0.902f, 0.275f, 0.235f), new Color(0.247f, 0.75f, 0.55f) };

        private const byte Untouched = 0, Suit = 1, Panel = 2, Skin = 3;

        public static void Rebuild()
        {
            Build(true);
        }

        public static void EnsureBuilt()
        {
            Build(false);
        }

        private static void Build(bool force)
        {
            var made = new GameObject[Avatars.Length];
            for (int i = 0; i < Avatars.Length; i++)
            {
                made[i] = BuildOne(i, force);
                if (made[i] == null)
                {
                    Debug.Log("RocketboxSurferBuilder: " + Avatars[i] + " could not be built, the surfers stay built in code.");
                    made = new GameObject[0];
                    break;
                }
            }
            AmbienceSettings settings = AmbienceBuilder.CreateSettings();
            var so = new SerializedObject(settings);
            SerializedProperty models = so.FindProperty("surferModels");
            models.arraySize = made.Length;
            for (int i = 0; i < made.Length; i++)
            {
                models.GetArrayElementAtIndex(i).objectReferenceValue = made[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildOne(int i, bool force)
        {
            string name = Avatars[i];
            string dir = Folder + "/" + name;
            string prefabPath = Folder + "/Surfer_" + name + ".prefab";
            string bodyPath = dir + "/Textures/" + TexturePrefix[i] + "_body_color.jpg";
            string headPath = dir + "/Textures/" + TexturePrefix[i] + "_head_color.jpg";
            string suitPath = dir + "/Textures/" + TexturePrefix[i] + "_body_wetsuit.jpg";
            string collarPath = dir + "/Textures/" + TexturePrefix[i] + "_head_wetsuit.jpg";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null && !force)
            {
                return existing;
            }
            if (!SetUpImport(dir + "/" + name + ".fbx"))
            {
                return null;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + name + ".fbx");
            if (model == null || !File.Exists(bodyPath) || !File.Exists(headPath))
            {
                return null;
            }
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                SkinnedMeshRenderer body = KeepOneDetailLevel(copy, "lowpoly");
                if (body == null)
                {
                    return null;
                }
                if (force || !File.Exists(suitPath) || !File.Exists(collarPath))
                {
                    // The head part of the mesh also holds the neck and the top of the chest: the collar and the
                    // top of the panel are painted there.
                    PaintWetsuit(body, bodyPath, suitPath, SuitColours[i], PanelColours[i], false);
                    PaintWetsuit(body, headPath, collarPath, SuitColours[i], PanelColours[i], true);
                    AssetDatabase.ImportAsset(suitPath);
                    AssetDatabase.ImportAsset(collarPath);
                }
                var suitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(suitPath);
                var headTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(collarPath);
                Material suit = MakeMaterial(dir + "/" + name + "_Wetsuit.mat", suitTexture, 0.42f);
                Material head = MakeMaterial(dir + "/" + name + "_Head.mat", headTexture, 0.2f);
                var materials = new Material[body.sharedMesh.subMeshCount];
                for (int s = 0; s < materials.Length; s++)
                {
                    materials[s] = IsHeadPart(body, s) ? head : suit;
                }
                body.sharedMaterials = materials;
                body.updateWhenOffscreen = true;            // the pose changes a lot (lying, standing): bounds follow
                body.shadowCastingMode = ShadowCastingMode.On;
                body.receiveShadows = false;
                body.lightProbeUsage = LightProbeUsage.Off;
                body.reflectionProbeUsage = ReflectionProbeUsage.Off;
                WirePuppet(copy);
                copy.name = "Surfer_" + name;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
                Debug.Log("RocketboxSurferBuilder: " + prefabPath + " with " + body.name + ", " + body.sharedMesh.triangles.Length / 3 + " triangles, " + body.bones.Length + " bones.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        // Rig and import settings: skeleton only (no animation clips, cameras, lights or face shapes), no materials
        // from the file (they are 3ds Max materials; ours are URP, made below).
        private static bool SetUpImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.Log("RocketboxSurferBuilder: no model at " + path + ".");
                return false;
            }
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None
                           || importer.animationType != ModelImporterAnimationType.Generic
                           || importer.importAnimation || importer.importCameras || importer.importLights
                           || importer.importBlendShapes || importer.optimizeGameObjects;
            if (changed)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
            }
            return true;
        }

        // A Rocketbox file can hold 4 detail levels (hipoly, midpoly, lowpoly, ultralowpoly): keep the wanted one and
        // drop the rest. The "Export" files in the repo hold only one level (hipoly, about 7,400 triangles): then that one.
        private static SkinnedMeshRenderer KeepOneDetailLevel(GameObject copy, string level)
        {
            SkinnedMeshRenderer[] all = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer kept = null;
            for (int r = 0; r < all.Length && kept == null; r++)
            {
                string lower = all[r].name.ToLowerInvariant();
                if (lower.Contains(level) && !lower.Contains("ultra" + level))
                {
                    kept = all[r];
                }
            }
            bool levelFound = kept != null;
            for (int r = 0; r < all.Length && !levelFound; r++)
            {
                if (kept == null || all[r].sharedMesh.triangles.Length < kept.sharedMesh.triangles.Length)
                {
                    kept = all[r];               // no such level: the smallest one there is
                }
            }
            for (int r = 0; r < all.Length; r++)
            {
                Debug.Log("RocketboxSurferBuilder: mesh " + all[r].name + " (" + all[r].sharedMesh.triangles.Length / 3 + " triangles)" + (all[r] == kept ? " kept" : " dropped"));
                if (all[r] != kept)
                {
                    Object.DestroyImmediate(all[r].gameObject);
                }
            }
            if (kept != null)
            {
                kept.gameObject.SetActive(true);
            }
            return kept;
        }

        private static bool IsHeadPart(SkinnedMeshRenderer body, int subMesh)
        {
            int[] triangles = body.sharedMesh.GetTriangles(subMesh);
            BoneWeight[] weights = body.sharedMesh.boneWeights;
            int head = 0;
            for (int t = 0; t < triangles.Length; t++)
            {
                if (BoneName(body, weights[triangles[t]]).Contains("Head"))
                {
                    head++;
                }
            }
            return head * 2 > triangles.Length;
        }

        private static string BoneName(SkinnedMeshRenderer body, BoneWeight weight)
        {
            Transform[] bones = body.bones;
            int index = weight.boneIndex0;
            return index >= 0 && index < bones.Length && bones[index] != null ? bones[index].name : "";
        }

        // What a spot becomes, from the bone that moves it most. The neck gets the suit too (a wetsuit has a high
        // collar); on the head part everything but neck and chest (face, hair, the face bones) keeps the photo.
        private static byte KindOf(string bone, bool headPart)
        {
            if (headPart && !bone.Contains("Neck") && !bone.Contains("Spine") && !bone.Contains("Clavicle"))
            {
                return Skin;                                // face and hair (Head and the face bones): keep the photo
            }
            if (bone.Contains("Hand") || bone.Contains("Finger") || bone.Contains("Foot") || bone.Contains("Toe"))
            {
                return Skin;
            }
            if (bone.Contains("Spine2") || bone.Contains("Clavicle"))
            {
                return Panel;
            }
            return Suit;
        }

        // Paints the wetsuit onto the body picture: every triangle of the body is filled in a map with its kind
        // (suit, panel, skin), the map is grown a few pixels over the seams, then suit and panel spots get the suit
        // colour, keeping the light and dark of the photo so folds and muscles still show.
        // headPart = paint the head picture (only its neck and chest spots; the face and hair, moved by the Head
        // bone, stay as they are), else the body picture.
        private static void PaintWetsuit(SkinnedMeshRenderer body, string sourcePath, string outPath, Color suit, Color panel, bool headPart)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            int w = source.width, h = source.height;
            Color32[] pixels = source.GetPixels32();
            var kind = new byte[w * h];
            Mesh mesh = body.sharedMesh;
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (IsHeadPart(body, s) != headPart)
                {
                    continue;
                }
                int[] triangles = mesh.GetTriangles(s);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    byte a = KindOf(BoneName(body, weights[triangles[t]]), headPart);
                    byte b = KindOf(BoneName(body, weights[triangles[t + 1]]), headPart);
                    byte c = KindOf(BoneName(body, weights[triangles[t + 2]]), headPart);
                    byte k = b == c ? b : a;                // the kind most corners agree on
                    Fill(kind, w, h, uv[triangles[t]], uv[triangles[t + 1]], uv[triangles[t + 2]], k);
                }
            }
            for (int pass = 0; pass < 4; pass++)
            {
                Grow(kind, w, h);
            }
            double sum = 0;
            int count = 0;
            for (int p = 0; p < pixels.Length; p++)
            {
                if (kind[p] == Suit || kind[p] == Panel)
                {
                    sum += Brightness(pixels[p]);
                    count++;
                }
            }
            float mean = count > 0 ? (float)(sum / count) : 0.5f;
            for (int p = 0; p < pixels.Length; p++)
            {
                if (kind[p] != Suit && kind[p] != Panel)
                {
                    continue;
                }
                float shade = Mathf.Lerp(1f, Mathf.Clamp(Brightness(pixels[p]) / Mathf.Max(0.05f, mean), 0.55f, 1.35f), 0.6f);
                Color colour = (kind[p] == Panel ? panel : suit) * shade;
                colour.a = 1f;
                pixels[p] = colour;
            }
            var result = new Texture2D(w, h, TextureFormat.RGB24, false);
            result.SetPixels32(pixels);
            result.Apply();
            File.WriteAllBytes(outPath, result.EncodeToJPG(90));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        private static float Brightness(Color32 c)
        {
            return (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        }

        // Fills one triangle (in picture coordinates 0..1) into the kind map.
        private static void Fill(byte[] kind, int w, int h, Vector2 a, Vector2 b, Vector2 c, byte k)
        {
            Vector2 pa = new Vector2(a.x * w, a.y * h), pb = new Vector2(b.x * w, b.y * h), pc = new Vector2(c.x * w, c.y * h);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
            float area = Cross(pb - pa, pc - pa);
            if (Mathf.Abs(area) < 0.000001f)
            {
                return;
            }
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float u = Cross(pc - pb, p - pb) / area;
                    float v = Cross(pa - pc, p - pc) / area;
                    float wgt = 1f - u - v;
                    if (u >= -0.01f && v >= -0.01f && wgt >= -0.01f)
                    {
                        kind[y * w + x] = k;
                    }
                }
            }
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // Every empty pixel next to a filled one takes its kind (so the seams of the picture get painted too).
        private static void Grow(byte[] kind, int w, int h)
        {
            var copy = (byte[])kind.Clone();
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int p = y * w + x;
                    if (copy[p] != Untouched)
                    {
                        continue;
                    }
                    byte next = copy[p - 1] != Untouched ? copy[p - 1] : copy[p + 1] != Untouched ? copy[p + 1] : copy[p - w] != Untouched ? copy[p - w] : copy[p + w];
                    kind[p] = next;
                }
            }
        }

        private static Material MakeMaterial(string path, Texture2D texture, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Finds the Rocketbox skeleton bones (3ds Max "Biped" names) and gives them to a new SurferModelPuppet.
        private static void WirePuppet(GameObject copy)
        {
            SurferModelPuppet puppet = copy.AddComponent<SurferModelPuppet>();
            var so = new SerializedObject(puppet);
            string[] fields = { "pelvis", "spine", "neck", "head", "upperArmL", "forearmL", "handL", "upperArmR", "forearmR", "handR",
                                "thighL", "calfL", "footL", "toeL", "thighR", "calfR", "footR", "toeR" };
            string[] bones = { "Pelvis", "Spine", "Neck", "Head", "L UpperArm", "L Forearm", "L Hand", "R UpperArm", "R Forearm", "R Hand",
                               "L Thigh", "L Calf", "L Foot", "L Toe0", "R Thigh", "R Calf", "R Foot", "R Toe0" };
            for (int f = 0; f < fields.Length; f++)
            {
                Transform bone = FindBone(copy.transform, "Bip01 " + bones[f]);
                if (bone == null)
                {
                    bone = FindBone(copy.transform, "Bip02 " + bones[f]);
                }
                if (bone == null)
                {
                    Debug.Log("RocketboxSurferBuilder: bone " + bones[f] + " not found, the model will only be moved, not posed.");
                }
                so.FindProperty(fields[f]).objectReferenceValue = bone;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName)
            {
                return root;
            }
            for (int c = 0; c < root.childCount; c++)
            {
                Transform found = FindBone(root.GetChild(c), boneName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
