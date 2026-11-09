using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CarcavelosCircuit.Visuals
{
    // Sits on the root of CampusWorld.prefab (Role 3, task V8). Light settings (ambient light, sky, reflections, fog)
    // belong to a scene, not to a prefab, so a scene that only holds the prefab (e.g. Role 2's Track_Campus) would get
    // Unity's defaults. This component copies the campus look (golden hour: cool sky fill, warm bounce from the
    // paving, the painted sky as skybox and reflection source, no fog) into the scene that holds the prefab, but only
    // while that scene is the active one: in the race RaceBootstrap makes the track scene active, in the Editor it is
    // the scene you opened. It never changes the settings of another scene (e.g. Main.unity) while that one is active.
    // The values are set by CampusWorldBuilder; the defaults here are the same.
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

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;   // the race switches the active scene after loading
            ApplyIfActiveScene();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ApplyIfActiveScene();
        }

        // True when the settings were applied (this object's scene is the active scene).
        public bool ApplyIfActiveScene()
        {
            if (gameObject.scene != SceneManager.GetActiveScene())
            {
                return false;
            }
            if (sun != null)
            {
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;          // no light baking needed
            RenderSettings.ambientSkyColor = skyColour;
            RenderSettings.ambientEquatorColor = horizonColour;
            RenderSettings.ambientGroundColor = groundColour;
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
            }
            if (reflection != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
                RenderSettings.reflectionIntensity = 1f;
            }
            RenderSettings.fog = false;
            return true;
        }
    }
}
