using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P4, teacher tribute: this game exists because of Introduction to Programming 2026 at Nova SBE.
    // Plain C# rules (Edit Mode tested), used by TeacherTribute: who is thanked, which checkpoint gates get a
    // thank-you board, where a board may stand, and who wins the Gil Pereira Cup. Names only, no photos.
    public static class TributeRules
    {
        // The teaching staff, in the order of the boards in TributeBoards.png (tools/textures/make_tribute_boards.py).
        public static readonly string[] Teachers =
        {
            "Ricardo João Gil Pereira",
            "Cláudio Vieira",
            "Luís Carvalho",
            "Paulo Marques",
        };

        public const string CupLine = "YOU WON THE GIL PEREIRA CUP";

        // A gold lap or better (the Dean's List) wins the cup.
        public static bool WinsCup(Medal medal)
        {
            return medal >= Medal.Gold;
        }

        // "Gate 7" -> 7. Anything else (another object, "Gate", "Gate x", "Gate -1") -> -1.
        public static int GateNumber(string objectName)
        {
            const string prefix = "Gate ";
            if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith(prefix))
            {
                return -1;
            }
            int number;
            if (!int.TryParse(objectName.Substring(prefix.Length), out number) || number < 0)
            {
                return -1;
            }
            return number;
        }

        // Which gates get a board: spread evenly over the lap, in driving order. Gate 0 is the start/finish line,
        // already busy with the grid and the start lights, so it never gets one. Fewer gates than boards: each of
        // the other gates once. gateCount = number of gates (0 .. gateCount - 1).
        public static int[] PickGates(int gateCount, int boards)
        {
            var picked = new List<int>();
            int usable = gateCount - 1; // gates 1 .. gateCount - 1
            if (usable <= 0 || boards <= 0)
            {
                return picked.ToArray();
            }
            for (int k = 0; k < boards; k++)
            {
                int gate = 1 + Mathf.Clamp(Mathf.RoundToInt((k + 0.5f) * usable / boards - 0.5f), 0, usable - 1);
                if (!picked.Contains(gate))
                {
                    picked.Add(gate);
                }
            }
            return picked.ToArray();
        }

        // The gate to try when a picked gate has no free spot for its board: the next one, never gate 0.
        public static int NextGate(int gate, int gateCount)
        {
            if (gateCount <= 1)
            {
                return -1;
            }
            int next = gate + 1;
            return next >= gateCount ? 1 : next;
        }

        // Board size and where it stands, in metres, seen from the driver.
        public const float BoardWidth = 3.2f;
        public const float BoardHeight = 0.8f;    // 4:1, like one board in the picture
        public const float BoardBottom = 1.2f;    // above the ground, so the kart camera sees it over the kerb
        public const float Ahead = 3.0f;          // how far before (or after) the gate

        // The spots to try for a board next to one gate, best first, in the gate's own space (driver looks along +z,
        // +x = right). The inner edge of the board is on the line of the banner pole, which the campus builder
        // already checked is off the road, so the board only reaches further out. First before the gate (seen
        // while driving towards it), right then left; then after the gate.
        public static Vector3[] BoardSpots(Vector3 leftFoot, Vector3 rightFoot)
        {
            float half = BoardWidth * 0.5f;
            return new[]
            {
                new Vector3(rightFoot.x + half, rightFoot.y, -Ahead),
                new Vector3(leftFoot.x - half, leftFoot.y, -Ahead),
                new Vector3(rightFoot.x + half, rightFoot.y, Ahead),
                new Vector3(leftFoot.x - half, leftFoot.y, Ahead),
            };
        }

        // The ground found under a spot is only used when it is close to the height of the pole's foot: a much
        // higher or lower hit is a roof, a wall top or the beach below, not the roadside.
        public const float MaxGroundStep = 1.0f;

        public static bool GroundFits(float footHeight, float groundHeight)
        {
            return !float.IsNaN(groundHeight) && Mathf.Abs(groundHeight - footHeight) <= MaxGroundStep;
        }

        // Which part of TributeBoards.png board i shows: boards are stacked top to bottom in the picture, and a
        // texture's v runs bottom to top. Returns (scale.x, scale.y, offset.x, offset.y) for the material.
        public static Vector4 BoardUv(int board, int boards)
        {
            if (boards <= 0)
            {
                return new Vector4(1f, 1f, 0f, 0f);
            }
            int i = Mathf.Clamp(board, 0, boards - 1);
            float h = 1f / boards;
            return new Vector4(1f, h, 0f, 1f - (i + 1) * h);
        }
    }
}
