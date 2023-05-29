/*
 Silica Surrender Command Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, provides a command (!surrender) which
 each team's commander can use to have their team give up early.
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
using Il2CppSteamworks;
using MelonLoader;
using Microsoft.VisualBasic;
using Si_SurrenderCommand;
using UnityEngine;

[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.2", "databomb", "https://github.com/data-bomb/Silica_ListenServer"))]
[assembly: MelonGame("Bohemia Interactive", "Silica")]



namespace Si_SurrenderCommand
{

    public class SurrenderCommand : MelonMod
    {
        const string ChatPrefix = "[BOT] ";

        [HarmonyPatch(typeof(Il2Cpp.Player), nameof(Il2Cpp.Player.SendChatMessage))]
        private static class ApplyChatSendSurrenderPatch
        {
            public static bool Prefix(Il2Cpp.Player __instance, bool __result, string __0, bool __1)
            {
                bool bIsSurrenderCommand = String.Equals(__0, "!surrender", StringComparison.OrdinalIgnoreCase);
                if (bIsSurrenderCommand)
                {
                    String sCallingPlayer = __instance.PlayerName;
                    Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                    // check if we are actually a commander
                    bool bIsCommander = __instance.IsCommander;
                    if (bIsCommander)
                    {
                        // is there a game currently started?
                        if (Il2Cpp.GameMode.CurrentGameMode.GameOngoing)
                        {
                            // destroy all structures on team that's surrendering
                            Il2Cpp.Team SurrenderTeam = __instance.Team;
                            for (int i = 0; i < SurrenderTeam.Structures.Count; i++)
                            {
                                SurrenderTeam.Structures[i].DamageManager.SetHealth01(0.0f);
                            }

                            // notify all players
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, ChatPrefix + sCallingPlayer + " used !surrender to end", false);
                        }
                        else
                        {
                            // notify player on invalid usage
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, ChatPrefix + sCallingPlayer + "- !surrender can only be used when the game is in-progress", false);
                        }
                    }
                    else
                    {
                        // notify player on invalid usage
                        Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, ChatPrefix + sCallingPlayer + " - only commanders can use !surrender", false);
                    }

                    // prevent chat message from reaching clients
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyChatReceiveSurrenderPatch
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                bool bIsSurrenderCommand = String.Equals(__1, "!surrender", StringComparison.OrdinalIgnoreCase);
                if (bIsSurrenderCommand)
                {
                    String sCallingPlayer = __0.PlayerName;
                    Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                    // check if we are actually a commander
                    bool bIsCommander = __0.IsCommander;
                    if (bIsCommander)
                    {
                        // is there a game currently started?
                        if (Il2Cpp.GameMode.CurrentGameMode.GameOngoing)
                        {
                            // destroy all structures on team that's surrendering
                            Il2Cpp.Team SurrenderTeam = __0.Team;
                            for (int i = 0; i < SurrenderTeam.Structures.Count; i++)
                            {
                                SurrenderTeam.Structures[i].DamageManager.SetHealth01(0.0f);
                            }

                            // notify all players
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, ChatPrefix + sCallingPlayer + " used !surrender to end", false);
                        }
                    }
                    else
                    {
                        // notify player on invalid usage
                        Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, ChatPrefix + sCallingPlayer + " - only commanders can use !surrender", false);
                    }
                }
            }
        }
    }
}