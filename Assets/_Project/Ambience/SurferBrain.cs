using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // One surfer's behaviour as a plain state machine (Edit Mode tested, no Unity objects):
    // Paddling -> Waiting -> Catching -> Riding -> Finished -> Paddling, or Riding -> Falling -> Hidden -> Paddling.
    // Position is in the beach frame: Along = metres along the beach (0 = centre), Distance = metres from the
    // waterline out to sea, Height = metres above the calm sea. Heading: 0 = facing the beach, +90 degrees (PI/2) =
    // facing +Along, PI = facing the sea. The body pose (SurferBody) reads State, StateTime and the Pose values.
    public class SurferBrain
    {
        private readonly SurferTuning tuning;
        private readonly System.Random random;   // seeded: the same surfer always surfs the same way (repeatable)
        private readonly float homeAlong;

        private int waveId = -1;
        private float rideDirection = 1f;
        private float rideSpeed;
        private float fallAt = -1f;               // seconds into the ride when this ride ends in a fall, -1 = no fall
        private float carvePhase;

        public SurferBrain(SurferTuning tuning, float homeAlong, int seed)
        {
            this.tuning = tuning != null ? tuning : new SurferTuning();
            this.homeAlong = float.IsNaN(homeAlong) ? 0f : homeAlong;
            random = new System.Random(seed);
            State = SurferState.Paddling;
            Along = this.homeAlong;
            Distance = this.tuning.LineupDistance + 10f;
            Heading = Mathf.PI;
            Visible = true;
        }

        public SurferState State { get; private set; }
        public float StateTime { get; private set; }  // seconds in the current state
        public float Along { get; private set; }
        public float Distance { get; private set; }
        public float Height { get; private set; }
        public float Heading { get; private set; }     // radians, see the class comment
        public float Lean { get; private set; }        // -1..1, into the turn (body and board roll)
        public float Pitch { get; private set; }       // radians, nose down is positive (riding down the face)
        public bool Visible { get; private set; }
        public float PaddleCycle { get; private set; } // grows while paddling: arm strokes
        public float RideDirection { get { return rideDirection; } }

        // The rule table: which state may follow which. Everything not listed is refused.
        public static bool IsAllowed(SurferState from, SurferState to)
        {
            switch (from)
            {
                case SurferState.Hidden: return to == SurferState.Paddling;
                case SurferState.Paddling: return to == SurferState.Waiting || to == SurferState.Catching;
                case SurferState.Waiting: return to == SurferState.Catching || to == SurferState.Paddling;
                case SurferState.Catching: return to == SurferState.Riding || to == SurferState.Paddling;
                case SurferState.Riding: return to == SurferState.Falling || to == SurferState.Finished;
                case SurferState.Falling: return to == SurferState.Hidden;
                case SurferState.Finished: return to == SurferState.Paddling;
                default: return false;
            }
        }

        // Changes state only if the rule table allows it. False = refused, nothing changed.
        public bool TryChange(SurferState next)
        {
            if (!IsAllowed(State, next))
            {
                return false;
            }
            State = next;
            StateTime = 0f;
            return true;
        }

        // One simulation step. dt <= 0, NaN or infinite = nothing happens; long frames are cut to 0.1 s so a hitch
        // in the browser never teleports a surfer. waves == null = a calm sea.
        public void Step(float dt, ISurfWaves waves)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f)
            {
                return;
            }
            dt = Mathf.Min(dt, 0.1f);
            StateTime += dt;
            switch (State)
            {
                case SurferState.Hidden: StepHidden(); break;
                case SurferState.Paddling: StepPaddling(dt, waves); break;
                case SurferState.Waiting: StepWaiting(dt, waves); break;
                case SurferState.Catching: StepCatching(dt, waves); break;
                case SurferState.Riding: StepRiding(dt, waves); break;
                case SurferState.Falling: StepFalling(dt, waves); break;
                case SurferState.Finished: StepFinished(dt, waves); break;
            }
        }

        private void StepHidden()
        {
            if (StateTime < tuning.RespawnSeconds)
            {
                return;
            }
            // Appears again out at sea behind the waiting spot and paddles in.
            Along = homeAlong;
            Distance = tuning.LineupDistance + 12f;
            Heading = 0f;
            Lean = 0f;
            Visible = true;
            TryChange(SurferState.Paddling);
        }

        private void StepPaddling(float dt, ISurfWaves waves)
        {
            float toAlong = homeAlong - Along;
            float toDistance = tuning.LineupDistance - Distance;
            float gap = Mathf.Sqrt(toAlong * toAlong + toDistance * toDistance);
            if (gap < 1f)
            {
                TryChange(SurferState.Waiting);
                Float(waves, dt);
                return;
            }
            float speed = Mathf.Max(0f, tuning.PaddleSpeed);
            float step = Mathf.Min(gap, speed * dt);
            Along += toAlong / gap * step;
            Distance += toDistance / gap * step;
            TurnTowards(Mathf.Atan2(toAlong, -toDistance), dt, 2.5f);
            PaddleCycle += dt * 1.6f;
            Float(waves, dt);
            if (Mathf.Abs(toDistance) < 6f && LookForWave(waves))
            {
                return;
            }
        }

        private void StepWaiting(float dt, ISurfWaves waves)
        {
            TurnTowards(Mathf.PI, dt, 1.2f);   // sit and watch the sea
            Float(waves, dt);
            LookForWave(waves);
        }

        // True = a good wave is coming: turn round and start paddling for it.
        private bool LookForWave(ISurfWaves waves)
        {
            WaveSample incoming;
            if (waves == null || !waves.FindIncoming(Along, Distance, tuning.CatchWindow, tuning.MinRideHeight, out incoming))
            {
                return false;
            }
            waveId = incoming.Id;
            return TryChange(SurferState.Catching);
        }

        private void StepCatching(float dt, ISurfWaves waves)
        {
            WaveSample w;
            if (waves == null || !waves.TryGetWave(waveId, Along, out w) || StateTime > 10f)
            {
                TryChange(SurferState.Paddling);
                return;
            }
            TurnTowards(0f, dt, 4f);                       // face the beach
            Distance -= tuning.PaddleSpeed * 1.4f * dt;     // paddle hard with the wave
            PaddleCycle += dt * 2.6f;
            Float(waves, dt);
            if (w.Broken > 0.5f)
            {
                TryChange(SurferState.Paddling);           // it broke on top of them: try the next one
                return;
            }
            if (w.Crest <= Distance + 0.6f)
            {
                if (w.Height >= tuning.MinRideHeight)
                {
                    StartRide(w);
                }
                else
                {
                    TryChange(SurferState.Paddling);       // too small here, let it pass
                }
            }
        }

        private void StartRide(WaveSample w)
        {
            // Ride away from the peak (towards the part that has not broken yet); at the peak itself pick a side.
            float fromPeak = Along - w.PeakAlong;
            if (Mathf.Abs(fromPeak) < 1f)
            {
                rideDirection = random.NextDouble() < 0.5 ? -1f : 1f;
            }
            else
            {
                rideDirection = fromPeak > 0f ? 1f : -1f;
            }
            float min = Mathf.Max(0f, tuning.RideSpeedMin);
            float max = Mathf.Max(min, tuning.RideSpeedMax);
            rideSpeed = min + (float)random.NextDouble() * (max - min);
            fallAt = random.NextDouble() < tuning.FallChance ? 1.5f + (float)random.NextDouble() * 3f : -1f;
            carvePhase = 0.25f;                             // start at the bottom of the face
            TryChange(SurferState.Riding);
        }

        private void StepRiding(float dt, ISurfWaves waves)
        {
            WaveSample w;
            if (waves == null || !waves.TryGetWave(waveId, Along, out w))
            {
                TryChange(SurferState.Finished);
                return;
            }
            Along += rideDirection * rideSpeed * dt;
            float carve = tuning.CarveSeconds > 0.2f ? tuning.CarveSeconds : 0.2f;
            carvePhase += dt / carve;
            float wave = Mathf.Sin(carvePhase * Mathf.PI * 2f);      // +1 top of the face, -1 bottom
            float h = Mathf.Max(0.3f, w.Height);
            float popUp = Mathf.Clamp01(StateTime / Mathf.Max(0.05f, tuning.PopUpSeconds));
            float faceOffset = Mathf.Lerp(0.85f, 0.3f, 0.5f + 0.5f * wave) * h;   // bottom of the face .. just under the lip
            faceOffset = Mathf.Lerp(0.2f * h, faceOffset, popUp);
            Distance = w.Crest - faceOffset;
            Height = WaveProfile.HeightAtOffset(h, faceOffset) + 0.08f;   // board on the water, not in it
            // Bottom turn: nose to the beach and along; top turn: nose up the face. Lean into the turn.
            float turn = Mathf.Cos(carvePhase * Mathf.PI * 2f);      // > 0 turning up the face
            Heading = rideDirection * (Mathf.PI * 0.5f + 0.55f * turn * popUp);
            Lean = -rideDirection * 0.6f * turn * popUp;
            Pitch = 0.18f + 0.12f * -wave;

            bool reachedEnd = Mathf.Abs(Along) > tuning.BeachHalfLength;
            if (w.Broken > 0.55f || (fallAt >= 0f && StateTime >= fallAt))
            {
                TryChange(SurferState.Falling);
            }
            else if (w.Crest < tuning.KickOutDistance || reachedEnd)
            {
                TryChange(SurferState.Finished);
            }
        }

        private void StepFalling(float dt, ISurfWaves waves)
        {
            Distance -= 1.2f * dt;                          // the foam pushes them towards the beach
            Float(waves, dt);
            if (StateTime >= tuning.FallSeconds)
            {
                Visible = false;
                TryChange(SurferState.Hidden);
            }
        }

        private void StepFinished(float dt, ISurfWaves waves)
        {
            // Kick out: the board goes over the back of the wave, then they sit for a moment.
            if (StateTime < 0.8f)
            {
                Distance += 2.2f * dt;
            }
            Lean = Mathf.MoveTowards(Lean, 0f, dt * 2f);
            TurnTowards(Mathf.PI, dt, 2f);
            Float(waves, dt);
            if (StateTime >= 1.8f)
            {
                TryChange(SurferState.Paddling);
            }
        }

        // Bob on whatever water is at this spot; the board follows the slope a little.
        private void Float(ISurfWaves waves, float dt)
        {
            if (waves == null)
            {
                Height = 0f;
                Pitch = 0f;
                return;
            }
            Height = waves.SurfaceHeight(Along, Distance);
            float forwardAlong = Mathf.Sin(Heading) * 0.8f;
            float forwardDistance = -Mathf.Cos(Heading) * 0.8f;
            float ahead = waves.SurfaceHeight(Along + forwardAlong, Distance + forwardDistance);
            float behind = waves.SurfaceHeight(Along - forwardAlong, Distance - forwardDistance);
            float slope = Mathf.Atan2(behind - ahead, 1.6f);   // nose down = positive
            Pitch = Mathf.MoveTowards(Pitch, slope, dt * 2f);
            Lean = Mathf.MoveTowards(Lean, 0f, dt);
        }

        private void TurnTowards(float target, float dt, float radiansPerSecond)
        {
            float delta = Mathf.DeltaAngle(Heading * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float step = radiansPerSecond * dt;
            Heading += Mathf.Clamp(delta, -step, step);
        }
    }
}
