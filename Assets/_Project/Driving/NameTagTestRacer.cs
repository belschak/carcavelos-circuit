using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // TEST SCENE ONLY (D9): a pretend racer, so the name tags can be seen in Driving_TestBed without
    // Role 4's race flow or Role 3's bots. Put it on any object (a cube, the Kart); it registers itself
    // in RacerRegistry while it is enabled. Never use it in Main.unity: the real racers register themselves there.
    public class NameTagTestRacer : MonoBehaviour, IRacer
    {
        [SerializeField, Tooltip("Name shown on the tag")]
        private string displayName = "Test Bot";

        [SerializeField, Tooltip("Colour of the tag")]
        private Color mapColor = new Color(0.95f, 0.55f, 0.1f);

        [SerializeField, Tooltip("Tick for the player's own kart: its tag must stay hidden")]
        private bool isPlayer;

        public string DisplayName { get { return displayName; } }
        public Transform Root { get { return transform; } }
        public IKartState Kart { get { return GetComponent<IKartState>(); } } // null on a plain cube, that is fine here
        public bool IsPlayer { get { return isPlayer; } }
        public Color MapColor { get { return mapColor; } }

        private void OnEnable()
        {
            RacerRegistry.Register(this);
        }

        private void OnDisable()
        {
            RacerRegistry.Unregister(this);
        }
    }
}
