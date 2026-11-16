using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals.Editor
{
    // V27: fewer shadow casters and no far-away small props (Role 3), so the game runs smoothly while you drive.
    // From the performance audit of 13 Nov: 1,547 props, all casting shadows within 50 m and all drawn up to the
    // 2,000 m far plane (about 2,600 material draws, on one thread in the browser).
    // Applied to every tree/prop prefab in CampusWorldBuilder.BuildPrefabs, so every placed instance follows.
    // - Shadows: off for small things (lower than 1.3 m), grass, shrubs, railing pieces and the far-away "_Far"
    //   props. Trees, cars, lamps, parasols, kiosk, signs and boards keep theirs.
    // - Draw distance: one LODGroup per prefab hides the prop once it is smaller than CullScreenHeight of the
    //   screen: with the 60 degree race camera a 1 m chair goes at ~60 m, a 4.5 m car at ~270 m, a 10 m tree at ~600 m.
    //   The quality level's LOD bias scales this (Web 1, PC 2, so on Mac/Windows desktop builds twice as far).
    public static class PropRenderBudget
    {
        public const float ShadowMinHeight = 1.3f;            // metres; anything lower drops its shadow
        public const float CullScreenHeight = 0.0144f;        // fraction of the screen height (1 m at 60 m, 60 degree FOV)
        private const float RaceFov = 60f;

        private static readonly string[] NoShadowKinds = { "Veg_Grass", "Veg_Shrub", "Prop_RailingSegment" };

        private static int shadowsOff, shadowsKept, culled;
        private static readonly List<string> lines = new List<string>();

        public static bool CastsShadow(string kind, float height)
        {
            if (kind.EndsWith("_Far", StringComparison.Ordinal))
            {
                return false;
            }
            foreach (string k in NoShadowKinds)
            {
                if (kind.StartsWith(k, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return height >= ShadowMinHeight;
        }

        // Distance (m) at which an object of this size is hidden, at LOD bias 1.
        public static float CullDistance(float size)
        {
            return size / (2f * Mathf.Tan(RaceFov * 0.5f * Mathf.Deg2Rad) * CullScreenHeight);
        }

        public static void Reset()
        {
            shadowsOff = shadowsKept = culled = 0;
            lines.Clear();
        }

        // root = the prefab root (at the origin), r = its one mesh renderer.
        public static void Apply(GameObject root, MeshRenderer r, string kind)
        {
            if (r == null)
            {
                return;
            }
            Bounds b = r.bounds;
            bool shadow = CastsShadow(kind, b.size.y);
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (shadow)
            {
                shadowsKept++;
            }
            else
            {
                shadowsOff++;
            }

            LODGroup lod = root.GetComponent<LODGroup>();
            if (lod == null)
            {
                lod = root.AddComponent<LODGroup>();
            }
            lod.fadeMode = LODFadeMode.None;
            lod.SetLODs(new[] { new LOD(CullScreenHeight, new Renderer[] { r }) });
            lod.RecalculateBounds();
            culled++;
            lines.Add(kind + " h " + b.size.y.ToString("0.0") + " m, shadow " + (shadow ? "on" : "off") +
                      ", hidden beyond ~" + Mathf.RoundToInt(CullDistance(lod.size)) + " m");
        }

        public static string Summary()
        {
            return "PropRenderBudget: " + culled + " prefabs with a draw distance, shadows off on " + shadowsOff + ", kept on " +
                   shadowsKept + "\n  " + string.Join("\n  ", lines);
        }
    }
}
