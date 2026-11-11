using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R8: the human player's kart as an IRacer, so positions, the minimap and Role 1's name tags see the player
    // exactly like a bot. RaceRoster adds it to the spawned kart at race set-up (no prefab change for Role 1).
    // It registers itself in RacerRegistry while it is enabled.
    public class PlayerRacer : MonoBehaviour, IRacer
    {
        // Orange = the kart's own accent colour and our "this is you" signal; the map dot also gets a white ring and an arrow.
        public static readonly Color PlayerColour = HudColors.Orange;

        private Transform root;        // cached: a destroyed kart then reads as null instead of throwing
        private IKartState kart;
        private string displayName = "You";

        // "You" until the leaderboard gives the player a name (task R10 can call SetDisplayName).
        public string DisplayName { get { return displayName; } }
        public Transform Root { get { return root; } }
        public IKartState Kart { get { return kart; } }
        public bool IsPlayer { get { return true; } }
        public Color MapColor { get { return PlayerColour; } }

        private void Awake()
        {
            root = transform;
            kart = GetComponent<IKartState>();
        }

        private void OnEnable()
        {
            RacerRegistry.Register(this);
        }

        private void OnDisable()
        {
            RacerRegistry.Unregister(this);
        }

        // Empty or too long names are refused (max 16 letters, so the name tag stays one short line).
        public bool SetDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 16)
            {
                return false;
            }
            displayName = name.Trim();
            return true;
        }
    }
}
