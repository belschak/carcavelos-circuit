using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // ?test=itemrows (package C, 17.11.2026): after GO the camera stops behind every row of item boxes and every wave
    // box for a few seconds, so a headless browser can screenshot each one, then hands the camera back to the chase
    // camera. ItemSystem adds this only when the flag is in the address; players never see it.
    // Console: "ItemRowTour: stop 3/10 row 3 (after checkpoint 4): 3/3 boxes at (x, y, z)" when the camera is there,
    // "ItemRowTour: done, 24/24 boxes" at the end.
    public class ItemRowTour : MonoBehaviour
    {
        private const float HoldSeconds = 3f;
        private const float BackMetres = 9f;
        private const float UpMetres = 2.6f; // the tunnel and the hall have a ceiling

        private ItemSystem items;
        private RaceManager race;
        private Camera cam;
        private Behaviour[] pausedCameraScripts = new Behaviour[0];
        private int stop = -1;
        private float held;
        private bool done;

        public void SetUp(ItemSystem itemSystem, RaceManager raceManager)
        {
            items = itemSystem;
            race = raceManager;
            Debug.Log("ItemRowTour: " + items.StopCount + " stops, starts at GO.");
        }

        private void LateUpdate()
        {
            if (done || items == null || race == null)
            {
                return;
            }
            if (stop < 0)
            {
                if (race.State != RaceState.Racing)
                {
                    return;
                }
                Begin();
            }
            held += Time.unscaledDeltaTime;
            if (held >= HoldSeconds)
            {
                stop++;
                held = 0f;
                if (stop >= items.StopCount)
                {
                    Finish();
                    return;
                }
                Announce();
            }
            Aim();
        }

        private void Begin()
        {
            cam = Camera.main;
            if (cam == null || items.StopCount == 0)
            {
                Debug.Log("ItemRowTour: nothing to show (camera or stops missing).");
                done = true;
                return;
            }
            // The chase camera and the fly-over move the camera every frame: they step aside for the tour.
            ChaseCameraRig rig = cam.GetComponent<ChaseCameraRig>();
            MapFlyover flyover = cam.GetComponent<MapFlyover>();
            pausedCameraScripts = new Behaviour[] { rig, flyover };
            for (int i = 0; i < pausedCameraScripts.Length; i++)
            {
                if (pausedCameraScripts[i] != null) pausedCameraScripts[i].enabled = false;
            }
            stop = 0;
            held = 0f;
            Announce();
        }

        private void Announce()
        {
            ItemStop s = items.StopAt(stop);
            Debug.Log("ItemRowTour: stop " + (stop + 1) + "/" + items.StopCount + " " + s.Label + ": " + s.Placed + "/"
                + s.Planned + " boxes at (" + s.Centre.x.ToString("0.0") + ", " + s.Centre.y.ToString("0.0") + ", "
                + s.Centre.z.ToString("0.0") + ")");
        }

        private void Aim()
        {
            if (cam == null || stop < 0 || stop >= items.StopCount)
            {
                return;
            }
            ItemStop s = items.StopAt(stop);
            cam.transform.position = ItemStop.CameraSpot(s.Centre, s.Forward, BackMetres, UpMetres);
            cam.transform.LookAt(s.Centre + Vector3.up * 0.8f);
        }

        private void Finish()
        {
            done = true;
            for (int i = 0; i < pausedCameraScripts.Length; i++)
            {
                if (pausedCameraScripts[i] != null) pausedCameraScripts[i].enabled = true;
            }
            Debug.Log("ItemRowTour: done, " + items.BoxCount + "/" + items.PlannedBoxes + " boxes.");
        }
    }
}
