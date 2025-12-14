/*
Silica Admin Mod
Copyright (C) 2025 by databomb

* Description *
Provides basic admin mod system to allow additional admins beyond
the host.

* License *
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using MelonLoader;
using System;
using MelonLoader.Utils;
using Tomlet;
using Tomlet.Models;
using UnityEngine;

namespace SilicaAdminMod
{
    public partial class SiAdminMod
    {
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Prefix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    GameModeExt gameModeInstance = GameObject.FindFirstObjectByType<GameModeExt>();
                    GameModeExt.ETeamsVersus versusMode = gameModeInstance.TeamsVersus;
                    string gametype = versusMode.ToString();

                    if (!System.IO.Directory.Exists(GetGameTypeConfigFileFullDirectory()))
                    {
                        MelonLogger.Msg("Creating gametype config file directory at: " + GetMapConfigFileFullDirectory());
                        System.IO.Directory.CreateDirectory(GetGameTypeConfigFileFullDirectory());
                    }

                    string gameTypeConfigFullPath = GetGameTypeConfigFilePath(gametype);
                    if (!System.IO.File.Exists(gameTypeConfigFullPath))
                    {
                        MelonLogger.Msg("Map config file [" + GetGameTypeConfigFileSubDirectory() + gametype + ".cfg] not found. Skipping.");
                        return;
                    }

                    TomlDocument mapConfigToml = TomlParser.ParseFile(gameTypeConfigFullPath);
                    LoadConfigEntries(mapConfigToml);

                    MelonLogger.Msg($"MelonPreferences Loaded from {gameTypeConfigFullPath}");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        public static string GetGameTypeConfigFileSubDirectory()
        {
            return @"cfg\gametype\";
        }

        public static string GetGameTypeConfigFileFullDirectory()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, GetGameTypeConfigFileSubDirectory());
        }

        public static string GetGameTypeConfigFilePath(string gameTypeName)
        {
            string fileName = gameTypeName + ".cfg";
            return System.IO.Path.Combine(GetGameTypeConfigFileSubDirectory(), fileName);
        }
    }
}