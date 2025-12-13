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
using Il2CppSteamworks;
#else
using Steamworks;
#endif

using UnityEngine;
using System;
using SilicaAdminMod;

namespace Si_Logging
{
    public partial class HL_Logging
    {
        // Generate prefix in format "L mm/dd/yyyy - hh:mm:ss:"
        public static string GetLogPrefix()
        {
            DateTime currentDateTime = DateTime.Now;
            string LogPrefix = "L " + currentDateTime.ToString("MM/dd/yyyy - HH:mm:ss: ");
            return LogPrefix;
        }

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

        public static string GetPlayerID(NetworkID networkID)
        {
            return networkID.SteamID.ToString();
        }

        public static string GetGameMode()
        {
            return GameMode.CurrentGameMode.ToString().Split(' ')[0];
        }

        public static string GetGameType(GameModeExt gameModeInstance)
        {
            GameModeExt.ETeamsVersus versusMode = gameModeInstance.TeamsVersus;
            return versusMode.ToString();
        }

        public static string GetStructureName(Structure structure)
        {
            if (structure == null || structure.ObjectInfo == null)
            {
                return "";
            }

            return GetLogNameFromDisplayName(structure.ObjectInfo.DisplayName);
        }

        public static string GetStructureName(Target target)
        {
            if (target == null || target.ObjectInfo == null)
            {
                return "";
            }

            return GetLogNameFromDisplayName(target.ObjectInfo.DisplayName);
        }

        public static string GetStructureName(ConstructionSite site)
        {
            if (site == null || site.ObjectInfo == null)
            {
                return "";
            }

            return GetLogNameFromDisplayName(site.ObjectInfo.DisplayName);
        }

        public static string GetLogPosition(Vector3 position)
        {
            string x = position.x.ToString("F0");
            // make z the vertical axis like a sane person
            string y = position.z.ToString("F0");
            string z = position.y.ToString("F0");

            return $"{x} {y} {z}";
        }

        public static string GetLogResourceType(Resource resourceType)
        {
            return resourceType.ToString().Split('(')[0].Split('_')[1].TrimEnd();
        }


        public static string GetPlayerPosition(Unit victim)
        {
            string victimLogPosition = GetLogPosition(victim.transform.position);
            return $"(victim_position \"{victimLogPosition}\")";
        }

        public static string GetPlayerPosition(Unit victim, GameObject attacker)
        {
            string victimLogPosition = GetLogPosition(victim.transform.position);
            string attackerLogPosition = GetLogPosition(attacker.transform.position);
            return $"(attacker_position \"{attackerLogPosition}\") (victim_position \"{victimLogPosition}\")";
        }

        public static string GetLogNameFromDisplayName(string displayName)
        {
            // remove any spaces or dashes from the display name
            // this is still slightly different than calling ToString() but this should be more reliable with game updates
            return displayName.Replace(" ", "").Replace("-", "");
        }

        public static string GetPlayerID(P2PSessionRequest_t session)
        {
            return session.m_steamIDRemote.ToString();
        }

        public static string GetNameFromObject(GameObject gameObject)
        {
            return gameObject.ToString().Split('(')[0];
        }

        public static string GetNameFromUnit(Unit unit)
        {
            return unit.ToString().Split('(')[0];
        }

        public static string GetConnectionString(P2PSessionRequest_t session)
        {
            string steamID = GetPlayerID(session);

            return $"\"...<><{steamID}><>\"";
        }

        public static string AddKilledWithEntry(Unit unit, GameObject killerObject)
        {
            string killerString = GetNameFromObject(killerObject);
            string victimString = GetNameFromUnit(unit);

            return $"\"{killerString}\" (dmgtype \"\") (victim \"{victimString}\")";
        }

        public static string AddAIVictimLogEntry(Unit unit)
        {
            string victimUnit = GetNameFromUnit(unit);
            string teamName = GetTeamName(unit.Team);

            return $"\"{victimUnit}<><><{teamName}>\"";
        }

        public static string AddAIAttackerLogEntry(GameObject gameObject, Team team)
        {
            string instigator = GetNameFromObject(gameObject);
            string teamName = GetTeamName(team);

            return $"\"{instigator}<><><{teamName}>\"";
        }

        public static string GetTeamName(Player player)
        {
            if (player.Team == null)
            {
                return string.Empty;
            }

            return player.Team.TeamShortName;
        }

        public static string GetTeamName(Team team)
        {
            if (team == null)
            {
                return string.Empty;
            }

            return team.TeamShortName;
        }

        public static string GetTeamName(int index)
        {
            if (index < 0 || index > (int)SiConstants.ETeam.Sol)
            {
                return string.Empty;
            }

            if (index == (int)SiConstants.ETeam.Alien)
            {
                return "Alien";
            }
            else if (index == (int)SiConstants.ETeam.Wildlife)
            {
                return "Wildlife";
            }
            else if (index == (int)SiConstants.ETeam.Gamemaster)
            {
                return "Gamemaster";
            }
            else if (index == (int)SiConstants.ETeam.Centauri)
            {
                return "Centauri";
            }
            else if (index == (int)SiConstants.ETeam.Sol)
            {
                return "Sol";
            }

            return string.Empty;
        }

        public static string AddAIConsoleEntry()
        {
            return $"<b>AI</b>";
        }

        public static string AddPlayerLogEntry(Player? player)
        {
            if (player == null)
            {
                return String.Empty;
            }

            int userId = GetUserId(player);
            string steamId = GetPlayerID(player);
            string teamName = GetTeamName(player);

            return $"\"{player.PlayerName}<{userId}><{steamId}><{teamName}>\"";
        }

        public static string AddPlayerLogEntry(Player? player, string teamOverride)
        {
            if (player == null)
            {
                return String.Empty;
            }

            int userId = GetUserId(player);
            string steamId = GetPlayerID(player);
            string teamName = teamOverride;

            return $"\"{player.PlayerName}<{userId}><{steamId}><{teamName}>\"";
        }

        public static string AddPlayerConsoleEntry(Player? player)
        {
            if (player == null)
            {
                return String.Empty;
            }

            return $"<b>{HelperMethods.GetTeamColor(player)}{player.PlayerName}</color></b>";
        }

        public static string GetRoleName(GameModeExt.ETeamRole role)
        {
            if (role == GameModeExt.ETeamRole.COMMANDER)
            {
                return "Commander";
            }
            else if (role == GameModeExt.ETeamRole.UNIT)
            {
                return "Infantry";
            }

            return "None";
        }
    }
}
