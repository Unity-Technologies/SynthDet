using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;

public class UniformMaterialRandomizer : Randomizer
{
    public MaterialParameter[] Materials;

    /// <summary>
    /// Sets every tagged object to the same randomly selected material of the correct set
    /// </summary>
    protected override void OnIterationStart()
    {
        //var tags = tagManager.Query<UniformMaterialRandomizerTag>();
       
        for(int i = 0; i < Materials.Length; i++)
        {
            Material newMat = Materials[i].Sample();
        }
    }
}
