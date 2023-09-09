﻿using C2GUILauncher.JsonModels;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Octokit;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace C2GUILauncher.ViewModels {
    [AddINotifyPropertyChangedInterface]
    public class SettingsViewModel {
        public static readonly int[] version = { 0, 4, 1 };

        private static readonly string SettingsFilePath = $"{FilePaths.ModCachePath}\\unchained_launcher_settings.json";

        public InstallationType InstallationType { get; set; }
        public bool EnablePluginLogging { get; set; }
        public bool EnablePluginAutomaticUpdates { get; set; }
        public string CLIArgs { get; set; }

        public string CurrentVersion {
            get => "v" + string.Join(".", version.Select(x => x.ToString()));
        }

        public ICommand CheckForUpdateCommand { get; }

        public static IEnumerable<InstallationType> AllInstallationTypes {
            get { return Enum.GetValues(typeof(InstallationType)).Cast<InstallationType>(); }
        }

        public SettingsViewModel(InstallationType installationType, bool enablePluginLogging, bool enablePluginAutomaticUpdates, string cliArgs) {
            InstallationType = installationType;
            EnablePluginLogging = enablePluginLogging;
            EnablePluginAutomaticUpdates = enablePluginAutomaticUpdates;
            CLIArgs = cliArgs;

            CheckForUpdateCommand = new RelayCommand<Window?>(CheckForUpdate);
        }

        public static SettingsViewModel LoadSettings() {
            var cliArgsList = Environment.GetCommandLineArgs();
            var cliArgs = cliArgsList.Length > 1 ? Environment.GetCommandLineArgs().Skip(1).Aggregate((x, y) => x + " " + y) : "";

            var defaultSettings = new SettingsViewModel(InstallationType.NotSet, false, true, cliArgs);

            if (!File.Exists(SettingsFilePath))
                return defaultSettings;

            var savedSettings = JsonConvert.DeserializeObject<SavedSettings>(File.ReadAllText(SettingsFilePath));

            if (savedSettings is not null) {
                return new SettingsViewModel(savedSettings.InstallationType, savedSettings.EnablePluginLogging, savedSettings.EnablePluginAutomaticUpdates, cliArgs);
            } else {
                MessageBox.Show("Settings malformed or from an unsupported old version. Loading defaults.");
                return defaultSettings;
            }
        }

        public void SaveSettings() {
            var settings = new SavedSettings(InstallationType, EnablePluginLogging, EnablePluginAutomaticUpdates);
            var json = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);

            if (!Directory.Exists(FilePaths.ModCachePath))
                Directory.CreateDirectory(FilePaths.ModCachePath);

            File.WriteAllText(SettingsFilePath, json);
        }

        // TODO: Somehow generalize the updater and installer
        private void CheckForUpdate(Window? window) {
            var github = new GitHubClient(new ProductHeaderValue("C2GUILauncher"));

            var repoCall = github.Repository.Release.GetLatest(667470779); //C2GUILauncher repo id
            repoCall.Wait();
            if (!repoCall.IsCompletedSuccessfully) {
                MessageBox.Show("Could not connect to github to retrieve latest version information:\n" + repoCall?.Exception?.Message);
                return;
            }
            var latestInfo = repoCall.Result;
            string tagName = latestInfo.TagName;
            int[] latest = tagName
                .Split(".")
                .Select(
                    s => int.Parse( //parse as int
                        string.Concat(s.Where(c => char.IsDigit(c))) //filter out non-numeric characters
                    )
                ).ToArray(); //join to array representing version
            //if latest is newer than current version
            if (latest[0] > version[0] ||
                latest[1] > version[1] ||
                latest[2] > version[2]) {
                string currentVersionString = string.Join(".", version.Select(i => i.ToString()));
                MessageBoxResult dialogResult = MessageBox.Show(
                    $"A newer version was found.\n " +
                    $"{tagName} > v{currentVersionString}\n\n" +
                    $"Download the new update?",
                    "Update?", MessageBoxButton.YesNo);

                if (dialogResult == MessageBoxResult.No) {
                    return;
                } else if (dialogResult == MessageBoxResult.Yes) {
                    try {
                        var url = latestInfo.Assets.Where(
                                    a => a.Name.Contains("C2GUILauncher.exe") //find the launcher exe
                                ).First().BrowserDownloadUrl; //get the download URL
                        var newDownloadTask = HttpHelpers.DownloadFileAsync(url, "C2GUILauncher.exe");
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        string exeDir = System.IO.Path.GetDirectoryName(exePath) ?? "";

                        newDownloadTask.Task.Wait();
                        if (!repoCall.IsCompletedSuccessfully) {
                            MessageBox.Show("Failed to download the new version:\n" + newDownloadTask?.Task.Exception?.Message);
                            return;
                        }

                        Process pwsh = new Process();
                        pwsh.StartInfo.FileName = "powershell.exe";
                        var commandLinePass = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
                        //relative paths here are safe. This will never move the executable
                        //to a different directory
                        string powershellCommand =
                        $"Wait-Process -Id {Environment.ProcessId}; " +
                        $"Start-Sleep -Milliseconds 500; " +
                        $"Move-Item -Force C2GUILauncher.exe gamelaunchhelper.exe;" +
                        $"Start-Sleep -Milliseconds 500; " +
                        $".\\gamelaunchhelper.exe {commandLinePass}";
                        pwsh.StartInfo.Arguments = $"-Command \"{powershellCommand}\"";
                        pwsh.StartInfo.CreateNoWindow = true;
                        pwsh.Start();
                        MessageBox.Show("The launcher will now close and start the new version. No further action must be taken.");
                        window?.Close(); //close the program
                        return;
                    } catch (Exception ex) {
                        MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                    }

                }
            } else {
                MessageBox.Show("You are currently running the latest version.");
            }
        }

    }
}
