using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // Task D9: keeps one NameTag for every racer in RacerRegistry (the player and every bot).
    // When racers join or leave (RacerRegistry.Changed) it builds the tags again; every frame it moves them.
    // Put it once in the race scene (request Q22 to Role 4 for Main.unity). Racers are only read, never changed.
    public class NameTagManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Camera the tags face. Empty = the camera tagged MainCamera")]
        private Camera viewCamera;

        [SerializeField, Tooltip("Font of the names: drag in the game's HUD font (RaceFlow/UI/Fonts/BarlowCondensed/BarlowCondensed-ExtraBold.ttf). Empty = Unity's built-in font")]
        private Font tagFont;

        [SerializeField, Tooltip("Show names in capital letters, like the HUD")]
        private bool upperCase = true;

        [SerializeField, Range(0f, 1f), Tooltip("How dark the shadow behind the letters is (0 = no shadow)")]
        private float shadowStrength = 0.6f;

        [SerializeField, Tooltip("Height of the bottom of the name above the kart's root, in metres")]
        private float heightAboveKart = 1.6f;

        [SerializeField, Tooltip("Size of the letters (TextMesh character size). Bigger = bigger letters; check it in Play Mode")]
        private float letterSize = 0.08f;

        [SerializeField, Tooltip("Closer to the camera than this (m) the tag is hidden, so it never covers the road in front of you")]
        private float nearHideMetres = 3f;

        [SerializeField, Tooltip("From this distance (m) the tag is fully visible")]
        private float nearFullMetres = 6f;

        [SerializeField, Tooltip("Beyond this distance (m) the tag starts to fade out")]
        private float farFullMetres = 45f;

        [SerializeField, Tooltip("Beyond this distance (m) the tag is invisible")]
        private float farHideMetres = 60f;

        private readonly List<NameTag> tags = new List<NameTag>();
        private Font font;
        private bool needsRebuild; // racers joined or left; the tags are rebuilt in the next LateUpdate

        public bool UpperCase { get { return upperCase; } }
        public float ShadowStrength { get { return shadowStrength; } }
        public float HeightAboveKart { get { return heightAboveKart; } }
        public float LetterSize { get { return letterSize; } }
        public float NearHideMetres { get { return nearHideMetres; } }
        public float NearFullMetres { get { return nearFullMetres; } }
        public float FarFullMetres { get { return farFullMetres; } }
        public float FarHideMetres { get { return farHideMetres; } }

        private void Awake()
        {
            // The HUD font if one is set in the Inspector, otherwise Unity's built-in font (nothing to import).
            font = tagFont != null ? tagFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            RacerRegistry.Changed += OnRacersChanged;
            needsRebuild = true;
        }

        private void OnDisable()
        {
            RacerRegistry.Changed -= OnRacersChanged;
            DestroyTags();
        }

        // Only a note: the work happens in LateUpdate. When a scene closes, racers unregister one by one, and
        // creating tags in the middle of that would leave objects behind; LateUpdate does not run while closing.
        private void OnRacersChanged()
        {
            needsRebuild = true;
        }

        // Racers joined or left: simplest correct answer is to throw the tags away and make one per racer again.
        private void Rebuild()
        {
            DestroyTags();
            if (font == null)
            {
                return;
            }
            IReadOnlyList<IRacer> racers = RacerRegistry.All;
            for (int i = 0; i < racers.Count; i++)
            {
                GameObject tagObject = new GameObject("NameTag " + NameTagRules.CleanName(racers[i].DisplayName));
                tagObject.transform.SetParent(transform, false);
                NameTag tag = tagObject.AddComponent<NameTag>();
                tag.Init(racers[i], this, font);
                tags.Add(tag);
            }
        }

        private void DestroyTags()
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null)
                {
                    Destroy(tags[i].gameObject);
                }
            }
            tags.Clear();
        }

        // After the karts and the camera have moved this frame.
        private void LateUpdate()
        {
            if (needsRebuild)
            {
                needsRebuild = false;
                Rebuild();
            }
            Camera cameraToFace = viewCamera != null ? viewCamera : Camera.main;
            Transform cameraTransform = cameraToFace != null ? cameraToFace.transform : null;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null)
                {
                    tags[i].Refresh(cameraTransform);
                }
            }
        }
    }
}
