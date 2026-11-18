using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The four drivers on the first screen (Luc's wish, 18 Nov 2026: "show off our characters"). The first thing a
    // player sees is the web page's "Click to play" screen (Assets/WebGLTemplates/.../index.html). As soon as the game is
    // loaded, the page fades its key art out on the right half and shows the live game there ("cc-live"); this class then
    // draws Rookie, Tiago, Nova and Amara in their own karts side by side in that half, gently alive (the karts idle and
    // steer, KartStage/StageKart), with each name under the kart. The page keeps its title, CLICK TO PLAY and tips on the
    // left half, so nothing covers them. While the page covers the game, the game's own start screen stays hidden (it
    // would show through twice); after the click the line-up fades out and the start screen fades in.
    // Phones narrower than 761 CSS px have the page in one centred column: the page then never says "live" and the
    // line-up is not drawn (WebPage.FirstScreen). In the Editor and in tests there is no page: nothing happens here.
    public class StartLineup
    {
        private const float FadeSeconds = 0.8f;

        private readonly VisualElement startScreen;
        private readonly VisualElement element;
        private readonly VisualElement picture;
        private KartStage stage;
        private bool shown;
        private bool coveringStartScreen;
        private float hiddenAt = -10f;

        // The line-up is on screen now (tests, QA).
        public bool IsShown { get { return shown; } }
        public KartStage Stage { get { return stage; } }

        public StartLineup(VisualElement root)
        {
            VisualElement hudRoot = root.Q<VisualElement>("hud-root");
            startScreen = root.Q<VisualElement>("start-screen");

            element = new VisualElement { name = "start-lineup" };
            element.style.position = Position.Absolute;
            element.style.left = Length.Percent(53f);   // the page's wordmark ends at 50 % of the width
            element.style.right = Length.Percent(1f);
            element.style.top = Length.Percent(30f);    // the karts stand low, just above their names
            element.style.bottom = Length.Percent(15f);  // above the page's key dock
            element.style.opacity = 0f;
            element.style.translate = new Translate(Length.Percent(4f), 0f);
            element.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName>
                { new StylePropertyName("opacity"), new StylePropertyName("translate") };
            element.style.transitionDuration = new System.Collections.Generic.List<TimeValue>
                { new TimeValue(FadeSeconds, TimeUnit.Second), new TimeValue(FadeSeconds, TimeUnit.Second) };
            element.style.transitionTimingFunction = new System.Collections.Generic.List<EasingFunction>
                { new EasingFunction(EasingMode.EaseOutCubic), new EasingFunction(EasingMode.EaseOutCubic) };
            element.style.display = DisplayStyle.None;
            element.pickingMode = PickingMode.Ignore;

            // A soft shadow band under the row grounds the karts over the moving world.
            VisualElement shadow = new VisualElement();
            shadow.style.position = Position.Absolute;
            shadow.style.left = Length.Percent(2f);
            shadow.style.right = Length.Percent(2f);
            shadow.style.bottom = Length.Percent(13f);
            shadow.style.height = Length.Percent(22f);
            shadow.style.backgroundImage = new StyleBackground(ShadowBand());
            shadow.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            shadow.pickingMode = PickingMode.Ignore;
            element.Add(shadow);

            picture = new VisualElement { name = "start-lineup-picture" };
            picture.style.position = Position.Absolute;
            picture.style.left = 0f;
            picture.style.right = 0f;
            picture.style.top = 0f;
            picture.style.bottom = Length.Percent(12f);
            picture.pickingMode = PickingMode.Ignore;
            picture.RegisterCallback<GeometryChangedEvent>(evt => SizePicture());
            element.Add(picture);

            // The four names under the karts (the karts stand evenly spaced, so four equal cells line up with them).
            VisualElement names = new VisualElement();
            names.style.position = Position.Absolute;
            names.style.left = Length.Percent(4f);
            names.style.right = Length.Percent(4f);
            names.style.bottom = 0f;
            names.style.height = Length.Percent(14f);
            names.style.flexDirection = FlexDirection.Row;
            names.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < DriverRoster.Count; i++)
            {
                VisualElement cell = new VisualElement();
                cell.style.flexGrow = 1f;
                cell.style.flexBasis = 0f;
                cell.style.alignItems = Align.Center;
                cell.pickingMode = PickingMode.Ignore;
                Label name = new Label(DriverRoster.Get(i).Name.ToUpperInvariant());
                name.AddToClassList("f-display");
                name.style.fontSize = 20f;
                name.style.color = HudColors.Cream;
                name.style.paddingBottom = 2f;
                name.style.borderBottomWidth = 3f;
                name.style.borderBottomColor = DriverRoster.Get(i).Colour;
                name.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 8f, color = new Color(0f, 0f, 0f, 0.85f) };
                name.pickingMode = PickingMode.Ignore;
                cell.Add(name);
                Label kart = new Label(KartLoadout.KartNames[i]);
                kart.AddToClassList("key-text");
                kart.AddToClassList("f-ui");
                kart.style.fontSize = 13f;
                kart.style.marginTop = 3f;
                kart.style.color = HudColors.Cream;
                kart.style.textShadow = new TextShadow { offset = new Vector2(0f, 1f), blurRadius = 6f, color = new Color(0f, 0f, 0f, 0.9f) };
                kart.pickingMode = PickingMode.Ignore;
                cell.Add(kart);
                names.Add(cell);
            }
            element.Add(names);

            if (hudRoot != null)
            {
                hudRoot.Add(element);
            }
        }

        public void Dispose()
        {
            if (stage != null)
            {
                Object.Destroy(stage.gameObject);
                stage = null;
            }
        }

        // RaceHud.Update, every frame.
        public void Update(RaceState state)
        {
            int page = WebPage.FirstScreen;
            bool onStart = state == RaceState.Ready;

            // While the page still covers the game, the game's own start screen stays hidden (no double title).
            bool cover = onStart && page != WebPage.FirstScreenGone;
            if (cover != coveringStartScreen && startScreen != null)
            {
                coveringStartScreen = cover;
                startScreen.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity") };
                startScreen.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(cover ? 0f : 0.45f, TimeUnit.Second) };
                if (cover)
                {
                    startScreen.style.opacity = 0f;
                }
                else
                {
                    startScreen.style.opacity = StyleKeyword.Null;
                }
            }

            bool want = onStart && page == WebPage.FirstScreenLive && KartModels.Available;
            if (want && !shown)
            {
                Show();
            }
            else if (!want && shown)
            {
                Hide();
            }
            if (stage != null && !shown && Time.unscaledTime - hiddenAt > FadeSeconds + 0.1f)
            {
                // faded out: the studio is no longer needed (the first screen comes once per visit)
                element.style.display = DisplayStyle.None;
                Object.Destroy(stage.gameObject);
                stage = null;
            }
        }

        private void Show()
        {
            shown = true;
            if (stage == null)
            {
                stage = KartStage.Create("First Screen Studio", new Vector3(300f, -6000f, 0f));
                KartLoadout[] four = new KartLoadout[DriverRoster.Count];
                for (int i = 0; i < four.Length; i++)
                {
                    four[i] = KartLoadout.Signature(i);
                }
                stage.ShowLineup(four);
            }
            element.style.display = DisplayStyle.Flex;
            SizePicture();
            stage.Rendering = true;
            element.style.opacity = 1f;
            element.style.translate = new Translate(0f, 0f);
        }

        private void Hide()
        {
            shown = false;
            hiddenAt = Time.unscaledTime;
            element.style.opacity = 0f;
            element.style.translate = new Translate(Length.Percent(-3f), 0f);
        }

        // The picture in the size it is shown at, in real screen pixels.
        private void SizePicture()
        {
            if (stage == null || picture.panel == null)
            {
                return;
            }
            Rect rect = picture.contentRect;
            if (rect.width < 4f || rect.height < 4f)
            {
                return;
            }
            float scale = picture.panel.scaledPixelsPerPoint;
            RenderTexture texture = stage.EnsurePicture(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale));
            picture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
            picture.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        private static Texture2D shadowBand;

        private static Texture2D ShadowBand()
        {
            if (shadowBand != null)
            {
                return shadowBand;
            }
            const int width = 256, height = 32;
            shadowBand = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "StartLineupShadow", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) / width * 2f - 1f;
                    float dy = (y + 0.5f) / height * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx * 0.9f + dy * dy));
                    shadowBand.SetPixel(x, y, new Color(0f, 0f, 0f, a * a * 0.6f));
                }
            }
            shadowBand.Apply();
            return shadowBand;
        }
    }
}
