using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // Another player's kart on my screen (M2 online race). Looks only: no Rigidbody, no collider, no controller,
    // karts drive through each other. The body is a copy of my own kart's "Visual" slot with THAT player's driver in it
    // (feedback round 17 Nov 2026: before, every remote kart was a recoloured copy of my own driver). The driver comes
    // with the player (OnlinePlayer.DriverIndex), the kart model from Role 3's list (BotSettings.KartVisual), swapped in
    // with KartVisualSwap like BotSquad does for the bots, and keeps its own paint so Cláudio looks like Cláudio on every
    // screen. The player's room colour stays on the name tag and the minimap dot (MapColor), which tells two players
    // with the same driver apart. Only without a kart model (no BotSettings list) the body is painted in that colour.
    // It implements IKartState so the model reads speed and steer from it like from a real kart, and IRacer so the
    // positions readout, the minimap and the name tags see it like a bot. Where it is comes from the network: the
    // director adds every received KartPose, and Show() draws the pose of "a moment ago" (PoseBuffer: smooth between
    // packets, a short straight guess after the last one, then still).
    // RaceRoster treats it like a bot: ResetTo and InputEnabled are accepted and change nothing.
    public class ProxyKart : MonoBehaviour, IKartState, IRacer
    {
        public const string VisualName = "Visual";

        private readonly PoseBuffer buffer = new PoseBuffer();
        private readonly AdaptiveRenderDelay delay = new AdaptiveRenderDelay(); // how far in the past this kart is drawn
        private KartPose shown;
        private bool hasShown;
        private string displayName = "PLAYER";
        private Color colour = Color.white;
        private float maxSpeed = 30f;
        private bool inputEnabled;

        public int PlayerId { get; private set; }
        public int DriverIndex { get; private set; }
        public string ModelName { get; private set; } = ""; // the KartVisual_* model in the body, "" = a copy or a box
        public int PoseCount { get { return buffer.Count; } }
        public double NewestPoseTime { get { return buffer.NewestTime; } } // shared clock of the newest pose, -1 = none
        public double RenderDelay { get { return delay.Seconds; } }
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
        // visualSource = my kart's "Visual" slot to copy (null = an empty slot), driverModel = the player's driver kart
        // (BotSettings.KartVisual of DriverRoster.Get(player.DriverIndex).KartVisual; null = keep the copy and paint
        // it, or a plain box without either), maxSpeed = my kart's top speed.
        public static ProxyKart Spawn(OnlinePlayer player, Transform visualSource, GameObject driverModel, float maxSpeed)
        {
            GameObject root = new GameObject("Proxy " + (player != null ? player.Name : "?"));
            root.SetActive(false);
            ProxyKart proxy = root.AddComponent<ProxyKart>();
            proxy.SetUp(player, visualSource, driverModel, maxSpeed);
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

        private void SetUp(OnlinePlayer player, Transform visualSource, GameObject driverModel, float topSpeed)
        {
            if (player != null)
            {
                PlayerId = player.Id;
                DriverIndex = player.DriverIndex;
                displayName = string.IsNullOrEmpty(player.DisplayName) ? "PLAYER" : player.DisplayName; // "ANA 2" (OnlineNames)
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
            if (!BuildBody(visualSource, driverModel))
            {
                BotKartPaint.Paint(gameObject, colour); // no driver model to show: at least the room colour tells karts apart
            }
        }

        // The body under this root: my kart's Visual slot (its offset, wheels and arms) with the player's driver model
        // in it. True when the driver's own model is in (keeps its paint); false for a painted copy of mine or a box.
        private bool BuildBody(Transform visualSource, GameObject driverModel)
        {
            GameObject visual = null;
            if (visualSource != null)
            {
                visual = Instantiate(visualSource.gameObject, transform, false); // false = keep the local offset
                visual.name = VisualName;
                visual.transform.localRotation = Quaternion.identity; // not my kart's lean of this moment (KartController tilts it)
            }
            else if (driverModel != null)
            {
                visual = new GameObject(VisualName);
                visual.transform.SetParent(transform, false);
            }
            if (visual != null && driverModel != null)
            {
                // My driver out, theirs in, the same way KartVisualSwap does it, but at once: this root is still
                // inactive, and a model taken out with SetParent(null) would wake up for a frame on its own (without a
                // kart around it). DestroyImmediate on an inactive copy wakes nothing.
                Transform slot = visual.transform;
                Transform old = KartVisualSwap.CurrentModel(slot);
                if (old != null && old.name != driverModel.name)
                {
                    DestroyImmediate(old.gameObject);
                    old = null;
                }
                GameObject model = old != null ? old.gameObject : Instantiate(driverModel, slot, false);
                model.name = driverModel.name;
                ModelName = model.name;
                return true;
            }
            if (visual != null)
            {
                return false;
            }
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "ProxyBox";
            box.transform.SetParent(transform, false);
            box.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            box.transform.localScale = new Vector3(1.1f, 0.5f, 1.7f);
            return false;
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

        // Draws the pose of a moment ago on the shared clock: at least RenderDelaySeconds, more when this kart's poses
        // arrive late or unevenly (AdaptiveRenderDelay). Called by the director every frame.
        public void Show(double now)
        {
            if (buffer.Count > 0)
            {
                delay.Add(now, now - buffer.NewestTime);
            }
            KartPose pose;
            if (!buffer.TrySample(now - delay.Seconds, out pose))
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
