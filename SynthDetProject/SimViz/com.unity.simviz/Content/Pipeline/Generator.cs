namespace UnityEngine.SimViz.Content.Pipeline
{
    public interface IGeneratorSystem
    {
    }
    public interface IGeneratorSystem<TParameters> where TParameters : struct 
    {
        TParameters Parameters{ get; set; }
    }
    
}