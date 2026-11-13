using System.Reflection;
using CarcavelosCircuit.Driving;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Playtest
{
    // LOCAL PLAYTEST (free roam, Luc 13 Nov 2026): no race, no bots, no timer. Spawns the player's kart at the start,
    // lets it drive at once (KartController's test-bed switch "drive without RaceFlow") and points the chase camera
    // at it. PlaytestTools adds marks, screenshots, jumps and the free camera.
    public class FreeRoam : MonoBehaviour
    {
        [SerializeField] private GameObject kartPrefab;
        [SerializeField] private GameObject kartEffectsPrefab;
        [SerializeField] private Object[] kartAttachments = new Object[0];
        [SerializeField] private Transform spawn;

        private void Start()
        {
            GameObject kart = Instantiate(kartPrefab, spawn.position, spawn.rotation);
            kart.name = "Kart (free roam)";
            foreach (Object a in kartAttachments)
            {
                if (a is GameObject go) Instantiate(go, kart.transform);
            }
            if (kartEffectsPrefab != null)
            {
                Instantiate(kartEffectsPrefab, kart.transform);
            }
            KartController controller = kart.GetComponent<KartController>();
            FieldInfo f = typeof(KartController).GetField("driveWithoutRaceFlow", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                f.SetValue(controller, true);
            }
            ChaseCameraRig rig = FindAnyObjectByType<ChaseCameraRig>();
            if (rig != null)
            {
                rig.SetTarget(kart.transform);
                rig.SnapBehindTarget();
            }
        }
    }
}
