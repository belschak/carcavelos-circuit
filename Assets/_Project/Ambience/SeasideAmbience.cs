using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Ambience
{
    // Task R12: waves rolling onto Carcavelos beach and a few surfers riding them, so the seaside part of the lap
    // feels alive. Lives on the "Ambience" object in Main.unity (made by the Role 4 scene builder). When a track scene
    // is loaded it looks for the sea in it READ ONLY (Luc's scene is never changed), measures the beach next to the
    // racing line and puts its own meshes under this object: all waves (water layer + foam lace layer, the same
    // points drawn twice) and all surfers (one opaque mesh). Three draw calls in total. No sea or no beach found =
    // one log line and nothing else, the race never depends on this.
    public class SeasideAmbience : MonoBehaviour
    {
        [SerializeField, Tooltip("Every number of the waves and surfers (Ambience/Settings/AmbienceSettings.asset)")]
        private AmbienceSettings settings;

        [SerializeField, Tooltip("Transparent vertex-colour material for the waves (made by AmbienceBuilder)")]
        private Material waveMaterial;

        [SerializeField, Tooltip("Transparent material for the foam lace on top of the waves; its picture is made in code at the start (made by AmbienceBuilder)")]
        private Material foamMaterial;

        [SerializeField, Tooltip("Lit vertex-colour material for the surfers and boards (made by AmbienceBuilder)")]
        private Material surferMaterial;

        private WaveField field;
        private BeachLine beach;
        private WaveMeshWriter waveWriter;
        private Mesh waveMesh;
        private Mesh foamMesh;
        private Texture2D foamTexture;
        private GameObject foamObject;
        private Mesh surferMesh;
        private SurferBrain[] surfers;
        private Vector3[] surferVertices;
        private Vector3[] surferNormals;
        private Color32[] surferColours;
        private readonly Vector3[] joints = new Vector3[SurferBody.JointCount];
        private SurferTuning surferTuning;
        private GameObject waveObject;
        private GameObject surferObject;
        private string seaSceneName = "";
        private bool scanPending;

        // True once waves and surfers are placed on a beach.
        public bool IsPlaced { get { return beach != null; } }
        public BeachLine Beach { get { return beach; } }
        public WaveField Waves { get { return field; } }
        public int SurferCount { get { return surfers != null ? surfers.Length : 0; } }

        // Surfer i (read it, e.g. for a camera or a test; null if not placed).
        public SurferBrain Surfer(int i)
        {
            return surfers != null && i >= 0 && i < surfers.Length ? surfers[i] : null;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            scanPending = true;           // a track scene may already be open (Editor with both scenes open)
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            scanPending = true;           // look on the next frame, when the new scene's colliders are ready
        }

        // RaceBootstrap closes track scenes without a track; the waves must not stay behind on a beach that is gone.
        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == seaSceneName)
            {
                Clear();
                scanPending = true;
            }
        }

        private void Update()
        {
            if (scanPending)
            {
                scanPending = false;
                if (!IsPlaced)
                {
                    PlaceInLoadedScenes();
                }
            }
            if (IsPlaced)
            {
                // Game time: a pause (time scale 0) freezes the sea, the finish slow motion slows it down.
                Simulate(Time.time, Time.deltaTime);
            }
        }

        // Looks through every open scene except Main for the sea. Also called by the local QA render script.
        public void PlaceInLoadedScenes()
        {
            if (settings == null)
            {
                Debug.Log("SeasideAmbience: no AmbienceSettings, no waves.");
                return;
            }
            string looked = "";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene == gameObject.scene)
                {
                    continue;
                }
                if (TryPlace(scene))
                {
                    return;
                }
                looked += (looked == "" ? "" : ", ") + scene.name;
            }
            if (looked != "")
            {
                Debug.Log("SeasideAmbience: no sea found in " + looked + " (looked for " + string.Join(", ", settings.SeaNames) + "), no waves.");
            }
        }

        // Finds the sea in one scene and builds the waves and surfers. False = no sea there (nothing changed).
        public bool TryPlace(Scene scene)
        {
            if (settings == null || waveMaterial == null || foamMaterial == null || surferMaterial == null)
            {
                Debug.Log("SeasideAmbience: settings or materials missing (run the Role 4 scene builder), no waves.");
                return true;
            }
            Renderer sea = FindSea(scene);
            if (sea == null)
            {
                return false;
            }
            List<Vector3> anchors = FindAnchors(scene);
            Bounds bounds = sea.bounds;
            float seaLevel = bounds.max.y;
            WaveTuning waveTuning = settings.MakeWaveTuning().Sanitised();
            Physics.SyncTransforms();
            var scanner = new BeachScanner(new PhysicsGroundProbe(seaLevel + 150f, 400f), settings.ScanStep, settings.MaxScanDistance);
            BeachLine found = scanner.Scan(bounds, seaLevel, anchors, waveTuning.BeachLength, settings.BeachSamples, waveTuning.SpawnDistance);
            if (found == null)
            {
                Debug.Log("SeasideAmbience: sea " + sea.name + " found in " + scene.name + ", but " + scanner.Problem + ", no waves.");
                return true;              // the sea was there: do not log a second "no sea" line
            }
            Build(found, waveTuning);
            seaSceneName = scene.name;
            Debug.Log("SeasideAmbience: " + surfers.Length + " surfers and waves on the beach at " + found.Origin.ToString("F0") + " in " + scene.name + ".");
            return true;
        }

        // Moves the sea and the surfers to 'time'. Also called by the local QA render script with a fixed step.
        public void Simulate(float time, float deltaTime)
        {
            if (!IsPlaced)
            {
                return;
            }
            field.Update(time);
            for (int i = 0; i < surfers.Length; i++)
            {
                surfers[i].Step(deltaTime, field);
            }
            waveWriter.Write(field, beach);
            waveMesh.vertices = waveWriter.Vertices;
            waveMesh.colors32 = waveWriter.Colours;
            foamMesh.vertices = waveWriter.Vertices;
            foamMesh.colors32 = waveWriter.FoamColours;
            foamMesh.uv = waveWriter.FoamUV;
            WriteSurfers();
            surferMesh.vertices = surferVertices;
            surferMesh.normals = surferNormals;
            surferMesh.colors32 = surferColours;
        }

        private Renderer FindSea(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int n = 0; n < settings.SeaNames.Length; n++)
            {
                string[] one = { settings.SeaNames[n] };       // names in priority order
                for (int r = 0; r < roots.Length; r++)
                {
                    foreach (Renderer candidate in roots[r].GetComponentsInChildren<Renderer>(true))
                    {
                        if (candidate.gameObject.activeInHierarchy && SeaLookup.NameMatches(candidate.gameObject.name, one))
                        {
                            return candidate;
                        }
                    }
                }
            }
            return null;
        }

        // Points on the racing line: Role 2's ITrackProgress if the scene has it, plus the named race objects
        // (spawn, checkpoints). Read only.
        private List<Vector3> FindAnchors(Scene scene)
        {
            var points = new List<Vector3>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                ITrackProgress progress = roots[r].GetComponentInChildren<ITrackProgress>();
                if (progress != null && progress.RacingLine != null)
                {
                    for (int i = 0; i < progress.RacingLine.Count; i += 10)   // every 10th point is plenty and keeps the raycasts few
                    {
                        points.Add(progress.RacingLine[i]);
                    }
                }
                foreach (Transform t in roots[r].GetComponentsInChildren<Transform>(true))
                {
                    if (SeaLookup.NameMatches(t.name, settings.AnchorNames))
                    {
                        points.Add(t.position);
                    }
                }
            }
            return points;
        }

        private void Build(BeachLine line, WaveTuning waveTuning)
        {
            Clear();
            beach = line;
            field = new WaveField(waveTuning);
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            waveWriter = new WaveMeshWriter(field.Capacity, line.Samples, settings.DeepWater, settings.FaceWater, settings.CrestWater, settings.Foam, linear);

            surferTuning = settings.MakeSurferTuning();
            surferTuning.BeachHalfLength = waveTuning.BeachLength * 0.5f - waveTuning.EndFade * 0.6f;
            int count = Mathf.Clamp(settings.SurferCount, 2, 4);
            surfers = new SurferBrain[count];
            float usable = waveTuning.BeachLength * 0.5f - waveTuning.EndFade;
            for (int i = 0; i < count; i++)
            {
                // Spread out along the beach, each one with its own repeatable "random" surfing.
                float home = Mathf.Lerp(-usable, usable, (i + 0.5f) / count) * 0.8f;
                surfers[i] = new SurferBrain(surferTuning, home, 1234 + i * 977);
            }
            surferVertices = new Vector3[count * SurferBody.VertsPerSurfer];
            surferNormals = new Vector3[surferVertices.Length];
            surferColours = new Color32[surferVertices.Length];
            var surferIndices = new int[count * SurferBody.IndicesPerSurfer];
            for (int i = 0; i < count; i++)
            {
                SurferBody.WriteIndices(surferIndices, i);
            }

            // Big enough bounds once, so Unity never recalculates them per frame and never culls a wave by mistake.
            Bounds area = new Bounds(line.Origin, new Vector3(1f, 1f, 1f));
            area.Encapsulate(line.PointAt(-line.Length * 0.5f, waveTuning.SpawnDistance + 10f, 4f));
            area.Encapsulate(line.PointAt(line.Length * 0.5f, waveTuning.SpawnDistance + 10f, 4f));
            area.Encapsulate(line.PointAt(-line.Length * 0.5f, -waveTuning.RunUp - 5f, -2f));
            area.Encapsulate(line.PointAt(line.Length * 0.5f, -waveTuning.RunUp - 5f, -2f));

            waveMesh = NewMesh("Waves (R12)", waveWriter.Vertices, waveWriter.Indices, area);
            waveMesh.colors32 = waveWriter.Colours;
            waveObject = NewChild("Waves", waveMesh, waveMaterial, false);

            // The foam lace: the same points and triangles again, drawn after the water with the foam picture.
            foamMesh = NewMesh("Foam (R12)", waveWriter.Vertices, waveWriter.Indices, area);
            foamMesh.colors32 = waveWriter.FoamColours;
            foamMesh.uv = waveWriter.FoamUV;
            foamObject = NewChild("Foam", foamMesh, foamMaterial, false);
            foamTexture = MakeFoamTexture();
            var block = new MaterialPropertyBlock();
            block.SetTexture("_BaseMap", foamTexture);      // per renderer, so the material asset stays untouched
            foamObject.GetComponent<MeshRenderer>().SetPropertyBlock(block);

            surferMesh = NewMesh("Surfers (R12)", surferVertices, surferIndices, area);
            surferMesh.normals = surferNormals;
            surferMesh.colors32 = surferColours;
            surferObject = NewChild("Surfers", surferMesh, surferMaterial, true);
        }

        private void WriteSurfers()
        {
            Color[] suits = settings.WetsuitColours;
            Color[] boards = settings.BoardColours;
            Color[] skins = settings.SkinColours;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < surfers.Length; i++)
            {
                SurferBrain s = surfers[i];
                Vector3 position = beach.PointAt(s.Along, s.Distance, s.Height);
                Quaternion rotation = beach.Rotation(s.Heading) * Quaternion.Euler(s.Pitch * Mathf.Rad2Deg, 0f, s.Lean * 28f);
                Matrix4x4 world = Matrix4x4.TRS(position, rotation, Vector3.one * settings.SurferScale);
                Color32 hair = Pick(settings.HairColours, i, linear);
                SurferBody.Write(s, world, Pick(suits, i, linear), Pick(skins, i, linear), hair, Pick(boards, i, linear),
                                 surferTuning.PopUpSeconds, joints, surferVertices, surferNormals, surferColours, i * SurferBody.VertsPerSurfer);
            }
        }

        private static Color32 Pick(Color[] colours, int i, bool linear)
        {
            Color c = colours != null && colours.Length > 0 ? colours[i % colours.Length] : Color.grey;
            return linear ? (Color32)c.linear : (Color32)c;
        }

        // The foam picture (FoamPattern) as a small repeating texture with mip maps, so far away foam turns into
        // a soft white instead of flickering lines.
        private static Texture2D MakeFoamTexture()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            FoamPattern.Fill(pixels, size, 6, 2026);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "Foam lace (R12)";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.SetPixels32(pixels);
            texture.Apply(true, true);       // make the mip maps, then free the copy in main memory
            return texture;
        }

        private static Mesh NewMesh(string meshName, Vector3[] vertices, int[] indices, Bounds bounds)
        {
            var mesh = new Mesh();
            mesh.name = meshName;
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.MarkDynamic();             // points change every frame
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.bounds = bounds;
            return mesh;
        }

        private GameObject NewChild(string childName, Mesh mesh, Material material, bool castShadows)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            // The meshes are written in world space, so the child must sit exactly at the world origin.
            child.transform.position = Vector3.zero;
            child.transform.rotation = Quaternion.identity;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return child;
        }

        private void Clear()
        {
            Remove(waveObject);
            Remove(foamObject);
            Remove(foamMesh);
            Remove(foamTexture);
            foamObject = null;
            foamMesh = null;
            foamTexture = null;
            Remove(surferObject);
            Remove(waveMesh);
            Remove(surferMesh);
            waveObject = null;
            surferObject = null;
            waveMesh = null;
            surferMesh = null;
            beach = null;
            field = null;
            seaSceneName = "";
        }

        // Destroy in the game; outside Play Mode (the QA render script) Unity needs DestroyImmediate.
        private static void Remove(Object thing)
        {
            if (thing == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(thing);
            }
            else
            {
                DestroyImmediate(thing);
            }
        }
    }
}
