using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // One lifebuoy ring of the Seagull Delivery (task R7): a red and white ring standing upright on the road.
    // Drive through it and it bursts (bigger, then gone) and ItemSystem gives a boost. Made in code from eight
    // cylinder pieces, no prefab and no model files (Luc's 3D models can replace this later, TASKS.md request).
    // It pops in a little after the gull's cry, wobbles while it waits and shrinks away when its time is up.
    public class SeagullRing : MonoBehaviour
    {
        private const int Pieces = 8;
        private const float PopInSeconds = 0.35f;
        private const float BurstSeconds = 0.3f;
        private const float ShrinkSeconds = 0.4f;

        private ItemSystem system;
        private Transform visual;
        private float age = 0f;
        private float delay;          // seconds after the drop before this ring appears (one after the other)
        private float lifeSeconds;
        private float burstTime = -1f; // seconds since it was driven through, -1 = not yet
        private bool collected;

        public bool IsCollected { get { return collected; } }

        // Called once by ItemSystem right after AddComponent.
        public void SetUp(ItemSystem owner, Material red, Material white, float radius, float appearDelay, float life)
        {
            system = owner;
            delay = appearDelay;
            lifeSeconds = life;
            gameObject.layer = 2; // "Ignore Raycast", like the item boxes: the kart's ground ray never sees it

            // The trigger fills the whole hole and the rim: clipping the ring counts too (forgiving on purpose).
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(radius * 2.2f, radius * 2.2f, 2f);

            visual = new GameObject("Lifebuoy").transform;
            visual.SetParent(transform, false);
            float thickness = radius * 0.32f;
            float pieceLength = 2f * Mathf.PI * radius / Pieces * 1.08f; // a little overlap: no gaps at the joints
            for (int i = 0; i < Pieces; i++)
            {
                float angle = (i + 0.5f) * 2f * Mathf.PI / Pieces;
                Vector3 onRing = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 along = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                piece.name = "Piece " + i;
                Destroy(piece.GetComponent<Collider>());
                piece.layer = 2;
                piece.transform.SetParent(visual, false);
                piece.transform.localPosition = onRing * radius;
                piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along);
                piece.transform.localScale = new Vector3(thickness, pieceLength * 0.5f, thickness); // a cylinder is 2 units tall
                MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = i % 2 == 0 ? red : white;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            visual.localScale = Vector3.zero; // grows in after the delay
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!collected && age >= delay && system != null && system.IsKart(other))
            {
                collected = true;
                burstTime = 0f;
                system.OnRingHit(this);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (burstTime >= 0f)
            {
                // Driven through: swells to 160 % and vanishes, like a soap bubble.
                burstTime += dt;
                float p = Mathf.Clamp01(burstTime / BurstSeconds);
                visual.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, p) * (1f - p);
                if (p >= 1f)
                {
                    Destroy(gameObject);
                }
                return;
            }

            float shown = age - delay;
            if (shown < 0f)
            {
                return;
            }
            float scale = 1f;
            if (shown < PopInSeconds)
            {
                float p = shown / PopInSeconds; // 0 -> 115 % -> 100 %
                scale = p < 0.7f ? Mathf.Lerp(0f, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            }
            float timeLeft = lifeSeconds - shown;
            if (timeLeft < ShrinkSeconds)
            {
                scale *= Mathf.Clamp01(timeLeft / ShrinkSeconds);
                if (timeLeft <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            visual.localScale = Vector3.one * scale;
            // A slow roll and a little bob: it floats on the sea breeze and says "drive through me".
            visual.localRotation = Quaternion.Euler(0f, 0f, shown * 60f);
            visual.localPosition = new Vector3(0f, Mathf.Sin(shown * 3f) * 0.12f, 0f);
        }
    }
}
