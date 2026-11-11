using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // Makes one copy of a rider model per surfer (AmbienceSettings > Surfer Models, see ISurferVisual); surfer i
    // gets model i, i + 1 ... taking turns when there are more surfers than models.
    // No models, or any model without an ISurferVisual component (e.g. SurferModelPuppet) = null: then every
    // rider is built in code (SurferBody). All or nothing, so the beach never mixes the two looks.
    public static class SurferRiders
    {
        public static ISurferVisual[] Spawn(GameObject[] models, Transform parent, int count)
        {
            if (models == null || models.Length == 0 || count <= 0)
            {
                return null;
            }
            for (int m = 0; m < models.Length; m++)
            {
                if (models[m] == null || models[m].GetComponent<ISurferVisual>() == null)
                {
                    Debug.Log("SeasideAmbience: surfer model " + (m + 1) + " is missing or has no SurferModelPuppet (ISurferVisual), the surfers are built in code.");
                    return null;
                }
            }
            var riders = new ISurferVisual[count];
            for (int i = 0; i < count; i++)
            {
                GameObject copy = Object.Instantiate(models[i % models.Length], parent);
                copy.name = "Surfer model " + (i + 1);
                riders[i] = copy.GetComponent<ISurferVisual>();
                riders[i].Hide();
            }
            return riders;
        }
    }
}
