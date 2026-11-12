using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). Sits on every kart in the bot race, the player's too.
    // It knows where the kart is on the route, counts its laps and times, and counts kart-to-kart contacts.
    // Race position comes from one number: Progress = laps x route length + distance along the route.
    public class RaceKart : MonoBehaviour
    {
        public string DisplayName { get; private set; }
        public Color Colour { get; private set; }
        public bool IsPlayer { get; private set; }
        public int Number { get; set; }                 // place in BotRace.Karts (0 = the player)
        public IKartState Kart { get; private set; }

        public int RouteIndex { get; private set; }     // nearest route point
        public float Lateral { get; private set; }      // metres right (+) / left (-) of the centre line
        public int Lap { get; private set; }            // -1 on the grid (before the line), 0 = first lap running ...
        public float Progress { get; private set; }     // metres raced; negative on the grid
        public int LastCheckpoint { get; private set; } // checkpoint passed last on this lap (0 = start/finish)
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        public List<float> LapTimes { get; private set; }
        public List<float[]> LapSectors { get; private set; } // per finished lap: seconds from each checkpoint to the next

        // Numbers for the test report.
        public int OvertakesMade { get; set; }      // places gained on the track (BotRace counts them)
        public int OvertakesSuffered { get; set; }  // places lost on the track
        public int Contacts { get; private set; }
        public int Respawns { get; private set; }
        public int StuckEvents { get; private set; }
        public int LaunchesAfterContact { get; private set; } // times the kart left the ground right after a contact
        public float MaxAirHeightAfterContact { get; private set; }

        // How drifts ended (for the test report): wall, hard kart hit, key released, anything else (too slow, in the air).
        public int DriftsEndedByWall { get; private set; }
        public int DriftsEndedByKartHit { get; private set; }
        public int DriftsReleased { get; private set; }
        public int DriftsEndedOtherwise { get; private set; }
        public int KartRubsWhileDrifting { get; private set; } // light touches during a drift (the drift goes on)

        private bool wasDrifting;
        private int contactThisStep; // 0 none, 1 wall, 2 hard kart hit
        // Edge probe (test only): every time the kart leaves the ground outside the intended jump, with what it
        // touched just before.
        public bool LogLaunches { get; set; }
        private bool flying;
        private int flightStartIndex;
        private Vector3 flightStartPosition;
        private float flightStartLateral;
        private float flightMaxHeight;
        private float flightMaxUpSpeed;
        private string groundBeforeFlight = "?";
        private string lastGround = "?";
        private string lastSideHit = "";
        private float lastSideHitTime = -10f;
        private Rigidbody body;
        private BotRoute route;
        private float lapStartTime;
        private float sectorStartTime;
        private float[] sectorsThisLap = new float[0];
        private float contactWatchUntil = -1f;
        private bool countedThisLaunch;

        public void SetUp(BotRoute raceRoute, string displayName, Color colour, bool isPlayer)
        {
            route = raceRoute;
            DisplayName = displayName;
            Colour = colour;
            IsPlayer = isPlayer;
            Kart = GetComponent<IKartState>();
            body = GetComponent<Rigidbody>();
            LapTimes = new List<float>();
            LapSectors = new List<float[]>();
            sectorsThisLap = new float[raceRoute.checkpointAt.Length];
            ResetForStart();
        }

        // Back on the grid: forget laps and times, find the route point under the kart.
        public void ResetForStart()
        {
            LapTimes.Clear();
            LapSectors.Clear();
            sectorStartTime = 0f;
            Finished = false;
            FinishTime = 0f;
            lapStartTime = 0f;
            SnapToRoute();
            // The grid stands behind the start line, so its route index is near the END of the loop:
            // that is "lap -1", and crossing the line for the first time starts lap 0.
            Lap = RouteIndex > route.Count / 2 ? -1 : 0;
            UpdateNumbers();
        }

        // After a teleport (respawn): find the route point again without counting a lap.
        public void SnapToRoute()
        {
            RouteIndex = route.NearestAnywhere(transform.position);
            UpdateNumbers();
        }

        public void NoteRespawn(bool becauseStuck)
        {
            Respawns++;
            if (becauseStuck)
            {
                StuckEvents++;
            }
        }

        // Called every physics step by BotRace. raceTime = seconds since GO.
        public void Track(float raceTime, int totalLaps, bool raceRunning)
        {
            int next = route.NearestInWindow(transform.position, RouteIndex, 6, 20);

            // Far from the tracked point (the player pressed R, or took a big shortcut): look on the whole loop,
            // but only accept a point less than 160 m along the route from the old one. So a recover is picked up,
            // and teleporting across the map would not count.
            if ((route.points[next] - transform.position).sqrMagnitude > 25f * 25f)
            {
                int anywhere = route.NearestAnywhere(transform.position);
                if (Mathf.Abs(route.IndexDifference(RouteIndex, anywhere)) <= 80)
                {
                    next = anywhere;
                }
            }

            int moved = route.IndexDifference(RouteIndex, next);
            bool crossedLineForward = moved > 0 && next < RouteIndex;
            bool crossedLineBackward = moved < 0 && next > RouteIndex;
            RouteIndex = next;

            // Sector times: every time the kart reaches the next checkpoint, the sector before it is finished.
            int checkpointNow = route.CheckpointBefore(RouteIndex);
            if (raceRunning && moved > 0 && checkpointNow != LastCheckpoint)
            {
                sectorsThisLap[LastCheckpoint] = raceTime - sectorStartTime;
                sectorStartTime = raceTime;
            }
            if (crossedLineBackward)
            {
                Lap--;
            }
            if (crossedLineForward)
            {
                Lap++;
                // A lap counts once: LapTimes has one entry per finished lap.
                if (raceRunning && Lap >= 1 && LapTimes.Count < Lap && !Finished)
                {
                    LapTimes.Add(raceTime - lapStartTime);
                    LapSectors.Add((float[])sectorsThisLap.Clone());
                    lapStartTime = raceTime;
                    if (Lap >= totalLaps)
                    {
                        Finished = true;
                        FinishTime = raceTime;
                    }
                }
            }
            UpdateNumbers();
            WatchContact();
            WatchDrift();
            if (LogLaunches && raceRunning)
            {
                WatchLaunch();
            }
        }

        // One flight = from leaving the ground to touching it again. Logged when the kart got more than 0.3 m above
        // the road, outside the intended ramp jump.
        private void WatchLaunch()
        {
            float height = transform.position.y - route.points[RouteIndex].y;
            if (Kart.IsGrounded)
            {
                if (flying && flightMaxHeight > 0.3f && !route.IsJumpZone(flightStartIndex))
                {
                    System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                    BotRaceLog.Add("LAUNCH index " + flightStartIndex + " x " + flightStartPosition.x.ToString("0.0", inv) + " z " + flightStartPosition.z.ToString("0.0", inv)
                        + " lateral " + flightStartLateral.ToString("0.0", inv) + " height " + flightMaxHeight.ToString("0.00", inv)
                        + " upspeed " + flightMaxUpSpeed.ToString("0.0", inv) + " lands " + RouteIndex
                        + " ground [" + groundBeforeFlight + "] hit [" + (Time.time - lastSideHitTime < 1.5f ? lastSideHit : "-") + "] kart " + DisplayName);
                }
                flying = false;
                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    lastGround = Describe(hit.collider);
                }
                return;
            }
            if (!flying)
            {
                flying = true;
                flightStartIndex = RouteIndex;
                flightStartPosition = transform.position;
                flightStartLateral = Lateral;
                flightMaxHeight = 0f;
                flightMaxUpSpeed = 0f;
                groundBeforeFlight = lastGround;
            }
            if (height > flightMaxHeight) flightMaxHeight = height;
            if (body.linearVelocity.y > flightMaxUpSpeed) flightMaxUpSpeed = body.linearVelocity.y;
        }

        // "object name / material name" of a collider, for the launch log.
        private static string Describe(Collider collider)
        {
            Renderer renderer = collider.GetComponent<Renderer>();
            string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "no material";
            return collider.name + " / " + material;
        }

        // Notes why a drift ended, by looking at what happened in the step it stopped.
        private void WatchDrift()
        {
            bool drifting = Kart != null && Kart.IsDrifting;
            if (wasDrifting && !drifting)
            {
                if (contactThisStep == 1) DriftsEndedByWall++;
                else if (contactThisStep == 2) DriftsEndedByKartHit++;
                else if (Kart.IsBoosting || Kart.IsGrounded && Kart.Speed > 8f) DriftsReleased++;
                else DriftsEndedOtherwise++;
            }
            wasDrifting = drifting;
            contactThisStep = 0;
        }

        private void UpdateNumbers()
        {
            Lateral = route.Lateral(transform.position, RouteIndex);
            Progress = (Lap * route.Count + route.RoutePos(transform.position, RouteIndex)) * route.Spacing;
            LastCheckpoint = route.CheckpointBefore(RouteIndex);
        }

        // Fair-racing check: for half a second after touching another kart, did this kart leave the ground?
        // (Not on the ramp jump, where flying is normal.)
        private void WatchContact()
        {
            if (Time.time > contactWatchUntil || Kart == null)
            {
                return;
            }
            if (Kart.IsGrounded || route.IsJumpZone(RouteIndex))
            {
                countedThisLaunch = false;
                return;
            }
            if (!countedThisLaunch)
            {
                countedThisLaunch = true;
                LaunchesAfterContact++;
                BotRaceLog.Add(Time.time.ToString("0.0") + " s (game clock)  " + DisplayName + " left the ground after a contact at route index " + RouteIndex);
            }
            float height = transform.position.y - route.points[RouteIndex].y; // the kart's pivot is at ground level
            if (height > MaxAirHeightAfterContact)
            {
                MaxAirHeightAfterContact = height;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            RaceKart other = collision.collider.GetComponentInParent<RaceKart>();
            bool steep = collision.GetContact(0).normal.y < 0.5f; // a wall or a kart, not the road
            if (LogLaunches && other == null && collision.GetContact(0).normal.y < 0.9f)
            {
                lastSideHit = Describe(collision.collider) + " normal.y " + collision.GetContact(0).normal.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                lastSideHitTime = Time.time;
            }
            if (steep && wasDrifting)
            {
                if (other == null) contactThisStep = 1;
                else if (collision.relativeVelocity.magnitude >= 6f) contactThisStep = 2; // same limit as KartController
                else KartRubsWhileDrifting++;
            }
            if (other != null)
            {
                Contacts++;
                contactWatchUntil = Time.time + 0.5f;
                // For calibrating "rub or hard hit": how fast the two karts met, and from which side.
                float hitSpeed = collision.relativeVelocity.magnitude;
                bool fromTheSide = Mathf.Abs(Vector3.Dot(collision.GetContact(0).normal, transform.right)) > 0.7f;
                BotRaceLog.Add("CONTACT " + DisplayName + " with " + other.DisplayName + (fromTheSide ? " side " : " front/rear ")
                    + hitSpeed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s");
            }
        }
    }
}
