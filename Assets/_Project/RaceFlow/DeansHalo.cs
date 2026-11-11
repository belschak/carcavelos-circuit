using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The Dean's List shield you can see (task R7): a ring of gold coins circling the kart, like the coin stack
    // under the graduation cap in Luc's design. ItemSystem switches it on and off; it follows the kart's position
    // every frame without being its child (RaceFlow never changes the kart prefab or its hierarchy).
    // In the last two seconds it blinks ("hurry up"); when the shield ends the coins fly off in every direction.
    public class DeansHalo : MonoBehaviour
    {
        private const int Coins = 14;
        private const float Radius = 1.5f;
        private const float BurstSeconds = 0.55f;

        private Transform follow;
        private Transform[] coins = new Transform[0];
        private Vector3[] coinVelocity = new Vector3[0];
        private bool on;
        private float spin;
        private float burstTime = -1f;
        private float secondsLeft;

        // Called once by ItemSystem right after AddComponent.
        public void SetUp(Transform kart, Material gold)
        {
            follow = kart;
            coins = new Transform[Coins];
            coinVelocity = new Vector3[Coins];
            for (int i = 0; i < Coins; i++)
            {
                GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.name = "Coin " + i;
                Destroy(coin.GetComponent<Collider>());
                coin.layer = 2;
                coin.transform.SetParent(transform, false);
                coin.transform.localScale = new Vector3(0.32f, 0.04f, 0.32f); // flat gold coins
                MeshRenderer renderer = coin.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = gold;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                coin.SetActive(false);
                coins[i] = coin.transform;
            }
        }

        // The shield is on (Dean's List used), with this many seconds to go.
        public void Show(float seconds)
        {
            on = true;
            burstTime = -1f;
            secondsLeft = seconds;
            SetCoinsActive(true);
        }

        // The shield is used up or ran out: the coins fly off.
        public void Burst()
        {
            if (!on)
            {
                return;
            }
            on = false;
            burstTime = 0f;
            for (int i = 0; i < coins.Length; i++)
            {
                Vector3 outwards = coins[i].position - transform.position;
                outwards.y = 0f;
                coinVelocity[i] = outwards.normalized * Random.Range(5f, 9f) + Vector3.up * Random.Range(3f, 7f);
            }
        }

        // Retry: gone at once, no burst.
        public void Hide()
        {
            on = false;
            burstTime = -1f;
            SetCoinsActive(false);
        }

        private void SetCoinsActive(bool active)
        {
            for (int i = 0; i < coins.Length; i++)
            {
                coins[i].gameObject.SetActive(active);
            }
        }

        // LateUpdate: after the kart has moved this frame, so the coins never trail one frame behind.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (burstTime >= 0f)
            {
                burstTime += dt;
                float left = Mathf.Clamp01(1f - burstTime / BurstSeconds);
                for (int i = 0; i < coins.Length; i++)
                {
                    coinVelocity[i] += Vector3.down * 18f * dt;
                    coins[i].position += coinVelocity[i] * dt;
                    coins[i].Rotate(720f * dt, 0f, 360f * dt, Space.Self);
                    coins[i].localScale = new Vector3(0.32f, 0.04f, 0.32f) * left;
                }
                if (burstTime >= BurstSeconds)
                {
                    burstTime = -1f;
                    SetCoinsActive(false);
                }
                return;
            }
            if (!on || follow == null)
            {
                return;
            }

            secondsLeft -= dt;
            transform.position = follow.position + Vector3.up * 0.6f;
            spin += 200f * dt;
            bool blinkOff = secondsLeft < 2f && Mathf.Repeat(secondsLeft * 6f, 1f) < 0.35f;
            for (int i = 0; i < coins.Length; i++)
            {
                float angle = (spin + i * 360f / coins.Length) * Mathf.Deg2Rad;
                float wobble = Mathf.Sin(spin * Mathf.Deg2Rad * 2f + i) * 0.12f; // a wavy laurel, not a flat disc
                coins[i].localPosition = new Vector3(Mathf.Cos(angle) * Radius, wobble, Mathf.Sin(angle) * Radius);
                coins[i].localRotation = Quaternion.Euler(90f, -spin - i * 360f / coins.Length, 0f); // face outwards
                coins[i].localScale = blinkOff ? Vector3.zero : new Vector3(0.32f, 0.04f, 0.32f);
            }
        }
    }
}
