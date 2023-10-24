﻿using LanguageExt;
using log4net;

using UnchainedLauncher.Core.JsonModels.Metadata.V3;
using UnchainedLauncher.Core.Mods.Registry.Resolver;
using UnchainedLauncher.Core.Utilities;

namespace UnchainedLauncher.Core.Mods.Registry {
    public abstract class JsonRegistry : IModRegistry {
        protected static readonly ILog logger = LogManager.GetLogger(nameof(JsonRegistry));

        public abstract ModRegistryDownloader ModRegistryDownloader { get; }
        public abstract string Name { get; }

        /// <summary>
        /// Gets the mod metadata located at the given path within the registry
        /// </summary>
        /// <param name="modPath"></param>
        /// <returns></returns>
        public EitherAsync<RegistryMetadataException, Mod> GetModMetadata(string modPath) {
            return GetModMetadataString(modPath).Bind(json =>
                // Try to deserialize as V3 first
                JsonHelpers.Deserialize<Mod>(json).RecoverWith(e => {
                    // If that fails, try to deserialize as V2
                    logger.Warn("Falling back to V2 deserialization: " + e?.Message ?? "unknown failure");
                    return JsonHelpers.Deserialize<JsonModels.Metadata.V2.Mod>(json)
                        .Select(Mod.FromV2);
                }).RecoverWith(e => {
                    // If that fails, try to deserialize as V1
                    logger.Warn("Falling back to V1 deserialization" + e?.Message ?? "unknown failure");
                    return JsonHelpers.Deserialize<JsonModels.Metadata.V1.Mod>(json)
                        .Select(JsonModels.Metadata.V2.Mod.FromV1)
                        .Select(Mod.FromV2);
                })
                .ToEither()
                .ToAsync()
                .MapLeft(err => new RegistryMetadataException(modPath, err))
            );
        }

        public abstract Task<GetAllModsResult> GetAllMods();
        public abstract EitherAsync<RegistryMetadataException, string> GetModMetadataString(string modPath);
    }
}