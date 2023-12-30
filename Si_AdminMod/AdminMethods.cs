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

namespace SilicaAdminMod
{
    public class AdminMethods
    {
        public static Admin? FindAdminFromSteamId(long steamId)
        {
            foreach (Admin admin in SiAdminMod.AdminList)
            {
                if (admin.SteamId == steamId)
                {
                    return admin;
                }
            }

            return null;
        }

        public static AdminCommand? FindAdminCommandFromString(String commandText)
        {
            foreach (AdminCommand command in SiAdminMod.AdminCommands)
            {
                if (command.AdminCommandText == commandText)
                {
                    return command;
                }
            }
            return null;
        }

        public static bool PowerInPowers(Power power, Power powers)
        {
            Power powerLess = powers & (power | Power.Root);
            if (powerLess != Power.None)
            {
                return true;
            }

            return false;
        }
    }
}