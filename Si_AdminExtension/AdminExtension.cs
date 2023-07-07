/*
Silica Admin Extension System
Copyright (C) 2023 by databomb

* Description *
Provides helper methods, extension methods, and acts as the API 
between other mods and the Admin Mod.

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

using Il2Cpp;
using Il2CppSilica.UI;
using MelonLoader;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(SiAdminMod), "Admin Extension", "1.1.3", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace AdminExtension
{
    public enum Power
    {
        None = 0,
        Slot = (1 << 0),
        Vote = (1 << 1),
        Kick = (1 << 2),
        Ban = (1 << 3),
        Unban = (1 << 4),
        Slay = (1 << 5),
        Map = (1 << 6),
        Cheat = (1 << 7),
        Commander = (1 << 8),
        Skip = (1 << 9),
        End = (1 << 10),
        Eject = (1 << 11),
        Mute = (1 << 12),
        MuteForever = (1 << 13),
        Generic = (1 << 14),
        Teams = (1 << 15),
        Custom1 = (1 << 16),
        Custom2 = (1 << 17),
        Custom3 = (1 << 18),
        Custom4 = (1 << 19),
        Reserved1 = (1 << 21),
        Reserved2 = (1 << 22),
        Reserved3 = (1 << 23),
        Reserved4 = (1 << 24),
        Rcon = (1 << 25),
        Root = (1 << 26)
    }

    public static class HelperMethods
    {
        const string defaultColor = "<color=#DDE98C>";
        const string chatPrefix = "<b>" + defaultColor + "[<color=#DFA725>SAM" + defaultColor + "]</b> ";

        public delegate void CommandCallback(Il2Cpp.Player callerPlayer, String args);

        public static void RegisterAdminCommand(String adminCommand, CommandCallback adminCallback, Power adminPower)
        {
            SiAdminMod.RegisterAdminCommand(adminCommand, adminCallback, adminPower);
        }

        public static void ReplyToCommand(params string[] messages)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            serverPlayer.SendChatMessage(chatPrefix + String.Concat(messages), false);
        }

        public static void ReplyToCommand_Player(Il2Cpp.Player player, params string[] messages)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            serverPlayer.SendChatMessage(chatPrefix + GetTeamColor(player) + player.PlayerName + defaultColor + " " + String.Concat(messages), false);
        }

        public static void AlertAdminActivity(Il2Cpp.Player adminPlayer, Il2Cpp.Player targetPlayer, string action)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            serverPlayer.SendChatMessage(chatPrefix + GetAdminColor() + adminPlayer.PlayerName + defaultColor + " " + action + " " + GetTeamColor(targetPlayer) + targetPlayer.PlayerName, false);
        }

        public static void AlertAdminAction(Il2Cpp.Player adminPlayer, string action)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            serverPlayer.SendChatMessage(chatPrefix + GetAdminColor() + adminPlayer.PlayerName + defaultColor + " " + action, false);
        }

        public static void PrintError(Exception exception, string? message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.Error(error);
        }

        public static Il2Cpp.Player? FindTargetPlayer(String sTarget)
        {
            Il2Cpp.Player? targetPlayer = null;
            int iTargetCount = 0;

            // loop through all players
            Il2CppSystem.Collections.Generic.List<Il2Cpp.Player> players = Il2Cpp.Player.Players;
            int iPlayerCount = players.Count;

            for (int i = 0; i < iPlayerCount; i++)
            {
                if (players[i].PlayerName.Contains(sTarget))
                {
                    iTargetCount++;
                    targetPlayer = players[i];
                }
            }

            if (iTargetCount != 1)
            {
                targetPlayer = null;
            }

            return targetPlayer;
        }

        public static string GetAdminColor()
        {
            return "<color=#DFA725>";
        }

        public static string GetTeamColor(Il2Cpp.Player player)
        {
            if (player == null)
            {
                return "<color=#FFFFFF>";
            }
            Il2Cpp.Team? team = player.m_Team;
            if (team == null)
            {
                return "<color=#FFFFFF>";
            }
            int teamIndex = team.Index;

            switch (teamIndex)
            {
                // Alien
                case 0:
                    return "<color=#70FF70>";
                // Centauri
                case 1:
                    return "<color=#FF7070>";
                // Sol
                case 2:
                    return "<color=#7070FF>";
                default:
                    return "<color=#FFFFFF>";
            }
        }

        public static Power PowerTextToPower(String powerText)
        {
            Power powers = Power.None;
            if (powerText.Contains('a'))
            {
                powers = powers | Power.Slot;
            }
            if (powerText.Contains('b'))
            {
                powers = powers | Power.Vote;
            }
            if (powerText.Contains('c'))
            {
                powers = powers | Power.Kick;
            }
            if (powerText.Contains('d'))
            {
                powers = powers | Power.Ban;
            }
            if (powerText.Contains('e'))
            {
                powers = powers | Power.Unban;
            }
            if (powerText.Contains('f'))
            {
                powers = powers | Power.Slay;
            }
            if (powerText.Contains('g'))
            {
                powers = powers | Power.Map;
            }
            if (powerText.Contains('h'))
            {
                powers = powers | Power.Cheat;
            }
            if (powerText.Contains('i'))
            {
                powers = powers | Power.Commander;
            }
            if (powerText.Contains('j'))
            {
                powers = powers | Power.Skip;
            }
            if (powerText.Contains('k'))
            {
                powers = powers | Power.End;
            }
            if (powerText.Contains('l'))
            {
                powers = powers | Power.Eject;
            }
            if (powerText.Contains('m'))
            {
                powers = powers | Power.Mute;
            }
            if (powerText.Contains('n'))
            {
                powers = powers | Power.MuteForever;
            }
            if (powerText.Contains('o'))
            {
                powers = powers | Power.Generic;
            }
            if (powerText.Contains('p'))
            {
                powers = powers | Power.Teams;
            }
            if (powerText.Contains('q'))
            {
                powers = powers | Power.Custom1;
            }
            if (powerText.Contains('r'))
            {
                powers = powers | Power.Custom2;
            }
            if (powerText.Contains('s'))
            {
                powers = powers | Power.Custom3;
            }
            if (powerText.Contains('t'))
            {
                powers = powers | Power.Custom4;
            }
            if (powerText.Contains('u'))
            {
                powers = powers | Power.Reserved1;
            }
            if (powerText.Contains('v'))
            {
                powers = powers | Power.Reserved2;
            }
            if (powerText.Contains('w'))
            {
                powers = powers | Power.Reserved3;
            }
            if (powerText.Contains('x'))
            {
                powers = powers | Power.Reserved4;
            }
            if (powerText.Contains('y'))
            {
                powers = powers | Power.Rcon;
            }
            if (powerText.Contains('z'))
            {
                powers = powers | Power.Root;
            }

            return powers;
        }
        public static bool KickPlayer(Il2Cpp.Player playerToKick)
        {
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

            if (playerToKick == serverPlayer)
            {
                return false;
            }

            Il2CppSteamworks.CSteamID serverSteam = NetworkGameServer.GetServerID();
            int playerChannel = playerToKick.PlayerChannel;
            Il2CppSteamworks.CSteamID playerSteam = playerToKick.PlayerID;

            Il2Cpp.NetworkLayer.SendPlayerConnectResponse(ENetworkPlayerConnectType.Kicked, playerSteam, playerChannel, serverSteam);
            Il2Cpp.Player.RemovePlayer(playerSteam, playerChannel);
            NetworkLayer.SendPlayerConnect(ENetworkPlayerConnectType.Disconnected, playerSteam, playerChannel);

            return true;
        }
    }

    public static class PlayerExtension
    {
        public static bool CanAdminExecute(this Il2Cpp.Player player, Power power, Il2Cpp.Player? targetPlayer = null)
        {
            return SiAdminMod.PlayerAdmin.CanAdminExecute(player, power, targetPlayer);
        }

        public static bool CanAdminTarget(this Il2Cpp.Player player, Il2Cpp.Player targetPlayer)
        {
            return SiAdminMod.PlayerAdmin.CanAdminTarget(player, targetPlayer);
        }

        public static Power GetAdminPowers(this Il2Cpp.Player player)
        {
            return SiAdminMod.PlayerAdmin.GetAdminPowers(player);
        }

        public static byte GetAdminLevel(this Il2Cpp.Player player)
        {
            return SiAdminMod.PlayerAdmin.GetAdminLevel(player);
        }

        public static bool IsAdmin(this Il2Cpp.Player player)
        {
            return SiAdminMod.PlayerAdmin.IsAdmin(player);
        }
    }
}