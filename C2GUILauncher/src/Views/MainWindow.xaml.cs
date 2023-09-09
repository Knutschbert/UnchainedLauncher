﻿using C2GUILauncher.JsonModels;
using C2GUILauncher.Mods;
using C2GUILauncher.ViewModels;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

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

        private readonly ModManager ModManager;

        /// <summary>
        /// Shows the install dialog for the given install type.
        /// Returns null if there is nothing to do, true if the user wants to install, and false if they don't.
        /// </summary>
        /// <param name="currentDir"></param>
        /// <param name="targetDir"></param>
        /// <param name="installType"></param>
        /// <returns></returns>
        private InstallResult ShowInstallRequestFor(string currentDir, string? targetDir, InstallationType installType) {
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

        public MainWindow() {
            InitializeComponent();
            var egsDir = InstallHelpers.FindEGSDir();
            var steamDir = InstallHelpers.FindSteamDir();
            var curDir = Directory.GetCurrentDirectory();
            var exeName = Process.GetCurrentProcess().ProcessName;

            // DESNOTE(2023-08-28, jbarber): We check for the exe name here 
            // because we assume we are already installed in that case. We 
            // check if we're in steam already, because steam users may 
            // install the launcher without it being named gamelaunchhelper;
            // it just needs to be in the steam dir to function.
            if (exeName != "gamelaunchhelper" && !Path.Equals(curDir, steamDir)) {
                var installResult = Install(steamDir, egsDir);

                switch (installResult) {
                    case InstallResult.Rejected:
                        MessageBox.Show($"Installation rejected. Running launcher in-place.");
                        break;
                    case InstallResult.Installed:
                        MessageBox.Show($"Launcher installation is complete.");
                        this.Close();
                        break;
                    case InstallResult.Failed:
                        MessageBox.Show($"Launcher installation failed.");
                        this.Close();
                        break;
                    case InstallResult.NoTarget:
                        // This case should be impossible, but lets handle it here just in case.
                        MessageBox.Show($"Launcher installation failed because no target was found. Please install manually");
                        this.Close();
                        break;
                }
            }

            this.ModManager = ModManager.ForRegistry(
                "Chiv2-Community",
                "C2ModRegistry",
                "TBL\\Content\\Paks"
            );

            this.SettingsViewModel = SettingsViewModel.LoadSettings();
            if (this.SettingsViewModel.InstallationType == InstallationType.NotSet) {
                if (Path.Equals(curDir, egsDir))
                    this.SettingsViewModel.InstallationType = InstallationType.EpicGamesStore;
                else if (Path.Equals(curDir, steamDir))
                    this.SettingsViewModel.InstallationType = InstallationType.Steam;
            }
            this.ModManagerViewModel = new ModListViewModel(ModManager);
            this.LauncherViewModel = new LauncherViewModel(this, SettingsViewModel, ModManager);

            this.SettingsTab.DataContext = this.SettingsViewModel;
            this.ModManagerTab.DataContext = this.ModManagerViewModel;
            this.LauncherTab.DataContext = this.LauncherViewModel;

            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e) {
            this.SettingsViewModel.SaveSettings();
        }
    }

}
