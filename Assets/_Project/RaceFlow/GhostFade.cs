using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // How see-through the ghost is right now (0 = invisible, 1 = its normal strength). Plain C#, unit tested.
    // The ghost must never block the view: it fades out when it is close to the player's kart (at GO they stand on
    // the same spot) or close to the camera, and fades out at the end of its lap instead of freezing.
    public static class GhostFade
    {
        // hideDistance: closer than this = invisible; fullDistance: further than this = full strength;
        // in between a smooth S-curve (no visible "edge" where the fade starts).
        public static float ByDistance(float distance, float hideDistance, float fullDistance)
        {
            if (fullDistance <= hideDistance)
            {
                return distance >= hideDistance ? 1f : 0f;
            }
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hideDistance, fullDistance, distance));
        }

        // secondsPastEnd: how long ago the ghost's lap ended (negative = still driving). Fades to 0 over fadeSeconds.
        public static float ByEnd(float secondsPastEnd, float fadeSeconds)
        {
            if (secondsPastEnd <= 0f)
            {
                return 1f;
            }
            if (fadeSeconds <= 0f)
            {
                return 0f;
            }
            return 1f - Mathf.Clamp01(secondsPastEnd / fadeSeconds);
        }

        // All reasons together: the strongest fade wins.
        public static float Combined(float distanceToKart, float distanceToCamera, float secondsPastEnd,
            float hideDistance, float fullDistance, float cameraHideDistance, float cameraFullDistance, float endFadeSeconds)
        {
            float alpha = ByDistance(distanceToKart, hideDistance, fullDistance);
            alpha = Mathf.Min(alpha, ByDistance(distanceToCamera, cameraHideDistance, cameraFullDistance));
            alpha = Mathf.Min(alpha, ByEnd(secondsPastEnd, endFadeSeconds));
            return alpha;
        }
    }
}
