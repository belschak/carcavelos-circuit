using System;
using System.Globalization;
using System.IO;
using System.Text;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). The automatic test: only active when Unity was started with -botTest.
    // All six karts are bots (the player's kart gets an autopilot), the race starts by itself, and when everybody
    // has finished the numbers are written to a text file and Unity quits.
    //   -botOut <folder>   where the report (and pictures) go
    //   -botNoRubber       switch the rubber band off, to see the bots' own pace
    //   -botSlowInFront    turn the grid upside down (slowest bot in front): a stress test for overtaking
    //   -botParkPlayer     the player's kart just stands on the grid: the bots must drive around it every lap
    //   -botRetryAt <secs> press retry once at this race time: checks that a restart puts everybody back
    //   -botDifficulty easy|hard   run the race on another difficulty than the scene's
    //   -botBlind          bots ignore each other (many bumps on purpose): checks that a bump never launches a kart
    //   -botShots <secs>   save a picture from the chase camera every <secs> seconds of race time
    //   -botGif <from>-<to> save small frames between these race seconds (for a GIF)
    [DefaultExecutionOrder(1000)] // after the chase camera has moved, so pictures show this frame
    public class BotRaceTest : MonoBehaviour
    {
        [SerializeField] private BotRace race;
        [SerializeField] private Camera raceCamera;
        [SerializeField, Tooltip("Give up after this many race seconds")]
        private float timeoutSeconds = 600f;

        private string outFolder = "BotTest";
        private float shotEvery;
        private float nextShot;
        private float gifFrom = -1f;
        private float gifTo = -1f;
        private int frame;
        private bool done;
        private StringBuilder shotLog = new StringBuilder();
        private StringBuilder trace;
        private bool parkPlayer;
        private int samples;
        private int samplesWithBotNear;
        private float nearestSum;
        private float gapSum;
        private int gapCount;
        private float biggestBand;
        private string testDifficulty;
        private float retryAt = -1f;

        private void Awake()
        {
            string[] args = new string[0];
            try
            {
                args = Environment.GetCommandLineArgs();
            }
            catch (Exception)
            {
                // the browser has no command line
            }
            if (Array.IndexOf(args, "-botTest") < 0)
            {
                enabled = false;
                return;
            }
            parkPlayer = Array.IndexOf(args, "-botParkPlayer") >= 0;
            race.AutoPilotPlayer = !parkPlayer;
            string retry = Arg(args, "-botRetryAt");
            if (retry != null)
            {
                retryAt = float.Parse(retry, CultureInfo.InvariantCulture);
            }
            race.RubberBandOff = Array.IndexOf(args, "-botNoRubber") >= 0;
            if (Array.IndexOf(args, "-botNoLine") >= 0)
            {
                race.GetComponent<BotRoute>().useRacingLine = false; // before BotRoute.Awake? no: see BotRoute.Rebuild
                race.GetComponent<BotRoute>().Rebuild();
            }
            AudioListener.volume = 0f; // the automatic test is always silent
            race.MeasureCost = true;
            string autoPace = Arg(args, "-botAutopilotPace");
            if (autoPace != null)
            {
                race.AutoPilotTopSpeed = float.Parse(autoPace, CultureInfo.InvariantCulture);
            }
            // -botProbe "0,0.5,-0.5,0.8,-0.8,0.95": edge probe, one fixed line per kart (9 / -9 = half a metre over the edge).
            string probe = Arg(args, "-botProbe");
            if (probe != null)
            {
                string[] parts = probe.Split(',');
                float[] lanes = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    lanes[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
                }
                race.ProbeLanes = lanes;
                race.TotalLapsOverride = 1;
            }
            string scale = Arg(args, "-botSpeedScale");
            if (scale != null)
            {
                race.SpeedScaleOverride = float.Parse(scale, CultureInfo.InvariantCulture);
            }
            string seed = Arg(args, "-botSeed");
            race.Seed = seed != null ? int.Parse(seed) : 1;
            string level = Arg(args, "-botDifficulty");
            if (level != null)
            {
                testDifficulty = level.ToLowerInvariant();
            }
            race.SlowBotsInFront = Array.IndexOf(args, "-botSlowInFront") >= 0;
            BotDriver.Blind = Array.IndexOf(args, "-botBlind") >= 0;
            string blindLane = Arg(args, "-botBlindLane");
            BotDriver.BlindLane = blindLane != null ? float.Parse(blindLane, CultureInfo.InvariantCulture) : 0f;
            BotDriver.AllDrift = Array.IndexOf(args, "-botDrift") >= 0;
            BotDriver.NoSlipstream = Array.IndexOf(args, "-botNoSlipstream") >= 0;
            outFolder = Arg(args, "-botOut") ?? outFolder;
            string shots = Arg(args, "-botShots");
            if (shots != null)
            {
                shotEvery = float.Parse(shots, CultureInfo.InvariantCulture);
            }
            string gif = Arg(args, "-botGif");
            if (gif != null)
            {
                string[] parts = gif.Split('-');
                gifFrom = float.Parse(parts[0], CultureInfo.InvariantCulture);
                gifTo = float.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            Directory.CreateDirectory(outFolder);
            if (Array.IndexOf(args, "-botTrace") >= 0)
            {
                trace = new StringBuilder("time,kart,index,lateral,speed,grounded,y,drift,boost\n");
            }
            BotRaceLog.Events.Clear();
            // Every frame is exactly one physics step (50 per second), as fast as the computer can go.
            // The race is then the same on every run and does not depend on the frame rate.
            Time.captureFramerate = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
            Debug.Log("BotRaceTest: active, report goes to " + outFolder);
        }

        // -botTrace: one line per kart and physics step (for looking at a problem corner afterwards).
        private void FixedUpdate()
        {
            if (!race.IsSetUp || !race.RaceRunning)
            {
                return;
            }
            SamplePlayerGaps();
            if (trace == null)
            {
                return;
            }
            for (int i = 0; i < race.Karts.Length; i++)
            {
                RaceKart kart = race.Karts[i];
                trace.AppendLine(race.RaceTime.ToString("0.00", CultureInfo.InvariantCulture) + "," + kart.DisplayName + ","
                    + kart.RouteIndex + "," + kart.Lateral.ToString("0.00", CultureInfo.InvariantCulture) + ","
                    + kart.Kart.Speed.ToString("0.0", CultureInfo.InvariantCulture) + "," + (kart.Kart.IsGrounded ? 1 : 0) + ","
                    + kart.transform.position.y.ToString("0.00", CultureInfo.InvariantCulture) + "," + (kart.Kart.IsDrifting ? 1 : 0)
                    + "," + (kart.Kart.IsBoosting ? 1 : 0));
            }
        }

        // How close the racing is around the player: sampled every step until the player finishes.
        // Gap between P1 and P2 each time the leader reaches a checkpoint (metres and seconds), and who leads.
        private readonly System.Collections.Generic.List<float> leadGapMetres = new System.Collections.Generic.List<float>();
        private readonly System.Collections.Generic.List<float> leadGapSeconds = new System.Collections.Generic.List<float>();
        private RaceKart lastLeader;
        private int lastLeaderCheckpoint = -1;
        private int lastLeaderLap = -1;
        private int leaderChanges;
        private int longestLeadCheckpoints;   // longest run of checkpoints one leader held at least 10 m ahead
        private int currentLeadRun;

        private void SampleLeaderGap()
        {
            RaceKart[] order = race.Standings;
            if (order.Length < 2 || order[0].Finished)
            {
                return;
            }
            RaceKart leader = order[0];
            if (lastLeader != null && leader != lastLeader)
            {
                leaderChanges++;
                currentLeadRun = 0;
            }
            lastLeader = leader;
            if (leader.LastCheckpoint == lastLeaderCheckpoint && leader.Lap == lastLeaderLap)
            {
                return;
            }
            lastLeaderCheckpoint = leader.LastCheckpoint;
            lastLeaderLap = leader.Lap;
            if (race.RaceTime < 5f)
            {
                return; // the grid itself is not a gap
            }
            float metres = leader.Progress - order[1].Progress;
            leadGapMetres.Add(metres);
            leadGapSeconds.Add(metres / Mathf.Max(5f, order[1].Kart.Speed));
            currentLeadRun = metres >= 10f ? currentLeadRun + 1 : 0;
            longestLeadCheckpoints = Mathf.Max(longestLeadCheckpoints, currentLeadRun);
        }

        private void SamplePlayerGaps()
        {
            SampleLeaderGap();
            if (race.Player.Finished)
            {
                return;
            }
            float nearest = float.MaxValue;
            for (int i = 0; i < race.Karts.Length; i++)
            {
                if (race.Karts[i].IsPlayer)
                {
                    continue;
                }
                float gap = Mathf.Abs(race.Karts[i].Progress - race.Player.Progress);
                gapSum += gap;
                gapCount++;
                if (gap < nearest)
                {
                    nearest = gap;
                }
                float band = Mathf.Abs(race.RubberBandOf(race.Karts[i]));
                if (band > biggestBand)
                {
                    biggestBand = band;
                }
            }
            samples++;
            nearestSum += nearest;
            if (nearest < 25f)
            {
                samplesWithBotNear++;
            }
        }

        private void LateUpdate()
        {
            if (done || !race.IsSetUp)
            {
                return;
            }
            RaceManager manager = race.RaceManager;
            if (testDifficulty == "easy") race.Difficulty = BotDifficulty.Easy;
            if (testDifficulty == "hard") race.Difficulty = BotDifficulty.Hard;
            manager.ReducedMotion = true; // no finish slow motion, no fly-over glide
            if (manager.State == RaceState.Ready)
            {
                manager.BeginRace(); // the "any key" that starts the countdown
            }

            frame++;
            TakePictures();

            // -botRetryAt: press "retry" once in the middle of the race; everything must start again cleanly.
            if (retryAt >= 0f && race.RaceTime >= retryAt)
            {
                retryAt = -1f;
                BotRaceLog.Add(race.RaceTime.ToString("0.0", CultureInfo.InvariantCulture) + " s  TEST pressed retry");
                manager.Retry();
                return;
            }

            bool allFinished = true;
            for (int i = 0; i < race.Karts.Length; i++)
            {
                if (!race.Karts[i].Finished && !(parkPlayer && race.Karts[i].IsPlayer))
                {
                    allFinished = false;
                }
            }
            if (allFinished || race.RaceTime > timeoutSeconds)
            {
                done = true;
                WriteReport(allFinished);
                Quit();
            }
        }

        private void TakePictures()
        {
            if (raceCamera == null || !race.RaceRunning)
            {
                return;
            }
            float t = race.RaceTime;
            if (shotEvery > 0f && t >= nextShot)
            {
                nextShot += shotEvery;
                string name = "shot_" + t.ToString("000.0", CultureInfo.InvariantCulture) + ".png";
                Save(Path.Combine(outFolder, name), 1280, 720);
                shotLog.AppendLine(name + "  P" + race.PositionOf(race.Player) + "  karts within 15 m ahead: " + KartsAheadOfPlayer(15f)
                    + "  route index " + race.Player.RouteIndex + "  lap " + (race.Player.Lap + 1));
            }
            if (t >= gifFrom && t <= gifTo && frame % 5 == 0)
            {
                Directory.CreateDirectory(Path.Combine(outFolder, "gif"));
                Save(Path.Combine(outFolder, "gif", "f_" + frame.ToString("000000") + ".png"), 480, 270);
            }
        }

        private int KartsAheadOfPlayer(float metres)
        {
            int count = 0;
            for (int i = 0; i < race.Karts.Length; i++)
            {
                float gap = race.Karts[i].Progress - race.Player.Progress;
                if (!race.Karts[i].IsPlayer && gap > 0f && gap < metres)
                {
                    count++;
                }
            }
            return count;
        }

        // Draws the race camera once into a picture of the given size and saves it as PNG.
        private void Save(string file, int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24);
            target.antiAliasing = 4;
            raceCamera.targetTexture = target;
            raceCamera.Render();
            RenderTexture.active = target;
            Texture2D picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            RenderTexture.active = null;
            raceCamera.targetTexture = null;
            Destroy(target);
            Destroy(picture);
        }

        private void WriteReport(bool allFinished)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("BOT RACE TEST  difficulty " + race.Difficulty + "  laps " + race.TotalLaps + "  rubber band " + (race.RubberBandOff ? "off" : "on")
                + "  race seconds " + race.RaceTime.ToString("0.0", CultureInfo.InvariantCulture)
                + (allFinished ? "  ALL FINISHED" : "  TIMEOUT: NOT ALL FINISHED"));
            RaceKart[] order = race.Standings;
            for (int i = 0; i < order.Length; i++)
            {
                RaceKart kart = order[i];
                text.Append((i + 1) + ". " + kart.DisplayName.PadRight(16));
                text.Append(kart.Finished ? " total " + BotRaceOverlay.FormatTime(kart.FinishTime) : " DID NOT FINISH (lap " + (kart.Lap + 1) + ")");
                text.Append("  laps");
                for (int lap = 0; lap < kart.LapTimes.Count; lap++)
                {
                    text.Append(" " + kart.LapTimes[lap].ToString("0.00", CultureInfo.InvariantCulture));
                }
                text.Append("  respawns " + kart.Respawns + "  stuck " + kart.StuckEvents + "  contacts " + kart.Contacts
                    + "  off-ground-after-contact " + kart.LaunchesAfterContact
                    + " (max " + kart.MaxAirHeightAfterContact.ToString("0.00", CultureInfo.InvariantCulture) + " m)");
                if (kart.LapSectors.Count > 1)
                {
                    text.AppendLine();
                    text.Append("   SECT " + kart.DisplayName + " lap 2:" + BotRace.SectorText(kart.LapSectors[1]));
                }
                BotDriver driver = race.DriverOf(kart);
                if (driver != null)
                {
                    text.Append("  drift entries " + driver.DriftEntries + " aborts " + driver.DriftAborts);
                    text.Append("  form top " + (driver.FormTopSpeed * 100f).ToString("0.0", CultureInfo.InvariantCulture) + " % corner "
                        + (driver.FormCornering * 100f).ToString("0.0", CultureInfo.InvariantCulture) + " %  big mistakes " + driver.BigMistakes);
                    text.Append("  mistakes " + driver.Mistakes + " of " + driver.MistakeChances + " chances");
                    text.Append("  passes " + driver.PassAttempts + " started / " + driver.PassCompletions + " done / " + driver.PassAborts + " given up");
                }
                text.Append("  overtakes made " + kart.OvertakesMade + " suffered " + kart.OvertakesSuffered);
                text.Append("  drifts: wall " + kart.DriftsEndedByWall + " kart-hit " + kart.DriftsEndedByKartHit + " released " + kart.DriftsReleased
                    + " other " + kart.DriftsEndedOtherwise + " rubs-kept " + kart.KartRubsWhileDrifting);
                text.AppendLine();
            }
            if (samples > 0)
            {
                text.AppendLine("STATS player gaps: mean distance to every bot " + (gapSum / gapCount).ToString("0", CultureInfo.InvariantCulture)
                    + " m, nearest bot on average " + (nearestSum / samples).ToString("0", CultureInfo.InvariantCulture)
                    + " m, a bot within 25 m " + (100f * samplesWithBotNear / samples).ToString("0", CultureInfo.InvariantCulture)
                    + " % of the time, biggest band " + (biggestBand * 100f).ToString("0.0", CultureInfo.InvariantCulture) + " %");
            }
            // One line with the numbers the review compares (all karts together).
            int started = 0, finished = 0, givenUp = 0, contacts = 0, respawns = 0, overtakes = 0, laneN = 0, laneOff = 0;
            float laneErr = 0f;
            for (int i = 0; i < order.Length; i++)
            {
                contacts += order[i].Contacts;
                respawns += order[i].Respawns;
                overtakes += order[i].OvertakesMade;
                BotDriver d = race.DriverOf(order[i]);
                if (d != null)
                {
                    started += d.PassAttempts;
                    finished += d.PassCompletions;
                    givenUp += d.PassAborts;
                    laneErr += d.PassLaneErrorSum;
                    laneN += d.PassLaneSamples;
                    laneOff += d.PassLaneSamplesOffsetLine;
                }
            }
            float meanGap = 0f, maxGap = 0f, meanGapS = 0f;
            int breakAways = 0;
            for (int i = 0; i < leadGapMetres.Count; i++)
            {
                meanGap += leadGapMetres[i];
                meanGapS += leadGapSeconds[i];
                maxGap = Mathf.Max(maxGap, leadGapMetres[i]);
                if (leadGapMetres[i] >= 10f)
                {
                    breakAways++;
                }
            }
            if (leadGapMetres.Count > 0)
            {
                meanGap /= leadGapMetres.Count;
                meanGapS /= leadGapMetres.Count;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            text.AppendLine("SUMMARY slipstream " + (BotDriver.NoSlipstream ? "off" : "on") + "  rubber " + (race.RubberBandOff ? "off" : "on") + "  seed " + race.Seed
                + "  P1-P2 gap at checkpoints: mean " + meanGap.ToString("0.0", inv) + " m (" + meanGapS.ToString("0.00", inv) + " s), max " + maxGap.ToString("0.0", inv)
                + " m, >=10 m at " + breakAways + "/" + leadGapMetres.Count + " checkpoints, longest lead run " + longestLeadCheckpoints + " checkpoints, leader changes " + leaderChanges
                + "  pass-lane error " + (laneN > 0 ? (laneErr / laneN).ToString("0.00", inv) : "-") + " m (" + laneN + " samples, " + laneOff + " on offset line)"
                + "  passes " + started + " started / " + finished + " done / " + givenUp + " given up  overtakes " + overtakes + "  contacts " + contacts + "  respawns " + respawns);
            if (race.CostSamples > 0)
            {
                text.AppendLine("STATS bot code per physics step: " + (race.CostBytesTotal / (float)race.CostSamples).ToString("0.0", CultureInfo.InvariantCulture)
                    + " B allocated on average, " + race.CostStepsWithAllocation + " of " + race.CostSamples + " steps allocated anything, 95 % of steps under "
                    + race.CostMilliseconds95().ToString("0.000", CultureInfo.InvariantCulture) + " ms");
            }
            for (int i = 0; i < BotRaceLog.Events.Count; i++)
            {
                text.AppendLine("  " + BotRaceLog.Events[i]);
            }
            File.WriteAllText(Path.Combine(outFolder, "report.txt"), text.ToString());
            if (trace != null)
            {
                File.WriteAllText(Path.Combine(outFolder, "trace.csv"), trace.ToString());
            }
            if (shotLog.Length > 0)
            {
                File.WriteAllText(Path.Combine(outFolder, "shots.txt"), shotLog.ToString());
            }
            Debug.Log(text.ToString());
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
