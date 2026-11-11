using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9: live position ("2nd / 6") and the minimap. On the "Racers" object in Main.unity, with its own UIDocument
    // on the race HUD's PanelSettings, drawn below the race HUD and the item slot (sorting order -2), so menus and the
    // pause card cover it. Built in code like ItemSlotHud (no UXML), so RaceHud.uxml/uss stay untouched.
    //
    // Where things sit (1280x720 reference, the HUD scales with the screen height):
    // - Minimap: right edge, under the checkpoint panel, at the same height as the item slot (which moved one map
    //   width to the left, see ItemSlotHud). Top area on purpose: on a phone the touch pedals cover the right side
    //   from about 290 px down, and the minimap must never sit under a thumb.
    // - Position: bottom right, beside the speed gauge (where arcade racers put it). On narrow screens (4:3, 16:10)
    //   the footer key bar needs that gap, so it moves above the speed gauge instead.
    // The map is fixed, not rotating: turned once so the start straight points up, then the player learns one picture
    // of the track, which reads better on a small phone screen than a spinning map (MinimapFit).
    // Everything animates on real time (Time.unscaledTime), like RaceHud; Reduced Motion = no pops, no pulsing.
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(10)] // after RaceRoster (order 0), so the readout shows this frame's standings
    public class PositionsHud : MonoBehaviour
    {
        public const float MapSize = 168f;              // the minimap tile, reference pixels
        public const float MapGap = 14f;                // between the minimap and the item slot
        public const float MapColumnWidth = MapSize + MapGap; // ItemSlotHud keeps this much free at the right edge
        private const float MapPadding = 17f;           // racing line to the tile edge
        private const float NarrowHudWidth = 1230f;     // same switch as RaceHud (hud--narrow)
        private const float GaugeWidth = 176f;          // RaceHud.uss .speed-gauge
        private const float PopSeconds = 0.42f;
        private const float ChevronSeconds = 1.4f;

        private static readonly Color MedalGold = new Color(0.949f, 0.702f, 0.239f, 1f);   // --medal-gold #F2B33D
        private static readonly Color MedalSilver = new Color(0.773f, 0.800f, 0.839f, 1f); // --medal-silver #C5CCD6
        private static readonly Color MedalBronze = new Color(0.788f, 0.541f, 0.333f, 1f); // --medal-bronze #C98A55
        // --cc-panel's colour, a little denser (0.82 there): the coloured place number must keep its contrast over bright sand, sky or a kerb
        private static readonly Color Panel = new Color(29f / 255f, 29f / 255f, 31f / 255f, 0.90f);
        private static readonly Color MapPanel = new Color(29f / 255f, 29f / 255f, 31f / 255f, 0.92f); // denser still: a map over building windows or palms must read at a glance
        private static readonly Color Lip = new Color(0f, 0f, 0f, 0.45f);                   // --cc-lip

        [SerializeField]
        private RaceManager raceManager;

        [SerializeField]
        private RaceRoster roster;

        // ---- elements ----
        private VisualElement root;
        private VisualElement mapFrame;
        private VisualElement mapArea;
        private MinimapLine road;
        private MinimapLine trail;
        private VisualElement dotLayer;
        private VisualElement playerMarker;
        private VisualElement playerHalo;
        private readonly List<VisualElement> botDots = new List<VisualElement>();
        private VisualElement positionBox;
        private Label positionNumber;
        private Label positionSuffix;
        private Label positionTotal;
        private TriangleMark chevron;

        // ---- state ----
        private MinimapFit fit;
        private List<Vector2> linePoints = new List<Vector2>();
        private Vector2 builtForSize;
        private bool shown;
        private bool positionShown;
        private bool narrow;
        private int shownPosition = -1;
        private int shownTotal = -1;
        private float changedAt = -10f;
        private bool gained;

        // For the Play Mode test and the results screen: what the readout shows right now.
        public bool IsPositionShown { get { return shown && positionShown; } }
        public string PositionText { get { return positionNumber == null ? "" : positionNumber.text + positionSuffix.text; } }
        public bool IsMapShown { get { return shown && fit != null && fit.IsValid; } }

        private void OnEnable()
        {
            Build(GetComponent<UIDocument>().rootVisualElement);
        }

        private void OnDisable()
        {
            if (root != null)
            {
                root.UnregisterCallback<GeometryChangedEvent>(OnRootResized);
                mapArea.UnregisterCallback<GeometryChangedEvent>(OnMapResized);
            }
        }

        private void Update()
        {
            if (raceManager == null || roster == null || root == null)
            {
                return;
            }
            // Same rule as the race layer of the HUD and the item slot: visible in the countdown and while racing.
            RaceState state = raceManager.State;
            bool wantShown = raceManager.IsSetUp && roster.IsSetUp && (state == RaceState.Countdown || state == RaceState.Racing);
            if (wantShown != shown)
            {
                shown = wantShown;
                root.style.opacity = shown ? 1f : 0f;
            }
            if (!shown)
            {
                return;
            }
            float now = Time.unscaledTime;
            bool reduced = raceManager.ReducedMotion;
            UpdatePosition(now, reduced);
            UpdateMap(now, reduced);
        }

        // ---------- position readout ----------

        private void UpdatePosition(float now, bool reduced)
        {
            int total = roster.RacerCount;
            int position = roster.PlayerPosition;
            // Alone on the track = the plain time trial: no "1st / 1".
            bool wanted = total > 1 && position > 0;
            if (wanted != positionShown)
            {
                positionShown = wanted;
                positionBox.style.opacity = wanted ? 1f : 0f;
                shownPosition = -1; // write the texts fresh when it comes back
            }
            if (!wanted)
            {
                return;
            }
            if (position != shownPosition || total != shownTotal)
            {
                if (shownPosition > 0 && position != shownPosition && total == shownTotal)
                {
                    changedAt = now;
                    gained = position < shownPosition;
                }
                shownPosition = position;
                shownTotal = total;
                positionNumber.text = position.ToString();
                positionSuffix.text = PositionFormat.Suffix(position).ToUpperInvariant();
                positionTotal.text = PositionFormat.OfTotal(total);
                Color colour = ColourFor(position);
                positionNumber.style.color = colour;
                positionSuffix.style.color = colour;
                positionBox.style.borderLeftColor = colour;
            }

            // Pop: 1 -> 1.28 -> 1 with a little overshoot (the item slot's pop). Reduced motion: no scale change.
            float since = now - changedAt;
            float scale = 1f;
            if (!reduced && since < PopSeconds)
            {
                float p = since / PopSeconds;
                scale = 1f + 0.28f * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.3f);
            }
            positionNumber.style.scale = new Scale(new Vector3(scale, scale, 1f));

            // Chevron: blue up = you gained a place, coral down = you lost one. Fades out (a fade is allowed with
            // reduced motion, only the little hop is left out).
            if (since < ChevronSeconds)
            {
                float p = since / ChevronSeconds;
                chevron.style.opacity = p < 0.75f ? 1f : 1f - (p - 0.75f) / 0.25f;
                chevron.SetFill(gained ? HudColors.Blue : HudColors.Coral);
                chevron.style.rotate = new Rotate(Angle.Degrees(gained ? 0f : 180f));
                float hop = reduced ? 0f : (gained ? -5f : 5f) * Mathf.Sin(Mathf.Min(1f, p * 3f) * Mathf.PI);
                chevron.style.translate = new Translate(0f, hop);
            }
            else
            {
                chevron.style.opacity = 0f;
            }
        }

        // 1st gold, 2nd silver, 3rd bronze (the medal tokens), everyone else white.
        private static Color ColourFor(int position)
        {
            if (position == 1) return MedalGold;
            if (position == 2) return MedalSilver;
            if (position == 3) return MedalBronze;
            return HudColors.White;
        }

        // ---------- minimap ----------

        private void UpdateMap(float now, bool reduced)
        {
            ITrackProgress progress = roster.Progress;
            Rect area = mapArea.contentRect;
            if (progress == null || area.width <= 1f)
            {
                mapFrame.style.opacity = 0f;
                return;
            }
            if (fit == null || builtForSize != area.size)
            {
                BuildMap(progress, area.size);
            }
            if (!fit.IsValid)
            {
                mapFrame.style.opacity = 0f;
                return;
            }
            mapFrame.style.opacity = 1f;

            IReadOnlyList<IRacer> racers = RacerRegistry.All;
            int botIndex = 0;
            bool playerSeen = false;
            for (int i = 0; i < racers.Count; i++)
            {
                IRacer racer = racers[i];
                Transform racerRoot = racer.Root;
                if (racerRoot == null)
                {
                    continue;
                }
                Vector2 point = fit.Clamp(fit.ToMap(racerRoot.position), 5f);
                if (racer.IsPlayer)
                {
                    playerSeen = true;
                    PlaceCentred(playerMarker, point, 32f);
                    PlaceCentred(playerHalo, point, 22f);
                    playerMarker.style.rotate = new Rotate(Angle.Degrees(fit.ToMapAngle(racerRoot.forward)));
                    UpdateTrail(racer, point);
                }
                else
                {
                    VisualElement dot = BotDot(botIndex);
                    botIndex++;
                    dot.style.backgroundColor = racer.MapColor;
                    dot.style.opacity = 1f;
                    PlaceCentred(dot, point, 10f);
                }
            }
            for (int i = botIndex; i < botDots.Count; i++)
            {
                botDots[i].style.opacity = 0f; // a bot that left
            }
            playerMarker.style.opacity = playerSeen ? 1f : 0f;
            playerHalo.style.opacity = playerSeen ? 1f : 0f;

            // The player's halo breathes slowly so the eye finds "me" at once. Reduced motion: still.
            float halo = reduced ? 1f : 1f + 0.18f * Mathf.Sin(now * 3.4f);
            playerHalo.style.scale = new Scale(new Vector3(halo, halo, 1f));
        }

        // The trail: the racing line from the start line up to the player (the part of the lap already driven).
        private void UpdateTrail(IRacer player, Vector2 playerPoint)
        {
            LapProgress lap = roster.Standings.ProgressOf(player);
            ITrackProgress progress = roster.Progress;
            if (lap == null || !lap.HasValue || lap.LapsDone < 0 || progress.LapLength <= 0f || linePoints.Count < 2)
            {
                trail.SetDrawCount(0);
                return;
            }
            if (lap.LapsDone >= 1)
            {
                trail.SetDrawCount(linePoints.Count);
                return;
            }
            // Rough index from the distance, then the closest line point near it, so the trail ends under the dot
            // even where the smoothed map line and the real distance differ a little.
            int count = linePoints.Count;
            int guess = Mathf.RoundToInt(lap.Distance / progress.LapLength * count);
            int window = Mathf.Max(4, count / 8);
            int best = Mathf.Clamp(guess, 0, count - 1);
            float bestSqr = float.MaxValue;
            for (int k = Mathf.Max(0, guess - window); k <= Mathf.Min(count - 1, guess + window); k++)
            {
                float sqr = (linePoints[k] - playerPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = k;
                }
            }
            trail.SetDrawCount(best + 1);
        }

        // Fits the racing line into the map tile (once, and again if the tile changes size).
        private void BuildMap(ITrackProgress progress, Vector2 size)
        {
            builtForSize = size;
            Vector3 up = raceManager.Track != null && raceManager.Track.SpawnPoint != null ? raceManager.Track.SpawnPoint.forward : Vector3.forward;
            IReadOnlyList<Vector3> line = progress.RacingLine;
            List<Vector3> world = new List<Vector3>();
            for (int i = 0; i < line.Count; i++)
            {
                world.Add(line[i]);
            }
            fit = new MinimapFit(world, up, size.x, size.y, MapPadding);
            List<Vector2> mapPoints = new List<Vector2>();
            for (int i = 0; i < world.Count; i++)
            {
                mapPoints.Add(fit.ToMap(world[i]));
            }
            mapPoints = MinimapPath.Thin(mapPoints, 120);
            int steps = Mathf.Clamp(240 / Mathf.Max(1, mapPoints.Count), 2, 12); // few corner points = more smoothing
            linePoints = MinimapPath.SmoothClosed(mapPoints, steps);
            road.SetPoints(linePoints);
            trail.SetPoints(linePoints);
        }

        private VisualElement BotDot(int index)
        {
            while (botDots.Count <= index)
            {
                VisualElement dot = new VisualElement();
                dot.pickingMode = PickingMode.Ignore;
                dot.style.position = Position.Absolute;
                dot.style.left = 0f;
                dot.style.top = 0f;
                dot.style.width = 10f;
                dot.style.height = 10f;
                SetRadius(dot, 5f);
                SetBorder(dot, 1.5f, new Color(0f, 0f, 0f, 0.8f));
                botDots.Add(dot);
                dotLayer.Insert(1, dot); // above the player's halo, under the player's dot (a bot right beside you stays visible)
            }
            return botDots[index];
        }

        private static void PlaceCentred(VisualElement element, Vector2 point, float size)
        {
            element.style.translate = new Translate(point.x - size * 0.5f, point.y - size * 0.5f);
        }

        private void OnRootResized(GeometryChangedEvent evt)
        {
            bool isNarrow = evt.newRect.width < NarrowHudWidth;
            if (isNarrow == narrow)
            {
                return;
            }
            narrow = isNarrow;
            // Wide: beside the speed gauge. Narrow: above it (the footer key bar uses the gap beside it).
            positionBox.style.marginRight = narrow ? 0f : GaugeWidth + 16f;
            positionBox.style.marginBottom = narrow ? GaugeWidth + 14f : 0f;
        }

        private void OnMapResized(GeometryChangedEvent evt)
        {
            fit = null; // rebuilt in the next Update with the new size
        }

        // ---------- building the tree (once) ----------

        private void Build(VisualElement documentRoot)
        {
            documentRoot.Clear();
            botDots.Clear();
            fit = null;
            shown = false;
            positionShown = false;
            narrow = false;

            root = new VisualElement();
            root.name = "positions-root";
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;
            root.style.alignItems = Align.Center;
            root.style.opacity = 0f;
            FadeOn(root, 0.25f);
            documentRoot.Add(root);
            root.RegisterCallback<GeometryChangedEvent>(OnRootResized);

            // Same safe frame as RaceHud.uss (.hud-safe): max 1600 wide, 26 px from the top, 34 from the sides, 24 from the bottom.
            VisualElement safe = new VisualElement();
            safe.pickingMode = PickingMode.Ignore;
            safe.style.width = Length.Percent(100f);
            safe.style.maxWidth = 1600f;
            safe.style.paddingTop = 26f;
            safe.style.paddingRight = 34f;
            safe.style.paddingLeft = 34f;
            safe.style.paddingBottom = 24f;
            safe.style.flexGrow = 1f;
            safe.style.justifyContent = Justify.SpaceBetween;
            root.Add(safe);

            BuildMapTile(safe);
            BuildPositionBox(safe);
        }

        private void BuildMapTile(VisualElement safe)
        {
            mapFrame = new VisualElement();
            mapFrame.name = "minimap";
            mapFrame.pickingMode = PickingMode.Ignore;
            mapFrame.style.alignSelf = Align.FlexEnd;
            mapFrame.style.marginTop = 86f; // under the checkpoint panel, level with the item slot
            mapFrame.style.width = MapSize;
            mapFrame.style.height = MapSize;
            mapFrame.style.backgroundColor = MapPanel;
            SetRadius(mapFrame, 22f);
            mapFrame.style.borderBottomWidth = 5f;
            mapFrame.style.borderBottomColor = Lip; // the darker "lip" every HUD tile has
            mapFrame.style.overflow = Overflow.Hidden;
            mapFrame.style.opacity = 0f;
            safe.Add(mapFrame);

            // Faint azulejo tiles in the background (the start screen's pattern), so the tile is not a flat grey box.
            AzulejoPattern pattern = new AzulejoPattern();
            pattern.style.position = Position.Absolute;
            pattern.style.left = 0f;
            pattern.style.right = 0f;
            pattern.style.top = 0f;
            pattern.style.bottom = 0f;
            pattern.style.opacity = 0.04f;
            mapFrame.Add(pattern);

            mapArea = new VisualElement();
            mapArea.pickingMode = PickingMode.Ignore;
            mapArea.style.position = Position.Absolute;
            mapArea.style.left = 0f;
            mapArea.style.right = 0f;
            mapArea.style.top = 0f;
            mapArea.style.bottom = 0f;
            mapFrame.Add(mapArea);
            mapArea.RegisterCallback<GeometryChangedEvent>(OnMapResized);

            road = new MinimapLine(true, HudColors.White);
            mapArea.Add(road);
            trail = new MinimapLine(false, PlayerRacer.PlayerColour);
            mapArea.Add(trail);

            dotLayer = new VisualElement();
            dotLayer.pickingMode = PickingMode.Ignore;
            dotLayer.style.position = Position.Absolute;
            dotLayer.style.left = 0f;
            dotLayer.style.right = 0f;
            dotLayer.style.top = 0f;
            dotLayer.style.bottom = 0f;
            mapArea.Add(dotLayer);

            // The player: a soft halo under everything (its own element, so bots beside you are drawn above it), then an
            // orange dot with a white ring and a white arrow in front, turned with the kart. Small on purpose: on a
            // 1.2 km lap one map pixel is about 3 m, so a big marker would hide every bot near you.
            playerHalo = new VisualElement();
            playerHalo.pickingMode = PickingMode.Ignore;
            playerHalo.style.position = Position.Absolute;
            playerHalo.style.left = 0f;
            playerHalo.style.top = 0f;
            playerHalo.style.width = 22f;
            playerHalo.style.height = 22f;
            SetRadius(playerHalo, 11f);
            playerHalo.style.backgroundColor = HudColors.WithAlpha(PlayerRacer.PlayerColour, 0.3f);
            playerHalo.style.opacity = 0f;
            dotLayer.Add(playerHalo);

            playerMarker = new VisualElement();
            playerMarker.pickingMode = PickingMode.Ignore;
            playerMarker.style.position = Position.Absolute;
            playerMarker.style.left = 0f;
            playerMarker.style.top = 0f;
            playerMarker.style.width = 32f;
            playerMarker.style.height = 32f;
            playerMarker.style.alignItems = Align.Center;
            playerMarker.style.justifyContent = Justify.Center;
            playerMarker.style.opacity = 0f;
            dotLayer.Add(playerMarker);

            VisualElement playerDot = new VisualElement();
            playerDot.pickingMode = PickingMode.Ignore;
            playerDot.style.width = 12f;
            playerDot.style.height = 12f;
            SetRadius(playerDot, 6f);
            SetBorder(playerDot, 2f, Color.white);
            playerDot.style.backgroundColor = PlayerRacer.PlayerColour;
            playerMarker.Add(playerDot);

            TriangleMark arrow = new TriangleMark(Color.white, new Color(0f, 0f, 0f, 0.7f), 1.2f);
            arrow.style.position = Position.Absolute;
            arrow.style.width = 12f;
            arrow.style.height = 10f;
            arrow.style.left = 10f;
            arrow.style.top = 0f;
            playerMarker.Add(arrow);
        }

        private void BuildPositionBox(VisualElement safe)
        {
            // Bottom right, beside the speed gauge: big "2", "ND" and "/ 6" stacked to its right.
            positionBox = new VisualElement();
            positionBox.name = "position";
            positionBox.pickingMode = PickingMode.Ignore;
            positionBox.style.alignSelf = Align.FlexEnd;
            positionBox.style.marginRight = GaugeWidth + 16f;
            positionBox.style.flexDirection = FlexDirection.Row;
            positionBox.style.alignItems = Align.Center;
            positionBox.style.backgroundColor = Panel;
            SetRadius(positionBox, 16f);
            positionBox.style.borderBottomWidth = 5f;
            positionBox.style.borderBottomColor = Lip;
            positionBox.style.borderLeftWidth = 6f;
            positionBox.style.borderLeftColor = HudColors.White;
            positionBox.style.paddingLeft = 16f;
            positionBox.style.paddingRight = 20f;
            positionBox.style.paddingTop = 2f;
            positionBox.style.paddingBottom = 6f;
            positionBox.style.opacity = 0f;
            FadeOn(positionBox, 0.25f);
            safe.Add(positionBox);

            positionNumber = MakeLabel("1", 84f, HudColors.White);
            positionNumber.style.unityTextAlign = TextAnchor.MiddleCenter;
            positionNumber.style.minWidth = 52f;
            positionNumber.style.textShadow = new TextShadow { offset = new Vector2(0f, 3f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.35f) };
            positionNumber.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(60f));
            positionBox.Add(positionNumber);

            VisualElement side = new VisualElement();
            side.pickingMode = PickingMode.Ignore;
            side.style.marginLeft = 8f;
            side.style.translate = new Translate(0f, 1.75f); // "TH" lines up with the top of the big number, "/ 6" with its baseline
            side.style.justifyContent = Justify.Center;
            positionBox.Add(side);

            VisualElement suffixRow = new VisualElement();
            suffixRow.pickingMode = PickingMode.Ignore;
            suffixRow.style.flexDirection = FlexDirection.Row;
            suffixRow.style.alignItems = Align.Center;
            side.Add(suffixRow);

            positionSuffix = MakeLabel("ST", 30f, HudColors.White);
            positionSuffix.style.letterSpacing = 1f;
            suffixRow.Add(positionSuffix);

            chevron = new TriangleMark(HudColors.Blue, new Color(0f, 0f, 0f, 0.35f), 1f);
            chevron.style.width = 16f;
            chevron.style.height = 13f;
            chevron.style.marginLeft = 8f;
            chevron.style.opacity = 0f;
            suffixRow.Add(chevron);

            positionTotal = MakeLabel("/ 1", 24f, HudColors.WithAlpha(HudColors.White, 0.7f));
            positionTotal.style.marginTop = 7f; // its baseline sits on the big number's baseline
            positionTotal.style.letterSpacing = 1f;
            side.Add(positionTotal);
        }

        // ---------- small helpers (same look rules as ItemSlotHud) ----------

        private static Label MakeLabel(string text, float size, Color colour)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
            label.style.paddingTop = 0f;
            label.style.paddingBottom = 0f;
            return label;
        }

        // A USS opacity fade set from code (a fade is fine with reduced motion: it is not movement).
        private static void FadeOn(VisualElement element, float seconds)
        {
            element.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            element.style.transitionDuration = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetBorder(VisualElement element, float width, Color colour)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }
    }
}
