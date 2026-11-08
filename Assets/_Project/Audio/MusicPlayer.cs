using UnityEngine;

namespace CarcavelosCircuit.Audio
{
    // Background music in Main.unity: one looping track at a low volume so the engine stays audible.
    // Browsers keep all sound muted until the first click or key press; Unity resumes it automatically then,
    // and our "Press any key to start" screen makes sure that press happens before the race.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Music volume")]
        private float volume = 0.3f;

        private void Start()
        {
            AudioSource source = GetComponent<AudioSource>();
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 0f; // 2D: same loudness everywhere
            if (source.clip != null)
            {
                source.Play();
            }
        }
    }
}
