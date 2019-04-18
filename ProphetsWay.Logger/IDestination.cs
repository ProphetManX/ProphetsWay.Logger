namespace ProphetsWay.Utilities
{
    /// <summary>
    /// Base interface for all destinations, identifies them as an object for use with Logger
    /// </summary>
    public interface IDestination
    {
        /// <summary>
        /// Should return true if the messageLevel matches the level specified when the Destination was created.
        /// </summary>
        bool ValidateMessageLevel(LogLevels messageLevel);
    }
}
