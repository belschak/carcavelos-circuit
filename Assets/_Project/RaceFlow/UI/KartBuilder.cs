using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The kart builder on the start screen (Luc's idea, 18 Nov 2026; it replaces the driver select of 17 Nov), between
    // the race mode and the countdown, in every mode: pick a mode -> this panel -> READY. Offline READY starts the
    // countdown; online READY opens the online lobby (OnlineRaceHooks.OpenLobby), which starts the race for the room.
    // Like the kart select of a classic 3DS kart racer, without the glider: three columns DRIVER | KART | TYRES with four
    // tiles each, and the finished kart turning on a turntable on the left (KartStage: Role 3's real kart model, built
    // for exactly this set by KartAssembler). 4 x 4 x 4 = 64 sets, all looks only: every set drives the same kart.
    // - Mouse or finger: click/tap a tile = pick it (the kart on the turntable changes at once with a small pop). The
    //   turntable turns by itself; drag it to turn it by hand. The mouse on a tile shows its one line under the columns.
    // - Keys: A/D or left/right = column, W/S or up/down = pick inside the column, Enter or Space = READY,
    //   Escape or Backspace = back to the modes.
    // - Picking another driver keeps the kart and tyres (like the original). The tiles of the driver's own kart and
    //   tyres carry the driver's face in a small orange ring, so "Tiago's buggy" is easy to find again.
    // - The set is saved at once (DriverChoice), so the next visit opens with it.
    // Built in code like ModePicker, so Main.unity and RaceHud.uxml need no change.
    public class KartBuilder
    {
        public const int DriverColumn = 0;
        public const int KartColumn = 1;
        public const int TyreColumn = 2;
        public const int ColumnCount = 3;

        public const float TileWidth = 150f;
        public const float SidePadding = 28f;       // left and right of the builder
        public const float StageMinWidth = 300f;    // the turntable shrinks first on narrow windows
        public const float StageGap = 16f;          // between the turntable and the DRIVER column
        public const float ColumnGap = 10f;         // between the columns
        public const float TileHeight = 92f;        // >= 44 CSS px on an iPhone SE in landscape (375 px high: 0.52 px each)
        private const float TileGap = 8f;
        private const float BadgeSize = 22f;
        private const int TileShotWidth = 288;       // the tile pictures: about 2x their size on a 1080p screen
        private const int TileShotHeight = 124;
        private const float DragDegreesPerPixel = 0.6f;
        private const float SpinAgainAfterSeconds = 2.5f;
        private static readonly string[] ColumnTitles = { "DRIVER", "KART", "TYRES" };

        private readonly RaceManager raceManager;
        private readonly VisualElement hudRoot;
        private readonly VisualElement startScreen;
        private readonly VisualElement panel;
        private readonly VisualElement[,] tiles = new VisualElement[ColumnCount, KartLoadout.PartCount];
        private readonly Label[,] tileNames = new Label[ColumnCount, KartLoadout.PartCount];
        private readonly VisualElement[,] tilePictures = new VisualElement[ColumnCount, KartLoadout.PartCount];
        private readonly VisualElement[,] badges = new VisualElement[ColumnCount, KartLoadout.PartCount];
        private readonly Label[] columnHeads = new Label[ColumnCount];
        private readonly VisualElement preview;
        private readonly VisualElement previewShadow;
        private readonly Label previewTitle;
        private readonly Label previewNote;
        private readonly Label line;
        private readonly VisualElement readyButton;
        private readonly VisualElement backButton;
        private readonly List<VisualElement> hiddenWhileOpen = new List<VisualElement>();
        private readonly List<Texture2D> portraits = new List<Texture2D>();
        private readonly List<RenderTexture> tileShots = new List<RenderTexture>();

        private KartStage stage;
        private bool tilesShot;
        private KartLoadout shownOnStage;
        private bool stageHasKart;
        private int hoveredColumn = -1, hoveredRow = -1;
        private int pressedColumn = -1, pressedRow = -1;
        private int closedFrame = -1;
        private bool readyThisFrame;
        private bool backThisFrame;
        private bool dragging;
        private float dragX;
        private float lastDragTime = -10f;

        // The panel is on screen (it takes every key and click of the start screen while open).
        public bool IsOpen { get; private set; }

        // True while a click or key belongs to the panel (and in the frame it closed), so it never ALSO counts as
        // "any key" (RaceManager.MenuUsesInput).
        public bool UsesInput { get { return IsOpen || closedFrame == Time.frameCount; } }

        // The column the keys move in (0 driver, 1 kart, 2 tyres).
        public int Column { get; private set; }

        // The set shown and saved (DriverChoice.Loadout).
        public KartLoadout Loadout { get { return DriverChoice.Loadout; } }

        // The turntable studio (Play Mode tests check it shows the picked set). Null until the first Open.
        public KartStage Stage { get { return stage; } }

        public event Action<MenuSound> SoundRequested;

        public KartBuilder(VisualElement root, RaceManager raceManager)
        {
            this.raceManager = raceManager;
            hudRoot = root.Q<VisualElement>("hud-root");
            startScreen = root.Q<VisualElement>("start-screen");
            AddIfFound(root.Q<VisualElement>("start-layout"));  // title, modes, menu row and the leaderboard
            AddIfFound(startScreen != null ? startScreen.Q<VisualElement>(className: "ts-footer") : null);

            panel = new VisualElement { name = "kart-builder" };
            panel.style.position = Position.Absolute;
            panel.style.left = 0f;
            panel.style.right = 0f;
            panel.style.top = 0f;
            panel.style.bottom = 0f;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.display = DisplayStyle.None;
            panel.pickingMode = PickingMode.Ignore;

            // The world behind: darkened like behind every menu card, so the tiles read on the bright noon map.
            VisualElement backdrop = new VisualElement();
            backdrop.AddToClassList("menu-bg");
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0f;
            backdrop.style.right = 0f;
            backdrop.style.top = 0f;
            backdrop.style.bottom = 0f;
            backdrop.pickingMode = PickingMode.Ignore;
            panel.Add(backdrop);

            // ---- heading: BUILD YOUR KART + the fairness note ----
            VisualElement head = Row();
            head.style.alignItems = Align.FlexEnd;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.width = Length.Percent(100f);
            head.style.maxWidth = 1180f;
            head.style.paddingLeft = 28f;
            head.style.paddingRight = 28f;
            head.style.marginBottom = 10f;
            Label heading = new Label("BUILD YOUR KART") { name = "kart-builder-title" };
            heading.AddToClassList("menu-title");
            heading.AddToClassList("f-display");
            heading.pickingMode = PickingMode.Ignore;
            head.Add(heading);
            Label fair = new Label("LOOKS ONLY: EVERY SET DRIVES THE SAME") { name = "kart-builder-fair" };
            fair.AddToClassList("key-text");
            fair.AddToClassList("f-ui");
            fair.style.fontSize = 14f;
            fair.style.marginBottom = 10f;
            fair.pickingMode = PickingMode.Ignore;
            head.Add(fair);
            panel.Add(head);

            // ---- body: turntable left, three columns right ----
            VisualElement body = Row();
            body.style.width = Length.Percent(100f);
            body.style.maxWidth = 1180f;
            body.style.paddingLeft = SidePadding;
            body.style.paddingRight = SidePadding;
            body.style.alignItems = Align.Stretch;

            VisualElement stagePlate = new VisualElement { name = "kart-builder-stage" };
            stagePlate.AddToClassList("plate");
            stagePlate.AddToClassList("kb-stage");   // UI/FrontEnd.uss: the big glass panel picture
            stagePlate.style.flexGrow = 1f;
            stagePlate.style.flexShrink = 1f;
            stagePlate.style.minWidth = StageMinWidth;
            stagePlate.style.marginRight = StageGap;
            stagePlate.style.overflow = Overflow.Hidden;
            stagePlate.pickingMode = PickingMode.Ignore;

            previewShadow = new VisualElement { name = "kart-builder-shadow" };
            previewShadow.style.position = Position.Absolute;
            previewShadow.style.left = Length.Percent(18f);
            previewShadow.style.right = Length.Percent(18f);
            previewShadow.style.bottom = Length.Percent(14f);
            previewShadow.style.height = Length.Percent(20f);
            previewShadow.style.backgroundImage = new StyleBackground(SoftShadow());
            previewShadow.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            previewShadow.pickingMode = PickingMode.Ignore;
            stagePlate.Add(previewShadow);

            preview = new VisualElement { name = "kart-builder-preview" };
            preview.style.position = Position.Absolute;
            preview.style.left = 0f;
            preview.style.right = 0f;
            preview.style.top = 0f;
            preview.style.bottom = 0f;
            preview.RegisterCallback<GeometryChangedEvent>(evt => SizePicture());
            preview.RegisterCallback<PointerDownEvent>(OnPreviewDown);
            preview.RegisterCallback<PointerMoveEvent>(OnPreviewMove);
            preview.RegisterCallback<PointerUpEvent>(OnPreviewUp);
            preview.RegisterCallback<PointerCaptureOutEvent>(evt => dragging = false);
            stagePlate.Add(preview);

            VisualElement caption = new VisualElement();
            caption.style.position = Position.Absolute;
            caption.style.left = 18f;
            caption.style.right = 18f;
            caption.style.bottom = 14f;
            caption.style.alignItems = Align.Center;
            caption.pickingMode = PickingMode.Ignore;
            previewTitle = new Label(" ") { name = "kart-builder-set" };
            previewTitle.AddToClassList("f-display");
            previewTitle.style.fontSize = 20f;
            previewTitle.style.color = HudColors.Cream;
            previewTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            previewTitle.style.whiteSpace = WhiteSpace.Normal;
            previewTitle.pickingMode = PickingMode.Ignore;
            caption.Add(previewTitle);
            previewNote = new Label(" ") { name = "kart-builder-note" };
            previewNote.AddToClassList("key-text");
            previewNote.AddToClassList("f-ui");
            previewNote.style.fontSize = 13f;
            previewNote.style.marginTop = 2f;
            previewNote.pickingMode = PickingMode.Ignore;
            caption.Add(previewNote);
            stagePlate.Add(caption);
            body.Add(stagePlate);

            for (int c = 0; c < ColumnCount; c++)
            {
                body.Add(MakeColumn(c));
            }
            panel.Add(body);

            // ---- the hovered part's line, then BACK and READY ----
            line = new Label(" ") { name = "kart-builder-line" };
            line.AddToClassList("key-text");
            line.AddToClassList("f-ui");
            line.style.fontSize = 17f;
            line.style.marginTop = 12f;
            line.style.height = 24f;
            line.style.unityTextAlign = TextAnchor.MiddleCenter;
            line.pickingMode = PickingMode.Ignore;
            panel.Add(line);

            VisualElement buttons = Row();
            buttons.style.alignItems = Align.Center;
            buttons.style.marginTop = 8f;
            backButton = MakeButton("kart-builder-back", "ESC", "BACK", false);
            backButton.RegisterCallback<ClickEvent>(evt => backThisFrame = true);
            buttons.Add(backButton);
            readyButton = MakeButton("kart-builder-ready", "ENTER", "READY!", true);
            readyButton.RegisterCallback<ClickEvent>(evt => readyThisFrame = true);
            buttons.Add(readyButton);
            panel.Add(buttons);

            if (startScreen != null)
            {
                startScreen.Add(panel);
            }
            else
            {
                root.Add(panel);
            }
            raceManager.StartRequested += Open;
        }

        // RaceHud.OnDisable: without the HUD, "any key" must start the race again (RaceManager.RequestStart).
        public void Dispose()
        {
            raceManager.StartRequested -= Open;
            for (int i = 0; i < portraits.Count; i++)
            {
                Resources.UnloadAsset(portraits[i]);
            }
            portraits.Clear();
            for (int i = 0; i < tileShots.Count; i++)
            {
                if (tileShots[i] != null)
                {
                    tileShots[i].Release();
                    UnityEngine.Object.Destroy(tileShots[i]);
                }
            }
            tileShots.Clear();
            if (stage != null)
            {
                UnityEngine.Object.Destroy(stage.gameObject);
            }
        }

        private void AddIfFound(VisualElement element)
        {
            if (element != null)
            {
                hiddenWhileOpen.Add(element);
            }
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.pickingMode = PickingMode.Ignore;
            return row;
        }

        // ---------- columns and tiles ----------
        private VisualElement MakeColumn(int column)
        {
            VisualElement box = new VisualElement { name = "kart-builder-column-" + column };
            box.style.width = TileWidth;
            box.style.flexShrink = 0f;
            box.style.marginLeft = column == 0 ? 0f : ColumnGap;
            box.pickingMode = PickingMode.Ignore;

            Label head = new Label(ColumnTitles[column]);
            head.AddToClassList("f-display");
            head.style.fontSize = 18f;
            head.style.letterSpacing = 2f;
            head.style.color = HudColors.Cream;
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            head.style.paddingBottom = 4f;
            head.style.marginBottom = 8f;
            head.style.borderBottomWidth = 3f;
            head.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
            head.pickingMode = PickingMode.Ignore;
            columnHeads[column] = head;
            box.Add(head);

            for (int row = 0; row < KartLoadout.PartCount; row++)
            {
                box.Add(MakeTile(column, row));
            }
            return box;
        }

        private VisualElement MakeTile(int column, int row)
        {
            VisualElement tile = new VisualElement { name = "kart-tile-" + column + "-" + row };
            tile.AddToClassList("menu-pill");
            tile.AddToClassList("plate");
            tile.style.height = TileHeight;
            tile.style.marginBottom = row < KartLoadout.PartCount - 1 ? TileGap : 0f;
            tile.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
            tile.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };

            VisualElement picture = new VisualElement();
            picture.style.position = Position.Absolute;
            picture.style.left = 6f;
            picture.style.right = 6f;
            picture.style.top = 5f;
            picture.style.bottom = 26f;
            picture.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            picture.pickingMode = PickingMode.Ignore;
            if (column == DriverColumn)
            {
                DriverInfo driver = DriverRoster.Get(row);
                Texture2D portrait = Resources.Load<Texture2D>("DriverPortraits/" + driver.PortraitFile);
                if (portrait != null)
                {
                    portraits.Add(portrait);
                    picture.style.backgroundImage = new StyleBackground(portrait);
                }
            }
            tilePictures[column, row] = picture;
            tile.Add(picture);

            Label name = new Label(PartName(column, row));
            name.style.position = Position.Absolute;
            name.style.left = 0f;
            name.style.right = 0f;
            name.style.bottom = 8f;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.AddToClassList("f-ui");
            name.style.fontSize = 14f;
            name.style.letterSpacing = 1f;
            name.style.color = HudColors.Cream;
            name.pickingMode = PickingMode.Ignore;
            tileNames[column, row] = name;
            tile.Add(name);

            if (column != DriverColumn)
            {
                // Whose own part this is: that driver's face in a small ring (orange when it is the picked driver's).
                VisualElement badge = new VisualElement();
                badge.style.position = Position.Absolute;
                badge.style.right = 5f;
                badge.style.top = 5f;
                badge.style.width = BadgeSize;
                badge.style.height = BadgeSize;
                SetRadius(badge, BadgeSize * 0.5f);
                SetBorder(badge, 2f, new Color(1f, 1f, 1f, 0.35f));
                badge.style.backgroundColor = HudColors.WithAlpha(DriverRoster.Get(row).Colour, 0.6f);
                badge.style.overflow = Overflow.Hidden;
                Texture2D face = Resources.Load<Texture2D>("DriverPortraits/" + DriverRoster.Get(row).PortraitFile);
                if (face != null)
                {
                    portraits.Add(face);
                    badge.style.backgroundImage = new StyleBackground(face);
                    badge.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                }
                badge.pickingMode = PickingMode.Ignore;
                badges[column, row] = badge;
                tile.Add(badge);
            }

            tile.RegisterCallback<PointerEnterEvent>(evt => SetHovered(column, row));
            tile.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (hoveredColumn != column || hoveredRow != row)
                {
                    SetHovered(column, row); // the mouse moves again after a key pick
                }
            });
            tile.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (pressedColumn == column && pressedRow == row)
                {
                    pressedColumn = -1;
                    pressedRow = -1;
                }
                if (hoveredColumn == column && hoveredRow == row)
                {
                    SetHovered(-1, -1);
                }
            });
            tile.RegisterCallback<PointerDownEvent>(evt =>
            {
                pressedColumn = column;
                pressedRow = row;
                ShowState();
            });
            tile.RegisterCallback<PointerUpEvent>(evt =>
            {
                pressedColumn = -1;
                pressedRow = -1;
                ShowState();
            });
            tile.RegisterCallback<ClickEvent>(evt => Pick(column, row));
            tiles[column, row] = tile;
            return tile;
        }

        public static string PartName(int column, int row)
        {
            if (column == DriverColumn)
            {
                return DriverRoster.Get(row).Name.ToUpperInvariant();
            }
            return column == KartColumn ? KartLoadout.KartNames[row] : KartLoadout.TyreNames[row];
        }

        public static string PartLine(int column, int row)
        {
            if (column == DriverColumn)
            {
                return DriverRoster.Get(row).Line;
            }
            return column == KartColumn ? KartLoadout.KartLines[row] : KartLoadout.TyreLines[row];
        }

        // READY (orange, big) and BACK (glass, small), each with its key on a keycap (hidden on touch screens).
        private static VisualElement MakeButton(string name, string key, string text, bool main)
        {
            VisualElement button = new VisualElement { name = name };
            button.AddToClassList("menu-pill");
            button.AddToClassList("plate");
            if (main)
            {
                button.AddToClassList("plate--orange");
            }
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.height = ButtonHeight(main, WebPage.TouchMode);
            button.style.paddingLeft = 16f;
            button.style.paddingRight = 20f;
            button.style.paddingBottom = 6f;
            button.style.marginLeft = 8f;
            button.style.marginRight = 8f;

            VisualElement keycap = new VisualElement();
            keycap.AddToClassList("keycap");
            keycap.AddToClassList("keycap--small");
            keycap.pickingMode = PickingMode.Ignore;
            Label keyLabel = new Label(key);
            keyLabel.AddToClassList("f-ui");
            keyLabel.pickingMode = PickingMode.Ignore;
            keycap.Add(keyLabel);
            button.Add(keycap);

            Label label = new Label(text);
            label.AddToClassList(main ? "f-display" : "f-ui");
            label.style.fontSize = main ? 30f : 18f;
            label.style.marginLeft = 10f;
            label.style.color = main ? HudColors.Ink : HudColors.Cream;
            if (main)
            {
                label.style.unityTextOutlineWidth = 0f;
            }
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);
            return button;
        }

        // Phone round 17.11: a thumb needs a bigger target: on touch >= 36 CSS px on every phone in landscape.
        public static float ButtonHeight(bool main, bool touch)
        {
            return main ? (touch ? 88f : 64f) : (touch ? 82f : 48f);
        }

        // A soft dark ellipse under the turntable (the kart floats on a see-through picture, this grounds it).
        private static Texture2D softShadow;

        private static Texture2D SoftShadow()
        {
            if (softShadow != null)
            {
                return softShadow;
            }
            const int width = 128, height = 32;
            softShadow = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "KartBuilderShadow", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) / width * 2f - 1f;
                    float dy = (y + 0.5f) / height * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    softShadow.SetPixel(x, y, new Color(1f, 0.82f, 0.62f, a * a * 0.16f)); // a warm pool of light on the floor
                }
            }
            softShadow.Apply();
            return softShadow;
        }

        // ---------- open / close ----------

        // "Any key" or a mode card on the start screen: the builder takes the start screen's place.
        private void Open()
        {
            if (IsOpen || raceManager.State != RaceState.Ready || !raceManager.IsSetUp || raceManager.IsPaused
                || raceManager.ExternalStartOnly)
            {
                return; // ExternalStartOnly: the online lobby is open and owns the start screen
            }
            IsOpen = true;
            hoveredColumn = -1;
            hoveredRow = -1;
            pressedColumn = -1;
            pressedRow = -1;
            Column = DriverColumn;
            bool touch = WebPage.TouchMode;
            readyButton.style.height = ButtonHeight(true, touch);
            backButton.style.height = ButtonHeight(false, touch);
            panel.style.display = DisplayStyle.Flex;
            for (int i = 0; i < hiddenWhileOpen.Count; i++)
            {
                hiddenWhileOpen[i].style.display = DisplayStyle.None;
            }
            ShowPostcard(false);
            EnsureStage();
            ShowOnStage();
            ShowState();
            PlaySound(MenuSound.Open);
        }

        // Back to the modes (BACK, Escape) or away because the race starts.
        private void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            closedFrame = Time.frameCount;
            pressedColumn = -1;
            pressedRow = -1;
            dragging = false;
            SetHovered(-1, -1);
            panel.style.display = DisplayStyle.None;
            for (int i = 0; i < hiddenWhileOpen.Count; i++)
            {
                hiddenWhileOpen[i].style.display = StyleKeyword.Null;
            }
            ShowPostcard(true);
            if (stage != null)
            {
                stage.Rendering = false;
            }
        }

        // The "on the track" postcard (TitleScreen adds it later than this panel is made, so it is looked up here).
        private void ShowPostcard(bool shown)
        {
            VisualElement postcard = startScreen != null ? startScreen.Q<VisualElement>("start-postcard") : null;
            if (postcard != null)
            {
                postcard.style.display = shown ? StyleKeyword.Null : DisplayStyle.None;
            }
        }

        // Called by RaceHud.Update every frame (before RaceManager reads the keys, DefaultExecutionOrder -50).
        public void Update(RaceState state, bool otherMenuOpen)
        {
            if (IsOpen && state != RaceState.Ready)
            {
                Close(); // the race started (READY, or a test called BeginRace)
            }
            bool active = IsOpen && raceManager.IsSetUp && !otherMenuOpen && !raceManager.IsPaused;
            for (int c = 0; c < ColumnCount; c++)
            {
                for (int r = 0; r < KartLoadout.PartCount; r++)
                {
                    HudFx.SetClickable(tiles[c, r], active);
                }
            }
            HudFx.SetClickable(readyButton, active);
            HudFx.SetClickable(backButton, active);
            HudFx.SetClickable(preview, active);
            if (stage != null)
            {
                stage.Rendering = IsOpen;
                stage.Spins = !dragging && Time.unscaledTime - lastDragTime > SpinAgainAfterSeconds;
            }
            if (!active)
            {
                readyThisFrame = false;
                backThisFrame = false;
                return;
            }

            int across = RaceKeys.MenuHorizontal();
            if (across != 0)
            {
                Column = Mathf.Clamp(Column + across, 0, ColumnCount - 1);
                hoveredColumn = -1; // a key wins over a mouse that only rests on a tile (QA 17.11.2026)
                PlaySound(MenuSound.SliderStep);
                ShowState();
            }
            int down = RaceKeys.MenuVertical();
            if (down != 0)
            {
                hoveredColumn = -1;
                Pick(Column, KartLoadout.Step(PickedRow(Column), down));
            }
            if (backThisFrame || RaceKeys.BackPressed())
            {
                backThisFrame = false;
                Close();
                PlaySound(MenuSound.Close);
                return;
            }
            if (readyThisFrame || RaceKeys.ReadyPressed())
            {
                readyThisFrame = false;
                Ready();
            }
            readyThisFrame = false;
        }

        // The row picked in a column right now.
        public int PickedRow(int column)
        {
            KartLoadout set = DriverChoice.Loadout;
            return column == DriverColumn ? set.Driver : (column == KartColumn ? set.Kart : set.Tyres);
        }

        // Picks a part (click, tap or W/S). Saved at once, so the player's kart on the grid follows too.
        public void Pick(int column, int row)
        {
            if (column < 0 || column >= ColumnCount || !KartLoadout.IsPart(row))
            {
                return;
            }
            Column = column;
            bool done;
            if (column == DriverColumn)
            {
                done = DriverChoice.Select(row);
            }
            else if (column == KartColumn)
            {
                done = DriverChoice.SelectKart(row);
            }
            else
            {
                done = DriverChoice.SelectTyres(row);
            }
            if (!done)
            {
                return;
            }
            ShowOnStage();
            ShowState();
            PlaySound(MenuSound.Toggle);
        }

        // Kept for the 17 Nov tests and the online lobby flow: picking a driver keeps the kart and tyres.
        public void PickDriver(int driver)
        {
            Pick(DriverColumn, driver);
        }

        // READY: offline the countdown begins in the mode picked before; online the lobby opens (the set is saved
        // first, so the lobby sends it to the room: DriverChoice.Loadout.Code).
        public void Ready()
        {
            DriverChoice.SelectLoadout(DriverChoice.Loadout);
            Close();
            if (MenuInputRules.ReadyOpensLobby(raceManager.Mode))
            {
                if (OnlineRaceHooks.OpenLobby != null)
                {
                    OnlineRaceHooks.OpenLobby(raceManager);
                }
                return;
            }
            raceManager.BeginRace();
        }

        private void SetHovered(int column, int row)
        {
            hoveredColumn = column;
            hoveredRow = row;
            ShowState();
        }

        // Picked tiles in glass with an orange edge, the column the keys are in with an orange underline, the hovered
        // tile a little bigger, the line under the columns for the hovered (else the last picked) part.
        private void ShowState()
        {
            KartLoadout set = DriverChoice.Loadout;
            bool reduced = hudRoot != null && hudRoot.ClassListContains("reduce-motion");
            for (int c = 0; c < ColumnCount; c++)
            {
                int picked = PickedRow(c);
                bool focused = c == Column;
                columnHeads[c].style.borderBottomColor = focused ? HudColors.Orange : new Color(1f, 1f, 1f, 0.16f);
                columnHeads[c].style.color = focused ? HudColors.Cream : HudColors.WithAlpha(HudColors.Cream, 0.7f);
                for (int r = 0; r < KartLoadout.PartCount; r++)
                {
                    bool isPicked = r == picked;
                    tiles[c, r].EnableInClassList("plate--blue", isPicked);
                    tileNames[c, r].style.color = isPicked ? HudColors.Orange : HudColors.Cream;
                    float scale = c == pressedColumn && r == pressedRow ? 0.96f
                        : (c == hoveredColumn && r == hoveredRow ? 1.06f : (isPicked && focused ? 1.03f : 1f));
                    tiles[c, r].style.scale = new Scale(new Vector3(scale, scale, 1f));
                    tiles[c, r].style.transitionDuration = new List<TimeValue> { new TimeValue(reduced ? 0 : 120, TimeUnit.Millisecond) };
                    if (badges[c, r] != null)
                    {
                        bool own = r == set.Driver;
                        SetBorder(badges[c, r], own ? 3f : 2f, own ? HudColors.Orange : new Color(1f, 1f, 1f, 0.35f));
                        badges[c, r].style.opacity = own ? 1f : 0.55f;
                    }
                }
            }
            int lineColumn = hoveredColumn >= 0 ? hoveredColumn : Column;
            int lineRow = hoveredColumn >= 0 ? hoveredRow : PickedRow(Column);
            line.text = PartLine(lineColumn, lineRow);
            previewTitle.text = set.Title;
            previewNote.text = set.IsSignature ? DriverRoster.Get(set.Driver).Name.ToUpperInvariant() + "'S OWN SET"
                : "DRAG TO TURN";
        }

        // ---------- the turntable ----------
        private void EnsureStage()
        {
            if (stage == null && KartModels.Available)
            {
                stage = KartStage.Create("Kart Builder Studio", new Vector3(0f, -6000f, 0f));
            }
            if (stage != null && !tilesShot && !Application.isBatchMode) // the CI test machine has no GPU: no stills
            {
                tilesShot = true;
                ShootTiles();
            }
            SizePicture();
        }

        // The kart and tyre tiles: one still picture of each, taken once in the studio (Luc's real models, 3/4 view).
        private void ShootTiles()
        {
            for (int i = 0; i < KartLoadout.PartCount; i++)
            {
                stage.ShowSingle(KartLoadout.Signature(i));
                RenderTexture kart = stage.Snapshot(TileShotWidth, TileShotHeight, null, true, -38f, 1.04f);
                RenderTexture tyre = stage.Snapshot(TileShotWidth, TileShotHeight, "Wheel_FL", true, 64f, 1.0f);
                if (kart != null)
                {
                    tileShots.Add(kart);
                    tilePictures[KartColumn, i].style.backgroundImage = new StyleBackground(Background.FromRenderTexture(kart));
                }
                if (tyre != null)
                {
                    tileShots.Add(tyre);
                    tilePictures[TyreColumn, i].style.backgroundImage = new StyleBackground(Background.FromRenderTexture(tyre));
                }
            }
            stageHasKart = false;
        }

        private void ShowOnStage()
        {
            if (stage == null)
            {
                return;
            }
            KartLoadout set = DriverChoice.Loadout;
            if (stageHasKart && set.Equals(shownOnStage))
            {
                return;
            }
            stage.ShowSingle(set);
            shownOnStage = set;
            stageHasKart = stage.HasKarts;
        }

        // The picture in the size it is shown at, in real screen pixels (sharp on high-DPI screens).
        private void SizePicture()
        {
            if (stage == null || preview.panel == null)
            {
                return;
            }
            Rect rect = preview.contentRect;
            if (rect.width < 4f || rect.height < 4f)
            {
                return;
            }
            float scale = preview.panel.scaledPixelsPerPoint;
            RenderTexture picture = stage.EnsurePicture(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale));
            preview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(picture));
            preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        private void OnPreviewDown(PointerDownEvent evt)
        {
            if (!IsOpen || stage == null)
            {
                return;
            }
            dragging = true;
            dragX = evt.position.x;
            lastDragTime = Time.unscaledTime;
            preview.CapturePointer(evt.pointerId);
        }

        private void OnPreviewMove(PointerMoveEvent evt)
        {
            if (!dragging || stage == null)
            {
                return;
            }
            stage.Drag((evt.position.x - dragX) * DragDegreesPerPixel);
            dragX = evt.position.x;
            lastDragTime = Time.unscaledTime;
        }

        private void OnPreviewUp(PointerUpEvent evt)
        {
            dragging = false;
            lastDragTime = Time.unscaledTime;
            if (preview.HasPointerCapture(evt.pointerId))
            {
                preview.ReleasePointer(evt.pointerId);
            }
        }

        private void PlaySound(MenuSound sound)
        {
            if (SoundRequested != null)
            {
                SoundRequested(sound);
            }
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
