using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // QA aid (phone round 17.11): with ?test=uirects in the page address, the Console gets one line
    // "UIRECTS {...}" every time the set of visible menu buttons changes: each button's name, text and box in HUD
    // pixels plus the panel size, so a phone test can measure the tap targets (CSS px = HUD px * canvas height / panel
    // height) and check them against the page's own buttons. Without the flag nothing is created.
    public class UiRectLog : MonoBehaviour
    {
        private const float Interval = 0.4f;
        private string last;
        private float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!HasTestFlag(Application.absoluteURL, "uirects"))
            {
                return;
            }
            GameObject host = new GameObject("UiRectLog");
            DontDestroyOnLoad(host);
            host.AddComponent<UiRectLog>();
        }

        // True when the address has test=<value> (several values may share one flag: ?test=a,b).
        public static bool HasTestFlag(string url, string value)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(value))
            {
                return false;
            }
            int hash = url.IndexOf('#');
            if (hash >= 0)
            {
                url = url.Substring(0, hash);
            }
            int query = url.IndexOf('?');
            if (query < 0)
            {
                return false;
            }
            string[] pairs = url.Substring(query + 1).Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int equals = pairs[i].IndexOf('=');
                if (equals <= 0 || !string.Equals(pairs[i].Substring(0, equals), "test", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] values = Uri.UnescapeDataString(pairs[i].Substring(equals + 1)).Split(',');
                for (int v = 0; v < values.Length; v++)
                {
                    if (string.Equals(values[v].Trim(), value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void Update()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }
            next = Time.unscaledTime + Interval;
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            List<VisualElement> buttons = new List<VisualElement>();
            Rect panel = Rect.zero;
            for (int d = 0; d < documents.Length; d++)
            {
                VisualElement root = documents[d].rootVisualElement;
                if (root == null || root.panel == null)
                {
                    continue;
                }
                panel = root.panel.visualTree.layout; // every HUD document shares the same panel settings
                Collect(root, buttons);
            }
            string line = Describe(panel, buttons);
            if (line != last)
            {
                last = line;
                Debug.Log("UIRECTS " + line);
            }
        }

        // The tappable menu things of one document: pills (menus, mode cards, lobby), RETRY, driver cards, text fields.
        private static void Collect(VisualElement root, List<VisualElement> buttons)
        {
            root.Query<VisualElement>(className: "menu-pill").ForEach(buttons.Add);
            root.Query<VisualElement>(className: "retry-button").ForEach(buttons.Add);
            root.Query<VisualElement>(className: "plate").Where(e => e.name != null && e.name.StartsWith("driver-", StringComparison.Ordinal) && e.name.Length <= 8).ForEach(buttons.Add);
            root.Query<TextField>().ForEach(buttons.Add);
        }

        private static string Describe(Rect panel, List<VisualElement> buttons)
        {
            StringBuilder text = new StringBuilder();
            text.Append("{\"w\":").Append(F(panel.width)).Append(",\"h\":").Append(F(panel.height)).Append(",\"e\":[");
            bool first = true;
            for (int i = 0; i < buttons.Count; i++)
            {
                VisualElement element = buttons[i];
                Rect box = element.worldBound;
                if (float.IsNaN(box.width) || box.width < 1f || box.height < 1f || Opacity(element) < 0.5f)
                {
                    continue;
                }
                if (!first)
                {
                    text.Append(',');
                }
                first = false;
                text.Append("[\"").Append(Clean(element.name)).Append("\",\"").Append(Clean(Caption(element))).Append("\",")
                    .Append(F(box.x)).Append(',').Append(F(box.y)).Append(',').Append(F(box.width)).Append(',').Append(F(box.height))
                    .Append(',').Append(element.pickingMode == PickingMode.Position ? '1' : '0').Append(']');
            }
            text.Append("]}");
            return text.ToString();
        }

        // Visible = every element up to the root is displayed and the opacities multiply to more than half.
        private static float Opacity(VisualElement element)
        {
            float opacity = 1f;
            for (VisualElement e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return 0f;
                }
                opacity *= e.resolvedStyle.opacity;
            }
            return opacity;
        }

        private static string Caption(VisualElement element)
        {
            if (element is TextElement own && !string.IsNullOrEmpty(own.text))
            {
                return own.text;
            }
            // The first label that shows (on touch the keycap labels are hidden).
            Label shown = null;
            element.Query<Label>().ForEach(l => { if (shown == null && Opacity(l) > 0.5f) shown = l; });
            return shown != null ? shown.text : "";
        }

        private static string Clean(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "").Replace("\"", "'").Replace("\n", " ");
        }

        private static string F(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
