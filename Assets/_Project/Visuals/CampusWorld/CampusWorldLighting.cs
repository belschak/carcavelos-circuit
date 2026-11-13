using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Visuals
{
    // Sits on the root of CampusWorld.prefab (Role 3, task V8). Light settings (ambient light, sky, reflections, fog)
    // belong to a scene, not to a prefab, so a scene that only holds the prefab (e.g. Role 2's Track_Campus) would get
    // Unity's defaults. This component copies the campus look (golden hour: cool sky fill, warm bounce from the
    // paving, the painted sky as skybox and reflection source, a light warm sea haze) into the scene that holds the prefab, but only
    // while that scene is the active one: in the race RaceBootstrap makes the track scene active, in the Editor it is
    // the scene you opened. It never changes the settings of another scene (e.g. Main.unity) while that one is active.
    // The values are set by CampusWorldBuilder; the defaults here are the same.
    // V28: time of day. The fields below are the Noon look (and the fallback). With looks set, the mood the player
    // picked (TimeOfDayChoice, start screen) chooses one TimeOfDayLook: sun direction, colour and strength, sky light,
    // sky, reflections, haze, colour grade. A change in the menu switches everything at once, no reload, no bake.
    [ExecuteAlways]
    public class CampusWorldLighting : MonoBehaviour
    {
        [SerializeField, Tooltip("The directional light of the world (child 'Sun')")]
        private Light sun;

        [SerializeField] private Color skyColour = new Color(0.70f, 0.76f, 0.87f);
        [SerializeField] private Color horizonColour = new Color(0.66f, 0.66f, 0.66f);
        [SerializeField] private Color groundColour = new Color(0.62f, 0.55f, 0.46f);

        [SerializeField, Tooltip("Sky behind the world; empty = keep the scene's sky")]
        private Material skybox;

        [SerializeField, Tooltip("Cubemap that glass and glossy surfaces reflect; empty = keep the scene's reflections")]
        private Cubemap reflection;

        [SerializeField, Tooltip("V17: colour of the sea haze far away (the painted sky's colour at the horizon)")]
        private Color hazeColour = new Color(0.86f, 0.81f, 0.73f);

        [SerializeField, Tooltip("V17: how thick the haze is (exponential fog density per metre); 0 = no haze")]
        private float hazeDensity = 0.0008f;

        [SerializeField, Tooltip("V28: the global look volume (child 'Look (global volume)'); its profile follows the mood")]
        private Volume lookVolume;

        [SerializeField, Tooltip("V28: one look per mood (Settings/Look_*.asset); empty = always the fields above")]
        private TimeOfDayLook[] looks = new TimeOfDayLook[0];

        [SerializeField, Tooltip("V28: the mood shown in the Editor outside Play Mode (the game uses the player's choice)")]
        private TimeOfDay editorMood = TimeOfDay.Noon;

        // Global shader value of Ground.shader and WorldLit.shader: how much of the baked noon shadow on far ground to
        // drop (0 = keep all). Unset globals read 0, so scenes without this component keep their baked shadow.
        public const string BakedShadowDropProperty = "_CampusBakedShadowDrop";
        private static readonly int BakedShadowDropId = Shader.PropertyToID(BakedShadowDropProperty);

        // The mood this world shows now.
        public TimeOfDay Mood
        {
            get { return Application.isPlaying ? TimeOfDayChoice.Current : editorMood; }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;   // the race switches the active scene after loading
            TimeOfDayChoice.Changed += OnMoodChanged;                   // the start screen's mood button
            ApplyIfActiveScene();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            TimeOfDayChoice.Changed -= OnMoodChanged;
        }

        private void OnMoodChanged(TimeOfDay mood)
        {
            ApplyIfActiveScene();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && isActiveAndEnabled)
            {
                ApplyIfActiveScene();                                   // pick a mood in the Inspector, see it at once
            }
        }
#endif

        // Editor tools (builder self-test, screenshots): show a mood outside Play Mode. Not saved with the scene unless
        // the scene is saved afterwards; the builder always saves at noon.
        public void ShowInEditor(TimeOfDay mood)
        {
            editorMood = mood;
            ApplyIfActiveScene();
        }

        // The look for a mood, or null when this world has none for it (then the fields above are used).
        public TimeOfDayLook LookFor(TimeOfDay mood)
        {
            if (looks == null)
            {
                return null;
            }
            foreach (TimeOfDayLook look in looks)
            {
                if (look != null && look.mood == mood)
                {
                    return look;
                }
            }
            return null;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ApplyIfActiveScene();
        }

        // True when the settings were applied (this object's scene is the active scene). The sun and the look volume
        // belong to this prefab and follow the mood in any case; sky light, sky, reflections and haze are settings of
        // the active scene, so they are only written while this world's scene is the active one.
        public bool ApplyIfActiveScene()
        {
            TimeOfDayLook look = LookFor(Mood);
            if (look != null)
            {
                if (sun != null)
                {
                    sun.transform.rotation = look.SunRotation;
                    sun.color = look.sunColour;
                    sun.intensity = look.sunIntensity;
                    sun.shadowStrength = look.shadowStrength;
                }
                if (lookVolume != null && look.profile != null)
                {
                    lookVolume.sharedProfile = look.profile;
                }
                Shader.SetGlobalFloat(BakedShadowDropId, 1f - Mathf.Clamp01(look.bakedShadowKeep));
            }
            else
            {
                Shader.SetGlobalFloat(BakedShadowDropId, 0f);
            }
            if (gameObject.scene != SceneManager.GetActiveScene())
            {
                return false;
            }
            if (sun != null)
            {
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;          // no light baking needed
            RenderSettings.ambientSkyColor = look != null ? look.skyColour : skyColour;
            RenderSettings.ambientEquatorColor = look != null ? look.horizonColour : horizonColour;
            RenderSettings.ambientGroundColor = look != null ? look.groundColour : groundColour;
            Material sky = look != null && look.skybox != null ? look.skybox : skybox;
            if (sky != null)
            {
                RenderSettings.skybox = sky;
            }
            Cubemap reflect = look != null && look.reflection != null ? look.reflection : reflection;
            if (reflect != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflect;
                RenderSettings.reflectionIntensity = 1f;
            }
            // V17: distant buildings, the fort and the coast fade into the warm sea air: the depth cue that makes
            // the world look big. Exponential = no hard start line; all our shaders (URP Lit, sea, ground, foliage) fog.
            float density = look != null ? look.hazeDensity : hazeDensity;
            RenderSettings.fog = density > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = look != null ? look.hazeColour : hazeColour;
            RenderSettings.fogDensity = density;
            return true;
        }
    }
}
