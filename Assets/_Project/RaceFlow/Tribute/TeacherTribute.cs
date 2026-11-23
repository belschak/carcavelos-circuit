using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P4, teacher tribute (the credits page "SPECIAL THANKS" lives in RaceHud.uxml). Two small things in the race:
    // 1. Four thank-you boards along the lap, one per teacher (TributeRules.Teachers), each next to a checkpoint gate.
    //    The gates' banner poles stand where Role 3's campus builder already checked ground and walls, so a board goes
    //    right beside one. A spot is only used when there is ground under it and nothing solid in the way; otherwise
    //    the next spot or the next gate is tried, and a board that finds no place is logged, never forced.
    // 2. A gold lap or better shows "YOU WON THE INTRODUCTION TO RACING CUP" under the medal on the results card.
    // It creates itself when the game starts (no scene edit) and never touches the race, the karts or the bots.
    // Boards are pure decoration: no colliders, layer "Ignore Raycast", materials copied from the gate banner.
    public class TeacherTribute : MonoBehaviour
    {
        private const string BoardsPicture = "TributeBoards"; // Tribute/Resources/TributeBoards.png
        private const int IgnoreRaycastLayer = 2;

        private readonly List<Material> madeMaterials = new List<Material>();
        private VisualElement medalBadge;
        private Label cupLabel;
        private float nextUiSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStart()
        {
            if (FindAnyObjectByType<TeacherTribute>() != null)
            {
                return;
            }
            var host = new GameObject("Teacher Tribute");
            DontDestroyOnLoad(host);
            host.AddComponent<TeacherTribute>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // A track scene that was open before this object existed (both scenes open in the Editor).
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                StartCoroutine(PlaceBoardsNextFrame(SceneManager.GetSceneAt(i)));
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(PlaceBoardsNextFrame(scene));
        }

        // One frame later the gates have built their banners (Awake) and removed their colliders (Destroy runs at the
        // end of the frame), so the free-space check below only sees real walls and buildings.
        private IEnumerator PlaceBoardsNextFrame(Scene scene)
        {
            yield return null;
            if (scene.isLoaded)
            {
                PlaceBoards(scene);
            }
        }

        private void PlaceBoards(Scene scene)
        {
            var gates = new Dictionary<int, Transform>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                foreach (Transform t in roots[r].GetComponentsInChildren<Transform>(true))
                {
                    int number = TributeRules.GateNumber(t.name);
                    if (number < 0 || t.Find("Banner Right") == null || t.Find("Banner Left") == null)
                    {
                        continue;
                    }
                    if (t.Find("Teacher Board") != null)
                    {
                        return; // this track got its boards already (scene seen twice in one frame)
                    }
                    if (!gates.ContainsKey(number))
                    {
                        gates.Add(number, t);
                    }
                }
            }
            if (gates.Count < 2)
            {
                return; // not a track with checkpoint gates (Main, the test loop, the menus)
            }

            var picture = Resources.Load<Texture2D>(BoardsPicture);
            if (picture == null)
            {
                Debug.Log("TeacherTribute: " + BoardsPicture + ".png not found, no thank-you boards.");
                return;
            }

            int gateCount = MaxKey(gates) + 1;
            int[] picked = TributeRules.PickGates(gateCount, TributeRules.Teachers.Length);
            var used = new HashSet<int>();
            int placed = 0;
            for (int board = 0; board < TributeRules.Teachers.Length && board < picked.Length; board++)
            {
                int gate = picked[board];
                bool done = false;
                // The picked gate first, then up to two gates further on.
                for (int attempt = 0; attempt < 3 && !done && gate >= 0; attempt++)
                {
                    Transform gateTransform;
                    if (!used.Contains(gate) && gates.TryGetValue(gate, out gateTransform))
                    {
                        done = TryBuildBoard(gateTransform, board, picture);
                        if (done)
                        {
                            used.Add(gate);
                        }
                    }
                    gate = TributeRules.NextGate(gate, gateCount);
                }
                if (done)
                {
                    placed++;
                }
                else
                {
                    Debug.Log("TeacherTribute: no free spot for the board of " + TributeRules.Teachers[board] + ".");
                }
            }
            Debug.Log("TeacherTribute: " + placed + " of " + TributeRules.Teachers.Length + " thank-you boards placed.");
        }

        private static int MaxKey(Dictionary<int, Transform> gates)
        {
            int max = 0;
            foreach (int key in gates.Keys)
            {
                max = Mathf.Max(max, key);
            }
            return max;
        }

        private bool TryBuildBoard(Transform gate, int board, Texture2D picture)
        {
            Transform rightBanner = gate.Find("Banner Right");
            Transform leftBanner = gate.Find("Banner Left");
            Vector3[] spots = TributeRules.BoardSpots(leftBanner.localPosition, rightBanner.localPosition);
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 spot = spots[i];
                Vector3 world = gate.TransformPoint(new Vector3(spot.x, spot.y + 3f, spot.z));
                RaycastHit hit;
                int solid = ~(1 << IgnoreRaycastLayer);
                if (!Physics.Raycast(world, Vector3.down, out hit, 8f, solid, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                float groundLocal = gate.InverseTransformPoint(hit.point).y;
                if (!TributeRules.GroundFits(spot.y, groundLocal))
                {
                    continue;
                }
                // The whole board with its posts, from just above the ground (kerbs and grass may touch the posts).
                float top = TributeRules.BoardBottom + TributeRules.BoardHeight;
                Vector3 centre = gate.TransformPoint(new Vector3(spot.x, groundLocal + 0.25f + (top - 0.25f) * 0.5f, spot.z));
                Vector3 halfSize = new Vector3(TributeRules.BoardWidth * 0.5f + 0.1f, (top - 0.25f) * 0.5f, 0.3f);
                if (Physics.CheckBox(centre, halfSize, gate.rotation, solid, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                Build(gate, new Vector3(spot.x, groundLocal, spot.z), board, picture);
                return true;
            }
            return false;
        }

        // Two posts, a dark back panel and the picture, all built from Unity's cube and quad without their colliders.
        private void Build(Transform gate, Vector3 foot, int board, Texture2D picture)
        {
            Transform rightBanner = gate.Find("Banner Right");
            Material metal = SharedMaterial(rightBanner, "Pole");
            Material cloth = SharedMaterial(rightBanner, "Cloth");

            var root = new GameObject("Teacher Board").transform;
            root.SetParent(gate, false);
            root.localPosition = foot;
            root.localRotation = Quaternion.identity;
            root.gameObject.layer = IgnoreRaycastLayer;

            float w = TributeRules.BoardWidth;
            float h = TributeRules.BoardHeight;
            float middle = TributeRules.BoardBottom + h * 0.5f;
            Part(PrimitiveType.Cube, "Post L", root, new Vector3(-w * 0.4f, middle * 0.5f, 0.06f), new Vector3(0.09f, middle, 0.09f), metal);
            Part(PrimitiveType.Cube, "Post R", root, new Vector3(w * 0.4f, middle * 0.5f, 0.06f), new Vector3(0.09f, middle, 0.09f), metal);
            Part(PrimitiveType.Cube, "Back", root, new Vector3(0f, middle, 0.05f), new Vector3(w + 0.1f, h + 0.1f, 0.06f), metal);

            Material face = null;
            if (cloth != null)
            {
                face = new Material(cloth);
                face.name = "Teacher board " + board;
                face.mainTexture = picture;
                Vector4 uv = TributeRules.BoardUv(board, TributeRules.Teachers.Length);
                face.mainTextureScale = new Vector2(uv.x, uv.y);
                face.mainTextureOffset = new Vector2(uv.z, uv.w);
                madeMaterials.Add(face);
            }
            // Unity's quad faces -z, so the driver coming along +z reads it the right way round.
            Part(PrimitiveType.Quad, "Names", root, new Vector3(0f, middle, 0f), new Vector3(w, h, 1f), face);
        }

        private static Material SharedMaterial(Transform banner, string child)
        {
            Transform part = banner.Find(child);
            Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static void Part(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = IgnoreRaycastLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        // The cup line on the results card: found once, then shown or hidden with the medal stamp.
        private void Update()
        {
            if (medalBadge == null || medalBadge.panel == null)
            {
                if (Time.unscaledTime < nextUiSearch)
                {
                    return;
                }
                nextUiSearch = Time.unscaledTime + 1f;
                FindMedalBadge();
                if (medalBadge == null)
                {
                    return;
                }
            }
            bool cup = WinsCup(medalBadge);
            DisplayStyle wanted = cup ? DisplayStyle.Flex : DisplayStyle.None;
            if (cupLabel.style.display != wanted)
            {
                cupLabel.style.display = wanted;
            }
        }

        private void FindMedalBadge()
        {
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < documents.Length; i++)
            {
                VisualElement root = documents[i].rootVisualElement;
                VisualElement row = root != null ? root.Q<VisualElement>("res-medal") : null;
                VisualElement badge = row != null ? row.Q<VisualElement>("res-medal-badge") : null;
                if (badge == null)
                {
                    continue;
                }
                medalBadge = badge;
                cupLabel = row.Q<Label>("res-cup");
                if (cupLabel == null)
                {
                    // Same look and fade-in as the "next medal" line under the card's time (RaceHud.uss).
                    cupLabel = new Label(TributeRules.CupLine) { name = "res-cup" };
                    cupLabel.AddToClassList("res-medal-next");
                    cupLabel.AddToClassList("res-medal-next--new");
                    cupLabel.AddToClassList("f-ui");
                    cupLabel.style.display = DisplayStyle.None;
                    row.Add(cupLabel);
                }
                return;
            }
        }

        private static bool WinsCup(VisualElement badge)
        {
            Medal[] medals = { Medal.Gold, Medal.DeansList };
            for (int i = 0; i < medals.Length; i++)
            {
                if (TributeRules.WinsCup(medals[i]) && badge.ClassListContains(MedalRules.ColourClass(medals[i])))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < madeMaterials.Count; i++)
            {
                if (madeMaterials[i] != null)
                {
                    Destroy(madeMaterials[i]);
                }
            }
        }
    }
}
