using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace GameFinder.StoreHandlers.Steam
{
    /// <summary>
    /// Store Handler for Steam Games
    /// </summary>
    [PublicAPI]
    public class SteamHandler : AStoreHandler<SteamGame>
    {
        /// <inheritdoc cref="AStoreHandler{TGame}.StoreType"/>
        public override StoreType StoreType => StoreType.Steam;

        private const string SteamRegKey = @"Software\Valve\Steam";

        /// <summary>
        /// Path to the Steam Installation Directory
        /// </summary>
        public readonly string? SteamPath;
        public virtual string? SteamConfig { get; set; }
        public virtual string? SteamLibraries { get; set; }

        /// <summary>
        /// List of all found Steam Universes
        /// </summary>
        public List<string> SteamUniverses { get; internal set; } = new();

        /// <summary>
        /// True if steam was found.
        /// </summary>
        public readonly bool FoundSteam;


        /// <summary>
        /// Default constructor.
        /// </summary>
        public SteamHandler() : this(NullLogger.Instance) { }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="logger">Logger instance to use, will default to <see cref="NullLogger"/></param>
        public SteamHandler(ILogger? logger = null) : base(logger ?? NullLogger.Instance)
        {
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string steamPath = "";
            if (isWindows)
            {
                using RegistryKey? steamKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SteamRegKey);
                if (steamKey == null)
                {
                    Logger.LogError("Unable to open registry key {SteamKey}", steamKey);
                    return;
                }

                var steamPathPossible = RegistryUtils.RegistryHelper.GetStringValueFromRegistry(steamKey, "SteamPath", Logger);

                if (steamPathPossible == null) return;

                if (!Directory.Exists(steamPathPossible))
                {
                    Logger.LogError("Path to Steam from Registry does not exist: {SteamPath}", steamPath);
                    return;
                }
                else
                {
                    steamPath = steamPathPossible!;
                }
            }
            else
            {
                steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "steam");
                if (!Directory.Exists(steamPath))
                {
                    Logger.LogError("Default Steam path for Unix systems does not exist: {SteamPath}", steamPath);
                    return;
                }
            }
            //#if Windows
            //            using var steamKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SteamRegKey);
            //            if (steamKey == null)
            //            {
            //                Logger.LogError("Unable to open registry key {SteamKey}", steamKey);
            //                return;
            //            }

            //            var steamPath = RegistryUtils.RegistryHelper.GetStringValueFromRegistry(steamKey, "SteamPath", Logger);
            //            if (steamPath == null) return;

            //            if (!Directory.Exists(steamPath))
            //            {
            //                Logger.LogError("Path to Steam from Registry does not exist: {SteamPath}", steamPath);
            //                return;
            //            }
            //#else
            //            var steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            //                ".steam", "steam");
            //            if (!Directory.Exists(steamPath))
            //            {
            //                Logger.LogError("Default Steam path for Unix systems does not exist: {SteamPath}", steamPath);
            //                return;
            //            }
            //#endif

            string steamConfig = Path.Combine(steamPath, "config", "config.vdf");
            if (!File.Exists(steamConfig))
            {
                Logger.LogError("Unable to find config.vdf at {SteamConfigPath}", steamConfig);
                return;
            }

            string steamLibraries = Path.Combine(steamPath, "config", "libraryfolders.vdf");
            if (!File.Exists(steamLibraries))
            {
                Logger.LogWarning("Unable to find libraryfolders.vdf at {Path}", steamLibraries);

                steamLibraries = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(steamLibraries))
                {
                    Logger.LogError("Unable to find libraryfolders.vdf at {SteamLibrariesPath}", steamLibraries);
                    return;
                }
            }

            FoundSteam = true;
            SteamPath = steamPath;
            SteamConfig = steamConfig;
            SteamLibraries = steamLibraries;
        }

        /// <summary>
        /// SteamHandler constructor with <paramref name="steamPath"/> argument, will not use the registry to find
        /// the Steam path.
        /// </summary>
        /// <param name="steamPath">Path to the directory containing <c>Steam.exe</c></param>
        /// <param name="logger">Logger instance to use, will default to <see cref="NullLogger"/></param>
        /// <exception cref="ArgumentException"><paramref name="steamPath"/> is not a directory or does not exist</exception>
        public SteamHandler(string steamPath, ILogger? logger = null) : base(logger ?? NullLogger.Instance)
        {
            if (!Directory.Exists(steamPath))
                throw new ArgumentException($"Directory does not exist: {steamPath}", nameof(steamPath));

            string steamConfig = Path.Combine(steamPath, "config", "config.vdf");
            if (!File.Exists(steamConfig))
            {
                Logger.LogError("Unable to find config.vdf at {SteamConfigPath}", steamConfig);
                return;
            }

            string steamLibraries = Path.Combine(steamPath, "config", "libraryfolders.vdf");
            if (!File.Exists(steamLibraries))
            {
                Logger.LogWarning("Unable to find libraryfolders.vdf at {Path}", steamLibraries);

                steamLibraries = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(steamLibraries))
                {
                    Logger.LogError("Unable to find libraryfolders.vdf at {SteamLibrariesPath}", steamLibraries);
                    return;
                }
            }

            FoundSteam = true;
            SteamPath = steamPath;
            SteamConfig = steamConfig;
            SteamLibraries = steamLibraries;
        }

        private bool FindAllUniverses()
        {
            if (!FoundSteam) return false;
            if (SteamConfig == null) return false;
            if (SteamLibraries == null) return false;

            List<string> configRes = ParseSteamConfig(SteamConfig, Logger);
            List<string> libraryFolders = ParseLibraryFolders(SteamLibraries, Logger);

            HashSet<string> allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allPaths.UnionWith(configRes);
            allPaths.UnionWith(libraryFolders);

            foreach (string? path in allPaths)
            {
                string universePath = Path.Combine(path, "steamapps");
                if (!Directory.Exists(universePath))
                {
                    Logger.LogWarning("Steam Universe at {Path} does not exist", universePath);
                    continue;
                }

                SteamUniverses.Add(universePath);
            }

            if (SteamPath == null)
            {
                if (SteamUniverses.Count != 0) return true;
                Logger.LogError("Found 0 Steam Universes");
                return false;

            }

            string defaultPath = Path.Combine(SteamPath, "steamapps");
            if (Directory.Exists(defaultPath))
                SteamUniverses.Add(defaultPath);

            if (SteamUniverses.Count != 0) return true;
            Logger.LogError("Found 0 Steam Universes");
            return false;
        }

        /// <inheritdoc />
        public override bool FindAllGames()
        {
            if (!FoundSteam) return false;
            if (SteamConfig == null) return false;
            if (SteamLibraries == null) return false;

            bool universeRes = FindAllUniverses();
            if (!universeRes) return false;

            foreach (string? universe in SteamUniverses)
            {
                IEnumerable<string> acfFiles = Directory.EnumerateFiles(universe, "*.acf", SearchOption.TopDirectoryOnly);
                foreach (string? acfFile in acfFiles)
                {
                    SteamGame? game = ParseAcfFile(acfFile, Logger);
                    if (game == null) continue;

                    game.Path = Path.Combine(universe, "common", game.Path);
                    Games.Add(game);
                }
            }

            return true;
        }

        /// <summary>
        /// Get Game by Steam ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public SteamGame GetByID(int id)
        {
            return Games.First(x => x.ID == id);
        }

        /// <summary>
        /// Try get Game by Steam ID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public bool TryGetByID(int id, [MaybeNullWhen(false)] out SteamGame? game)
        {
            game = Games.FirstOrDefault(x => x.ID == id);
            return game != null;
        }

        private static readonly Regex SteamConfigRegex = new(@"\""BaseInstallFolder_\d*\""\s*\""(?<path>[^\""]*)\""", RegexOptions.Compiled);
        private static readonly Regex OldLibraryFoldersRegex = new(@"^\s+\""\d+\""\s+\""(?<path>.+)\""", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex NewLibraryFoldersPathRegex = new(@"\""path\""\s*\""(?<path>[^\""]*)\""", RegexOptions.Compiled);

        internal static List<string> ParseSteamConfig(string file, ILogger logger)
        {
            List<string> res = new List<string>();

            if (!File.Exists(file))
            {
                logger.LogError("Steam Config file at {Path} does not exist", file);
                return res;
            }

            string text = File.ReadAllText(file, Encoding.UTF8);
            MatchCollection matches = SteamConfigRegex.Matches(text);

            if (matches.Count == 0)
            {
                logger.LogError("Found no Regex matches in Steam Config at {Path}", file);
                return res;
            }

            GetAllMatches("path", matches, path => res.Add(MakeValidPath(path)));
            return res;
        }

        internal static List<string> ParseLibraryFolders(string file, ILogger logger)
        {
            List<string> res = new List<string>();

            if (!File.Exists(file))
            {
                logger.LogError("Config file at {Path} does not exist", file);
                return res;
            }

            IEnumerable<string> lines = File.ReadLines(file, Encoding.UTF8);
            string firstLine = lines.First();

            // old format (before 1623193086 2021-06-08)
            if (firstLine.Contains("LibraryFolders", StringComparison.Ordinal))
            {
                string text = File.ReadAllText(file, Encoding.UTF8);
                MatchCollection matches = OldLibraryFoldersRegex.Matches(text);

                if (matches.Count == 0)
                {
                    logger.LogWarning("Found no matches in Library Folders file at {Path}", file);
                    return res;
                }

                GetAllMatches("path", matches, path => res.Add(MakeValidPath(path)));
                return res;
            }

            // new format (after 1623193086 2021-06-08)
            if (firstLine.Contains("libraryfolders", StringComparison.Ordinal))
            {
                string text = File.ReadAllText(file, Encoding.UTF8);

                MatchCollection pathMatches = NewLibraryFoldersPathRegex.Matches(text);

                if (pathMatches.Count == 0)
                {
                    logger.LogWarning("Found no path-matches in Library Folders file at {Path}", file);
                    return res;
                }

                List<string> pathValues = new List<string>();
                GetAllMatches("path", pathMatches, path => pathValues.Add(path));

                res.AddRange(pathValues.Select(MakeValidPath));
                return res;
            }

            logger.LogError("Unknown format for Library Folders file at {Path}: {FirstLine}", file, firstLine);
            return res;
        }

        private static readonly IReadOnlyList<string> AcfKeys = new[]
        {
            "appid",
            "name",
            "installdir",
            "LastUpdated",
            "SizeOnDisk",
            "BytesToDownload",
            "BytesDownloaded",
            "BytesToStage",
            "BytesStaged"
        };

        internal static SteamGame? ParseAcfFile(string file, ILogger logger)
        {
            if (!File.Exists(file))
            {
                logger.LogError("ACF Manifest at {Path} does not exist", file);
                return null;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> lines = File.ReadLines(file, Encoding.UTF8);
            foreach (string? line in lines)
            {
                string? first = AcfKeys.FirstOrDefault(x => line.ContainsCaseInsensitive($"\"{x}\""));
                if (first == null) continue;
                if (dict.ContainsKey(first)) continue;

                string? value = GetVdfValue(line, logger);
                if (value == null) continue;

                dict.Add(first, value);
            }

            if (dict.Count != AcfKeys.Count)
            {
                foreach (string? acfKey in AcfKeys)
                {
                    if (dict.TryGetValue(acfKey, out _)) continue;
                    logger.LogError("ACF Manifest at {Path} does not contain a value with key {Key}", file, acfKey);
                }

                return null;
            }

            SteamGame game = new SteamGame();

            string? sAppId = dict["appid"];
            string? name = dict["name"];
            string? installDir = dict["installdir"];
            string? sLastUpdated = dict["LastUpdated"];
            string? sSizeOnDisk = dict["SizeOnDisk"];
            string? sBytesToDownload = dict["BytesToDownload"];
            string? sBytesDownloaded = dict["BytesDownloaded"];
            string? sBytesToStage = dict["BytesToStage"];
            string? sBytesStaged = dict["BytesStaged"];

            if (!int.TryParse(sAppId, out int appId))
            {
                logger.LogError("Unable to parse value \"{Value}\" (\"{ValueName}\") as {Type} in ACF Manifest {Path}",
                    sAppId, "appid", "int", file);
                return null;
            }

            game.ID = appId;

            bool ParseAndSet(string value, string key, Action<long> action)
            {
                if (!long.TryParse(value, out long lValue))
                {
                    logger.LogError("Unable to parse value \"{Value}\" (\"{ValueName}\") as {Type} in ACF Manifest {Path}",
                        value, key, "long", file);
                    return false;
                }

                action(lValue);
                return true;
            }

            if (!ParseAndSet(sLastUpdated, "LastUpdated", timeStamp =>
            {
                DateTime dateTime = timeStamp.ToDateTime();
                game.LastUpdated = dateTime;
            }))
            {
                return null;
            }

            if (!ParseAndSet(sSizeOnDisk, "SizeOnDisk", sizeOnDisk => game.SizeOnDisk = sizeOnDisk))
                return null;

            if (!ParseAndSet(sBytesToDownload, "BytesToDownload",
                bytesToDownload => game.BytesToDownload = bytesToDownload))
                return null;

            if (!ParseAndSet(sBytesDownloaded, "BytesDownloaded",
                bytesDownloaded => game.BytesDownloaded = bytesDownloaded))
                return null;

            if (!ParseAndSet(sBytesToStage, "BytesToStage", bytesToStage => game.BytesToStage = bytesToStage))
                return null;

            if (!ParseAndSet(sBytesStaged, "BytesStaged", bytesStaged => game.BytesStaged = bytesStaged))
                return null;

            game.Name = name;
            game.Path = installDir;

            return game;
        }

        private static void GetAllMatches(string group, MatchCollection matches, Action<string> action)
        {
            foreach (Match match in matches)
            {
                //Debug.WriteLine($"Match: {match}");
                GroupCollection groups = match.Groups;
#if NET5_0_OR_GREATER
                if (!groups.TryGetValue(group, out Group? currentGroup)) continue;
                if (!currentGroup.Success) continue;
#elif NETSTANDARD2_1
                Group? currentGroup = groups.FirstOrDefault(x => x.Success && x.Name.Equals(group, StringComparison.OrdinalIgnoreCase));
                if (currentGroup == null) continue;
#endif
                //foreach (var match2 in groups)
                //{
                //    Debug.WriteLine($"Group: {match2}");
                //}

                action(currentGroup.Value);
            }
        }

        private static string MakeValidPath(string input)
        {
            StringBuilder sb = new StringBuilder(input);
            sb.Replace("\\\\", "\\");
            return sb.ToString();
        }

        private static string? GetVdfValue(string line, ILogger logger)
        {
            string[]? split = line.Split("\"");
            if (string.IsNullOrWhiteSpace(line))
            {
                Debug.WriteLine($"Vdf Line: {line}");
            }

            if (split.Length == 5) return split[3];

            logger.LogError("Unable to split line correctly in VDF file: {Line}", line);
            return null;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "SteamHandler";
        }
    }
}
