using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Seagull Delivery (task V34): Nori, Luc's courier gull (Items v01 model), pops in just ahead of the kart in a puff of
    // feathers, flies ahead along the rings and tosses a lifebuoy onto each spot in an arc. It lands exactly where
    // and when ItemSystem shows the real ring, so the ring looks delivered. Then Nori climbs away with a barrel
    // roll. Driving through a ring bursts sparkles and a white splash ring there.
    // World space (the gull does not ride with the kart), so its root sits at the scene root, not under the kart.
    public class SeagullFx
    {
        // ItemSystem shows ring n after 0.25 + 0.15 n seconds (ItemSystem.TryPlaceRing); the tosses land then.
        public const float FirstRingDelay = 0.25f;
        public const float RingDelayStep = 0.15f;
        private const int MaxRings = 6;
        private const float GullScale = 1.35f;
        private const float TossSeconds = 0.32f;

        private readonly Transform root;
        private readonly Transform gull;
        private readonly Transform wingLeft;
        private readonly Transform wingRight;
        private readonly Transform[] buoys = new Transform[MaxRings];
        private readonly float buoyModelRadius;
        private readonly ParticleSystem feathers;
        private readonly ParticleSystem sparkles;
        private readonly ParticleSystem splash;
        private readonly Vector3[] ringSpots = new Vector3[MaxRings];
        private readonly Vector3[] ringFacing = new Vector3[MaxRings];
        private readonly Vector3[] tossFrom = new Vector3[MaxRings];
        private readonly float[] ringRadius = new float[MaxRings];
        private int ringCount;
        private Vector3[] path = new Vector3[0];
        private float flightSeconds;
        private float time = -1f;
        private Vector3 lastGull;

        public bool IsActive { get { return time >= 0f; } }

        // The gull's own root (scene root, world space), for ItemFx's shader warm-up.
        public Transform Root { get { return root; } }

        public SeagullFx(ItemFxLook look)
        {
            root = new GameObject("SeagullFx").transform; // scene root: world space
            root.gameObject.layer = 2;

            gull = new GameObject("Nori").transform;
            gull.SetParent(root, false);
            gull.localScale = Vector3.one * GullScale;
            Transform body = FxParts.Model(look.Seagull, gull, "Body");
            if (body != null)
            {
                // The sculpt stands on its feet (0.95 m); in flight it leans forward round its middle (Items v01).
                body.localRotation = Quaternion.Euler(38f, 0f, 0f);
                body.localPosition = new Vector3(0f, -0.38f, 0.28f);
                body.gameObject.SetActive(true);
            }
            wingLeft = Wing(look, -1);
            wingRight = Wing(look, 1);
            gull.gameObject.SetActive(false);

            buoyModelRadius = 0.5f;
            for (int i = 0; i < MaxRings; i++)
            {
                buoys[i] = FxParts.Model(look.Lifebuoy, root, "TossedLifebuoy " + i);
                if (buoys[i] != null && i == 0)
                {
                    Renderer r = buoys[i].GetComponentInChildren<Renderer>(true);
                    if (r != null)
                    {
                        Vector3 size = r.localBounds.size;
                        buoyModelRadius = Mathf.Max(0.05f, Mathf.Max(size.x, size.y, size.z) * 0.5f);
                    }
                }
            }

            feathers = FxParts.Particles(root, "Feathers", look.Confetti, true, ParticleSystemRenderMode.Billboard, 60);
            FxParts.Basics(feathers, 0.7f, 1.2f, 0f, 0f, 0.09f, 0.16f, new Color(1f, 1f, 1f, 1f), new Color(0.82f, 0.86f, 0.92f, 1f), 0.25f);
            ParticleSystem.MainModule fm = feathers.main;
            fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.RotationOverLifetimeModule spin = feathers.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = feathers.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;
            FxParts.FadeAndSize(feathers, 0.8f);

            sparkles = FxParts.Particles(root, "RingSparkles", look.Spark, true, ParticleSystemRenderMode.Stretch, 120);
            FxParts.Basics(sparkles, 0.3f, 0.6f, 0f, 0f, 0.06f, 0.12f, new Color(1f, 0.95f, 0.8f, 1f), look.LifebuoyRed, 0.6f);
            FxParts.FadeAndSize(sparkles, 0.3f);

            splash = FxParts.Particles(root, "RingSplash", look.Ring, true, ParticleSystemRenderMode.Billboard, 6);
            FxParts.Basics(splash, 0.4f, 0.4f, 0f, 0f, 4.5f, 4.5f, new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.9f, 0.85f, 0.9f), 0f);
            WaveRide.RingGrowth(splash);
        }

        // The delivery starts: kart = where the kart is now, rings = the spots ItemSystem put the rings on (in order),
        // facing = the way each ring faces, radius = each ring's radius. kartSpeed sets how fast Nori flies.
        public void Start(Transform kart, Vector3[] rings, Vector3[] facing, float[] radius, int count, float kartSpeed)
        {
            ringCount = Mathf.Min(count, MaxRings);
            Vector3 forward = kart.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            int points = ringCount + 2;
            path = new Vector3[points];
            path[0] = kart.position + forward * 4f + Vector3.up * 3.6f; // ahead of the kart: never between camera and kart
            Vector3 last = kart.position + forward * 12f;
            for (int i = 0; i < ringCount; i++)
            {
                ringSpots[i] = rings[i];
                ringFacing[i] = facing[i];
                ringRadius[i] = radius[i];
                path[i + 1] = rings[i] + Vector3.up * (radius[i] + 2.6f);
                last = rings[i];
            }
            path[points - 1] = last + forward * 16f + Vector3.up * 11f;
            float length = 0f;
            for (int i = 1; i < points; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }
            flightSeconds = Mathf.Clamp(length / Mathf.Max(24f, kartSpeed + 14f), 1.4f, 3.2f);
            time = 0f;
            gull.gameObject.SetActive(true);
            gull.position = path[0];
            lastGull = path[0];
            FxParts.BurstAt(feathers, path[0], 18, Vector3.up * 0.5f, 2.5f, 0.3f);
            for (int i = 0; i < MaxRings; i++)
            {
                if (buoys[i] != null) buoys[i].gameObject.SetActive(false);
            }
        }

        // A ring was driven through at this spot.
        public void RingCollected(Vector3 spot)
        {
            FxParts.BurstAt(sparkles, spot, 36, Vector3.zero, 6f, 0.5f);
            FxParts.BurstAt(splash, spot, 1, Vector3.zero, 0f, 0f);
            FxParts.BurstAt(feathers, spot, 6, Vector3.up, 2f, 0.6f);
        }

        public void Tick(float dt)
        {
            if (time < 0f)
            {
                return;
            }
            time += dt;
            float s = Mathf.Clamp01(time / flightSeconds);
            float eased = s * s * (3f - 2f * s) * 0.35f + s * 0.65f; // a quick start, steady, a little ease at the end
            Vector3 position = ItemFxRules.PathPoint(path, eased);
            Vector3 velocity = (position - lastGull) / Mathf.Max(0.0001f, dt);
            lastGull = position;
            gull.position = position;
            if (velocity.sqrMagnitude > 0.5f)
            {
                Quaternion look = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                float exit = Mathf.Clamp01((s - 0.7f) / 0.3f);
                gull.rotation = look * Quaternion.Euler(0f, 0f, ItemFxRules.ExitRoll(exit));
            }
            float flap = ItemFxRules.WingFlap(time, s < 0.15f || s > 0.7f ? 1f : 0.45f);
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, -flap);
            gull.localScale = Vector3.one * GullScale * (s > 0.9f ? Mathf.Clamp01((1f - s) / 0.1f) : Mathf.Clamp01(time / 0.12f));

            for (int i = 0; i < ringCount; i++)
            {
                TickToss(i);
            }
            if (time >= Mathf.Max(flightSeconds, FirstRingDelay + RingDelayStep * ringCount + 0.1f))
            {
                Stop();
            }
        }

        public void Clear()
        {
            Stop();
            feathers.Clear();
            sparkles.Clear();
            splash.Clear();
        }

        private void Stop()
        {
            time = -1f;
            gull.gameObject.SetActive(false);
            for (int i = 0; i < MaxRings; i++)
            {
                if (buoys[i] != null) buoys[i].gameObject.SetActive(false);
            }
        }

        // Lifebuoy n flies from the gull to its spot and disappears the moment the real ring pops up there.
        private void TickToss(int n)
        {
            Transform buoy = buoys[n];
            if (buoy == null)
            {
                return;
            }
            float land = FirstRingDelay + RingDelayStep * n;
            float start = Mathf.Max(0f, land - TossSeconds);
            if (time < start || time >= land)
            {
                if (buoy.gameObject.activeSelf && time >= land)
                {
                    buoy.gameObject.SetActive(false);
                    FxParts.BurstAt(feathers, ringSpots[n], 5, Vector3.up * 0.8f, 1.5f, 0.6f);
                }
                if (time < start)
                {
                    tossFrom[n] = gull.position;
                }
                return;
            }
            if (!buoy.gameObject.activeSelf)
            {
                buoy.gameObject.SetActive(true);
                tossFrom[n] = gull.position;
            }
            float p = (time - start) / (land - start);
            Vector3 straight = Vector3.Lerp(tossFrom[n], ringSpots[n], p * p * 0.4f + p * 0.6f);
            buoy.position = straight + Vector3.up * ItemFxRules.TossHeight(p, 1f, 1.4f);
            buoy.rotation = Quaternion.LookRotation(ringFacing[n].sqrMagnitude > 0.01f ? ringFacing[n] : Vector3.forward, Vector3.up)
                * Quaternion.Euler(0f, (1f - p) * 540f, 0f);
            buoy.localScale = Vector3.one * (ringRadius[n] * 1.15f / buoyModelRadius) * Mathf.Lerp(0.45f, 1f, p);
        }

        // Shoulder pivot with the sculpted wing (Items v01: the FBX arrives mirrored in X and Z).
        private Transform Wing(ItemFxLook look, int side)
        {
            Transform pivot = new GameObject(side < 0 ? "WingLeft" : "WingRight").transform;
            pivot.SetParent(gull, false);
            pivot.localPosition = new Vector3(side * 0.2f, 0.12f, 0.02f);
            Transform model = FxParts.Model(look.SeagullWing, pivot, "Wing");
            if (model == null)
            {
                return null;
            }
            model.localScale = new Vector3(-side * 0.8f, 0.8f, -0.8f);
            model.gameObject.SetActive(true);
            return pivot;
        }

        public void Destroy()
        {
            if (root != null)
            {
                FxParts.Kill(root.gameObject);
            }
        }
    }
}
