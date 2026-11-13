using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Feeds KartVisualSteering from the kart it sits on, through IKartState only (docs/interfaces.md).
    // Speed and Steer come straight from IKartState (Steer since #55, Luc's request Q11). Steer is the key input
    // (-1, 0 or +1 on a keyboard); KartVisualSteering smooths it, so the steering wheel still turns softly.
    // Without a kart (showroom, prefab preview) the wheel stays straight. Runs before KartVisualSteering.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(KartVisualSteering))]
    public class KartVisualFromState : MonoBehaviour
    {
        private KartVisualSteering steering;
        private IKartState state;

        private void Awake()
        {
            steering = GetComponent<KartVisualSteering>();
        }

        private void Start()
        {
            // The kart is a parent of this model (Kart > Visual > KartVisual). Without a parent: this object itself.
            Transform body = transform.parent != null ? transform.parent : transform;
            state = body.GetComponentInParent<IKartState>();
        }

        private void Update()
        {
            if (state == null)
            {
                steering.steer = 0f;
                steering.speed = 0f;
                return;
            }
            steering.steer = Mathf.Clamp(state.Steer, -1f, 1f);
            steering.speed = state.Speed;
        }
    }
}
