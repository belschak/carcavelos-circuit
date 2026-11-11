using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // The swap seam for the riders. Today every rider is built in code (SurferBody). A later imported, rigged human
    // model (licence-clean, e.g. CC0, only after David's OK) can ride instead: put a component that implements this
    // on the model (SurferModelPuppet does) and drag the model into AmbienceSettings > Surfer Models. SeasideAmbience
    // then calls Show every frame and builds only the board in code.
    public interface ISurferVisual
    {
        // brain = what the surfer does (state, lean ...); rider = where the rider stands: the board's world matrix
        // times SurferBody.RiderSpace (rider space: +z = nose, +x = right, +y = up). joints = the code-built pose of
        // this frame in rider space (SurferBody joint numbers), e.g. for hand and foot targets of the model.
        void Show(SurferBrain brain, Matrix4x4 rider, Vector3[] joints);

        // The surfer is gone (after a fall): hide the model.
        void Hide();
    }
}
