using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Feeds KartVisualSteering from the kart it sits on, through IKartState only (docs/interfaces.md).
    // Speed comes straight from IKartState.Speed. IKartState has no steering value, so the steer is worked out from
    // how fast the kart turns: at a given speed, full lock (front wheels at frontWheelMaxAngle) turns the kart at
    // speed * tan(angle) / wheelbase radians per second (bicycle model). Runs before KartVisualSteering.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(KartVisualSteering))]
    public class KartVisualFromState : MonoBehaviour
    {
        [SerializeField, Tooltip("Distance between front and rear axle in metres (Nova Classic: 0.685)")]
        private float wheelbase = 0.685f;

        [SerializeField, Tooltip("Below this speed (m/s) the steer estimate is not trusted and eases back to straight")]
        private float minSpeedForSteer = 1.5f;

        private KartVisualSteering steering;
        private IKartState state;
        private Transform body;
        private float lastYaw;

        private void Awake()
        {
            steering = GetComponent<KartVisualSteering>();
        }

        private void Start()
        {
            // The kart root that moves. Without a parent (showroom, prefab preview) this object itself.
            body = transform.parent != null ? transform.parent : transform;
            state = body.GetComponentInParent<IKartState>();
            lastYaw = body.eulerAngles.y;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;                                     // paused (Time.timeScale = 0)
            }
            float speed = state != null ? state.Speed : 0f;
            float yaw = body.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt * Mathf.Deg2Rad;   // rad/s, + = turning right
            lastYaw = yaw;

            float steer = 0f;
            if (Mathf.Abs(speed) > minSpeedForSteer)
            {
                float fullLock = Mathf.Abs(speed) * Mathf.Tan(steering.frontWheelMaxAngle * Mathf.Deg2Rad) / wheelbase;
                steer = Mathf.Clamp(yawRate / fullLock, -1f, 1f) * Mathf.Sign(speed);
            }
            steering.steer = steer;
            steering.speed = speed;
        }
    }
}
