using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // One boost pad on the road (task V11): a flat, glowing field of yellow and orange chevrons that run forward.
    // Drive over it and the kart gets a short boost through Shared/IKartBoost, the same path the item boxes use,
    // so the boost flame and the boost sound come on by themselves. It works for every kart (player and bots);
    // a kart without IKartBoost just drives over it. CampusWorldBuilder puts the pads on the route; the look is
    // made in code when the race starts, so the scene only stores the position and the numbers below.
    public class BoostPad : MonoBehaviour
    {
        private const float CooldownSeconds = 0.5f;   // one boost per pass, also when the kart bounces on the pad
        private const float ScrollPerSecond = 1.6f;   // chevron tiles per second
        private const float LiftMetres = 0.04f;       // the picture floats just above the road, no z-fighting

        [SerializeField, Tooltip("Seconds of boost per pass (the short drift boost is 0.4 s, the long one 1.2 s)")]
        private float boostSeconds = 0.8f;

        [SerializeField, Tooltip("Top speed while boosting = MaxSpeed * this (same as the drift boost)")]
        private float speedMultiplier = 1.35f;

        [SerializeField, Tooltip("Width across the road and length along it, in metres")]
        private Vector2 size = new Vector2(4.5f, 3f);

        [SerializeField, Tooltip("Lit material with emission switched on (Visuals/BoostPads/BoostPad.mat); the chevrons are added in code")]
        private Material baseMaterial;

        private static Material liveMaterial;   // one material for all pads, so they scroll in step
        private static int scrolledFrame = -1;

        private readonly BoostPadRule rule = new BoostPadRule(CooldownSeconds);

        public float BoostSeconds { get { return boostSeconds; } }
        public float SpeedMultiplier { get { return speedMultiplier; } }

        // Used by the builder, before the scene is saved.
        public void Configure(Vector2 padSize, Material material)
        {
            size = padSize;
            baseMaterial = material;
        }

        private void Awake()
        {
            gameObject.layer = 2; // "Ignore Raycast": the kart's ground ray and the camera never see the trigger

            // The trigger is a box from the road up to about the kart's roof, so a kart that hops a little still counts.
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2f, size.y);
            trigger.center = new Vector3(0f, 1f, 0f);

            GameObject picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
            picture.name = "Picture";
            Destroy(picture.GetComponent<Collider>());
            picture.layer = 2;
            picture.transform.SetParent(transform, false);
            picture.transform.localPosition = new Vector3(0f, LiftMetres, 0f);
            picture.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lies flat, the texture's "up" points forward
            picture.transform.localScale = new Vector3(size.x, size.y, 1f);
            MeshRenderer renderer = picture.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = LiveMaterial(baseMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The chevrons run forward. All pads share one material, so only the first pad each frame moves it.
        private void Update()
        {
            if (liveMaterial == null || scrolledFrame == Time.frameCount)
            {
                return;
            }
            scrolledFrame = Time.frameCount;
            float offset = -(Time.time * ScrollPerSecond % 1f); // a negative offset moves the picture forward
            liveMaterial.SetTextureOffset("_BaseMap", new Vector2(0f, offset));
            liveMaterial.SetTextureOffset("_EmissionMap", new Vector2(0f, offset));
        }

        private void OnTriggerEnter(Collider other)
        {
            // The kart's physics ball carries the Rigidbody; IKartBoost sits on it or on a parent.
            Component kart = other.attachedRigidbody != null ? (Component)other.attachedRigidbody : other;
            IKartBoost boost = kart.GetComponentInParent<IKartBoost>();
            if (boost == null)
            {
                return;
            }
            if (rule.TryBoost(kart.gameObject.GetInstanceID(), Time.time))
            {
                boost.AddBoost(boostSeconds, speedMultiplier);
            }
        }

        private static Material LiveMaterial(Material template)
        {
            if (liveMaterial != null)
            {
                return liveMaterial;
            }
            Texture2D chevrons = BoostPadTexture.Create();
            liveMaterial = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            liveMaterial.name = "BoostPad (live)";
            liveMaterial.SetColor("_BaseColor", Color.white);
            liveMaterial.SetTexture("_BaseMap", chevrons);
            liveMaterial.SetTexture("_EmissionMap", chevrons);
            liveMaterial.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0.8f, 1f)); // glows: easy to spot in the tunnel
            liveMaterial.EnableKeyword("_EMISSION");
            return liveMaterial;
        }
    }
}
