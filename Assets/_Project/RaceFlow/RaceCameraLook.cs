using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarcavelosCircuit.RaceFlow
{
    // The camera's "film" look on top of the base RaceCameraVolume (help for Role 4, done by Luc's Claude). Sits on
    // the PostProcessing object in Main.unity, made by Role4SceneBuilder. Three extra global volumes, all profiles
    // made by code (RaceCameraLookBuilder):
    //   - speed:     stronger vignette, a little colour fringing and lens bend at top speed or in a boost
    //                (cheap: the same full-screen pass as the bloom, so it also runs on phones and in the Web build);
    //   - high end:  camera motion blur, sun lens flare, fine film grain (PC and Mac only);
    //   - overview:  a soft background (depth of field) while the camera floats over the map (PC and Mac only).
    // The weights follow the kart smoothly; nothing changes the kart, the race or the HUD.
    public class RaceCameraLook : MonoBehaviour
    {
        [SerializeField, Tooltip("Gives the player's kart (speed, top speed, boost)")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Tells when the camera floats over the map (start screen, after the finish)")]
        private MapFlyover flyover;

        [SerializeField, Tooltip("The race camera: gets SMAA edge smoothing on PC and Mac")]
        private Camera raceCamera;

        [SerializeField, Tooltip("Global volume with the speed profile; its weight follows the kart's speed")]
        private Volume speedVolume;

        [SerializeField, Tooltip("Global volume with the high-end profile (motion blur, flare, grain); off on phones and Web")]
        private Volume highEndVolume;

        [SerializeField, Tooltip("Global volume with the depth of field for the map overview; off on phones and Web")]
        private Volume overviewVolume;

        [SerializeField, Tooltip("How fast the speed effects follow the kart, per second")]
        private float speedResponse = 3f;

        [SerializeField, Tooltip("How fast the soft background fades in and out, per second")]
        private float overviewResponse = 2f;

        private bool highEnd;
        private float speedWeight;
        private float overviewWeight;

        private void Start()
        {
            string level = QualitySettings.names[QualitySettings.GetQualityLevel()];
            highEnd = CameraLookRules.IsHighEnd(level, Application.isMobilePlatform);
            SetEnabled(highEndVolume, highEnd);
            SetEnabled(overviewVolume, highEnd);
            if (raceCamera != null && highEnd)
            {
                // SMAA smooths every edge after the image is drawn (MSAA misses leaf cut-outs and shader edges).
                UniversalAdditionalCameraData data = raceCamera.GetUniversalAdditionalCameraData();
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            SetWeight(speedVolume, 0f);
            SetWeight(overviewVolume, 0f);
        }

        private void Update()
        {
            // Unscaled time: the effects still fade while the game is paused or in the finish slow motion.
            float dt = Time.unscaledDeltaTime;
            IKartState kart = raceManager != null ? raceManager.Kart : null;
            bool flying = flyover != null && flyover.IsOverview;
            float wanted = 0f;
            if (kart != null && !flying && Time.timeScale > 0f)
            {
                wanted = CameraLookRules.SpeedWeight(kart.Speed, kart.MaxSpeed, kart.IsBoosting);
            }
            speedWeight = CameraLookRules.Approach(speedWeight, wanted, speedResponse, dt);
            SetWeight(speedVolume, speedWeight);

            if (highEnd)
            {
                overviewWeight = CameraLookRules.Approach(overviewWeight, flying ? 1f : 0f, overviewResponse, dt);
                SetWeight(overviewVolume, overviewWeight);
            }
        }

        private static void SetWeight(Volume volume, float weight)
        {
            if (volume != null)
            {
                volume.weight = weight;
            }
        }

        private static void SetEnabled(Volume volume, bool on)
        {
            if (volume != null)
            {
                volume.enabled = on;
            }
        }
    }
}
