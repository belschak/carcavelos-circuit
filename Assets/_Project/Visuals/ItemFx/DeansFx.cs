using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Dean's List (task V34), Luc's Items v01 read: a gold laurel wreath turns slowly round the kart and a
    // mortarboard floats above the driver, its tassel swinging; gold glitter rises off the wreath. In the shield's
    // last seconds both blink. A blocked bump = a gold flash ring, the cap is thrown up spinning and caught again,
    // and confetti in Nova blue, gold and white. When the shield runs out the wreath bursts into gold leaves.
    // Follows the kart (child of the kart's effects object). ItemFx tells it when the shield is on and how long is
    // left (ItemSystem.ShieldActive / ShieldSecondsLeft), so it always matches the real shield.
    public class DeansFx
    {
        private const float WreathRadius = 1.15f;
        private const float WreathHeight = 0.5f;
        private const float CapHeight = 2.05f;
        private const float WarnSeconds = 2f;
        private const float TossSeconds = 0.9f;
        private const float AppearSeconds = 0.35f;

        private readonly Transform root;
        private readonly Transform wreath;
        private readonly Transform cap;
        private readonly Transform tassel;
        private readonly ParticleSystem glitter;
        private readonly ParticleSystem flash;
        private readonly ParticleSystem confetti;
        private readonly ParticleSystem leaves;
        private readonly Color gold;
        private float time = -1f;
        private float tossTime = -1f;
        private float swing;
        private float swingSpeed;
        private Vector3 lastPosition;
        private float lastForwardSpeed;

        public bool IsActive { get { return time >= 0f; } }

        public DeansFx(Transform parent, ItemFxLook look)
        {
            gold = look.DeansGold;
            root = new GameObject("DeansFx").transform;
            root.gameObject.layer = 2;
            root.SetParent(parent, false);

            wreath = FxParts.Model(look.Laurel, root, "Laurel");
            if (wreath != null)
            {
                wreath.localPosition = new Vector3(0f, WreathHeight, 0f);
            }
            Transform capPivot = new GameObject("CapPivot").transform;
            capPivot.SetParent(root, false);
            cap = capPivot;
            Transform capModel = FxParts.Model(look.Mortarboard, capPivot, "Mortarboard");
            if (capModel != null)
            {
                capModel.localScale = Vector3.one * 1.15f;
                capModel.gameObject.SetActive(true);
                Transform tasselPivot = new GameObject("TasselPivot").transform;
                tasselPivot.SetParent(capModel, false);
                tasselPivot.localPosition = new Vector3(0.3f, 0.3f, 0f); // the board's right edge (Items v01: cap 0.64 m wide)
                Transform tasselModel = FxParts.Model(look.Tassel, tasselPivot, "Tassel");
                if (tasselModel != null)
                {
                    tasselModel.localScale = Vector3.one * 0.75f;
                    tasselModel.gameObject.SetActive(true);
                }
                tassel = tasselPivot;
            }
            cap.gameObject.SetActive(false);

            Color light = Color.Lerp(gold, Color.white, 0.5f);
            glitter = FxParts.Particles(root, "Glitter", look.Spark, false, ParticleSystemRenderMode.Billboard, 70);
            FxParts.Basics(glitter, 0.6f, 1.0f, 0.4f, 1.1f, 0.05f, 0.11f, light, gold, -0.15f);
            ParticleSystem.ShapeModule ring = glitter.shape;
            ring.enabled = true;
            ring.shapeType = ParticleSystemShapeType.Circle;
            ring.radius = WreathRadius;
            ring.radiusThickness = 0.05f;
            ring.position = new Vector3(0f, WreathHeight, 0f);
            ring.rotation = new Vector3(-90f, 0f, 0f); // the circle lies flat, particles go up
            FxParts.FadeAndSize(glitter, 0.2f);

            flash = FxParts.Particles(root, "GoldFlash", look.Ring, true, ParticleSystemRenderMode.HorizontalBillboard, 4);
            FxParts.Basics(flash, 0.5f, 0.5f, 0f, 0f, 8f, 8f, gold, light, 0f);
            WaveRide.RingGrowth(flash);

            confetti = FxParts.Particles(root, "Confetti", look.Confetti, true, ParticleSystemRenderMode.Billboard, 120);
            FxParts.Basics(confetti, 1.0f, 1.6f, 0f, 0f, 0.08f, 0.14f, gold, look.AtlanticBlue, 0.55f);
            ParticleSystem.MainModule cm = confetti.main;
            cm.startColor = Palette(gold, look.AtlanticBlue, Color.white);
            cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.RotationOverLifetimeModule spin = confetti.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = confetti.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.2f;
            FxParts.FadeAndSize(confetti, 0.9f);

            leaves = FxParts.Particles(root, "GoldLeaves", look.Confetti, true, ParticleSystemRenderMode.Billboard, 60);
            FxParts.Basics(leaves, 0.7f, 1.1f, 0f, 0f, 0.09f, 0.16f, gold, light, 0.7f);
            ParticleSystem.MainModule lm = leaves.main;
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            FxParts.FadeAndSize(leaves, 0.5f);
        }

        // Called every frame by ItemFx with the real shield state.
        public void Tick(float dt, bool shieldOn, float secondsLeft)
        {
            if (shieldOn && time < 0f)
            {
                Begin();
            }
            else if (!shieldOn && time >= 0f)
            {
                End(true);
            }
            if (time >= 0f)
            {
                time += dt;
                float appear = ItemFxRules.Envelope(time, AppearSeconds, 99f, 0f);
                bool visible = ItemFxRules.ShieldVisible(secondsLeft, time, WarnSeconds);
                if (wreath != null)
                {
                    wreath.gameObject.SetActive(visible);
                    wreath.localRotation = Quaternion.Euler(0f, time * 50f, 0f);
                    float pulse = 1f + 0.04f * Mathf.Sin(time * 6f);
                    float size = (WreathRadius / 0.95f) * appear * pulse; // the sculpt's centre line is ~0.95 m
                    wreath.localScale = new Vector3(size, size * 0.5f, size); // flatter: a crown of leaves, not a tyre
                    wreath.localPosition = new Vector3(0f, WreathHeight + 0.05f * Mathf.Sin(time * 2.4f), 0f);
                }
                cap.gameObject.SetActive(visible || tossTime >= 0f);
            }
            TickCap(dt);
        }

        // The shield took a bump: flash, cap toss and confetti.
        public void Block()
        {
            Vector3 centre = root.position + root.up * 0.08f;
            FxParts.BurstAt(flash, centre, 1, Vector3.zero, 0f, 0f);
            FxParts.BurstAt(confetti, root.position + root.up * 1.6f, 70, root.up * 3.5f, 4.5f, 0.7f);
            tossTime = 0f;
        }

        public void Clear()
        {
            End(false);
            tossTime = -1f;
            glitter.Clear();
            flash.Clear();
            confetti.Clear();
            leaves.Clear();
        }

        private void Begin()
        {
            time = 0f;
            tossTime = -1f;
            swing = 0f;
            swingSpeed = 0f;
            lastPosition = root.position;
            lastForwardSpeed = 0f;
            cap.gameObject.SetActive(true);
            FxParts.Rate(glitter, 28f);
            FxParts.BurstAt(flash, root.position + root.up * 0.08f, 1, Vector3.zero, 0f, 0f);
            FxParts.BurstAt(leaves, root.position + root.up * WreathHeight, 16, root.up * 1.2f, 1.5f, 0.5f);
        }

        // burst = the shield ran out by itself or was used up: the wreath bursts into gold leaves.
        private void End(bool burst)
        {
            if (burst && time >= 0f)
            {
                FxParts.BurstAt(leaves, root.position + root.up * WreathHeight, 40, Vector3.zero, 4.5f, 0.6f);
            }
            time = -1f;
            FxParts.Rate(glitter, 0f);
            if (wreath != null) wreath.gameObject.SetActive(false);
            if (tossTime < 0f) cap.gameObject.SetActive(false);
        }

        // The cap floats and bobs above the driver; thrown, it spins up and drops back. The tassel swings with
        // the kart's acceleration (a damped spring).
        private void TickCap(float dt)
        {
            if (!cap.gameObject.activeSelf)
            {
                return;
            }
            float t = Mathf.Max(0f, time);
            float toss = 0f;
            float spinDegrees = 0f;
            if (tossTime >= 0f)
            {
                tossTime += dt;
                toss = ItemFxRules.TossHeight(tossTime, TossSeconds, 2.2f);
                spinDegrees = 720f * Mathf.Clamp01(tossTime / TossSeconds);
                if (tossTime >= TossSeconds)
                {
                    tossTime = -1f;
                    if (time < 0f) cap.gameObject.SetActive(false);
                }
            }
            float appear = time >= 0f ? ItemFxRules.Envelope(time, AppearSeconds, 99f, 0f) : 1f;
            cap.localPosition = new Vector3(0f, CapHeight + 0.08f * Mathf.Sin(t * 3.2f) + toss, -0.15f);
            cap.localRotation = Quaternion.Euler(-8f + 4f * Mathf.Sin(t * 2.1f), t * 25f + spinDegrees, 3f * Mathf.Sin(t * 2.7f));
            cap.localScale = Vector3.one * appear;

            if (tassel != null && dt > 0f)
            {
                Vector3 moved = (root.position - lastPosition) / dt;
                lastPosition = root.position;
                float forwardSpeed = Vector3.Dot(moved, root.forward);
                float push = -(forwardSpeed - lastForwardSpeed) / dt * 0.35f; // speeding up swings it back
                lastForwardSpeed = forwardSpeed;
                swingSpeed += (-swing * 40f - swingSpeed * 5f) * dt + push * dt;
                swing += swingSpeed * dt;
                tassel.localRotation = Quaternion.Euler(Mathf.Clamp(swing * 30f, -50f, 50f), 0f, 0f);
            }
        }

        // Confetti colours: one of three, no blends in between.
        private static ParticleSystem.MinMaxGradient Palette(Color a, Color b, Color c)
        {
            Gradient g = new Gradient();
            g.mode = GradientMode.Fixed;
            g.SetKeys(
                new[] { new GradientColorKey(a, 0.34f), new GradientColorKey(b, 0.67f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            ParticleSystem.MinMaxGradient result = new ParticleSystem.MinMaxGradient(g);
            result.mode = ParticleSystemGradientMode.RandomColor;
            return result;
        }
    }
}
