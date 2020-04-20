namespace Unity.Simulation
{
    /// <summary>
    /// Interface for consuming data.
    /// Consumers must implement this, and return true from Initialize if the consumer is able/desired to operate.
    /// </summary>
    public interface IDataProduced
    {
        /// <summary>
        /// Gives the consumer the opportunity to check if it can operate, and return status accordingly.
        /// Consumers that return false are not notified of data that is produced by the SDK.
        /// </summary>
        bool Initialize();

        /// <summary>
        /// Called when data is availabl for consumption. All consumers are notified.
        /// </summary>
        /// <param name="data">Typically this is a string path to a file.</param>
        /// <param name="synchronous">When true, the comsumption completes before the call returns.</param>
        void Consume(object data, bool synchronous = false);

        /// <summary>
        /// Called on Shutdown to ask the consumer if any asynchronous requests are still being handled.
        /// </summary>
        bool ConsumptionStillInProgress();
    }
}
