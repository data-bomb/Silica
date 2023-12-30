/*
Silica Admin Mod
Copyright (C) 2023 by databomb

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

using Newtonsoft.Json;
using System;
using MelonLoader.Utils;
using MelonLoader;

#if NET6_0
using Il2CppSystem.Collections.Generic;
#else
using System.Collections.Generic;
#endif

namespace SilicaAdminMod
{
    public class AdminFile
    {
        static readonly String adminFilePath = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "admins.json");

        public static void Update(List<Admin> theAdminList)
        {
            // convert back to json string
            String JsonRaw = JsonConvert.SerializeObject(theAdminList, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(adminFilePath, JsonRaw);
        }

        public static List<Admin> Initialize()
        {
            if (!System.IO.File.Exists(adminFilePath))
            {
                MelonLogger.Warning("Did not find admins.json file. No admin entries loaded.");
                return new List<Admin>();
            }

            // Open the stream and read it back.
            System.IO.StreamReader adminFileStream = System.IO.File.OpenText(adminFilePath);
            using (adminFileStream)
            {
                String JsonRaw = adminFileStream.ReadToEnd();
                if (JsonRaw == null)
                {
                    MelonLogger.Warning("The admins.json read as empty. No admins loaded.");
                    return new List<Admin>();
                }
                
                List<Admin>? deserializedList = JsonConvert.DeserializeObject<List<Admin>>(JsonRaw);

                if (deserializedList == null)
                {
                    MelonLogger.Warning("Encountered deserialization error in admins.json file. Ensure file is in valid format (e.g., https://jsonlint.com/)");
                    return new List<Admin>();
                }

                MelonLogger.Msg("Loaded admins.json with " + deserializedList.Count + " admin entries.");
                return deserializedList;
            }
        }
    }
}