namespace GameFinder
{
    public interface IAStoreGame
    {
        /// <summary>
        /// Name of the Game
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Path to the Game
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Store Type
        /// </summary>
        public StoreType StoreType { get; }
    }
}
