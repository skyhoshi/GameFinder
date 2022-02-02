using System;

namespace GameFinder.StoreHandlers.Steam
{
    public interface ISteamGame
    {
        /// <summary>
        /// Steam ID of the game
        /// </summary>
        int ID { get; set; }

        /// <summary>
        /// Time when the game was last updated
        /// </summary>
        DateTime LastUpdated { get; set; }

        /// <summary>
        /// Size of the game on disk in bytes
        /// </summary>
        long SizeOnDisk { get; set; }

        /// <summary>
        /// Amount of bytes to download
        /// </summary>
        long BytesToDownload { get; set; }

        /// <summary>
        /// Amount of bytes already downloaded
        /// </summary>
        long BytesDownloaded { get; set; }

        /// <summary>
        /// Amount of bytes to stage
        /// </summary>
        long BytesToStage { get; set; }

        /// <summary>
        /// Amount of bytes already staged
        /// </summary>
        long BytesStaged { get; set; }
    }
}
