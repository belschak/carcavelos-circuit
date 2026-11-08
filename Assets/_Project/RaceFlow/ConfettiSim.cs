using System;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P6b: the physics of the results confetti (about 30 paper pieces). Plain C# without Unity types, so Edit
    // Mode tests can check it; UI/ConfettiBurst.cs only draws what this calculates.
    // Units are HUD pixels (1280x720 reference screen). y grows DOWNWARDS, as everywhere in UI Toolkit.
    // Each piece: shot up and out of one point, pulled down by gravity, slowed by air, spins, flips like paper
    // (its width follows a cosine), and fades out at the end of its life. After MaxLifeSeconds all are gone.
    public class ConfettiSim
    {
        public const int DefaultCount = 30;
        public const float Gravity = 1400f;           // px/s2
        public const float AirDrag = 2.2f;            // per second: speed shrinks by e^(-2.2) each second
        public const float MinSpeed = 380f;           // px/s at the burst
        public const float MaxSpeed = 760f;
        public const float SpreadDegrees = 62f;       // cone half-width around straight up
        public const float MinLifeSeconds = 1.45f;
        public const float MaxLifeSeconds = 1.9f;     // nothing is left after this (cleaned up after about 2 s)
        public const float FadeSeconds = 0.4f;        // the last part of a piece's life
        public const int ColourCount = 4;             // white, blue, orange, sand (UI/ConfettiBurst.cs maps them)
        private const float LongestStep = 0.05f;      // a hitch never makes the pieces jump far

        // One array per value (simple to read, no class per piece). Index i = piece i.
        public float[] X = new float[0];
        public float[] Y = new float[0];
        public float[] Angle = new float[0];          // degrees
        public float[] Flip = new float[0];           // 0..1: how much of its width the piece shows right now
        public float[] Width = new float[0];
        public float[] Height = new float[0];
        public int[] Colour = new int[0];
        private float[] speedX = new float[0];
        private float[] speedY = new float[0];
        private float[] spin = new float[0];          // degrees per second
        private float[] flipPhase = new float[0];
        private float[] flipSpeed = new float[0];
        private float[] life = new float[0];

        public int Count { get; private set; }
        public float Age { get; private set; }

        // True when no piece is visible any more (or nothing was ever burst).
        public bool IsDone { get { return Count == 0 || Age >= MaxLifeSeconds; } }

        // Starts a new burst from one point. The same seed always gives the same burst (tests rely on that).
        public void Burst(float originX, float originY, int count, int seed)
        {
            if (count < 0)
            {
                count = 0;
            }
            Allocate(count);
            Random random = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                // Direction: straight up (-90 degrees) plus or minus the spread.
                float degrees = -90f + Between(random, -SpreadDegrees, SpreadDegrees);
                float radians = degrees * (float)Math.PI / 180f;
                float speed = Between(random, MinSpeed, MaxSpeed);
                X[i] = originX;
                Y[i] = originY;
                speedX[i] = (float)Math.Cos(radians) * speed;
                speedY[i] = (float)Math.Sin(radians) * speed;
                Angle[i] = Between(random, 0f, 360f);
                spin[i] = Between(random, 240f, 720f) * (random.Next(2) == 0 ? -1f : 1f);
                flipPhase[i] = Between(random, 0f, 6.283f);
                flipSpeed[i] = Between(random, 7f, 14f);
                Flip[i] = 1f;
                Width[i] = Between(random, 7f, 10f);
                Height[i] = Between(random, 10f, 14f);
                Colour[i] = i % ColourCount; // an even mix of the four colours
                life[i] = Between(random, MinLifeSeconds, MaxLifeSeconds);
            }
            Count = count;
            Age = 0f;
        }

        // Moves every piece by one frame. dt = Time.unscaledDeltaTime (real time: the slow motion must not slow it).
        public void Step(float dt)
        {
            if (IsDone || !(dt > 0f))
            {
                return;
            }
            if (dt > LongestStep)
            {
                dt = LongestStep;
            }
            Age += dt;
            float keep = (float)Math.Exp(-AirDrag * dt); // air resistance
            for (int i = 0; i < Count; i++)
            {
                speedY[i] += Gravity * dt;
                speedX[i] *= keep;
                speedY[i] *= keep;
                X[i] += speedX[i] * dt;
                Y[i] += speedY[i] * dt;
                Angle[i] += spin[i] * dt;
                flipPhase[i] += flipSpeed[i] * dt;
                Flip[i] = Math.Abs((float)Math.Cos(flipPhase[i]));
            }
        }

        // 1 while the piece is fresh, then down to 0 over its last FadeSeconds.
        public float AlphaOf(int i)
        {
            float left = life[i] - Age;
            if (left <= 0f)
            {
                return 0f;
            }
            return left >= FadeSeconds ? 1f : left / FadeSeconds;
        }

        // Removes all pieces at once (retry, or leaving the results screen).
        public void Clear()
        {
            Count = 0;
            Age = 0f;
        }

        private void Allocate(int count)
        {
            if (X.Length >= count)
            {
                return; // reuse the arrays: no garbage for a second burst
            }
            X = new float[count];
            Y = new float[count];
            Angle = new float[count];
            Flip = new float[count];
            Width = new float[count];
            Height = new float[count];
            Colour = new int[count];
            speedX = new float[count];
            speedY = new float[count];
            spin = new float[count];
            flipPhase = new float[count];
            flipSpeed = new float[count];
            life = new float[count];
        }

        private static float Between(Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
