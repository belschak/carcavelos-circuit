using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Visuals
{
    // V28 (Role 3): one light mood of the campus world. CampusWorldBuilder writes one asset per mood
    // (Settings/Look_Noon.asset, Look_Sunset.asset); CampusWorldLighting switches between them while the game runs.
    // Everything here is switched at runtime: no lightmaps, so a mood costs one sky texture and one volume profile.
    [CreateAssetMenu(menuName = "Carcavelos Circuit/Time Of Day Look", fileName = "Look_")]
    public class TimeOfDayLook : ScriptableObject
    {
        public TimeOfDay mood;

        [Header("Sun (directional light)")]
        [Tooltip("Compass direction the sun stands in, degrees clockwise from north (+z)")]
        public float sunAzimuth = 225f;
        [Tooltip("Height of the sun above the horizon, degrees")]
        public float sunElevation = 42f;
        public Color sunColour = new Color(1f, 0.95f, 0.86f);
        public float sunIntensity = 2.1f;
        [Range(0f, 1f)] public float shadowStrength = 0.85f;

        [Header("Sky light (trilight ambient)")]
        public Color skyColour = new Color(0.60f, 0.66f, 0.76f);
        public Color horizonColour = new Color(0.64f, 0.65f, 0.66f);
        public Color groundColour = new Color(0.55f, 0.45f, 0.33f);

        [Header("Sky, reflections and haze")]
        [Tooltip("Skybox material; empty = keep the scene's sky")]
        public Material skybox;
        [Tooltip("Cubemap that glass and gloss reflect; empty = keep the scene's reflections")]
        public Cubemap reflection;
        public Color hazeColour = new Color(0.80f, 0.86f, 0.92f);
        [Tooltip("Exponential fog density per metre; 0 = no haze")]
        public float hazeDensity = 0.0008f;

        [Header("Colour grade and ground")]
        [Tooltip("Tonemapping + grade for the look volume")]
        public VolumeProfile profile;
        [Tooltip("How much of the baked noon sun shadow on far ground stays (1 = all; the low sunset sun casts elsewhere)")]
        [Range(0f, 1f)] public float bakedShadowKeep = 1f;

        // The way the light shines (from the sun towards the ground), for the directional light's rotation.
        public Quaternion SunRotation
        {
            get { return Quaternion.LookRotation(-TowardsSun(sunAzimuth, sunElevation)); }
        }

        // Unit vector from the ground towards the sun (x = east, y = up, z = north). Same maths as make_sky.py,
        // so the painted sun disc and the light come from the same place.
        public static Vector3 TowardsSun(float azimuth, float elevation)
        {
            float az = azimuth * Mathf.Deg2Rad, el = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }
    }
}
