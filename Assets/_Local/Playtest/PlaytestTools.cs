using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CarcavelosCircuit.Driving;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Track;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Playtest
{
    // LOCAL PLAYTEST TOOLS (Luc, 13 Nov 2026). Created by itself in every scene of the playtest app, never in the
    // team's build. Everywhere: F10 or Cmd+Q quits, Esc opens a small menu (continue, start menu, quit), F12 saves a
    // screenshot with the kart's pose in its name and a .json beside it (~/nova/playtest_shots). In free roam also:
    // marks (1 checkpoint, 2 item box row, 3 boost pad, 4 problem; Backspace undoes the last) written to
    // ~/nova/playtest_marks/marks.json and shown as little flags, Page Up/Down jumps 60 m along the racing line,
    // R puts the kart back on the road, F a free camera, H the key list.
    public class PlaytestTools : MonoBehaviour
    {
        public static string LauncherScene = "Launcher";
        public static string FreeRoamScene = "FreeRoam";

        private static PlaytestTools instance;
        private bool menu, help, freeCam;
        private string toast;
        private float toastUntil;
        private Vector2 camAngles;
        private readonly List<GameObject> flags = new List<GameObject>();
        private Behaviour[] cameraDrivers = new Behaviour[0];

        private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static string ShotDir { get { return Path.Combine(Home, "nova", "playtest_shots"); } }
        private static string MarkFile { get { return Path.Combine(Home, "nova", "playtest_marks", "marks.json"); } }
        private static readonly string[] MarkKinds = { "checkpoint", "itembox", "boostpad", "problem" };
        private static readonly Color[] MarkColours = { new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.6f, 1f),
                                                         new Color(1f, 0.45f, 0.1f), new Color(1f, 0.15f, 0.2f) };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }
            var go = new GameObject("Playtest Tools (local)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlaytestTools>();
            SceneManager.sceneLoaded += (s, m) => instance.OnSceneLoaded();
            instance.OnSceneLoaded();
        }

        private bool InFreeRoam { get { return SceneManager.GetActiveScene().name == FreeRoamScene; } }

        private void OnSceneLoaded()
        {
            menu = help = false;
            SetFreeCam(false);
            foreach (GameObject f in flags)
            {
                if (f != null) Destroy(f);
            }
            flags.Clear();
            if (InFreeRoam)
            {
                foreach (Mark m in LoadMarks())
                {
                    AddFlag(m);
                }
            }
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null)
            {
                return;
            }
            bool cmd = k.leftCommandKey.isPressed || k.rightCommandKey.isPressed;
            if (k.f10Key.wasPressedThisFrame || (cmd && k.qKey.wasPressedThisFrame))
            {
                Quit();
            }
            if (k.escapeKey.wasPressedThisFrame)
            {
                menu = !menu;
                Time.timeScale = menu ? 0f : 1f;
                SetKartIdle(menu || freeCam);
            }
            if (k.f12Key.wasPressedThisFrame)
            {
                Screenshot();
            }
            if (menu || !InFreeRoam)
            {
                return;
            }
            if (k.hKey.wasPressedThisFrame) help = !help;
            if (k.digit1Key.wasPressedThisFrame) AddMark(0);
            if (k.digit2Key.wasPressedThisFrame) AddMark(1);
            if (k.digit3Key.wasPressedThisFrame) AddMark(2);
            if (k.digit4Key.wasPressedThisFrame) AddMark(3);
            if (k.backspaceKey.wasPressedThisFrame) UndoMark();
            if (k.pageUpKey.wasPressedThisFrame) Jump(60f);
            if (k.pageDownKey.wasPressedThisFrame) Jump(-60f);
            if (k.rKey.wasPressedThisFrame) Jump(0f);
            if (k.fKey.wasPressedThisFrame) SetFreeCam(!freeCam);
            if (freeCam) FlyCamera(k);
        }

        // ---------- the kart, the racing line ----------
        private static KartController Kart()
        {
            return FindAnyObjectByType<KartController>();
        }

        private static ITrackProgress Line()
        {
            return FindAnyObjectByType<TrackProgress>();
        }

        private sealed class IdleSource : IKartInputSource
        {
            public KartInput ReadInput() { return KartInput.None; }
        }
        private static readonly IdleSource Idle = new IdleSource();

        private static void SetKartIdle(bool idle)
        {
            KartController kart = Kart();
            if (kart == null)
            {
                return;
            }
            if (idle && kart.InputSource == null) kart.InputSource = Idle;
            if (!idle && kart.InputSource == Idle) kart.InputSource = null;
        }

        private void Jump(float metres)
        {
            KartController kart = Kart();
            ITrackProgress line = Line();
            if (kart == null || line == null || line.LapLength <= 0f)
            {
                return;
            }
            float s = line.DistanceAlongLap(kart.transform.position) + metres;
            Vector3 p = line.PointAt(s) + Vector3.up * 0.6f;
            Vector3 d = line.DirectionAt(s);
            d.y = 0f;
            kart.ResetTo(p, Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward));
            ShowToast(metres == 0f ? "Zurück auf die Straße" : (metres > 0f ? "+60 m" : "-60 m"));
        }

        // ---------- quit, screenshot ----------
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string Pose(out Dictionary<string, string> info)
        {
            info = new Dictionary<string, string>();
            Transform t = freeCam && Camera.main != null ? Camera.main.transform : (Kart() != null ? Kart().transform : (Camera.main != null ? Camera.main.transform : null));
            if (t == null)
            {
                return "nopose";
            }
            Vector3 p = t.position;
            float heading = Mathf.Repeat(Mathf.Atan2(t.forward.x, t.forward.z) * Mathf.Rad2Deg, 360f);
            ITrackProgress line = Line();
            float s = line != null && line.LapLength > 0f ? line.DistanceAlongLap(p) : -1f;
            // the generator's frame: x east, y north, z up (Unity: x east, z north, y up)
            info["x"] = F(p.x); info["y"] = F(p.z); info["z"] = F(p.y); info["heading_deg"] = F(heading);
            info["s_m"] = F(s); info["route_index"] = s >= 0f ? Mathf.RoundToInt(s / 2f).ToString() : "-1";
            info["camera"] = freeCam ? "free" : "kart";
            info["scene"] = SceneManager.GetActiveScene().name;
            return "x" + Mathf.RoundToInt(p.x) + "_y" + Mathf.RoundToInt(p.z) + "_h" + Mathf.RoundToInt(heading) + "_s" + Mathf.RoundToInt(s);
        }

        private void Screenshot()
        {
            Directory.CreateDirectory(ShotDir);
            Dictionary<string, string> info;
            string name = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Pose(out info);
            ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, name + ".png"));
            File.WriteAllText(Path.Combine(ShotDir, name + ".json"), Json(info));
            ShowToast("Screenshot gespeichert: " + name);
        }

        // ---------- marks ----------
        [Serializable] private class Mark { public string kind; public float x, y, z, heading_deg, s_m; public int route_index; public string time; }
        [Serializable] private class MarkList { public List<Mark> marks = new List<Mark>(); }

        private static List<Mark> LoadMarks()
        {
            try
            {
                if (File.Exists(MarkFile))
                {
                    MarkList l = JsonUtility.FromJson<MarkList>(File.ReadAllText(MarkFile));
                    if (l != null && l.marks != null) return l.marks;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Playtest marks: " + e.Message);
            }
            return new List<Mark>();
        }

        private static void SaveMarks(List<Mark> marks)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkFile));
            File.WriteAllText(MarkFile, JsonUtility.ToJson(new MarkList { marks = marks }, true));
        }

        private void AddMark(int kind)
        {
            Dictionary<string, string> info;
            Pose(out info);
            if (!info.ContainsKey("x"))
            {
                return;
            }
            var m = new Mark
            {
                kind = MarkKinds[kind], x = P(info["x"]), y = P(info["y"]), z = P(info["z"]), heading_deg = P(info["heading_deg"]),
                s_m = P(info["s_m"]), route_index = int.Parse(info["route_index"]), time = DateTime.Now.ToString("s"),
            };
            List<Mark> marks = LoadMarks();
            marks.Add(m);
            SaveMarks(marks);
            AddFlag(m);
            ShowToast("Markiert: " + m.kind + " (Punkt " + m.route_index + ")");
        }

        private void UndoMark()
        {
            List<Mark> marks = LoadMarks();
            if (marks.Count == 0)
            {
                return;
            }
            marks.RemoveAt(marks.Count - 1);
            SaveMarks(marks);
            if (flags.Count > 0)
            {
                Destroy(flags[flags.Count - 1]);
                flags.RemoveAt(flags.Count - 1);
            }
            ShowToast("Letzte Markierung entfernt");
        }

        private void AddFlag(Mark m)
        {
            int k = Array.IndexOf(MarkKinds, m.kind);
            Color c = MarkColours[Mathf.Max(k, 0)];
            var root = new GameObject("Mark " + m.kind);
            root.transform.position = new Vector3(m.x, m.z, m.y);
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foreach (GameObject g in new[] { pole, cloth })
            {
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root.transform, false);
                var r = g.GetComponent<Renderer>();
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                r.material = new Material(unlit != null ? unlit : r.sharedMaterial.shader);
                r.material.color = g == pole ? Color.white : c;
            }
            pole.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);
            pole.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            cloth.transform.localScale = new Vector3(0.05f, 0.6f, 0.9f);
            cloth.transform.localPosition = new Vector3(0f, 2.65f, 0.45f);
            flags.Add(root);
        }

        // ---------- free camera ----------
        private void SetFreeCam(bool on)
        {
            Camera cam = Camera.main;
            if (on && cam == null)
            {
                return;
            }
            if (on)
            {
                var drivers = new List<Behaviour>();
                foreach (Behaviour b in cam.GetComponents<Behaviour>())
                {
                    if (b != cam && b.enabled && b.GetType().Name != "UniversalAdditionalCameraData")
                    {
                        drivers.Add(b);
                        b.enabled = false;              // the chase rig and the Cinemachine brain stop moving it
                    }
                }
                cameraDrivers = drivers.ToArray();
                Vector3 e = cam.transform.eulerAngles;
                camAngles = new Vector2(e.y, e.x);
            }
            else
            {
                foreach (Behaviour b in cameraDrivers)
                {
                    if (b != null) b.enabled = true;
                }
                cameraDrivers = new Behaviour[0];
            }
            freeCam = on;
            SetKartIdle(on || menu);
            if (on) ShowToast("Freie Kamera: WASD, Q/E runter/hoch, Pfeile drehen, Shift schnell, F zurück");
        }

        private void FlyCamera(Keyboard k)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            float turn = 70f * dt;
            if (k.leftArrowKey.isPressed) camAngles.x -= turn;
            if (k.rightArrowKey.isPressed) camAngles.x += turn;
            if (k.upArrowKey.isPressed) camAngles.y -= turn;
            if (k.downArrowKey.isPressed) camAngles.y += turn;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                camAngles.x += d.x * 0.15f;
                camAngles.y -= d.y * 0.15f;
            }
            camAngles.y = Mathf.Clamp(camAngles.y, -89f, 89f);
            cam.transform.rotation = Quaternion.Euler(camAngles.y, camAngles.x, 0f);
            float speed = (k.leftShiftKey.isPressed ? 30f : 8f) * dt;
            Vector3 move = Vector3.zero;
            if (k.wKey.isPressed) move += cam.transform.forward;
            if (k.sKey.isPressed) move -= cam.transform.forward;
            if (k.dKey.isPressed) move += cam.transform.right;
            if (k.aKey.isPressed) move -= cam.transform.right;
            if (k.eKey.isPressed) move += Vector3.up;
            if (k.qKey.isPressed) move -= Vector3.up;
            cam.transform.position += move * speed;
        }

        // ---------- on screen ----------
        private void ShowToast(string text)
        {
            toast = text;
            toastUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            var box = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true };
            box.normal.textColor = Color.white;
            if (InFreeRoam && !menu)
            {
                KartController kart = Kart();
                Dictionary<string, string> info;
                Pose(out info);
                string line = "FREIES FAHREN   " + (kart != null ? Mathf.RoundToInt(Mathf.Abs(kart.Speed) * 3.6f) + " km/h   " : "")
                    + (info.ContainsKey("route_index") ? "Punkt " + info["route_index"] : "") + "   H = Hilfe";
                GUI.Box(new Rect(12, 12, 560, 30), line, box);
            }
            if (help && InFreeRoam && !menu)
            {
                GUI.Box(new Rect(12, 50, 560, 330),
                    "W/S oder Pfeile: Gas und Bremse, A/D: lenken\nLeertaste: Driften, Shift in der Luft: Trick\n" +
                    "F12: Screenshot (Ordner ~/nova/playtest_shots)\n" +
                    "1: Checkpoint   2: Itembox-Reihe   3: Boost-Pad   4: Problem\nBackspace: letzte Markierung weg\n" +
                    "Bild auf / Bild ab: 60 m vor / zurück\nR: zurück auf die Straße\nF: freie Kamera an / aus\n" +
                    "Esc: Menü   F10 oder Cmd+Q: beenden", box);
            }
            if (toast != null && Time.unscaledTime < toastUntil)
            {
                GUI.Box(new Rect(12, Screen.height - 56, Mathf.Min(900, Screen.width - 24), 40), toast, box);
            }
            if (menu)
            {
                var button = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                float w = 360, h = 56, x = (Screen.width - w) / 2f, y = Screen.height / 2f - 110f;
                GUI.Box(new Rect(x - 20, y - 20, w + 40, 4 * (h + 12) + 30), "");
                if (GUI.Button(new Rect(x, y, w, h), "Weiterfahren", button)) { menu = false; Time.timeScale = 1f; SetKartIdle(freeCam); }
                if (GUI.Button(new Rect(x, y + (h + 12), w, h), "Startmenü", button)) { Time.timeScale = 1f; SceneManager.LoadScene(LauncherScene); }
                if (GUI.Button(new Rect(x, y + 2 * (h + 12), w, h), "Screenshot-Ordner öffnen", button)) { Directory.CreateDirectory(ShotDir); Application.OpenURL("file://" + ShotDir); }
                if (GUI.Button(new Rect(x, y + 3 * (h + 12), w, h), "Beenden", button)) { Quit(); }
            }
        }

        // ---------- small helpers ----------
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static float P(string v) { return float.Parse(v, CultureInfo.InvariantCulture); }

        private static string Json(Dictionary<string, string> d)
        {
            var sb = new StringBuilder("{\n");
            int n = 0;
            foreach (KeyValuePair<string, string> kv in d)
            {
                bool number = float.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                sb.Append("  \"").Append(kv.Key).Append("\": ").Append(number ? kv.Value : "\"" + kv.Value + "\"");
                sb.Append(++n < d.Count ? ",\n" : "\n");
            }
            return sb.Append("}\n").ToString();
        }
    }
}
