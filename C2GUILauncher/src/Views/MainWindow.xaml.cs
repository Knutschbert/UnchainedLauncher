﻿using C2GUILauncher.JsonModels;
using C2GUILauncher.Mods;
using C2GUILauncher.ViewModels;
using log4net;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace C2GUILauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindow : Window {

        enum InstallResult {
            Rejected,
            Installed,
            NoTarget,
            Failed
        }

        public ModListViewModel ModManagerViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public LauncherViewModel LauncherViewModel { get; }
        public ServerLauncherViewModel ServerSettingsViewModel { get; }
        public bool DisableSaveSettings { get; set; }

        private readonly ModManager ModManager;

        private static readonly ILog logger = LogManager.GetLogger(nameof(MainWindow));

        /// <summary>
        /// Shows the install dialog for the given install type.
        /// Returns null if there is nothing to do, true if the user wants to install, and false if they don't.
        /// </summary>
        /// <param name="currentDir"></param>
        /// <param name="targetDir"></param>
        /// <param name="installType"></param>
        /// <returns></returns>
        private static InstallResult ShowInstallRequestFor(string currentDir, string? targetDir, InstallationType installType) {
            if (targetDir == null) return InstallResult.NoTarget;
            if (currentDir == targetDir) return InstallResult.Installed;

            var installTypeStr = installType == InstallationType.Steam ? "Steam" : "Epic Games";

            MessageBoxResult res = MessageBox.Show(
                $"Detected install location ({installTypeStr}):\n\n" +
                $"{targetDir} \n\n" +
                $"Install the launcher for {installTypeStr} at this location?\n\n"
                , $"Install Launcher ({installTypeStr})", MessageBoxButton.YesNo);

            if (res == MessageBoxResult.Yes) {
                return InstallerViewModel.AttemptInstall(targetDir, installType) ? InstallResult.Installed : InstallResult.Failed;
            } else {
                return InstallResult.Rejected;
            }
        }

        /// <summary>
        /// Attempts an install for either steam or egs
        /// Returns true if installation was successful and we need a restart
        /// Returns false if installation was unsuccessful and we don't need a restart
        /// </summary>
        /// <param name="steamDir"></param>
        /// <param name="egsDir"></param>
        /// <returns></returns>
        private InstallResult Install(string? steamDir, string? egsDir) {
            string curDir = Directory.GetCurrentDirectory();

            // If we're already in the install dir, we don't need to do anything. 
            // If a TBL dir is in the current dir, and we're not in the source code dir, we're probably in the install dir.
            var alreadyInInstallDir = steamDir == curDir || egsDir == curDir || (Directory.Exists(Path.Combine(curDir, "TBL")) && !curDir.Contains("C2GUILauncher\\C2GUILauncher"));

            if (alreadyInInstallDir)
                return InstallerViewModel.AttemptInstall("", InstallationType.NotSet) ? InstallResult.Installed : InstallResult.Failed;

            InstallResult steamInstallResult = ShowInstallRequestFor(curDir, steamDir, InstallationType.Steam);
            if (steamInstallResult == InstallResult.Installed || steamInstallResult == InstallResult.Failed) return steamInstallResult;

            InstallResult egsInstallResult = ShowInstallRequestFor(curDir, egsDir, InstallationType.EpicGamesStore);
            if (egsInstallResult == InstallResult.Installed || egsInstallResult == InstallResult.Failed) return egsInstallResult;

            return InstallResult.Rejected;
        }

        // DESNOTE(2023-09-15, jbarber):
        //     This is a warning that we aren't initializing all of ourg
        //     non-nullable members of this class. The only time we fail to
        //     initialize things is if we're closing immediately, so its not a
        //     problem.
#pragma warning disable CS8618
        public MainWindow() {
            try {

                InitializeComponent();

                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

                logger.Info("Checking if installation is necessary...");
                var egsDir = InstallHelpers.FindEGSDir();
                var steamDir = InstallHelpers.FindSteamDir();
                var curDir = Directory.GetCurrentDirectory();
                var exeName = Process.GetCurrentProcess().ProcessName;

                var outsideVisualStudio = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VisualStudioEdition"));

                // DESNOTE(2023-08-28, jbarber): We check for the exe name here 
                // because we assume we are already installed in that case. We 
                // check if we're in steam already, because steam users may 
                // install the launcher without it being named Chivalry2Launcher;
                // it just needs to be in the steam dir to function.
                if ((exeName != "Chivalry2Launcher" && exeName != "Chivalry2-Win64-Shipping") && !Path.Equals(curDir, steamDir) && outsideVisualStudio) {
                    logger.Info("Running installation process");

                    var installResult = Install(steamDir, egsDir);

                    switch (installResult) {
                        case InstallResult.Rejected:
                            logger.Info("Installation rejected");
                            MessageBox.Show($"Installation rejected. Running launcher in-place.");
                            break;
                        case InstallResult.Installed:
                            logger.Info("Installed successfully");
                            MessageBox.Show($"Launcher installation is complete. Launch Chivalry 2 as you normally would.");
                            this.Close();
                            return;
                        case InstallResult.Failed:
                            logger.Info("Installation failed");
                            MessageBox.Show($"Launcher installation failed.");
                            this.Close();
                            return;
                        case InstallResult.NoTarget:
                            // This case should be impossible, but lets handle it here just in case.
                            logger.Info("No installation target found... This should never happen");
                            MessageBox.Show($"Launcher installation failed because no target was found. Please install manually");
                            this.Close();
                            return;
                    }

                } else {
                    logger.Info("Already installed.");
                }

                this.ModManager = ModManager.ForRegistry(
                    "Chiv2-Community",
                    "C2ModRegistry",
                    FilePaths.PakDir
                );

                var chiv2Launcher = new Chivalry2Launcher(ModManager);

                this.SettingsViewModel = SettingsViewModel.LoadSettings(this);
                if (this.SettingsViewModel.InstallationType == InstallationType.NotSet) {
                    if (Path.Equals(curDir, egsDir))
                        this.SettingsViewModel.InstallationType = InstallationType.EpicGamesStore;
                    else if (Path.Equals(curDir, steamDir))
                        this.SettingsViewModel.InstallationType = InstallationType.Steam;
                }

                this.LauncherViewModel = new LauncherViewModel(this, SettingsViewModel, ModManager, chiv2Launcher);
                this.ServerSettingsViewModel = ServerLauncherViewModel.LoadSettings(LauncherViewModel, SettingsViewModel, ModManager);

                this.ModManagerViewModel = new ModListViewModel(ModManager);

                this.SettingsTab.DataContext = this.SettingsViewModel;
                this.ModManagerTab.DataContext = this.ModManagerViewModel;
                this.LauncherTab.DataContext = this.LauncherViewModel;
                this.ServerSettingsTab.DataContext = this.ServerSettingsViewModel;

                DisableSaveSettings = false;
                this.Closed += MainWindow_Closed;

                var EnvArgs = Environment.GetCommandLineArgs();

                if (EnvArgs.Contains("--startvanilla"))
                    LauncherViewModel.LaunchVanilla();
                else if (EnvArgs.Contains("--startmodded"))
                    LauncherViewModel.LaunchModdedVanilla();
                else if (EnvArgs.Contains("--startunchained"))
                    LauncherViewModel.LaunchModded();


            } catch (Exception e) {
                logger.Error("Initialization Failed", e);
                throw;
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e) {
            if (DisableSaveSettings) return;

            this.SettingsViewModel.SaveSettings();
            this.ServerSettingsViewModel.SaveSettings();
        }

        private bool modManagerLoaded = false;
        private TabItem? lastTab = null;
        private void TabSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Tabs.SelectedItem == null || Tabs.SelectedItem == lastTab) return;

            lastTab = (TabItem)Tabs.SelectedItem;

            logger.Info("Opened Tab: " + ((TabItem)Tabs.SelectedItem).Header.ToString());
            if (!modManagerLoaded && ModManagerTab.IsSelected) {
                try {
                    ModManagerViewModel.RefreshModListCommand.Execute(null);
                    modManagerLoaded = true;
                } catch (Exception ex) {
                    logger.Error(ex);
                }
            }
        }
    }

}
