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
using AdminExtension;

[assembly: MelonInfo(typeof(SurrenderCommand), "[Si] Surrender Command", "1.1.8", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_SurrenderCommand
{

    public class SurrenderCommand : MelonMod
    {
        public static bool IsCommander(Il2Cpp.Player thePlayer)
        {
            if (thePlayer == null)
            {
                return false;
            }

            Il2Cpp.Team theTeam = thePlayer.m_Team;
            if (theTeam == null)
            {
                return false;
            }

            Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
            Il2Cpp.Player teamCommander = strategyInstance.GetCommanderForTeam(theTeam);

            if (teamCommander == thePlayer)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyChatReceiveSurrenderPatch
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance.ToString().Contains("alien") && __2 == false)
                    {
                        bool isSurrenderCommand = String.Equals(__1, "!surrender", StringComparison.OrdinalIgnoreCase);
                        if (isSurrenderCommand)
                        {
                            // check if we are actually a commander
                            bool isCommander = IsCommander(__0);

                            if (isCommander)
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
                                    HelperMethods.ReplyToCommand_Player(__0, "used !surrender to end");
                                }
                            }
                            else
                            {
                                // notify player on invalid usage
                                HelperMethods.ReplyToCommand_Player(__0, ": only commanders can use !surrender");
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }
    }
}