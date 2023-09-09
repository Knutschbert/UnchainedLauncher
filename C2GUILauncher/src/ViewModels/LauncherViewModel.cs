﻿using C2GUILauncher.JsonModels;
using C2GUILauncher.Mods;
using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace C2GUILauncher.ViewModels {

    [AddINotifyPropertyChangedInterface]
    public class LauncherViewModel {
        public ICommand LaunchVanillaCommand { get; }
        public ICommand LaunchModdedCommand { get; }

        private SettingsViewModel Settings { get; }

        private ModManager ModManager { get; }

        public bool CanClick { get; set; }

        private bool CLIArgsModified { get; set; }



        public LauncherViewModel(SettingsViewModel settings, ModManager modManager) {
            CanClick = true;

            this.Settings = settings;
            this.ModManager = modManager;

            this.LaunchVanillaCommand = new RelayCommand(LaunchVanilla);
            this.LaunchModdedCommand = new RelayCommand(LaunchModded);
        }

        private void LaunchVanilla() {
            try {
                // For a vanilla launch we need to pass the args through to the vanilla launcher.
                // Skip the first arg which is the path to the exe.
                var args = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
                GameLaunchHelpers.VanillaLauncher.Launch(args);
                CanClick = false;
            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private void LaunchModded() {
            // For a modded installation we need to download the mod files and then launch via the modded launcher.
            // For steam installations, args do not get passed through.

            // Get the installation type. If auto detect fails, exit this function.
            var installationType = GetInstallationType();
            if (installationType == InstallationType.NotSet) return;

            // Pass args through if the args box has been modified, or if we're an EGS install
            var shouldSendArgs = installationType == InstallationType.EpicGamesStore || this.CLIArgsModified;

            // pass empty string for args, if we shouldn't send any.
            var args = shouldSendArgs ? this.Settings.CLIArgs : "";

            // Download the mod files, potentially using debug dlls
            var launchThread = new Thread(async () => {
                try {
                    if (this.Settings.EnablePluginAutomaticUpdates) {
                        List<DownloadTask> downloadTasks = this.ModManager.DownloadModFiles(this.Settings.EnablePluginLogging).ToList();
                        await Task.WhenAll(downloadTasks.Select(x => x.Task));
                    }
                    var dlls = Directory.EnumerateFiles(FilePaths.PluginDir, "*.dll").ToArray();
                    GameLaunchHelpers.ModdedLauncher.Dlls = dlls;
                    var process = GameLaunchHelpers.ModdedLauncher.Launch(args);

                    await process.WaitForExitAsync();
                    Environment.Exit(0);
                } catch (Exception ex) {
                    MessageBox.Show(ex.ToString());
                }
            });

            launchThread.Start();
            CanClick = false;
        }

        private InstallationType GetInstallationType() {
            var installationType = this.Settings.InstallationType;
            if (installationType == InstallationType.NotSet) {
                installationType = InstallationTypeUtils.AutoDetectInstallationType();
                if (installationType == InstallationType.NotSet) {
                    MessageBox.Show("Could not detect installation type. Please select one manually.");
                }
            }

            return installationType;
        }



        private static class InstallationTypeUtils {
            const string SteamPathSearchString = "Steam";
            const string EpicGamesPathSearchString = "Epic Games";

            public static InstallationType AutoDetectInstallationType() {
                var currentDir = Directory.GetCurrentDirectory();
                return currentDir switch {
                    var _ when currentDir.Contains(SteamPathSearchString) => InstallationType.Steam,
                    var _ when currentDir.Contains(EpicGamesPathSearchString) => InstallationType.EpicGamesStore,
                    _ => InstallationType.NotSet,
                };
            }
        }
    }
}
