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

using MelonLoader;
using System;
using MelonLoader.Utils;
using Tomlet;
using Tomlet.Models;

namespace SilicaAdminMod
{
    public partial class SiAdminMod
    {
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (!IsMapNameValid(sceneName))
                {
                    return;
                }

                if (!System.IO.Directory.Exists(GetMapConfigFileFullDirectory()))
                {
                    MelonLogger.Msg("Creating map config file directory at: " + GetMapConfigFileFullDirectory());
                    System.IO.Directory.CreateDirectory(GetMapConfigFileFullDirectory());
                }

                string mapConfigFullPath = GetMapConfigFilePath(sceneName);
                if (!System.IO.File.Exists(mapConfigFullPath))
                {
                    MelonLogger.Msg("Map config file [" + GetMapConfigFileSubDirectory() + sceneName + ".cfg] not found. Skipping.");
                    return;
                }

                TomlDocument mapConfigToml = TomlParser.ParseFile(mapConfigFullPath);
                LoadConfigEntries(mapConfigToml);

                MelonLogger.Msg($"MelonPreferences Loaded from {mapConfigFullPath}");
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run map config file.");
            }
        }

        public static bool IsMapNameValid(string sceneName)
        {
            if (sceneName == "Intro" || sceneName == "MainMenu" || sceneName == "Loading" || sceneName.Length < 2)
            {
                return false;
            }

            return true;
        }

        public static string GetMapConfigFileSubDirectory()
        {
            return @"cfg\maps\";
        }

        public static string GetMapConfigFileFullDirectory()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, GetMapConfigFileSubDirectory());
        }

        public static string GetMapConfigFilePath(string sceneName)
        {
            string fileName = sceneName + ".cfg";
            return System.IO.Path.Combine(GetMapConfigFileFullDirectory(), fileName);
        }
    }
}