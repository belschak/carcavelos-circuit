using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // Another player's kart on my screen (M2 online race). Looks only: no Rigidbody, no collider, no controller,
    // karts drive through each other. The kart model is a copy of my own kart's "Visual" child (Role 3's KartVisual
    // with its steering wheel, wheels and arms), painted in the player's colour with BotKartPaint.
    // It implements IKartState so the model reads speed and steer from it like from a real kart, and IRacer so the
    // positions readout, the minimap and the name tags see it like a bot. Where it is comes from the network: the
    // director adds every received KartPose, and Show() draws the pose of "a moment ago" (PoseBuffer: smooth between
    // packets, a short straight guess after the last one, then still).
    // RaceRoster treats it like a bot: ResetTo and InputEnabled are accepted and change nothing.
    public class ProxyKart : MonoBehaviour, IKartState, IRacer
    {
        public const string VisualName = "Visual";

        private readonly PoseBuffer buffer = new PoseBuffer();
        private KartPose shown;
        private bool hasShown;
        private string displayName = "PLAYER";
        private Color colour = Color.white;
        private float maxSpeed = 30f;
        private bool inputEnabled;

        public int PlayerId { get; private set; }
        public int PoseCount { get { return buffer.Count; } }
        public double NewestPoseTime { get { return buffer.NewestTime; } } // shared clock of the newest pose, -1 = none
        public bool HasPose { get { return hasShown; } } // false until the first pose could be drawn

        // ---- IKartState (read by the model, the name tag and the standings) ----
        public float Speed { get { return hasShown ? shown.Speed : 0f; } }
        public float MaxSpeed { get { return maxSpeed; } }
        public bool IsDrifting { get { return hasShown && shown.IsDrifting; } }
        public float DriftCharge01 { get { return hasShown ? shown.DriftCharge01 : 0f; } }
        public int DriftStage { get { return hasShown ? shown.DriftStage : 0; } }
        public bool IsBoosting { get { return hasShown && shown.IsBoosting; } }
        public bool IsGrounded { get { return !hasShown || shown.IsGrounded; } }
        public float Steer { get { return hasShown ? shown.Steer : 0f; } }
        public bool InputEnabled { get { return inputEnabled; } set { inputEnabled = value; } }

        // The network says where this kart is; a teleport from the grid logic is ignored.
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
        }

        // ---- IRacer ----
        public string DisplayName { get { return displayName; } }
        public Transform Root { get { return transform; } }
        public IKartState Kart { get { return this; } }
        public bool IsPlayer { get { return false; } }
        public Color MapColor { get { return colour; } }

        // Makes one proxy, inactive: the director switches it on with SetVisible (OnlineProxyRules: not before its
        // first pose, not a late joiner mid-race), and OnEnable then registers it with its name and colour set.
        // visualSource = my kart's "Visual" child to copy (null = a plain box), maxSpeed = my kart's top speed.
        public static ProxyKart Spawn(OnlinePlayer player, Transform visualSource, float maxSpeed)
        {
            GameObject root = new GameObject("Proxy " + (player != null ? player.Name : "?"));
            root.SetActive(false);
            ProxyKart proxy = root.AddComponent<ProxyKart>();
            proxy.SetUp(player, visualSource, maxSpeed);
            return proxy;
        }

        // Shown = drawn and counted as a racer (RacerRegistry); hidden = neither. Show() keeps the pose up to date
        // either way, so it appears in the right place.
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private void SetUp(OnlinePlayer player, Transform visualSource, float topSpeed)
        {
            if (player != null)
            {
                PlayerId = player.Id;
                displayName = string.IsNullOrEmpty(player.Name) ? "PLAYER" : player.Name;
                colour = OnlineColours.Of(player.ColourIndex);
                if (player.HasPose)
                {
                    buffer.Add(player.LatestPose);
                    shown = player.LatestPose;
                    hasShown = true;
                    transform.SetPositionAndRotation(shown.Position, SafeRotation(shown.Rotation));
                }
            }
            maxSpeed = float.IsNaN(topSpeed) || topSpeed <= 0f ? 30f : topSpeed;
            BuildBody(visualSource);
            BotKartPaint.Paint(gameObject, colour);
        }

        // A copy of the player's kart model under this root, or a box when there is none to copy.
        private void BuildBody(Transform visualSource)
        {
            if (visualSource != null)
            {
                GameObject visual = Instantiate(visualSource.gameObject, transform, false); // false = keep the local offset
                visual.name = VisualName;
                visual.transform.localRotation = Quaternion.identity; // not my kart's lean of this moment (KartController tilts it)
                return;
            }
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "ProxyBox";
            box.transform.SetParent(transform, false);
            box.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            box.transform.localScale = new Vector3(1.1f, 0.5f, 1.7f);
        }

        private void OnEnable()
        {
            RacerRegistry.Register(this);
        }

        private void OnDisable()
        {
            RacerRegistry.Unregister(this);
        }

        // A pose from the network (newest wins, see PoseBuffer.Add).
        public bool AddPose(KartPose pose)
        {
            return buffer.Add(pose);
        }

        // Draws the pose of RenderDelaySeconds ago on the shared clock. Called by the director every frame.
        public void Show(double now)
        {
            KartPose pose;
            if (!buffer.TrySample(now - PoseBuffer.RenderDelaySeconds, out pose))
            {
                return;
            }
            shown = pose;
            hasShown = true;
            transform.SetPositionAndRotation(pose.Position, SafeRotation(pose.Rotation));
        }

        // A zero quaternion from a broken packet would make Unity complain; show "no rotation" instead.
        private static Quaternion SafeRotation(Quaternion q)
        {
            float lengthSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return lengthSquared < 0.0001f || float.IsNaN(lengthSquared) ? Quaternion.identity : q;
        }
    }
}
