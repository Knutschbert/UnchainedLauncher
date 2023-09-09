using System.IO;

namespace C2GUILauncher {
    static class GameLaunchHelpers {
        /// <summary>
        /// The original launcher is used to launch the game with no mods.
        /// </summary>
        public static ProcessLauncher VanillaLauncher { get; } = new ProcessLauncher(FilePaths.OriginalLauncherPath, FilePaths.RootDir);

        /// <summary>
        /// The modded launcher is used to launch the game with mods. The DLLs here are the relative paths to the DLLs that are to be injected.
        /// </summary>
        public static ProcessLauncher ModdedLauncher { get; } = new ProcessLauncher(FilePaths.GameBinPath, FilePaths.BinDir);
    }
}
