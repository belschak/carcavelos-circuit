using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals
{
    // Small helpers the item effects share (task V34): particle systems, mesh objects and model copies made in code
    // at runtime. Every particle system starts silent (no emission, no play on awake): the effect scripts emit
    // bursts or switch a rate on, so nothing runs while no item is used.
    public static class FxParts
    {
        // A particle system under parent. world = particles stay where they were born (trails, splashes);
        // otherwise they move with the kart (rings round it, glitter on the wreath).
        public static ParticleSystem Particles(Transform parent, string name, Material material, bool world,
            ParticleSystemRenderMode mode, int maxParticles)
        {
            GameObject go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.alignment = mode == ParticleSystemRenderMode.HorizontalBillboard
                ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 2f;
            }
            if (material == null)
            {
                renderer.enabled = false; // the look asset is missing this part: stay invisible, never pink
            }
            system.Play();
            return system;
        }

        // Life, speed, size and colour range of the particles a system makes.
        public static void Basics(ParticleSystem system, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, Color colourA, Color colourB, float gravity)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
            main.gravityModifier = gravity;
        }

        // Fade out over the particle's life (and grow or shrink by sizeEnd).
        public static void FadeAndSize(ParticleSystem system, float sizeEnd)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeEnd));
        }

        public static void Rate(ParticleSystem system, float perSecond)
        {
            if (system == null)
            {
                return;
            }
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = perSecond;
        }

        // Emit count particles at a world point with a velocity spread (for one-off bursts from any place).
        public static void BurstAt(ParticleSystem system, Vector3 position, int count, Vector3 baseVelocity,
            float spread, float upward)
        {
            if (system == null)
            {
                return;
            }
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            bool world = system.main.simulationSpace == ParticleSystemSimulationSpace.World;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * upward + (1f - upward) * dir.y;
                p.position = world ? position : system.transform.InverseTransformPoint(position);
                p.velocity = baseVelocity + dir * spread;
                p.applyShapeToPosition = false;
                system.Emit(p, 1);
            }
        }

        // A copy of one of Luc's models (or nothing if it is missing), without colliders and shadows, layer 2.
        public static Transform Model(GameObject prefab, Transform parent, string name)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject copy = Object.Instantiate(prefab, parent, false);
            copy.name = name;
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
            {
                Kill(c);
            }
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.gameObject.layer = 2;
            }
            copy.layer = 2;
            copy.SetActive(false);
            return copy.transform;
        }

        // Destroy in Play Mode, DestroyImmediate in Edit Mode (the builder's check renders run the effects there).
        public static void Kill(Object thing)
        {
            if (thing == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(thing);
            }
            else
            {
                Object.DestroyImmediate(thing);
            }
        }
    }
}
