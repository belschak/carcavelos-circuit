using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // A checkpoint gate (task V13): a tall feather banner on a pole on each side of the road, waving in the wind,
    // so the player sees where to drive through. The cloth shows CheckpointBanner.png ("NOVA KART", made by
    // tools/textures/make_checkpoint_banner.py) and reads correctly from both sides.
    // Pure decoration: no colliders, layer "Ignore Raycast"; the checkpoint trigger itself stays Role 2's.
    // CampusWorldBuilder puts one at every checkpoint and stores where each pole stands (it checks ground and walls);
    // pole and cloth are made in code when the race starts. The cloth is a small grid whose points move every frame
    // with CheckpointGatePlan.Wave, only while it is on screen. No cloth physics and no ParticleSystem: the Web build
    // strips engine modules no scene uses, and this costs almost nothing.
    public class CheckpointGate : MonoBehaviour
    {
        private const float PoleHeight = 5.0f;
        private const float PoleThickness = 0.08f;
        private const float ClothBottom = 1.0f;     // metres above the foot
        private const float ClothTop = 4.9f;        // top edge at the pole
        private const float ClothWidth = 1.2f;
        private const int Columns = 6;              // grid along the width ...
        private const int Rows = 14;                // ... and along the height

        [SerializeField, Tooltip("Local position of the left pole's foot (set by CampusWorldBuilder)")]
        private Vector3 leftFoot = new Vector3(-4.9f, 0f, 0f);

        [SerializeField, Tooltip("Local position of the right pole's foot (set by CampusWorldBuilder)")]
        private Vector3 rightFoot = new Vector3(4.9f, 0f, 0f);

        [SerializeField, Tooltip("Lit material for pole and foot (dark metal)")]
        private Material poleMaterial;

        [SerializeField, Tooltip("Lit material with the banner picture (Visuals/CheckpointGates/CheckpointBanner.mat)")]
        private Material clothMaterial;

        private Banner[] banners = new Banner[0];

        private class Banner
        {
            public Mesh Mesh;
            public Renderer Renderer;
            public Vector3[] Vertices;
            public float Side;      // -1 left of the road, +1 right
            public float Seed;
        }

        // Used by the builder, before the scene is saved.
        public void Configure(Vector3 left, Vector3 right, Material pole, Material cloth)
        {
            leftFoot = left;
            rightFoot = right;
            poleMaterial = pole;
            clothMaterial = cloth;
        }

        private void Awake()
        {
            gameObject.layer = 2;
            banners = new[] { Build("Left", leftFoot, -1f), Build("Right", rightFoot, 1f) };
            for (int i = 0; i < banners.Length; i++)
            {
                Wave(banners[i], 0f);
            }
        }

        private void Update()
        {
            float t = Time.time;
            for (int i = 0; i < banners.Length; i++)
            {
                if (banners[i].Renderer.isVisible)
                {
                    Wave(banners[i], t);
                }
            }
        }

        // Pole + foot + cloth for one side. The pole stands on the road side, the cloth reaches outwards.
        private Banner Build(string name, Vector3 foot, float side)
        {
            var root = new GameObject("Banner " + name).transform;
            root.SetParent(transform, false);
            root.localPosition = foot;
            root.localRotation = Quaternion.identity;
            Block("Pole", root, new Vector3(0f, PoleHeight * 0.5f, 0f), new Vector3(PoleThickness, PoleHeight, PoleThickness), poleMaterial);
            Block("Foot", root, new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.1f, 0.45f), poleMaterial);

            var banner = new Banner { Side = side, Seed = Mathf.Repeat(foot.x * 0.37f + transform.position.x * 0.11f + transform.position.z * 0.07f, 10f) };
            int perSide = (Columns + 1) * (Rows + 1);
            banner.Vertices = new Vector3[perSide * 2];
            var uvs = new Vector2[perSide * 2];
            var tris = new int[Columns * Rows * 12];
            int k = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int a = r * (Columns + 1) + c, b = a + 1, d = a + Columns + 1, e = d + 1;
                    // Seen from the approaching driver (looking along +z) the front must wind clockwise. On the left
                    // side the cloth grows towards -x, which mirrors the grid, so the order flips there.
                    bool flip = side < 0f;
                    k = Quad(tris, k, flip ? b : a, flip ? a : b, flip ? d : e, flip ? e : d);
                    k = Quad(tris, k, perSide + (flip ? a : b), perSide + (flip ? b : a), perSide + (flip ? e : d), perSide + (flip ? d : e));
                }
            }
            for (int r = 0; r <= Rows; r++)
            {
                for (int c = 0; c <= Columns; c++)
                {
                    float u = (float)c / Columns;
                    float v = (float)r / Rows;
                    // The picture is read left to right by the driver, so it follows world x, not the cloth: on the
                    // left side the gold band ends up at the pole. The back shows it mirrored back the right way.
                    float uFront = side > 0f ? u : 1f - u;
                    uvs[r * (Columns + 1) + c] = new Vector2(uFront, v);
                    uvs[perSide + r * (Columns + 1) + c] = new Vector2(1f - uFront, v);
                }
            }

            banner.Mesh = new Mesh { name = "Checkpoint banner" };
            banner.Mesh.MarkDynamic();
            banner.Mesh.vertices = banner.Vertices;
            banner.Mesh.uv = uvs;
            banner.Mesh.triangles = tris;
            var cloth = new GameObject("Cloth");
            cloth.layer = 2;
            cloth.transform.SetParent(root, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = banner.Mesh;
            MeshRenderer renderer = cloth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = clothMaterial;
            banner.Renderer = renderer;
            return banner;
        }

        // Moves every point of the cloth for this moment; the pole side stays put.
        private static void Wave(Banner banner, float time)
        {
            int perSide = (Columns + 1) * (Rows + 1);
            for (int r = 0; r <= Rows; r++)
            {
                for (int c = 0; c <= Columns; c++)
                {
                    float u = (float)c / Columns;
                    float v = (float)r / Rows;
                    float top = ClothTop + CheckpointGatePlan.TopEdge(u);
                    var p = new Vector3(banner.Side * (PoleThickness * 0.5f + u * ClothWidth),
                                        ClothBottom + v * (top - ClothBottom),
                                        CheckpointGatePlan.Wave(u, v, time, banner.Seed));
                    int i = r * (Columns + 1) + c;
                    banner.Vertices[i] = p;
                    banner.Vertices[perSide + i] = p;
                }
            }
            banner.Mesh.vertices = banner.Vertices;
            banner.Mesh.RecalculateNormals();
            banner.Mesh.bounds = new Bounds(new Vector3(banner.Side * ClothWidth * 0.5f, (ClothBottom + ClothTop) * 0.5f, 0f),
                                            new Vector3(ClothWidth + 0.2f, ClothTop - ClothBottom + 0.2f, CheckpointGatePlan.MaxWave * 2f + 0.2f));
        }

        // Two triangles of the quad a-b-c-d (a bottom-left, b bottom-right, c top-right, d top-left as seen from the front).
        private static int Quad(int[] tris, int k, int a, int b, int c, int d)
        {
            tris[k++] = a; tris[k++] = d; tris[k++] = c;
            tris[k++] = a; tris[k++] = c; tris[k++] = b;
            return k;
        }

        private static void Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Remove(go.GetComponent<Collider>());
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        // Destroy in the game; in the editor (preview renders) Unity only allows DestroyImmediate.
        private static void Remove(Object thing)
        {
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
