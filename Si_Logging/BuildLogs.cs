/*
 Silica Logging Mod
 Copyright (C) 2023-2025 by databomb
 
 * Description *
 For Silica servers, creates a log file with console replication
 in the Half-Life log standard format.

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

using UnityEngine;
using System;
using SilicaAdminMod;

namespace Si_Logging
{
    public partial class HL_Logging
    {
        public static int GetUserId(Player? player)
        {
            if (player == null)
            {
                return -1;
            }

            return Math.Abs(player.GetInstanceID());
        }
        public static string GetPlayerID(Player player)
        {
            return player.PlayerID.SteamID.m_SteamID.ToString();
        }

        public static string GetNameFromObject(GameObject gameObject)
        {
            return gameObject.ToString().Split('(')[0];
        }

        public static string GetNameFromUnit(Unit unit)
        {
            return unit.ToString().Split('(')[0];
        }

        public static string AddKilledWithEntry(Unit unit, GameObject killerObject)
        {
            string killerString = GetNameFromObject(killerObject);
            string victimString = GetNameFromUnit(unit);

            return $"\"{killerString}\" (dmgtype \"\") (victim \"{victimString}\")";
        }

        public static string AddPlayerLogEntry(Player? player)
        {
            if (player == null)
            {
                return String.Empty;
            }

            int userId = GetUserId(player);
            string steamId = GetPlayerID(player);

            return $"\"{player.PlayerName}<{userId}><{steamId}><{player.Team.TeamShortName}>\"";
        }

        public static string AddPlayerConsoleEntry(Player? player)
        {
            if (player == null)
            {
                return String.Empty;
            }

            return $"<b>{HelperMethods.GetTeamColor(player)}{player.PlayerName}</color></b>";
        }
    }
}
