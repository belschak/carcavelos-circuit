using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // The camera's post-processing look, made by code (help for Role 4, done by Luc's Claude, "AAA camera look").
    // Role4SceneBuilder calls it while it builds Main.unity, so every rebuild and every Web build gets the same look.
    // Four profiles in RaceFlow/Settings, each written fresh from the numbers below (tune here, not in the Inspector):
    //   RaceCameraVolume          (all platforms) bloom + vignette: glowing sparks, flames and sun glints, a darker
    //                             frame that pulls the eye to the road. Both run in URP's one final pass (cheap).
    //   RaceCameraVolume_Speed    (all platforms, weight = speed, set by RaceCameraLook) stronger vignette and colour
    //                             fringing at the screen edge: speed you can feel. Same final pass, still cheap.
    //   RaceCameraVolume_HighEnd  (PC and Mac only) camera motion blur, a sun lens flare from the bloom, fine grain.
    //   RaceCameraVolume_Overview (PC and Mac only, weight set by RaceCameraLook) depth of field over the map.
    // Colour grade and tone curve stay in Role 3's world look (CampusWorld_Look), so a track brings its own colours.
    public static class RaceCameraLookBuilder
    {
        public const string BaseProfilePath = "Assets/_Project/RaceFlow/Settings/RaceCameraVolume.asset";
        public const string SpeedProfilePath = "Assets/_Project/RaceFlow/Settings/RaceCameraVolume_Speed.asset";
        public const string HighEndProfilePath = "Assets/_Project/RaceFlow/Settings/RaceCameraVolume_HighEnd.asset";
        public const string OverviewProfilePath = "Assets/_Project/RaceFlow/Settings/RaceCameraVolume_Overview.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        // Bloom + vignette for every platform.
        public static VolumeProfile BuildBaseProfile()
        {
            VolumeProfile profile = FreshProfile(BaseProfilePath);
            Bloom bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);       // only really bright pixels glow (sun glints, sparks, lit glass)
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);         // a wide, soft glow, like a real lens
            bloom.tint.Override(new Color(1f, 0.94f, 0.85f));   // warm, the golden-hour sun
            bloom.maxIterations.Override(6);       // blur steps; 6 = soft and still cheap on phones
            bloom.highQualityFiltering.Override(false);
            Vignette vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.08f, 0.05f, 0.03f));   // warm dark, not grey
            return Save(profile);
        }

        // Speed rush: RaceCameraLook raises its weight from 0 (below 60 % of top speed) to 1 (top speed or boost).
        public static VolumeProfile BuildSpeedProfile()
        {
            VolumeProfile profile = FreshProfile(SpeedProfilePath);
            Vignette vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.08f, 0.05f, 0.03f));
            ChromaticAberration fringe = Add<ChromaticAberration>(profile);
            fringe.intensity.Override(0.32f);      // only at the screen edge; the middle stays sharp
            return Save(profile);
        }

        // PC and Mac: motion blur from the camera's own movement, a lens flare from the bloom, fine film grain.
        public static VolumeProfile BuildHighEndProfile()
        {
            VolumeProfile profile = FreshProfile(HighEndProfilePath);
            MotionBlur blur = Add<MotionBlur>(profile);
            blur.mode.Override(MotionBlurMode.CameraOnly);   // the camera's motion only: stronger in fast turns
            blur.quality.Override(MotionBlurQuality.Medium);
            blur.intensity.Override(0.3f);
            blur.clamp.Override(0.05f);            // never smears the whole screen
            Bloom smoothBloom = Add<Bloom>(profile);
            smoothBloom.highQualityFiltering.Override(true);  // no blocky glow edges on big screens
            ScreenSpaceLensFlare flare = Add<ScreenSpaceLensFlare>(profile);
            flare.intensity.Override(0.3f);        // ghosts and a soft halo opposite the sun, from the bloom's glow
            flare.tintColor.Override(new Color(1f, 0.9f, 0.75f));
            flare.streaksIntensity.Override(0.15f);
            FilmGrain grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);       // fine grain mostly in the shade: a "filmed", not "rendered" look
            grain.response.Override(0.85f);
            return Save(profile);
        }

        // PC and Mac: over the map the far background goes soft, the campus in front stays sharp.
        public static VolumeProfile BuildOverviewProfile()
        {
            VolumeProfile profile = FreshProfile(OverviewProfilePath);
            DepthOfField dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);   // the cheap kind; Bokeh costs too much for a background
            dof.gaussianStart.Override(320f);
            dof.gaussianEnd.Override(1100f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(false);
            return Save(profile);
        }

        // PC and Mac quality level only (the Web build has its own Web_RPAsset, phones use Mobile_RPAsset; both stay
        // as they are): shadows reach 80 m instead of 50 m, so the buildings ahead and across the plaza cast them
        // too (110 m spread the shadow map so thin that the fence rails lost theirs), and colour grading runs in HDR (the filmic tone curve keeps the bright sky's colours).
        public static void TunePcPipeline()
        {
            UniversalRenderPipelineAsset pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcPipelinePath);
            if (pc == null)
            {
                Debug.LogWarning("RaceCameraLookBuilder: " + PcPipelinePath + " not found, PC shadows unchanged.");
                return;
            }
            pc.shadowDistance = 80f;
            pc.colorGradingMode = ColorGradingMode.HighDynamicRange;
            EditorUtility.SetDirty(pc);
        }

        // A profile file with no effects in it (the old one's effects are removed, so a rebuild never doubles them).
        private static VolumeProfile FreshProfile(string path)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                return profile;
            }
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent old = profile.components[i];
                profile.components.RemoveAt(i);
                Object.DestroyImmediate(old, true);
            }
            return profile;
        }

        // Adds an effect and stores it inside the profile file. Only the values set with Override() count; all others
        // fall through to the volumes below (false = no blanket override, so e.g. the high-end bloom only switches on
        // the smoother filter and keeps the base bloom's strength).
        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T effect = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(effect, profile);
            return effect;
        }

        private static VolumeProfile Save(VolumeProfile profile)
        {
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
