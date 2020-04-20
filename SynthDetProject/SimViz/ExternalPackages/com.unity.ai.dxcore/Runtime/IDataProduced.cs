namespace Unity.AI.Simulation
{
    public interface IDataProduced
    {
        bool Initialize();

        void Consume(object data, bool synchronous = false);

        bool ConsumptionStillInProgress();
    }
}
