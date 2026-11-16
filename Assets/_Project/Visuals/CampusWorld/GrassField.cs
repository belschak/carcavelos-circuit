using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals
{
    // Draws the campus grass tufts as GPU instances (task V39). CampusWorldBuilder puts this on the "GrassTufts"
    // object of the world prefab with the tuft list (GrassFieldData) and a few prototype meshes (GrassTuftShape).
    // Like the V16 cell meshes it replaces, the tufts are grouped in cells (near 16 m, far 32 m) and a cell is drawn
    // only while it is within its cull distance (near 65 m, far 150 m) and in the camera's view. All visible tufts of one
    // prototype go out in as few draw calls as possible (up to 1023 instances each), with their lawn colour per
    // instance (Foliage.shader _InstanceTint). Download: ~36 bytes per tuft instead of ~900 for baked geometry.
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        public const int MaxPerDraw = 1023;

        [SerializeField, Tooltip("Every tuft: foot, turn, size, tint, prototype (CampusWorldBuilder writes it)")]
        private GrassFieldData data;

        [SerializeField, Tooltip("The prototype meshes; GrassFieldData.Prototype indexes this list")]
        private Mesh[] prototypes = new Mesh[0];

        [SerializeField, Tooltip("Material of the near tufts (W_grass_tuft, GPU instancing on)")]
        private Material nearMaterial;

        [SerializeField, Tooltip("Material of the far clumps (W_grass_tuft_far, GPU instancing on)")]
        private Material farMaterial;

        [SerializeField] private float nearCell = 16f;
        [SerializeField] private float nearCull = 65f;
        [SerializeField] private float farCell = 32f;
        [SerializeField] private float farCull = 150f;

        private struct Cell
        {
            public Bounds Bounds;
            public bool Far;
            public int[] Start;   // per prototype: first instance in the sorted arrays
            public int[] Count;   // per prototype: how many
        }

        private Matrix4x4[] matrices = new Matrix4x4[0];   // all tufts, sorted by cell, then prototype
        private Vector4[] tints = new Vector4[0];
        private readonly List<Cell> cells = new List<Cell>();
        private Matrix4x4[][] frameMatrices = new Matrix4x4[0][]; // per prototype: the visible tufts of this frame
        private Vector4[][] frameTints = new Vector4[0][];
        private int[] frameCount = new int[0];
        private readonly Matrix4x4[] chunkMatrices = new Matrix4x4[MaxPerDraw];
        private readonly Vector4[] chunkTints = new Vector4[MaxPerDraw];
        private readonly Plane[] planes = new Plane[6];
        private MaterialPropertyBlock block;
        private bool ready;

        public int TuftCount { get { return matrices.Length; } }
        public int CellCount { get { return cells.Count; } }
        public int LastDrawCalls { get; private set; }
        public int LastDrawnTufts { get; private set; }

        private void OnEnable()
        {
            Prepare();
        }

        // Sorts the tufts into cells once. Public for the builder's check and the Play Mode test.
        public void Prepare()
        {
            ready = false;
            cells.Clear();
            if (data == null || prototypes == null || prototypes.Length == 0 || data.Count == 0)
            {
                return;
            }
            int n = data.Count;
            int protoCount = prototypes.Length;
            var keys = new long[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                Vector4 p = data.PositionTurn[i];
                bool far = data.Far[i] != 0;
                keys[i] = GrassFieldRules.SortKey(far, GrassFieldRules.CellOf(p.x, p.z, far ? farCell : nearCell),
                    data.Prototype[i]);
                order[i] = i;
            }
            Array.Sort(keys, order);
            matrices = new Matrix4x4[n];
            tints = new Vector4[n];
            Cell current = default(Cell);
            long currentCell = long.MinValue;
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                Vector4 p = data.PositionTurn[i];
                Vector4 st = data.SizeTint[i];
                bool far = data.Far[i] != 0;
                matrices[k] = Matrix4x4.TRS(new Vector3(p.x, p.y, p.z), Quaternion.Euler(0f, p.w * Mathf.Rad2Deg, 0f), Vector3.one * st.x);
                tints[k] = new Vector4(st.y, st.z, st.w, 1f);
                long cellKey = keys[k] / 1024; // the key without the prototype
                if (cellKey != currentCell)
                {
                    if (currentCell != long.MinValue)
                    {
                        cells.Add(current);
                    }
                    currentCell = cellKey;
                    Vector2Int c = GrassFieldRules.CellOf(p.x, p.z, far ? farCell : nearCell);
                    float size = far ? farCell : nearCell;
                    current = new Cell
                    {
                        Bounds = new Bounds(new Vector3((c.x + 0.5f) * size, p.y, (c.y + 0.5f) * size), new Vector3(size, 2f, size)),
                        Far = far,
                        Start = new int[protoCount],
                        Count = new int[protoCount],
                    };
                    for (int q = 0; q < protoCount; q++) current.Start[q] = -1;
                }
                current.Bounds.Encapsulate(new Vector3(p.x, p.y + 1f, p.z));
                current.Bounds.Encapsulate(new Vector3(p.x, p.y - 0.5f, p.z));
                int proto = data.Prototype[i];
                if (current.Start[proto] < 0) current.Start[proto] = k;
                current.Count[proto]++;
            }
            cells.Add(current);
            var perPrototype = new int[protoCount];
            for (int i = 0; i < n; i++) perPrototype[data.Prototype[i]]++;
            frameMatrices = new Matrix4x4[protoCount][];
            frameTints = new Vector4[protoCount][];
            frameCount = new int[protoCount];
            for (int q = 0; q < protoCount; q++)
            {
                frameMatrices[q] = new Matrix4x4[perPrototype[q]];
                frameTints[q] = new Vector4[perPrototype[q]];
            }
            block = new MaterialPropertyBlock();
            ready = true;
        }

        private void Update()
        {
            if (!ready)
            {
                return;
            }
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            if (cam != null)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, planes);
            }
            Draw(cam != null, eye);
        }

        // Gathers the visible tufts per prototype and draws them. cull = false: every cell (editor without a camera).
        public void Draw(bool cull, Vector3 eye)
        {
            Array.Clear(frameCount, 0, frameCount.Length);
            for (int c = 0; c < cells.Count; c++)
            {
                Cell cell = cells[c];
                if (cull)
                {
                    float reach = cell.Far ? farCull : nearCull;
                    float size = cell.Far ? farCell : nearCell;
                    if (!GrassFieldRules.CellShown(eye, cell.Bounds.center, size, reach)
                        || !GeometryUtility.TestPlanesAABB(planes, cell.Bounds))
                    {
                        continue;
                    }
                }
                for (int q = 0; q < cell.Count.Length; q++)
                {
                    int count = cell.Count[q];
                    if (count == 0) continue;
                    Array.Copy(matrices, cell.Start[q], frameMatrices[q], frameCount[q], count);
                    Array.Copy(tints, cell.Start[q], frameTints[q], frameCount[q], count);
                    frameCount[q] += count;
                }
            }
            int calls = 0, drawn = 0;
            for (int q = 0; q < prototypes.Length; q++)
            {
                Mesh mesh = prototypes[q];
                Material material = GrassFieldRules.IsFarPrototype(q, prototypes.Length, farPrototypeCount) ? farMaterial : nearMaterial;
                if (mesh == null || material == null) continue;
                for (int start = 0; start < frameCount[q]; start += MaxPerDraw)
                {
                    int count = Mathf.Min(MaxPerDraw, frameCount[q] - start);
                    Array.Copy(frameMatrices[q], start, chunkMatrices, 0, count);
                    Array.Copy(frameTints[q], start, chunkTints, 0, count);
                    block.SetVectorArray("_InstanceTint", chunkTints);
                    Graphics.DrawMeshInstanced(mesh, 0, material, chunkMatrices, count, block, ShadowCastingMode.Off,
                        true, gameObject.layer, null, LightProbeUsage.Off);
                    calls++;
                    drawn += count;
                }
            }
            LastDrawCalls = calls;
            LastDrawnTufts = drawn;
        }

        [SerializeField, Tooltip("How many of the prototypes (at the end of the list) are far clumps")]
        private int farPrototypeCount = 2;

        // Called once by CampusWorldBuilder.
        public void SetUp(GrassFieldData tufts, Mesh[] meshes, int farMeshes, Material near, Material farMat)
        {
            data = tufts;
            prototypes = meshes;
            farPrototypeCount = farMeshes;
            nearMaterial = near;
            farMaterial = farMat;
            Prepare();
        }
    }

    // The plain rules of GrassField, for Edit Mode tests.
    public static class GrassFieldRules
    {
        public static Vector2Int CellOf(float x, float z, float cellSize)
        {
            return new Vector2Int(Mathf.FloorToInt(x / cellSize), Mathf.FloorToInt(z / cellSize));
        }

        // Sorts far after near, then by cell, then by prototype (prototype < 1024).
        public static long SortKey(bool far, Vector2Int cell, int prototype)
        {
            long cx = cell.x + 100000L;
            long cz = cell.y + 100000L;
            return ((((far ? 1L : 0L) * 400000L + cx) * 400000L + cz) * 1024L) + prototype;
        }

        // A cell is drawn while the camera is within the cull distance of its centre (plus most of a cell, so a cell
        // never pops while its near corner is still in reach). Measured flat, like the V16 LOD distance.
        public static bool CellShown(Vector3 eye, Vector3 cellCentre, float cellSize, float cullDistance)
        {
            float dx = eye.x - cellCentre.x;
            float dz = eye.z - cellCentre.z;
            float reach = cullDistance + cellSize * 0.7f;
            return dx * dx + dz * dz < reach * reach;
        }

        public static int DrawCalls(int instances)
        {
            return instances <= 0 ? 0 : (instances + GrassField.MaxPerDraw - 1) / GrassField.MaxPerDraw;
        }

        public static bool IsFarPrototype(int index, int prototypes, int farPrototypes)
        {
            return index >= prototypes - farPrototypes;
        }
    }
}
