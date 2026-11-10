using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Finds where the effects go on the kart model: the rear wheels (Wheel_RL, Wheel_RR) and the open pipe ends
    // (Exhaust_0, Exhaust_1, ...). These names come from our Blender kart export (art handoff). A kart without them,
    // like the placeholder box kart, gets null / an empty list, and KartEffects uses its fallback positions instead.
    public static class KartEffectAnchors
    {
        public const string RearLeftWheel = "Wheel_RL";
        public const string RearRightWheel = "Wheel_RR";
        public const string ExhaustPrefix = "Exhaust_";

        // The first child (at any depth) with exactly this name, or null.
        public static Transform FindChild(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }
            return null;
        }

        // Every child whose name starts with "Exhaust_". In Blender the empty's arrow points out of the pipe;
        // after the FBX export that arrow is the transform's up axis.
        public static List<Transform> FindExhausts(Transform root)
        {
            List<Transform> pipes = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith(ExhaustPrefix))
                {
                    pipes.Add(all[i]);
                }
            }
            return pipes;
        }

        // Where a rear wheel touches the road, just behind the tyre, in kart space.
        // wheelCentre is the wheel's centre in kart space; the kart pivot is at ground level (docs/interfaces.md),
        // so the centre's height is the tyre radius. Kart forward = +Z, so "behind" = smaller z.
        public static Vector3 ContactBehindWheel(Vector3 wheelCentre)
        {
            float radius = Mathf.Max(0.05f, wheelCentre.y);
            return new Vector3(wheelCentre.x, 0.015f, wheelCentre.z - radius * 0.95f);
        }
    }
}
