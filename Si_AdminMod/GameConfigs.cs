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
using Tomlet.Models;
using Tomlet.Exceptions;

namespace SilicaAdminMod
{
    public partial class SiAdminMod
    {
        private static void LoadConfigEntries(TomlDocument configToml)
        {
            if (MelonPreferences.Categories.Count > 0)
            {
                foreach (MelonPreferences_Category category in MelonPreferences.Categories)
                {
                    if (category.Entries.Count <= 0)
                    {
                        continue;
                    }

                    // attempt to find any updated preferences, ignore anything that is not found
                    foreach (MelonPreferences_Entry entry in category.Entries)
                    {
                        SetupEntryFromRawValue(configToml, entry);
                    }
                }
            }
        }

        // borrowed from https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader/Preferences/IO/File.cs
        private static string QuoteKey(string key) =>
            key.Contains('"')
                ? $"'{key}'"
                : $"\"{key}\"";

        // modified from https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader/Preferences/IO/File.cs
        public static void SetupEntryFromRawValue(TomlDocument config, MelonPreferences_Entry entry)
        {
            lock (config)
            {
                try
                {
                    var categoryTable = config.GetSubTable(entry.Category.Identifier);
                    var value = categoryTable.GetValue(QuoteKey(entry.Identifier));
                    entry.Load(value);
                }
                catch (TomlTypeMismatchException)
                {
                    //Ignore
                }
                catch (TomlNoSuchValueException)
                {
                    //Ignore
                }
            }
        }
    }
}