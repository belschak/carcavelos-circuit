// LOCAL ONLY (untracked). Compatibility adapter for the bots prototype after the merge of current main (V14 + hall
// integration, 2026-11-11). Main deleted RaceFlow's PlaceholderChaseCamera (Q13: Role 1's Cinemachine camera through
// ChaseCameraRig took over); Assets/_Local/Bots still names the type, and its scene builder refuses to build without
// one in Main.unity. Assets/_Local/Bots is not touched: this class has the two calls the prototype makes and passes
// them on to the ChaseCameraRig on the same Main Camera; Editor/BotRaceCompat.cs puts it there, in memory only, when
// the bot scene builder opens Main.unity. Delete this folder once the bots prototype is ported to ChaseCameraRig.
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    [RequireComponent(typeof(Camera))]
    public class PlaceholderChaseCamera : MonoBehaviour
    {
        // The old camera's settings; BotRace overwrites some of them by name (JsonUtility). Not used here.
        [SerializeField] private float distance = 6f;
        [SerializeField] private float height = 2.5f;
        [SerializeField] private float minFieldOfView = 65f;
        [SerializeField] private float maxFieldOfView = 80f;

        public void SetTarget(Transform kartTransform)
        {
            ChaseCameraRig rig = GetComponent<ChaseCameraRig>();
            if (rig != null) rig.SetTarget(kartTransform);
        }

        public void SnapBehindTarget()
        {
            ChaseCameraRig rig = GetComponent<ChaseCameraRig>();
            if (rig != null) rig.SnapBehindTarget();
        }

        private void Reset()
        {
            distance = 6f; height = 2.5f; minFieldOfView = 65f; maxFieldOfView = 80f;
        }
    }
}
