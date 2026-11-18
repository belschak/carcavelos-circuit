using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // One item box on the track (task R7): a spinning, bobbing azulejo tile cube. Drive through it and it bursts
    // into tile shards, then grows back a few seconds later. ItemSystem creates every box in code when the race is
    // set up (no prefab, no scene edits) and decides what the hit gives. The rules live in ItemBoxState.
    // The shards are our own tiny "physics": a start velocity, gravity and a shrink, no Rigidbodies.
    public class ItemBox : MonoBehaviour
    {
        private const int ShardCount = 10;
        private const float ShardSeconds = 0.8f;    // how long the shards fly
        private const float PopInSeconds = 0.45f;   // grow-back animation
        private const float ParkSeconds = 0.35f;    // shrink away after the finish line (Park)
        private const float SpinDegreesPerSecond = 95f;
        private const float BobMetres = 0.18f;

        private readonly ItemBoxState state = new ItemBoxState();
        private ItemSystem system;
        private SphereCollider trigger;
        private Transform cube;
        private Transform[] shards = new Transform[0];
        private Vector3[] shardVelocity = new Vector3[0];
        private Vector3[] shardSpin = new Vector3[0];
        private float[] shardStartScale = new float[0];
        private float size;
        private float bobPhase;      // different per box, so a row does not bob in lockstep
        private float shardTime = -1f;   // seconds since the burst, -1 = no shards flying
        private float popInTime = -1f;   // seconds since it came back, -1 = not growing
        private float parkTime = -1f;    // seconds since Park, -1 = not shrinking
        private bool parked;             // put away after the finish: hidden and not hittable until ResetBox

        public bool IsAvailable { get { return state.IsAvailable && !parked; } }

        // Put away after the finish line (Play Mode test).
        public bool IsParked { get { return parked; } }
        public bool GivesWave { get; private set; } // a wave box (WaveBoxSpot): always the Carcavelos Wave

        // Called once by ItemSystem right after AddComponent.
        // look / ground (task V34, both optional): Role 3's box body (unit size, hangs in the spinning cube instead of
        // the plain cube) and the light on the road (groundDrop metres below the box, does not spin or bob).
        public void SetUp(ItemSystem owner, Material material, float boxSize, float phase, bool givesWave = false,
            GameObject look = null, GameObject ground = null, float groundDrop = 0f)
        {
            system = owner;
            GivesWave = givesWave;
            size = boxSize;
            bobPhase = phase;
            gameObject.layer = 2; // "Ignore Raycast": the kart's ground ray and the camera never see the boxes

            // The trigger sits on the root, a bit bigger than the cube, so clipping a corner counts too.
            trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = boxSize * 0.95f;

            cube = MakeCube("Cube", transform, material, boxSize);
            cube.localRotation = Quaternion.Euler(25f, 0f, 35f); // tilted, so it shows three faces while it spins
            if (look != null)
            {
                MeshRenderer lookRenderer = AddLook(look, cube).GetComponentInChildren<MeshRenderer>();
                cube.GetComponent<MeshRenderer>().enabled = false; // the look draws the box now
                if (lookRenderer != null)
                {
                    material = lookRenderer.sharedMaterial; // the shards show the same glaze
                }
            }
            if (ground != null)
            {
                AddLook(ground, transform).transform.localPosition = Vector3.down * groundDrop;
            }

            shards = new Transform[ShardCount];
            shardVelocity = new Vector3[ShardCount];
            shardSpin = new Vector3[ShardCount];
            shardStartScale = new float[ShardCount];
            for (int i = 0; i < ShardCount; i++)
            {
                shards[i] = MakeCube("Shard", transform, material, boxSize * 0.3f);
                shards[i].gameObject.SetActive(false);
            }
        }

        // A copy of a look prefab under parent, on the Ignore Raycast layer like the rest of the box, without colliders.
        public static GameObject AddLook(GameObject prefab, Transform parent)
        {
            GameObject copy = Instantiate(prefab, parent, false);
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 2;
            }
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
            {
                Destroy(c);
            }
            return copy;
        }

        // A primitive cube without its collider (the root's trigger is the only collider of a box).
        private static Transform MakeCube(string name, Transform parent, Material material, float edge)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * edge;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // cheaper, and floating boxes read better without
            return go.transform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (system != null && IsAvailable && system.IsKart(other))
            {
                system.OnBoxHit(this);
            }
        }

        // ItemSystem decided the hit counts: burst now.
        public void Break(float respawnSeconds)
        {
            if (!state.TryBreak(respawnSeconds))
            {
                return;
            }
            cube.gameObject.SetActive(false);
            popInTime = -1f;
            shardTime = 0f;
            Vector3 kartForward = system.KartForward();
            for (int i = 0; i < shards.Length; i++)
            {
                // Out in every direction, up, and carried along with the kart so the burst flies past the camera.
                Vector3 outwards = Random.onUnitSphere;
                outwards.y = Mathf.Abs(outwards.y) * 0.8f + 0.4f;
                shardVelocity[i] = outwards * Random.Range(4f, 8f) + kartForward * Random.Range(6f, 12f);
                shardSpin[i] = Random.insideUnitSphere * 720f;
                shards[i].localPosition = Random.insideUnitSphere * size * 0.3f;
                shards[i].localRotation = Random.rotation;
                shardStartScale[i] = size * Random.Range(0.22f, 0.38f);
                shards[i].localScale = Vector3.one * shardStartScale[i];
                shards[i].gameObject.SetActive(true);
            }
        }

        // After the finish line (QA 17.11.2026): the first row stands just past the line, right where the finish
        // cinematic swings the camera in front of the kart, so a box filled half the screen. The race is over for
        // this player, so every box shrinks away (box, light on the road and shards) and stays away until ResetBox.
        public void Park()
        {
            if (parked)
            {
                return;
            }
            parked = true;
            parkTime = 0f;
            if (trigger != null)
            {
                trigger.enabled = false; // no hits, and no zero-sized collider once it has shrunk away
            }
        }

        // Retry: every box is back at once, without the grow animation.
        public void ResetBox()
        {
            parked = false;
            parkTime = -1f;
            transform.localScale = Vector3.one;
            if (trigger != null)
            {
                trigger.enabled = true;
            }
            state.Reset();
            cube.gameObject.SetActive(true);
            cube.localScale = Vector3.one * size;
            popInTime = -1f;
            HideShards();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (parked)
            {
                UpdatePark(Time.unscaledDeltaTime); // the finish slow motion must not slow the shrink down
                return;
            }
            if (state.Tick(dt))
            {
                cube.gameObject.SetActive(true);
                popInTime = 0f;
            }

            // Spin and bob (the cube is a child, the trigger on the root stays where it is).
            float t = Time.time + bobPhase;
            cube.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * BobMetres, 0f);
            cube.Rotate(0f, SpinDegreesPerSecond * dt, 0f, Space.World);

            UpdatePopIn(dt);
            UpdateShards(dt);
        }

        // Shrink the whole box (root: cube, light on the road, shards) to nothing, then leave it at zero.
        private void UpdatePark(float dt)
        {
            if (parkTime < 0f)
            {
                return;
            }
            parkTime += dt;
            float p = Mathf.Clamp01(parkTime / ParkSeconds);
            transform.localScale = Vector3.one * (1f - p * p);
            if (p >= 1f)
            {
                parkTime = -1f;
                transform.localScale = Vector3.zero;
            }
        }

        // Grow back with a small overshoot (0 -> 115 % -> 100 %), so the box "pops" into place.
        private void UpdatePopIn(float dt)
        {
            if (popInTime < 0f)
            {
                return;
            }
            popInTime += dt;
            float p = Mathf.Clamp01(popInTime / PopInSeconds);
            float scale = p < 0.7f ? Mathf.Lerp(0f, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            cube.localScale = Vector3.one * size * scale;
            if (p >= 1f)
            {
                popInTime = -1f;
            }
        }

        private void UpdateShards(float dt)
        {
            if (shardTime < 0f)
            {
                return;
            }
            shardTime += dt;
            float left = Mathf.Clamp01(1f - shardTime / ShardSeconds); // 1 at the burst, 0 when the shards are gone
            for (int i = 0; i < shards.Length; i++)
            {
                shardVelocity[i] += Vector3.down * 18f * dt; // a bit more than real gravity: snappier
                shards[i].position += shardVelocity[i] * dt;
                shards[i].Rotate(shardSpin[i] * dt, Space.Self);
                shards[i].localScale = Vector3.one * shardStartScale[i] * left;
            }
            if (shardTime >= ShardSeconds)
            {
                HideShards();
            }
        }

        private void HideShards()
        {
            shardTime = -1f;
            for (int i = 0; i < shards.Length; i++)
            {
                shards[i].gameObject.SetActive(false);
            }
        }
    }
}
