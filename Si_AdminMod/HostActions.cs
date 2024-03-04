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

using System;

#if NET6_0
using Il2Cpp;
#endif

namespace SilicaAdminMod
{
    public class HostAction
    {
        public static bool AddAdmin(Player player, String powerText, byte level)
        {
            long playerSteamId = long.Parse(player.ToString().Split('_')[1]);

            if (SiAdminMod.AdminList == null)
            {
                return false;
            }

            // check if we have a match before adding more details
            if (AdminMethods.FindAdminFromSteamId(playerSteamId) == null)
            {
                return false;
            }

            Admin admin = new Admin
            {
                Name = player.PlayerName,
                SteamId = playerSteamId,
                Level = level,
                Powers = HelperMethods.PowerTextToPower(powerText),
                CreatedOn = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
            };
            admin.LastModifiedOn = admin.CreatedOn;

            SiAdminMod.AdminList.Add(admin);
            AdminFile.Update(SiAdminMod.AdminList);
            return true;
        }
    }
}