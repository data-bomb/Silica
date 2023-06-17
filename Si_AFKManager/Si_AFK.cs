/*
Silica Commander Management Mod
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, allows hosts to use the !kick or !afk command
to disconnect a player without a session ban.

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

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_AFKManager;

[assembly: MelonInfo(typeof(AwayFromKeyboard), "AFK Manager", "0.8.1", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AFKManager
{
    public class AwayFromKeyboard : MelonMod
    {
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

        const string ChatPrefix = "<b><color=#DDE98C>[<color=#C4983F>BOT<color=#DDE98C>]</b> ";

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

        public static void KickWithoutBan(Il2Cpp.Player playerToKick)
        {
            // gather information to log in the banlist
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            string teamColor = GetTeamColor(playerToKick);

            if (playerToKick == serverPlayer)
            {
                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + playerToKick.PlayerName + "<color=#DDE98C> is host and cannot be targeted", false);
                return;
            }

            Il2CppSteamworks.CSteamID serverSteam = NetworkGameServer.GetServerID();
            int playerChannel = playerToKick.PlayerChannel;
            Il2CppSteamworks.CSteamID playerSteam = playerToKick.PlayerID;

            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + playerToKick.PlayerName + "<color=#DDE98C> disconnected", false);

            Il2Cpp.NetworkLayer.SendPlayerConnectResponse(ENetworkPlayerConnectType.Kicked, playerSteam, playerChannel, serverSteam);
            Il2Cpp.Player.RemovePlayer(playerSteam, playerChannel);
            NetworkLayer.SendPlayerConnect(ENetworkPlayerConnectType.Disconnected, playerSteam, playerChannel);
        }

        [HarmonyPatch(typeof(Il2Cpp.Player), nameof(Il2Cpp.Player.SendChatMessage))]
        private static class ApplySendChatDemoteCommandPatch
        {
            public static bool Prefix(Il2Cpp.Player __instance, bool __result, string __0, bool __1)
            {
                try
                {
                    bool bIsKickCommand = (String.Equals(__0.Split(' ')[0], "!kick", StringComparison.OrdinalIgnoreCase) ||
                                                    String.Equals(__0.Split(' ')[0], "!afk", StringComparison.OrdinalIgnoreCase));
                    if (bIsKickCommand)
                    {
                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                        String teamColor = GetTeamColor(__instance);

                        if (__instance != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __instance.PlayerName + "<color=#DDE98C> is not authorized to use " + __0.Split(' ')[0], false);
                            return false;
                        }

                        // count number of arguments
                        int argumentCount = __0.Split(' ').Count();
                        if (argumentCount > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __instance.PlayerName + "<color=#DDE98C>: Too many arguments specified", false);
                            return false;
                        }

                        String sTarget = __0.Split(' ')[1];
                        Il2Cpp.Player? playerToKick = FindTargetPlayer(sTarget);

                        if (playerToKick == null)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __instance.PlayerName + "<color=#DDE98C>: Ambiguous or invalid target", false);
                            return false;
                        }

                        KickWithoutBan(playerToKick);
                        return false;
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run SendChatMessage");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    // TODO: Begin timer to track AFK players every 30 seconds
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyReceiveChatKickCommandPatch
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    bool bIsKickCommand = (String.Equals(__1.Split(' ')[0], "!kick", StringComparison.OrdinalIgnoreCase) ||
                                                    String.Equals(__1.Split(' ')[0], "!afk", StringComparison.OrdinalIgnoreCase));
                    if (bIsKickCommand)
                    {
                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                        String teamColor = GetTeamColor(__0);

                        if (__0 != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __0.PlayerName + "<color=#DDE98C> is not authorized to use " + __1.Split(' ')[0], false);
                            return;
                        }

                        // count number of arguments
                        int argumentCount = __1.Split(' ').Count();
                        if (argumentCount > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __0.PlayerName + "<color=#DDE98C>: Too many arguments specified", false);
                            return;
                        }

                        String sTarget = __1.Split(' ')[1];
                        Il2Cpp.Player? playerToKick = FindTargetPlayer(sTarget);

                        if (playerToKick == null)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + teamColor + __0.PlayerName + "<color=#DDE98C>: Ambiguous or invalid target", false);
                            return;
                        }

                        KickWithoutBan(playerToKick);
                        return;
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run MessageReceived");
                }
            }
        }
    }
}