using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // The contract between the kart (Role 1) and everyone who reads it: race flow, HUD, audio, effects.
    // Exactly as agreed in docs/interfaces.md. Changing it needs the whole team's OK.
    public interface IKartState
    {
        float Speed { get; }            // current forward speed in m/s
        float MaxSpeed { get; }         // for HUD, engine pitch, FOV kick
        bool IsDrifting { get; }
        float DriftCharge01 { get; }    // 0..1 how charged the drift boost is
        int DriftStage { get; }         // 0 none, 1 blue sparks, 2 orange sparks
        bool IsBoosting { get; }
        bool IsGrounded { get; }        // false while in the air (ramp jump)
        bool InputEnabled { get; set; } // RaceFlow turns input off during countdown/results
        void ResetTo(Vector3 position, Quaternion rotation); // teleport + zero velocity + clear drift/boost
    }
}
