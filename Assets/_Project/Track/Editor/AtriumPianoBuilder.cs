using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Task T10: saves the prefab Track/Piano/AtriumPiano.prefab, the sound object for the grand piano by the west glass
    // of the Nova hall. The prefab sits at the piano's world position, so it goes into the campus world scene at its
    // own position (request Q18 for Luc, or drag it into a test scene). The piano itself is part of the hall model.
    public static class AtriumPianoBuilder
    {
        private const string PrefabPath = "Assets/_Project/Track/Piano/AtriumPiano.prefab";

        // Where nova_hall.py puts the piano (PIANO_U 93, o 5.4), in Unity world metres: x east, y up, z north.
        // y = hall floor (FLOOR_Z 14.56) + 1 m, the middle of the piano body.
        private static readonly Vector3 PianoPosition = new Vector3(40.71f, 15.56f, 74.82f);
        private const float PianoYaw = 69.4f; // the piano's long side, along the west glass

        [MenuItem("Carcavelos Circuit/Build Atrium Piano (Role 2)")]
        public static void Build()
        {
            var piano = new GameObject("AtriumPiano");
            piano.transform.SetPositionAndRotation(PianoPosition, Quaternion.Euler(0f, PianoYaw, 0f));
            AudioSource source = piano.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;                       // 3D: louder when the kart is close
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 45f;                       // heard across the long room, not outside the hall
            piano.AddComponent<SphereCollider>();           // AtriumPiano turns it into the trigger sphere
            piano.AddComponent<AtriumPiano>();
            PrefabUtility.SaveAsPrefabAsset(piano, PrefabPath);
            Object.DestroyImmediate(piano);
            Debug.Log("AtriumPianoBuilder: saved " + PrefabPath + " at " + PianoPosition
                + ". Drag it into the scene; it keeps its own position.");
        }
    }
}
