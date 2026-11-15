using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals
{
    // Carcavelos Wave (task V34): a turquoise wave bursts out of the road round the kart, carries it for the boost
    // and falls flat with a splash. Luc's Items v01 look (7 Nov), rebuilt for the team repo:
    //   form     (0.35 s) a splash ring and a burst of drops, the water rises round the kart
    //   surf     the ring of water rides with the kart: a big crest behind with a white lip curling over the tail,
    //            walls at the sides, a low bow wave in front; two rooster tails of spray fly off the rear corners,
    //            drops fly off the lip, mist behind, foam patches stay on the road
    //   collapse (0.6 s) the crest falls and spreads, landing splash and foam ring
    // Visual only: the boost itself comes from ItemSystem through IKartBoost. Plain class, ItemFx ticks it.
    public class WaveRide
    {
        private const int Segments = 40;
        private const float FormSeconds = 0.35f;
        private const float CollapseSeconds = 0.6f;

        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly MeshRenderer meshRenderer;
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock foamBlock = new MaterialPropertyBlock();
        private readonly WaveShape shape = WaveShape.Default;
        private readonly ParticleSystem sprayLeft;
        private readonly ParticleSystem sprayRight;
        private readonly ParticleSystem lipDrops;
        private readonly ParticleSystem sheetLeft;  // big white spray puffs at the sides
        private readonly ParticleSystem sheetRight;
        private readonly ParticleSystem mist;
        private readonly ParticleSystem foamTrail;
        private readonly ParticleSystem drops;     // one-off bursts (form, landing)
        private readonly ParticleSystem rings;     // splash rings on the road
        private readonly Color turquoise;
        private float time = -1f;
        private float holdSeconds;
        private bool landed;
        private Vector2 scroll;

        public bool IsActive { get { return time >= 0f; } }

        // True while the water is up (not in the last part of the collapse): KartEffects shows no exhaust fire then.
        public bool IsRiding { get { return time >= 0f && time < FormSeconds + holdSeconds + CollapseSeconds * 0.5f; } }

        public WaveRide(Transform parent, ItemFxLook look)
        {
            turquoise = look.WaveTurquoise;
            root = new GameObject("WaveRide").transform;
            root.gameObject.layer = 2;
            root.SetParent(parent, false);

            mesh = new Mesh { name = "CarcavelosWave" };
            mesh.MarkDynamic();
            vertices = new Vector3[WaveMesh.VertexCount(Segments)];
            uvs = new Vector2[vertices.Length];
            WaveMesh.Write(shape, 0f, 0f, 0f, Segments, vertices, uvs);
            mesh.vertices = vertices;
            mesh.uv = uvs;
            int[] surfaceTriangles = WaveMesh.Triangles(Segments);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(surfaceTriangles, 0); // water body
            mesh.SetTriangles(surfaceTriangles, 1); // foam layer on top
            GameObject surface = new GameObject("Water");
            surface.layer = 2;
            surface.transform.SetParent(root, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { look.Water, look.Foam };
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
            if (look.Water == null || look.Foam == null)
            {
                meshRenderer.gameObject.SetActive(false);
            }

            Color foamWhite = look.FoamWhite;
            Color spray = Color.Lerp(foamWhite, turquoise, 0.25f);
            sprayLeft = Spray(look, "SprayLeft", -1, spray, foamWhite);
            sprayRight = Spray(look, "SprayRight", 1, spray, foamWhite);
            sheetLeft = Sheet(look, "SpraySheetLeft", -1);
            sheetRight = Sheet(look, "SpraySheetRight", 1);

            lipDrops = FxParts.Particles(root, "LipDrops", look.Droplet, true, ParticleSystemRenderMode.Billboard, 120);
            FxParts.Basics(lipDrops, 0.35f, 0.6f, 1.2f, 2.6f, 0.06f, 0.13f, foamWhite, spray, 1.1f);
            ParticleSystem.ShapeModule lipShape = lipDrops.shape;
            lipShape.enabled = true;
            lipShape.shapeType = ParticleSystemShapeType.Box;
            lipShape.scale = new Vector3(2.0f, 0.1f, 0.25f);
            lipShape.position = new Vector3(0f, shape.CrestBack * 0.95f, -(shape.InnerHalfLength + shape.WidthBack * 0.38f));
            lipShape.rotation = new Vector3(-55f, 0f, 0f); // up and a little forward, over the tail
            FxParts.FadeAndSize(lipDrops, 0.6f);

            mist = FxParts.Particles(root, "Mist", look.Mist, true, ParticleSystemRenderMode.Billboard, 40);
            FxParts.Basics(mist, 0.6f, 1.0f, 0.3f, 1.0f, 0.9f, 1.5f, new Color(1f, 1f, 1f, 0.30f), new Color(0.85f, 0.98f, 1f, 0.22f), -0.05f);
            ParticleSystem.ShapeModule mistShape = mist.shape;
            mistShape.enabled = true;
            mistShape.shapeType = ParticleSystemShapeType.Box;
            mistShape.scale = new Vector3(2.6f, 0.2f, 0.4f);
            mistShape.position = new Vector3(0f, shape.CrestBack * 0.8f, -(shape.InnerHalfLength + shape.WidthBack * 0.5f));
            FxParts.FadeAndSize(mist, 2.0f);
            SmokeSheet(mist);

            foamTrail = FxParts.Particles(root, "FoamTrail", look.FoamPatch, true, ParticleSystemRenderMode.HorizontalBillboard, 80);
            FxParts.Basics(foamTrail, 1.2f, 1.9f, 0f, 0f, 1.0f, 1.9f, new Color(1f, 1f, 1f, 0.75f), new Color(0.88f, 0.99f, 1f, 0.55f), 0f);
            ParticleSystem.MainModule trailMain = foamTrail.main;
            trailMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.ShapeModule trailShape = foamTrail.shape;
            trailShape.enabled = true;
            trailShape.shapeType = ParticleSystemShapeType.Box;
            trailShape.scale = new Vector3(2.8f, 0f, 0.6f);
            trailShape.position = new Vector3(0f, 0.04f, -(shape.InnerHalfLength + shape.WidthBack * 0.8f));
            FxParts.FadeAndSize(foamTrail, 1.7f);

            drops = FxParts.Particles(root, "SplashDrops", look.Droplet, true, ParticleSystemRenderMode.Stretch, 160);
            FxParts.Basics(drops, 0.45f, 0.9f, 0f, 0f, 0.08f, 0.2f, foamWhite, spray, 1.6f);
            FxParts.FadeAndSize(drops, 0.7f);

            rings = FxParts.Particles(root, "SplashRings", look.Ring, true, ParticleSystemRenderMode.HorizontalBillboard, 6);
            FxParts.Basics(rings, 0.6f, 0.6f, 0f, 0f, 6.5f, 6.5f, turquoise, Color.Lerp(turquoise, Color.white, 0.4f), 0f);
            RingGrowth(rings);
        }

        // The wave starts now and lasts boostSeconds in all (form + surf + collapse).
        public void Start(float boostSeconds)
        {
            time = 0f;
            landed = false;
            holdSeconds = Mathf.Max(0.2f, boostSeconds - FormSeconds - CollapseSeconds);
            meshRenderer.enabled = true;
            Vector3 centre = root.position + root.up * 0.1f;
            FxParts.BurstAt(drops, centre, 70, root.up * 4.5f, 4f, 0.9f);
            FxParts.BurstAt(rings, centre, 1, Vector3.zero, 0f, 0f);
        }

        // One frame. speed = the kart's speed in m/s (foam patches only while it moves).
        public void Tick(float dt, float speed)
        {
            if (time < 0f)
            {
                return;
            }
            time += dt;
            float total = FormSeconds + holdSeconds + CollapseSeconds;
            float rise = ItemFxRules.Envelope(time, FormSeconds, 99f, 0f);
            rise = Mathf.Min(1.08f, rise + 0.08f * Mathf.Sin(Mathf.Clamp01(time / FormSeconds) * Mathf.PI)); // small overshoot
            float collapse = Mathf.Clamp01((time - FormSeconds - holdSeconds) / CollapseSeconds);

            WaveMesh.Write(shape, Mathf.Min(1f, rise), collapse, time, Segments, vertices, uvs);
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // The water flows round the kart (u only: v holds the shallow-to-deep shading and the foam bands).
            // The foam layer runs faster and the other way, so the lace churns.
            scroll += new Vector2(0.12f + speed * 0.004f, -0.2f - speed * 0.01f) * dt;
            block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, Mathf.Repeat(scroll.x, 1f), 0f));
            meshRenderer.SetPropertyBlock(block, 0);
            foamBlock.SetVector("_BaseMap_ST", new Vector4(1f, 1f, Mathf.Repeat(scroll.y, 1f), 0f));
            meshRenderer.SetPropertyBlock(foamBlock, 1);

            bool surfing = time > FormSeconds * 0.6f && collapse < 0.3f;
            float strength = surfing ? 1f : 0f;
            float pace = Mathf.Clamp01(0.45f + speed / 18f);
            FxParts.Rate(sprayLeft, 190f * strength * pace);
            FxParts.Rate(sprayRight, 190f * strength * pace);
            FxParts.Rate(sheetLeft, 26f * strength * pace);
            FxParts.Rate(sheetRight, 26f * strength * pace);
            FxParts.Rate(lipDrops, 60f * strength);
            FxParts.Rate(mist, 20f * strength);
            FxParts.Rate(foamTrail, (surfing && speed > 2f) ? 50f : 0f);

            if (!landed && collapse > 0.45f)
            {
                landed = true; // the crest hits the road: landing splash
                Vector3 back = root.position - root.forward * 1.2f + root.up * 0.1f;
                FxParts.BurstAt(drops, back, 80, root.up * 3.5f - root.forward * 1.5f, 5f, 0.85f);
                FxParts.BurstAt(rings, root.position + root.up * 0.05f, 1, Vector3.zero, 0f, 0f);
                FxParts.BurstAt(foamTrail, root.position, 10, Vector3.zero, 0f, 0f);
            }
            if (time >= total)
            {
                Stop();
            }
        }

        // Ends at once (retry, recovery, teleport): no landing splash, the water is gone; airborne drops fade out.
        public void Clear()
        {
            Stop();
            ParticleSystem[] all = { sprayLeft, sprayRight, sheetLeft, sheetRight, lipDrops, mist, foamTrail, drops, rings };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) all[i].Clear();
            }
        }

        private void Stop()
        {
            time = -1f;
            meshRenderer.enabled = false;
            FxParts.Rate(sprayLeft, 0f);
            FxParts.Rate(sprayRight, 0f);
            FxParts.Rate(sheetLeft, 0f);
            FxParts.Rate(sheetRight, 0f);
            FxParts.Rate(lipDrops, 0f);
            FxParts.Rate(mist, 0f);
            FxParts.Rate(foamTrail, 0f);
        }

        // A rooster tail of spray from one rear corner of the wave, up, back and outwards (side -1 = left, 1 = right):
        // the two tails make a V behind the kart and leave the middle, the camera's view of the kart, clear.
        private ParticleSystem Spray(ItemFxLook look, string name, int side, Color a, Color b)
        {
            ParticleSystem system = FxParts.Particles(root, name, look.Droplet, true, ParticleSystemRenderMode.Stretch, 160);
            FxParts.Basics(system, 0.4f, 0.7f, 4f, 7f, 0.06f, 0.16f, a, b, 1.6f);
            system.transform.localPosition = new Vector3(side * (shape.InnerHalfWidth + 0.35f), 0.45f,
                -(shape.InnerHalfLength + 0.45f));
            system.transform.localRotation = Quaternion.LookRotation(new Vector3(side * 1.0f, 1.4f, -0.6f), Vector3.up);
            ParticleSystem.ShapeModule spray = system.shape;
            spray.enabled = true;
            spray.shapeType = ParticleSystemShapeType.Cone;
            spray.angle = 14f;
            spray.radius = 0.25f;
            spray.radiusThickness = 1f;
            FxParts.FadeAndSize(system, 0.5f);
            return system;
        }

        // Big soft white puffs inside each rooster tail, so it reads as a sheet of spray from far away.
        private ParticleSystem Sheet(ItemFxLook look, string name, int side)
        {
            ParticleSystem system = FxParts.Particles(root, name, look.Mist, true, ParticleSystemRenderMode.Billboard, 60);
            FxParts.Basics(system, 0.35f, 0.6f, 1.5f, 2.6f, 0.45f, 0.85f, new Color(1f, 1f, 1f, 0.7f), new Color(0.88f, 0.98f, 1f, 0.55f), 0.6f);
            system.transform.localPosition = new Vector3(side * (shape.InnerHalfWidth + 0.45f), 0.6f,
                -(shape.InnerHalfLength + 0.5f));
            system.transform.localRotation = Quaternion.LookRotation(new Vector3(side * 1.0f, 1.2f, -0.6f), Vector3.up);
            ParticleSystem.ShapeModule cone = system.shape;
            cone.enabled = true;
            cone.shapeType = ParticleSystemShapeType.Cone;
            cone.angle = 18f;
            cone.radius = 0.3f;
            FxParts.FadeAndSize(system, 2.2f);
            SmokeSheet(system);
            return system;
        }

        // The smoke material is a 4 x 4 sheet: each puff gets a random picture from it.
        public static void SmokeSheet(ParticleSystem system)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f);
        }

        // A splash/shock ring: grows fast from a fifth of its size to full (ease out) and fades.
        public static void RingGrowth(ParticleSystem system)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.15f, 3.5f, 3.5f), new Keyframe(1f, 1f, 0f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, grow);
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.45f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);
        }
    }
}
