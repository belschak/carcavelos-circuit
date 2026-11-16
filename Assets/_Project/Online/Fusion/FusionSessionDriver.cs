using UnityEngine;

namespace CarcavelosCircuit.Online.Fusion
{
    // Gives FusionSession (a plain C# class) one call per frame on the main thread. Fusion delivers its state
    // during its own update; the session then reads what changed and raises Changed / PoseReceived from here.
    // Runs just before OnlineRaceDirector (-55), so the director sees this frame's state. Created by the session
    // on Join, destroyed on Leave.
    [DefaultExecutionOrder(-56)]
    public class FusionSessionDriver : MonoBehaviour
    {
        public FusionSession Session;

        private void Update()
        {
            if (Session != null)
            {
                Session.Tick();
            }
        }
    }
}
