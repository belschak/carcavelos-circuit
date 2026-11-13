using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Drift sparks, tyre smoke and boost flames (task V5). Reads IKartState only and never changes driving values
    // (docs/interfaces.md). Sits on the KartEffects prefab, which RaceBootstrap spawns as a child of the kart
    // (its "Kart Attachments" list), so the race scene needs no other setup.
    // The prefabs and particle systems are made by Carcavelos Circuit > Visuals > Build Kart Effects.
    public class KartEffects : MonoBehaviour
    {
        [Header("Parts (set by KartEffectsBuilder)")]
        [SerializeField, Tooltip("Tyre smoke, sparks and the stage-up burst: one copy at each rear wheel")]
        private GameObject wheelEffectsPrefab;

        [SerializeField, Tooltip("Boost flame and ignition sparks: one copy at each exhaust pipe")]
        private GameObject exhaustEffectsPrefab;

        [Header("Fallback positions in kart space (m), used when the model has no Wheel_RL / Wheel_RR / Exhaust_*")]
        [SerializeField, Tooltip("Centre of the rear left wheel (placeholder box kart: -0.6, 0.2, -0.6)")]
        private Vector3 rearLeftWheel = new Vector3(-0.6f, 0.2f, -0.6f);

        [SerializeField, Tooltip("Centre of the rear right wheel (placeholder box kart: 0.6, 0.2, -0.6)")]
        private Vector3 rearRightWheel = new Vector3(0.6f, 0.2f, -0.6f);

        [SerializeField, Tooltip("Open ends of the exhaust pipes; these flames point straight back")]
        private Vector3[] exhausts = { new Vector3(-0.2f, 0.32f, -0.88f), new Vector3(0.2f, 0.32f, -0.88f) };

        [Header("Amounts")]
        [SerializeField, Tooltip("Smoke puffs per second per rear wheel while drifting")]
        private float smokePerSecond = 60f;

        [SerializeField, Tooltip("Sparks per second per rear wheel at stage 1 (blue)")]
        private float sparksPerSecondStage1 = 55f;

        [SerializeField, Tooltip("Sparks per second per rear wheel at stage 2 (orange)")]
        private float sparksPerSecondStage2 = 95f;

        [SerializeField, Tooltip("Sparks thrown per wheel when the drift reaches a new stage")]
        private int stageUpSparks = 24;

        [SerializeField, Tooltip("Flame particles per second per exhaust while boosting")]
        private float flamePerSecond = 70f;

        [SerializeField, Tooltip("Sparks thrown per exhaust when a boost starts")]
        private int ignitionSparks = 12;

        [SerializeField, Tooltip("A jump further than this (m) in one frame is a teleport (retry, recovery): old smoke and sparks are cleared")]
        private float teleportDistance = 8f;

        [Header("Colours: stage 1 = blue, stage 2 = orange (docs/interfaces.md)")]
        [SerializeField] private Color blueSparkLight = new Color(0.62f, 0.86f, 1f);
        [SerializeField] private Color blueSparkDeep = new Color(0.2f, 0.45f, 1f);
        [SerializeField] private Color orangeSparkLight = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private Color orangeSparkDeep = new Color(1f, 0.42f, 0.06f);
        [SerializeField, Tooltip("Boost flame after a stage 1 drift")] private Color blueFlame = new Color(0.3f, 0.55f, 1f);
        [SerializeField] private Color blueFlameCore = new Color(0.82f, 0.93f, 1f);
        [SerializeField, Tooltip("Boost flame after a stage 2 drift, and for any other boost")] private Color orangeFlame = new Color(1f, 0.5f, 0.1f);
        [SerializeField] private Color orangeFlameCore = new Color(1f, 0.9f, 0.65f);

        private IKartState state;
        private readonly KartEffectsRules rules = new KartEffectsRules();
        private readonly List<ParticleSystem> smoke = new List<ParticleSystem>();
        private readonly List<ParticleSystem> sparks = new List<ParticleSystem>();
        private readonly List<ParticleSystem> bursts = new List<ParticleSystem>();
        private readonly List<ParticleSystem> flames = new List<ParticleSystem>();
        private readonly List<ParticleSystem> flameCores = new List<ParticleSystem>();
        private readonly List<ParticleSystem> ignitions = new List<ParticleSystem>();
        private Vector3 lastPosition;
        private bool isSetUp;

        private void Start()
        {
            Setup();
        }

        private void LateUpdate()
        {
            Tick();
        }

        // Finds the kart and its anchors, then puts one effect set on each rear wheel and each exhaust.
        // Public so the builder can run it in Edit Mode for the check renders.
        public void Setup()
        {
            if (isSetUp)
            {
                return;
            }
            Transform kart = transform.parent != null ? transform.parent : transform;
            state = kart.GetComponentInParent<IKartState>();
            if (state == null)
            {
                Debug.LogWarning("KartEffects: no IKartState on the kart, so the effects stay off.");
                enabled = false;
                return;
            }

            AddWheel(kart, KartEffectAnchors.RearLeftWheel, rearLeftWheel);
            AddWheel(kart, KartEffectAnchors.RearRightWheel, rearRightWheel);

            List<Transform> pipes = KartEffectAnchors.FindExhausts(kart);
            if (pipes.Count > 0)
            {
                // A pipe's up axis points out of the pipe; the flame shoots along its own +Z.
                for (int i = 0; i < pipes.Count; i++)
                {
                    AddExhaust(pipes[i], Vector3.zero, Quaternion.LookRotation(Vector3.up, Vector3.back));
                }
            }
            else
            {
                for (int i = 0; i < exhausts.Length; i++)
                {
                    AddExhaust(transform, exhausts[i], Quaternion.LookRotation(Vector3.back, Vector3.up));
                }
            }
            lastPosition = transform.position;
            isSetUp = true;
        }

        // One frame of effects. Public so the builder can step it in Edit Mode.
        public void Tick()
        {
            if (!isSetUp)
            {
                return;
            }
            if (Vector3.Distance(transform.position, lastPosition) > teleportDistance)
            {
                ClearAll();
                rules.Reset();
            }
            lastPosition = transform.position;

            rules.Update(state.IsDrifting, state.IsGrounded, state.DriftStage, state.IsBoosting);
            ShowDrift();
            ShowBoost();
        }

        // Smoke while drifting on the ground, sparks from stage 1, and a burst on every new stage.
        private void ShowDrift()
        {
            float sparkRate = 0f;
            if (rules.SparkStage == 1)
            {
                sparkRate = sparksPerSecondStage1;
            }
            else if (rules.SparkStage == 2)
            {
                sparkRate = sparksPerSecondStage2;
            }
            Color light = rules.SparkStage == 2 ? orangeSparkLight : blueSparkLight;
            Color deep = rules.SparkStage == 2 ? orangeSparkDeep : blueSparkDeep;

            for (int i = 0; i < smoke.Count; i++)
            {
                SetRate(smoke[i], rules.ShowSmoke ? smokePerSecond : 0f);
                SetRate(sparks[i], sparkRate);
                SetColours(sparks[i], light, deep);
                if (rules.StageUp)
                {
                    SetColours(bursts[i], light, deep);
                    bursts[i].Emit(stageUpSparks);
                }
            }
        }

        // Flames while boosting, in the colour of the drift that gave the boost; sparks when a boost starts.
        private void ShowBoost()
        {
            bool blue = rules.FlameStage == 1;
            Color outer = blue ? blueFlame : orangeFlame;
            Color core = blue ? blueFlameCore : orangeFlameCore;

            for (int i = 0; i < flames.Count; i++)
            {
                SetRate(flames[i], rules.ShowFlame ? flamePerSecond : 0f);
                SetColours(flames[i], outer, Color.Lerp(outer, core, 0.3f));
                SetRate(flameCores[i], rules.ShowFlame ? flamePerSecond * 0.6f : 0f);
                SetColours(flameCores[i], core, Color.white);
                if (rules.Ignition)
                {
                    SetColours(ignitions[i], core, outer);
                    ignitions[i].Emit(ignitionSparks);
                }
            }
        }

        private void AddWheel(Transform kart, string anchorName, Vector3 fallbackCentre)
        {
            Transform wheel = KartEffectAnchors.FindChild(kart, anchorName);
            Vector3 centre = wheel != null ? transform.InverseTransformPoint(wheel.position) : fallbackCentre;
            // The copy hangs under this object, not under the spinning wheel, so it does not turn with the tyre.
            GameObject set = Instantiate(wheelEffectsPrefab, transform);
            set.name = "WheelEffects_" + anchorName;
            set.transform.localPosition = KartEffectAnchors.ContactBehindWheel(centre);
            set.transform.localRotation = Quaternion.identity;
            smoke.Add(Part(set, "Smoke"));
            sparks.Add(Part(set, "Sparks"));
            bursts.Add(Part(set, "Burst"));
        }

        private void AddExhaust(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            // Under the pipe itself, so the flame stays on it when the kart model leans or turns.
            GameObject set = Instantiate(exhaustEffectsPrefab, parent);
            set.name = "ExhaustEffects";
            set.transform.localPosition = localPosition;
            set.transform.localRotation = localRotation;
            flames.Add(Part(set, "Flame"));
            flameCores.Add(Part(set, "FlameCore"));
            ignitions.Add(Part(set, "Ignition"));
        }

        private void ClearAll()
        {
            List<ParticleSystem>[] groups = { smoke, sparks, bursts, flames, flameCores, ignitions };
            foreach (List<ParticleSystem> group in groups)
            {
                foreach (ParticleSystem system in group)
                {
                    system.Clear();
                }
            }
        }

        private static void SetRate(ParticleSystem system, float perSecond)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = perSecond;
        }

        // New particles get a random colour between a and b (old ones keep theirs).
        private static void SetColours(ParticleSystem system, Color a, Color b)
        {
            ParticleSystem.MainModule main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        }

        // The particle system with this name inside one effect set.
        private static ParticleSystem Part(GameObject set, string name)
        {
            ParticleSystem[] systems = set.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i].name == name)
                {
                    return systems[i];
                }
            }
            Debug.LogError("KartEffects: particle system '" + name + "' is missing in " + set.name + ". Run Carcavelos Circuit > Visuals > Build Kart Effects.");
            return null;
        }
    }
}
