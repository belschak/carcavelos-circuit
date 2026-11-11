using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The item slot on the HUD (task R7). On the "Items" object in Main.unity, with its own UIDocument on the same
    // PanelSettings as the race HUD, drawn just below it (sorting order -1) so the pause card and menus cover it.
    // Built in code (no UXML): a rounded tile under the checkpoint panel with the item icon (ItemIcon), a charge
    // badge (only for an item with more than one use; none has that right now) and an "E" key cap. While the slot
    // rolls the icons spin; when the item is ready the tile pops and its border flashes sand; while you hold an item
    // it breathes gently. On use the item's name pops under the slot ("ESPRESSO!", "CARCAVELOS WAVE!" ...).
    // Extras: the Wave sends a blue wave across the screen; the Seagull's rings count up ("RING 2/3", "PERFECT
    // DELIVERY!"); while the Dean's List shield is on the screen edge glows gold, and a bump shows "BLOCKED!".
    // Like RaceHud everything animates on real time (Time.unscaledTime), so the finish slow motion never slows it.
    [RequireComponent(typeof(UIDocument))]
    public class ItemSlotHud : MonoBehaviour
    {
        private const float SlotSize = 104f;
        private const float PopSeconds = 0.35f;
        private const float CalloutSeconds = 0.9f;
        private const float WaveSeconds = 0.6f;
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f); // the Dean's List coins (ItemSystem)

        [SerializeField]
        private RaceManager raceManager;

        [SerializeField]
        private ItemSystem items;

        private VisualElement root;
        private VisualElement slot;
        private ItemIcon icon;
        private Label badge;
        private Label callout;
        private VisualElement wave;
        private VisualElement goldEdge;  // Dean's List shield: a gold frame round the screen

        private float readyAt = -10f;    // real time the last item landed (pop + sand flash)
        private float usedAt = -10f;     // real time the last callout started
        private float waveAt = -10f;     // real time the last Carcavelos Wave was used (screen wave)
        private bool shown;

        private void OnEnable()
        {
            Build(GetComponent<UIDocument>().rootVisualElement);
            if (items != null)
            {
                items.ItemReady += OnItemReady;
                items.ItemUsed += OnItemUsed;
                items.RingCollected += OnRingCollected;
                items.ShieldBlocked += OnShieldBlocked;
            }
        }

        private void OnDisable()
        {
            if (items != null)
            {
                items.ItemReady -= OnItemReady;
                items.ItemUsed -= OnItemUsed;
                items.RingCollected -= OnRingCollected;
                items.ShieldBlocked -= OnShieldBlocked;
            }
        }

        private void OnItemReady(ItemKind kind)
        {
            readyAt = Time.unscaledTime;
        }

        private void OnItemUsed(ItemKind kind)
        {
            if (kind == ItemKind.Espresso)
            {
                ShowCallout("ESPRESSO!", HudColors.Orange);
            }
            else if (kind == ItemKind.SeagullDelivery)
            {
                ShowCallout("SEAGULL DELIVERY!", HudColors.White);
            }
            else if (kind == ItemKind.CarcavelosWave)
            {
                ShowCallout("CARCAVELOS WAVE!", HudColors.Blue);
                waveAt = Time.unscaledTime;
            }
            else if (kind == ItemKind.DeansList)
            {
                ShowCallout("DEAN'S LIST!", Gold);
            }
        }

        // A ring of the Seagull Delivery: count up, and praise a clean sweep.
        private void OnRingCollected(int collected, int dropped)
        {
            if (collected >= dropped && dropped > 1)
            {
                ShowCallout("PERFECT DELIVERY!", HudColors.Sand);
            }
            else
            {
                ShowCallout("RING " + collected + "/" + dropped, HudColors.White);
            }
        }

        private void OnShieldBlocked()
        {
            ShowCallout("BLOCKED!", Gold);
        }

        private void ShowCallout(string text, Color colour)
        {
            usedAt = Time.unscaledTime;
            callout.text = text;
            callout.style.color = colour;
        }

        private void Update()
        {
            if (raceManager == null || items == null || root == null)
            {
                return;
            }
            // Same rule as the race layer of the HUD: visible in the countdown and while racing.
            RaceState state = raceManager.State;
            bool wantShown = raceManager.IsSetUp && (state == RaceState.Countdown || state == RaceState.Racing);
            if (wantShown != shown)
            {
                shown = wantShown;
                slot.parent.style.opacity = shown ? 1f : 0f;
            }

            float now = Time.unscaledTime;
            ItemSlot itemSlot = items.Slot;
            UpdateSlot(itemSlot, now);
            UpdateCallout(now);
            UpdateWave(now);
            UpdateGoldEdge(now);
        }

        // While the Dean's List shield is on: a gold frame that breathes, and blinks in the last two seconds.
        private void UpdateGoldEdge(float now)
        {
            if (!items.ShieldActive || !shown)
            {
                goldEdge.style.opacity = 0f;
                return;
            }
            float glow = 0.45f + 0.25f * Mathf.Sin(now * 5f);
            if (items.ShieldSecondsLeft < 2f && Mathf.Repeat(now * 6f, 1f) < 0.35f)
            {
                glow = 0.1f;
            }
            goldEdge.style.opacity = glow;
        }

        private void UpdateSlot(ItemSlot itemSlot, float now)
        {
            icon.Kind = itemSlot.ShownIcon;
            icon.SteamPhase = Mathf.Repeat(now * 0.8f, 1f);

            // Charge badge only for a ready item with more than one use left.
            bool showBadge = itemSlot.IsReady && itemSlot.Charges > 1;
            badge.style.opacity = showBadge ? 1f : 0f;
            badge.text = "x" + itemSlot.Charges;

            // Empty = a faint outline, rolling = full tile with the spinning icon (slight shake), ready = full + pop.
            float scale = 1f;
            float sincePop = now - readyAt;
            Color border = HudColors.Blue;
            if (itemSlot.IsEmpty)
            {
                slot.style.opacity = 0.45f;
            }
            else
            {
                slot.style.opacity = 1f;
                if (itemSlot.IsRolling)
                {
                    scale = 1f + 0.03f * Mathf.Sin(now * 40f);
                }
                else if (sincePop < PopSeconds)
                {
                    // 1 -> 1.25 -> 1 with a little overshoot, and the border flashes sand.
                    float p = sincePop / PopSeconds;
                    scale = 1f + 0.25f * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.3f);
                    border = Color.Lerp(HudColors.Sand, HudColors.Blue, p);
                }
                else
                {
                    scale = 1f + 0.035f * Mathf.Sin(now * 3.2f); // breathing: "use me"
                }
            }
            slot.style.scale = new Scale(new Vector3(scale, scale, 1f));
            SetBorderColour(slot, border);
        }

        private void UpdateCallout(float now)
        {
            float since = now - usedAt;
            if (since >= CalloutSeconds)
            {
                callout.style.opacity = 0f;
                return;
            }
            float p = since / CalloutSeconds;
            float scale = p < 0.2f ? Mathf.Lerp(0.4f, 1.2f, p / 0.2f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((p - 0.2f) / 0.2f));
            callout.style.scale = new Scale(new Vector3(scale, scale, 1f));
            callout.style.opacity = p < 0.7f ? 1f : 1f - (p - 0.7f) / 0.3f;
        }

        // The Carcavelos Wave: a blue band rises from the bottom of the screen and fades out over the top.
        private void UpdateWave(float now)
        {
            float since = now - waveAt;
            if (since >= WaveSeconds)
            {
                wave.style.opacity = 0f;
                return;
            }
            float p = since / WaveSeconds;
            wave.style.translate = new Translate(0f, Length.Percent(100f - 200f * p));
            wave.style.opacity = 0.55f * Mathf.Sin(p * Mathf.PI);
        }

        // ---------- building the tree (once) ----------

        private void Build(VisualElement documentRoot)
        {
            documentRoot.Clear();
            root = new VisualElement();
            root.name = "items-root";
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;
            root.style.alignItems = Align.Center;
            root.style.overflow = Overflow.Hidden;
            documentRoot.Add(root);

            // Carcavelos Wave screen wave (behind the slot): full width, starts below the screen.
            wave = new VisualElement();
            wave.name = "item-wave";
            wave.pickingMode = PickingMode.Ignore;
            wave.style.position = Position.Absolute;
            wave.style.left = 0f;
            wave.style.right = 0f;
            wave.style.top = 0f;
            wave.style.bottom = 0f;
            wave.style.backgroundColor = HudColors.WithAlpha(HudColors.Blue, 1f);
            wave.style.borderTopLeftRadius = new Length(50f, LengthUnit.Percent);
            wave.style.borderTopRightRadius = new Length(50f, LengthUnit.Percent);
            wave.style.borderTopWidth = 14f;
            wave.style.borderTopColor = HudColors.White;
            wave.style.opacity = 0f;
            root.Add(wave);

            // Dean's List shield frame: a gold border round the whole screen, see-through in the middle.
            goldEdge = new VisualElement();
            goldEdge.name = "item-gold-edge";
            goldEdge.pickingMode = PickingMode.Ignore;
            goldEdge.style.position = Position.Absolute;
            goldEdge.style.left = 0f;
            goldEdge.style.right = 0f;
            goldEdge.style.top = 0f;
            goldEdge.style.bottom = 0f;
            SetBorderWidth(goldEdge, 12f);
            goldEdge.style.borderTopColor = Gold;
            goldEdge.style.borderBottomColor = Gold;
            goldEdge.style.borderLeftColor = Gold;
            goldEdge.style.borderRightColor = Gold;
            goldEdge.style.opacity = 0f;
            root.Add(goldEdge);

            // Same safe frame as RaceHud.uss (.hud-safe): max 1600 wide, 26 px from the top, 34 px from the side.
            VisualElement safe = new VisualElement();
            safe.pickingMode = PickingMode.Ignore;
            safe.style.width = Length.Percent(100f);
            safe.style.maxWidth = 1600f;
            safe.style.paddingTop = 26f;
            safe.style.paddingRight = 34f;
            safe.style.paddingLeft = 34f;
            safe.style.alignItems = Align.FlexEnd;
            safe.style.flexGrow = 1f;
            root.Add(safe);

            // Column under the checkpoint panel (that panel is about 72 px tall).
            VisualElement column = new VisualElement();
            column.pickingMode = PickingMode.Ignore;
            column.style.marginTop = 86f;
            column.style.marginRight = PositionsHud.MapColumnWidth; // the minimap sits at the right edge beside the slot (task R9)
            column.style.alignItems = Align.FlexEnd; // right edge of the screen: the callout grows to the left, never off screen (phone check 2026-11-10)
            column.style.opacity = 0f;
            safe.Add(column);

            slot = new VisualElement();
            slot.name = "item-slot";
            slot.pickingMode = PickingMode.Ignore;
            slot.style.width = SlotSize;
            slot.style.height = SlotSize;
            slot.style.backgroundColor = new Color(29f / 255f, 29f / 255f, 31f / 255f, 0.82f); // --cc-panel
            SetRadius(slot, 20f);
            SetBorderWidth(slot, 4f);
            slot.style.borderBottomWidth = 7f; // the darker "lip" every HUD tile has
            SetBorderColour(slot, HudColors.Blue);
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            column.Add(slot);

            icon = new ItemIcon();
            icon.style.width = 72f;
            icon.style.height = 72f;
            slot.Add(icon);

            badge = MakeLabel("x3", 20f, HudColors.Black);
            badge.style.position = Position.Absolute;
            badge.style.right = -10f;
            badge.style.bottom = -10f;
            badge.style.backgroundColor = HudColors.Sand;
            badge.style.paddingLeft = 7f;
            badge.style.paddingRight = 7f;
            badge.style.paddingTop = 1f;
            badge.style.paddingBottom = 2f;
            SetRadius(badge, 10f);
            badge.style.opacity = 0f;
            slot.Add(badge);

            Label key = MakeLabel("E", 15f, HudColors.Black);
            key.style.position = Position.Absolute;
            key.style.left = -10f;
            key.style.bottom = -10f;
            key.style.width = 26f;
            key.style.height = 26f;
            key.style.unityTextAlign = TextAnchor.MiddleCenter;
            key.style.backgroundColor = HudColors.White;
            key.style.borderBottomWidth = 3f;
            key.style.borderBottomColor = HudColors.Asphalt;
            SetRadius(key, 6f);
            slot.Add(key);

            callout = MakeLabel("ESPRESSO!", 34f, HudColors.Orange);
            callout.style.marginTop = 16f;
            callout.style.letterSpacing = 2f;
            callout.style.opacity = 0f;
            callout.style.transformOrigin = new TransformOrigin(Length.Percent(100f), Length.Percent(50f)); // pop from the right edge
            column.Add(callout);
        }

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
            return label;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetBorderWidth(VisualElement element, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
        }

        // The bottom edge stays darker (the lip), the other three take the colour.
        private static void SetBorderColour(VisualElement element, Color colour)
        {
            element.style.borderTopColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
            element.style.borderBottomColor = Color.Lerp(colour, Color.black, 0.45f);
        }
    }
}
