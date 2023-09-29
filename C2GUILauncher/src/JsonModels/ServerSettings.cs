﻿using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;

namespace C2GUILauncher.JsonModels {
    public record ServerSettings(
        [property: JsonProperty("server_name")] string? ServerName,
        [property: JsonProperty("server_description")] string? ServerDescription,
        [property: JsonProperty("server_list")] string? ServerList,
        [property: JsonProperty("selected_map")] string? SelectedMap,
        [property: JsonProperty("game_port")] short? GamePort,
        [property: JsonProperty("rcon_port")] short? RconPort,
        [property: JsonProperty("a2s_port")] short? A2sPort,
        [property: JsonProperty("ping_port")] short? PingPort,
        [property: JsonProperty("show_in_server_browser")] bool? ShowInServerBrowser,
        [property: JsonProperty("selected_bool_options")] ObservableCollection<string>? SelectedBoolOptionsList,
        [property: JsonProperty("selected_mods")] ObservableCollection<string>? SelectedModsList
    );
}
