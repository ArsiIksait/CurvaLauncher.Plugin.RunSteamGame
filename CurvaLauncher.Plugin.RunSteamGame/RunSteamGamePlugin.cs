using CurvaLauncher.Data;
using CurvaLauncher.Plugin.RunProgram;
using CurvaLauncher.Plugin.RunSteamGame.Properties;
using CurvaLauncher.Utilities;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace CurvaLauncher.Plugin.RunSteamGame
{
    public class RunSteamGamePlugin : ISyncPlugin
    {
        readonly Lazy<ImageSource> laziedIcon = new(() => ImageUtils.CreateFromSvg(Resources.IconSvg)!);

        public ImageSource Icon => laziedIcon.Value;

        public string Name => "Run Steam Game";

        public string Description => "Run Steam Game or Application installed on your PC";

        [PluginOption]
        public string SteamPath { get; set; } = string.Empty;

        [PluginOption]
        public int ResultCount { get; set; } = 1;

        List<string> gameId = [];
        readonly Dictionary<string, (string gameId, string installdir, string iconPath)> _gamePathes = [];

        public IEnumerable<QueryResult> Query(CurvaLauncherContext context, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                yield break;
            var results = _gamePathes
                .Select(kv => (kv.Key, kv.Value, Weight: StringUtils.Match(kv.Key.ToLower(), query.ToLower())))
                .OrderByDescending(kvw => kvw.Weight)
                .Take(ResultCount);
            foreach (var (Key, Value, Weight) in results)
            {
                yield return new RunSteamGameQueryResult(context, Key, Value.gameId, Value.installdir, Value.iconPath, string.Empty);
            }
        }

        public void Init()
        {
            gameId.Clear();
            _gamePathes.Clear();

            if (string.IsNullOrWhiteSpace(SteamPath) || !Path.Exists(SteamPath) || Path.GetFileNameWithoutExtension(SteamPath).ToLower() != "steam.exe")
            {
                var commonProgramsFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                var programsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                var allShotcutsInStartMenu = new List<string>();

                allShotcutsInStartMenu.AddRange(Directory.GetFiles(commonProgramsFolder, "*.lnk", SearchOption.AllDirectories));
                allShotcutsInStartMenu.AddRange(Directory.GetFiles(programsFolder, "*.lnk", SearchOption.AllDirectories));

                foreach (var shortcut in allShotcutsInStartMenu)
                {
                    if (FileUtils.GetShortcutTarget(shortcut) is not string target)
                        continue;
                    var ext = Path.GetExtension(target);
                    if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(shortcut);

                    if (name == "Steam")
                    {
                        SteamPath = Path.GetDirectoryName(target) ?? string.Empty;
                    }
                }
            }

            if (!string.IsNullOrEmpty(SteamPath))
            {
                gameId = GetAllGameID();
            }

            if (gameId.Count > 0)
            {
                foreach (var id in gameId)
                {
                    var gameInfo = GetGamePath(id);
                    var gameIcon = GetGameIcon(id);

                    _gamePathes[gameInfo.name] = (id, gameInfo.installdir, gameIcon);
                }
            }
        }

        List<string> GetAllGameID()
        {
            string fileText = File.ReadAllText(SteamPath + @"\steamapps\libraryfolders.vdf");
            var result = Regex.Matches(fileText, @"\w+");

            bool add = false;
            List<string> list = [];

            foreach (Match match in result.Cast<Match>())
            {
                if (match.Value == "apps")
                {
                    add = true;
                }

                if (add && long.TryParse(match.Value, out var gameid))
                {
                    list.Add(gameid.ToString());
                }
                else
                {
                    if (match.Value != "apps" && add)
                    {
                        break;
                    }
                }
            }

            for (int i = 1; i < list.Count; i++)
            {
                list.Remove(list[i]);
            }

            return list;
        }

        (string name, string installdir) GetGamePath(string gameId)
        {
            string fileText = File.ReadAllText(SteamPath + $@"\steamapps\appmanifest_{gameId}.acf");
            var result = Regex.Matches(fileText, "(.*?)\".*?\"(.*?)");

            string addType = string.Empty;
            string gameName = "Unknown";
            string gameInstalldir = "Unknown";

            foreach (Match match in result.Cast<Match>())
            {
                var r = match.Value.Replace("\t", "").Replace("\"", "");
                if (r == "name")
                {
                    addType = "name";
                    continue;
                }

                if (r == "installdir")
                {
                    addType = "installdir";
                    continue;
                }

                if (addType == "name")
                {
                    gameName = r;
                    addType = string.Empty;
                }
                else if (addType == "installdir")
                {
                    gameInstalldir = $"{SteamPath}\\steamapps\\common\\{r}";
                    addType = string.Empty;
                }
                else
                {
                    if (gameName != "Unknown" && gameInstalldir != "Unknown")
                    {
                        break;
                    }
                }
            }

            return (gameName, gameInstalldir);
        }

        string GetGameIcon(string gameId)
        {
            string librarycache = $@"{SteamPath}\appcache\librarycache";
            if (File.Exists($@"{librarycache}\{gameId}_icon.png"))
                return $@"{librarycache}\{gameId}_icon.png";
            if (File.Exists($@"{librarycache}\{gameId}_icon.jpg"))
                return $@"{librarycache}\{gameId}_icon.jpg";
            return string.Empty;
        }

        public static string GetGameStartCMD(string gameId, string arguments = "")
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return $"steam://rungameid/{gameId}";
            }
            else
            {
                return $"steam://rungameid/{gameId}//{arguments}";
            }
        }
    }
}
