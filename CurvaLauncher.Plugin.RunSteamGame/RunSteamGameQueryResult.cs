using CurvaLauncher.Data;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CurvaLauncher.Plugin.RunProgram
{
    public class RunSteamGameQueryResult : SyncQueryResult
    {
        public RunSteamGameQueryResult(CurvaLauncherContext context, string gameName, string gameId, string installdir, string iconPath, string arguments)
        {
            GameName = gameName;
            StartCMD = RunSteamGame.RunSteamGamePlugin.GetGameStartCMD(gameId, arguments);
            Arguments = arguments;
            Installdir = installdir;

            context.Dispatcher.Invoke(() =>
            {
                icon = new BitmapImage(new Uri(iconPath, UriKind.Relative));
            });
        }

        private ImageSource? icon;

        public override float Weight => 1;

        public override string Title => GameName;

        public override string Description => !string.IsNullOrWhiteSpace(Arguments) ?
            $"Run Steam Application: '{Installdir}' with '{Arguments}'" :
            $"Run Steam Application: '{Installdir}'";

        public override ImageSource? Icon => icon;

        public string GameName { get; }
        public string StartCMD { get; }
        public string Installdir { get; }
        public string Arguments { get; }

        public override void Invoke()
        {
            Process.Start(
                new ProcessStartInfo()
                {
                    FileName = StartCMD,
                    Arguments = Arguments,
                    UseShellExecute = true,
                });
        }
    }
}