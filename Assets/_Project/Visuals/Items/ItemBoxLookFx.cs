using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The light on the road under an item box (task V34), on the ItemBoxGround prefab. Role 4's ItemBox puts a copy
    // of it under each box (ItemSettings > Box Ground Look, help PR) and keeps breaking and regrowing the box itself;
    // this only watches ItemBox.IsAvailable:
    //   box there   a soft ring of light pulses outwards on the road ("drive here")
    //   box breaks  a burst of glaze sparkles and a fast light ring (on top of Role 4's tile shards)
    //   box back    a small pop ring when it has grown back
    public class ItemBoxLookFx : MonoBehaviour
    {
        [SerializeField, Tooltip("Looping pulse ring on the road while the box is there")]
        private ParticleSystem groundRing;

        [SerializeField, Tooltip("Burst of sparkles when the box breaks (world space)")]
        private ParticleSystem breakSparkles;

        [SerializeField, Tooltip("One fast light ring when the box breaks, a small one when it is back (world space)")]
        private ParticleSystem breakRing;

        [SerializeField, Tooltip("Sparkles per break")]
        private int sparkleCount = 34;

        private ItemBox box;
        private bool wasAvailable = true;

        private void Start()
        {
            box = GetComponentInParent<ItemBox>();
        }

        private void Update()
        {
            if (box == null)
            {
                return;
            }
            bool available = box.IsAvailable;
            if (wasAvailable && !available)
            {
                Burst();
            }
            else if (!wasAvailable && available)
            {
                Back();
            }
            wasAvailable = available;
        }

        private void Burst()
        {
            Vector3 centre = box.transform.position;
            if (breakSparkles != null)
            {
                breakSparkles.transform.position = centre;
                breakSparkles.Emit(sparkleCount);
            }
            if (breakRing != null)
            {
                breakRing.transform.position = transform.position + Vector3.up * 0.05f;
                ParticleSystem.EmitParams ring = new ParticleSystem.EmitParams { startSize = 7f };
                breakRing.Emit(ring, 1);
            }
            if (groundRing != null)
            {
                groundRing.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Back()
        {
            if (breakRing != null)
            {
                breakRing.transform.position = transform.position + Vector3.up * 0.05f;
                ParticleSystem.EmitParams ring = new ParticleSystem.EmitParams { startSize = 3.2f };
                breakRing.Emit(ring, 1);
            }
            if (groundRing != null)
            {
                groundRing.Play(true);
            }
        }
    }
}
