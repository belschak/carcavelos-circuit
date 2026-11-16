using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Tasks R8 + R9: the "Racers" root object in Main.unity (RaceRoster = every racer's race flow, PositionsHud =
    // position readout + minimap, BotSquad = Role 3's five bots, task V10). Called by Role4SceneBuilder while Main.unity is open, so it only adds one object
    // and wires it; the builder keeps its list of root objects (BuilderComponents) in step.
    public static class Role4RacersBuilder
    {
        public const string RootName = "Racers";
        private const string TestBotKartPath = "Assets/_Project/RaceFlow/Prefabs/PlaceholderKart.prefab"; // dev test bots only
        private const string BotSettingsPath = "Assets/_Project/Visuals/Bots/BotSettings.asset"; // Role 3's bots (V10)

        // kartPrefab = the kart the player drives (Role 1's Kart.prefab); Role 3's bots drive copies of it.
        public static void Build(RaceManager manager, RaceSettings settings, PanelSettings panel, GameObject kartPrefab)
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

            // Task V10 (Role 3, Luc): BotSquad spawns the five bots once the roster is set up; they register as
            // IRacer, so RaceRoster puts them on the grid like any racer. A bot can only drive a kart with
            // IKartDrivable (Role 1's KartController, D8): with the placeholder kart the race simply has no bots.
            BotSquad squad = racers.AddComponent<BotSquad>();
            SerializedObject bso = new SerializedObject(squad);
            bso.FindProperty("roster").objectReferenceValue = roster;
            bso.FindProperty("raceManager").objectReferenceValue = manager;
            bool drivable = kartPrefab != null && kartPrefab.GetComponent<IKartDrivable>() != null;
            bso.FindProperty("kartPrefab").objectReferenceValue = drivable ? kartPrefab : null;
            // May be null (then BotSquad uses the defaults written in BotSettings.cs).
            bso.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotSettings>(BotSettingsPath);
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        // The components Build puts on the root object (Role4SceneBuilder's hand-edit guard).
        public static System.Type[] Components()
        {
            return new[] { typeof(Transform), typeof(RaceRoster), typeof(UIDocument), typeof(PositionsHud), typeof(BotSquad) };
        }
    }
}
