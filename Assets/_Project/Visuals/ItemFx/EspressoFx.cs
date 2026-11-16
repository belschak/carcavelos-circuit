using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals
{
    // Espresso (task V34): the coffee kick. On use a caramel crema shock ring races out over the road, a flash and
    // a spray of sparks pop at the kart and a puff of steam rises. While the boost holds, two glowing crema trails
    // stream from the rear wheels and thin speed lines rush past the kart; steam keeps curling off it.
    // Luc's Items v01 read ("crema shock ring + sparks, twin crema trails, edge speed streaks"), rebuilt here.
    // The exhaust fire itself stays KartEffects' job (it burns hotter during the kick, ItemFx tells it).
    public class EspressoFx
    {
        private readonly Transform root;
        private readonly ParticleSystem ring;
        private readonly ParticleSystem kickRing;  // an upright crema ring bursting off the kart's tail
        private readonly ParticleSystem flash;
        private readonly ParticleSystem sparks;
        private readonly ParticleSystem steam;
        private readonly ParticleSystem streaks;
        private readonly TrailRenderer[] trails = new TrailRenderer[2];
        private float time = -1f;
        private float holdSeconds;

        public bool IsActive { get { return time >= 0f; } }

        public EspressoFx(Transform parent, ItemFxLook look)
        {
            root = new GameObject("EspressoFx").transform;
            root.gameObject.layer = 2;
            root.SetParent(parent, false);
            Color crema = look.Crema;
            Color light = Color.Lerp(crema, Color.white, 0.55f);
            Color roast = new Color(crema.r * 0.85f, crema.g * 0.72f, crema.b * 0.5f, 1f); // darker: reads on sunny grass and stone

            ring = FxParts.Particles(root, "CremaRing", look.Ring, true, ParticleSystemRenderMode.HorizontalBillboard, 4);
            FxParts.Basics(ring, 0.65f, 0.65f, 0f, 0f, 8f, 8f, roast, crema, 0f);
            WaveRide.RingGrowth(ring);

            kickRing = FxParts.Particles(root, "KickRing", look.Ring, true, ParticleSystemRenderMode.Billboard, 3);
            FxParts.Basics(kickRing, 0.4f, 0.4f, 0f, 0f, 4.2f, 4.2f, crema, roast, 0f);
            WaveRide.RingGrowth(kickRing);

            flash = FxParts.Particles(root, "Flash", look.Spark, false, ParticleSystemRenderMode.Billboard, 2);
            FxParts.Basics(flash, 0.22f, 0.22f, 0f, 0f, 3.2f, 3.2f, light, light, 0f);
            FxParts.FadeAndSize(flash, 1.6f);

            sparks = FxParts.Particles(root, "Sparks", look.Spark, true, ParticleSystemRenderMode.Stretch, 80);
            FxParts.Basics(sparks, 0.35f, 0.7f, 0f, 0f, 0.12f, 0.22f, light, crema, 1.2f);
            FxParts.FadeAndSize(sparks, 0.4f);

            steam = FxParts.Particles(root, "Steam", look.Mist, true, ParticleSystemRenderMode.Billboard, 60);
            FxParts.Basics(steam, 0.8f, 1.3f, 0.6f, 1.4f, 0.5f, 0.9f, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 0.95f, 0.88f, 0.55f), -0.12f);
            ParticleSystem.ShapeModule steamShape = steam.shape;
            steamShape.enabled = true;
            steamShape.shapeType = ParticleSystemShapeType.Cone;
            steamShape.angle = 18f;
            steamShape.radius = 0.35f;
            steamShape.position = new Vector3(0f, 0.9f, -0.5f);
            steamShape.rotation = new Vector3(-80f, 0f, 0f); // the cone points up
            FxParts.FadeAndSize(steam, 2.6f);
            WaveRide.SmokeSheet(steam);

            // Speed lines: born ahead and round the kart, rushing back past it (local space: they ride along).
            streaks = FxParts.Particles(root, "SpeedLines", look.Trail, false, ParticleSystemRenderMode.Stretch, 90);
            FxParts.Basics(streaks, 0.22f, 0.32f, 26f, 34f, 0.04f, 0.08f, new Color(1f, 1f, 1f, 0.75f), new Color(1f, 0.9f, 0.75f, 0.6f), 0f);
            ParticleSystemRenderer streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
            streakRenderer.velocityScale = 0.05f;
            streakRenderer.lengthScale = 1f;
            ParticleSystem.ShapeModule streakShape = streaks.shape;
            streakShape.enabled = true;
            streakShape.shapeType = ParticleSystemShapeType.Box;
            streakShape.scale = new Vector3(5.5f, 2.6f, 0.5f);
            streakShape.position = new Vector3(0f, 0.9f, 5f);
            streakShape.rotation = new Vector3(0f, 180f, 0f); // fly backwards (-Z)
            streakShape.boxThickness = new Vector3(0.75f, 0.6f, 0f); // hollow in the middle: never across the kart
            FxParts.FadeAndSize(streaks, 1f);

            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                GameObject go = new GameObject(side < 0 ? "CremaTrailLeft" : "CremaTrailRight");
                go.layer = 2;
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(side * 0.52f, 0.14f, -0.78f);
                TrailRenderer trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = look.Trail;
                trail.time = 0.55f;
                trail.minVertexDistance = 0.25f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0.04f));
                Gradient g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(light, 0f), new GradientColorKey(crema, 0.5f), new GradientColorKey(crema, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                trail.enabled = look.Trail != null;
                trails[i] = trail;
            }
        }

        // The kick happens now; the boost lasts boostSeconds.
        public void Start(float boostSeconds)
        {
            time = 0f;
            holdSeconds = Mathf.Max(0.3f, boostSeconds);
            Vector3 ground = root.position + root.up * 0.06f;
            FxParts.BurstAt(ring, ground, 1, Vector3.zero, 0f, 0f);
            FxParts.BurstAt(flash, root.position + root.up * 0.7f - root.forward * 0.6f, 1, Vector3.zero, 0f, 0f);
            FxParts.BurstAt(kickRing, root.position + root.up * 0.6f - root.forward * 1.1f, 1, -root.forward * 2f, 0f, 0f);
            FxParts.BurstAt(sparks, root.position + root.up * 0.4f - root.forward * 0.9f, 40, -root.forward * 3f, 6f, 0.6f);
            FxParts.BurstAt(steam, root.position + root.up * 0.9f, 10, root.up * 1.2f, 0.8f, 1f);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
                trails[i].emitting = true;
            }
        }

        public void Tick(float dt, float speed)
        {
            if (time < 0f)
            {
                return;
            }
            time += dt;
            float strength = ItemFxRules.Envelope(time, 0.12f, holdSeconds - 0.12f - 0.4f, 0.4f);
            FxParts.Rate(streaks, 85f * strength * Mathf.Clamp01(speed / 12f));
            FxParts.Rate(steam, time < holdSeconds * 0.6f ? 14f : 0f);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].widthMultiplier = Mathf.Max(0.05f, strength);
                trails[i].emitting = strength > 0.05f;
            }
            if (time >= holdSeconds)
            {
                Stop();
            }
        }

        public void Clear()
        {
            Stop();
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
            }
            ParticleSystem[] all = { ring, kickRing, flash, sparks, steam, streaks };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) all[i].Clear();
            }
        }

        private void Stop()
        {
            time = -1f;
            FxParts.Rate(streaks, 0f);
            FxParts.Rate(steam, 0f);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].emitting = false;
            }
        }
    }
}
