using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Tasks R8 + R9: the "Racers" root object in Main.unity (RaceRoster = every racer's race flow, PositionsHud =
    // position readout + minimap). Called by Role4SceneBuilder while Main.unity is open, so it only adds one object
    // and wires it; the builder keeps its list of root objects (BuilderComponents) in step.
    public static class Role4RacersBuilder
    {
        public const string RootName = "Racers";
        private const string TestBotKartPath = "Assets/_Project/RaceFlow/Prefabs/PlaceholderKart.prefab"; // dev test bots only

        public static void Build(RaceManager manager, RaceSettings settings, PanelSettings panel)
        {
            GameObject racers = new GameObject(RootName);
            RaceRoster roster = racers.AddComponent<RaceRoster>();
            SerializedObject rso = new SerializedObject(roster);
            rso.FindProperty("raceManager").objectReferenceValue = manager;
            rso.FindProperty("settings").objectReferenceValue = settings;
            // May be null (then dev test bots just log a warning); the game itself never needs it.
            rso.FindProperty("testBotKart").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TestBotKartPath);
            rso.ApplyModifiedPropertiesWithoutUndo();

            // Own UIDocument on the HUD's PanelSettings, under the item slot (-1) and the race HUD (0).
            UIDocument document = racers.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = -2f;
            PositionsHud hud = racers.AddComponent<PositionsHud>();
            SerializedObject hso = new SerializedObject(hud);
            hso.FindProperty("raceManager").objectReferenceValue = manager;
            hso.FindProperty("roster").objectReferenceValue = roster;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        // The components Build puts on the root object (Role4SceneBuilder's hand-edit guard).
        public static System.Type[] Components()
        {
            return new[] { typeof(Transform), typeof(RaceRoster), typeof(UIDocument), typeof(PositionsHud) };
        }
    }
}
