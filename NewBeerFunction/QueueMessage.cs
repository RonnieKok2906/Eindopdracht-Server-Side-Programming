namespace BeerFunction
{
    /// <summary>
    /// Stores place name and image name to be send as a queue message.
    /// </summary>
    public class QueueMessage
    {
        /// <summary>
        /// Place name.
        /// </summary>
        public string PlaceName { get; set; }

        /// <summary>
        /// Image name.
        /// </summary>
        public string ImageName { get; set; }

        /// <summary>
        /// The sas uri.
        /// </summary>
        public string SasUri { get; set; }
    }
}
