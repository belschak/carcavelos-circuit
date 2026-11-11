using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Makes one copy of the optional rider model per surfer (AmbienceSettings > Surfer Model, see ISurferVisual).
    // No model, or a model without an ISurferVisual component = null: the riders are built in code (SurferBody).
    public static class SurferRiders
    {
        public static ISurferVisual[] Spawn(GameObject model, Transform parent, int count)
        {
            if (model == null || count <= 0)
            {
                return null;
            }
            if (model.GetComponent<ISurferVisual>() == null)
            {
                Debug.Log("SeasideAmbience: the surfer model " + model.name + " has no SurferModelPuppet (ISurferVisual), the surfers are built in code.");
                return null;
            }
            var riders = new ISurferVisual[count];
            for (int i = 0; i < count; i++)
            {
                GameObject copy = Object.Instantiate(model, parent);
                copy.name = "Surfer model " + (i + 1);
                riders[i] = copy.GetComponent<ISurferVisual>();
                riders[i].Hide();
            }
            return riders;
        }
    }
}
