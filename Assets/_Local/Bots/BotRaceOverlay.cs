using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). A small list on the right of the screen, drawn on top of the team's HUD:
    // the player's position (P2/6), the lap, everybody in race order, and the finish order at the end.
    // Drawn with Unity's simple OnGUI so the team's HUD files stay untouched.
    public class BotRaceOverlay : MonoBehaviour
    {
        [SerializeField] private BotRace race;

        private GUIStyle bigStyle;
        private GUIStyle rowStyle;
        private GUIStyle smallStyle;

        private void OnGUI()
        {
            if (race == null || !race.IsSetUp)
            {
                return;
            }
            if (bigStyle == null)
            {
                bigStyle = MakeStyle(40, FontStyle.Bold);
                rowStyle = MakeStyle(17, FontStyle.Bold);
                smallStyle = MakeStyle(13, FontStyle.Bold);
            }

            // Draw as if the screen were 720 pixels high, then scale: same look in every window size.
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale;

            RaceKart[] order = race.Standings;
            RaceKart player = race.Player;
            bool showResults = player.Finished || race.RaceManager.State == RaceState.Results;

            float width = 214f;
            float left = screenWidth - width - 18f;
            float top = 150f;
            float height = 96f + order.Length * 25f;

            // Start screen: only one small line (which bots, how to change them), so the title stays free.
            if (race.RaceManager.State == RaceState.Ready)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(left, top, width, 30f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                Label(new Rect(left + 14f, top + 5f, 200f, 20f), "BOTS: " + race.Difficulty.ToString().ToUpperInvariant() + "  (keys 1 2 3)",
                    smallStyle, Color.white);
                return;
            }
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(left, top, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Label(new Rect(left + 14f, top + 6f, 120f, 48f), "P" + race.PositionOf(player) + "/" + order.Length, bigStyle, Color.white);
            int lap = Mathf.Clamp(player.Lap + 1, 1, race.TotalLaps);
            Label(new Rect(left + 130f, top + 12f, 80f, 20f), "LAP", smallStyle, new Color(1f, 1f, 1f, 0.7f));
            Label(new Rect(left + 130f, top + 27f, 80f, 24f), lap + "/" + race.TotalLaps, rowStyle, Color.white);
            Label(new Rect(left + 14f, top + 62f, 190f, 20f), showResults ? "FINISH ORDER" : "RACE ORDER", smallStyle, new Color(1f, 0.75f, 0.3f));

            if (!race.RaceRunning)
            {
                // Before GO: which bots you race against, and how to change it.
                Label(new Rect(left + 14f, top + height + 4f, 200f, 20f), "BOTS: " + race.Difficulty.ToString().ToUpperInvariant() + "  (keys 1 2 3)",
                    smallStyle, new Color(1f, 1f, 1f, 0.85f));
            }

            for (int i = 0; i < order.Length; i++)
            {
                RaceKart kart = order[i];
                float y = top + 86f + i * 25f;
                if (kart.IsPlayer)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.18f); // highlight the player's row
                    GUI.DrawTexture(new Rect(left + 6f, y - 1f, width - 12f, 23f), Texture2D.whiteTexture);
                }
                GUI.color = kart.Colour;
                GUI.DrawTexture(new Rect(left + 34f, y + 5f, 11f, 11f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                Label(new Rect(left + 14f, y, 20f, 22f), (i + 1).ToString(), rowStyle, Color.white);
                Label(new Rect(left + 52f, y, 100f, 22f), kart.DisplayName, rowStyle, Color.white);

                string right = "L" + Mathf.Clamp(kart.Lap + 1, 1, race.TotalLaps);
                if (kart.Finished)
                {
                    right = FormatTime(kart.FinishTime);
                }
                Label(new Rect(left + 138f, y + 2f, 68f, 20f), right, smallStyle, new Color(1f, 1f, 1f, 0.8f));
            }
        }

        private static GUIStyle MakeStyle(int size, FontStyle fontStyle)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.wordWrap = false;
            return style;
        }

        // Text with a dark copy one pixel below it, so it stays readable on a bright sky.
        private static void Label(Rect rect, string text, GUIStyle style, Color colour)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(rect, text, style);
        }

        // 83.42 s -> "1:23.4"
        public static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes + ":" + rest.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
