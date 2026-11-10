namespace CarcavelosCircuit.RaceFlow
{
    // The one item slot on the HUD (task R7). Plain C#, no Unity objects, so the rules are Edit Mode tested.
    // Empty -> a box is hit -> the slot "rolls" (icons spin like a slot machine) -> the item is ready -> E uses it.
    // The item is decided when the box is hit; the roll is only the show. While the slot rolls or holds an item,
    // a new box gives nothing (it still breaks): one item at a time keeps the choice "use it now or save it?".
    public class ItemSlot
    {
        public const int RollIconSteps = 14; // how many times the icon changes during one roll

        public ItemKind Held { get; private set; } = ItemKind.None; // the item in the slot (also while rolling)
        public int Charges { get; private set; }                    // uses left (every item has 1 right now)
        public bool IsRolling { get; private set; }

        private float rollSeconds;
        private float rollElapsed;

        public bool IsEmpty { get { return Held == ItemKind.None; } }
        public bool IsReady { get { return Held != ItemKind.None && !IsRolling; } }

        // 0 at the start of a roll, 1 when it lands. 1 when not rolling.
        public float RollProgress01
        {
            get { return IsRolling && rollSeconds > 0f ? rollElapsed / rollSeconds : 1f; }
        }

        // How many icon changes happened so far in this roll. They come fast at first and slow down at the end
        // (ease-out), like a wheel that runs out of spin. The HUD plays a tick sound each time this grows.
        public int RollSteps
        {
            get
            {
                if (!IsRolling)
                {
                    return RollIconSteps;
                }
                float p = RollProgress01;
                float eased = 1f - (1f - p) * (1f - p);
                return (int)(eased * RollIconSteps);
            }
        }

        // The icon the HUD shows right now: the spinning icons during the roll, then the real item.
        public ItemKind ShownIcon
        {
            get
            {
                if (!IsRolling)
                {
                    return Held;
                }
                return IconForStep(RollSteps);
            }
        }

        // During the roll the icons go round Espresso -> Seagull Delivery -> Carcavelos Wave -> Dean's List -> ...
        public static ItemKind IconForStep(int step)
        {
            int index = step % 4;
            if (index == 0) return ItemKind.Espresso;
            if (index == 1) return ItemKind.SeagullDelivery;
            if (index == 2) return ItemKind.CarcavelosWave;
            return ItemKind.DeansList;
        }

        // How many times an item can be used before the slot is empty again. Every item is one use right now
        // (the Seagull's three rings come from one use); an item with more uses would return a bigger number here.
        public static int ChargesFor(ItemKind kind)
        {
            return kind == ItemKind.None ? 0 : 1;
        }

        // A box was hit. Starts the roll towards "result". False (and nothing changes) if the slot is busy.
        public bool TryStartRoll(ItemKind result, float seconds)
        {
            if (!IsEmpty || result == ItemKind.None)
            {
                return false;
            }
            Held = result;
            Charges = ChargesFor(result);
            rollSeconds = seconds;
            rollElapsed = 0f;
            IsRolling = seconds > 0f;
            return true;
        }

        // Called every frame. True only on the frame the roll lands (the HUD and the sound show "item ready").
        public bool Tick(float deltaSeconds)
        {
            if (!IsRolling)
            {
                return false;
            }
            rollElapsed += deltaSeconds;
            if (rollElapsed < rollSeconds)
            {
                return false;
            }
            rollElapsed = rollSeconds;
            IsRolling = false;
            return true;
        }

        // The use key. Returns the item that was used (None if there is nothing ready). The last charge empties the slot.
        public ItemKind TryUse()
        {
            if (!IsReady)
            {
                return ItemKind.None;
            }
            ItemKind used = Held;
            Charges--;
            if (Charges <= 0)
            {
                Clear();
            }
            return used;
        }

        // Retry: the new attempt starts with an empty slot.
        public void Clear()
        {
            Held = ItemKind.None;
            Charges = 0;
            IsRolling = false;
            rollElapsed = 0f;
            rollSeconds = 0f;
        }
    }
}
