/*
Silica Tech Glitch Command Mod
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, provides a command (!techglitch) which
allows each team's commander to use if there is a synchronization
issue between the server and commander's tech status.

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
using Si_TechGlitch;
using AdminExtension;
using UnityEngine;

[assembly: MelonInfo(typeof(TechGlitch), "Tech Glitch Command", "0.9.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_TechGlitch
{
    public class TechGlitch : MelonMod
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
        private static class TechGlitch_Chat_MessageReceived
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance.ToString().Contains("alien") && __2 == false)
                    {
                        bool isTechGlitchCommand = String.Equals(__1, "!techglitch", StringComparison.OrdinalIgnoreCase);
                        if (isTechGlitchCommand)
                        {
                            // check if we are actually a commander
                            bool isCommander = IsCommander(__0);

                            if (isCommander)
                            {
                                // is there a game currently started?
                                if (Il2Cpp.GameMode.CurrentGameMode.GameOngoing)
                                {
                                    HelperMethods.ReplyToCommand(__0.Team.TeamName + " is at tech level " + __0.Team.CurrentTechnologyTier.ToString());

                                    // look for ResearchFacility and QuantumCortex
                                    String techStructureName = __0.Team.TeamName.Contains("Human") ? "ResearchF" : "QuantumC";
                                    int techStructures = 0;

                                    for (int i = 0; i < __0.Team.Structures.Count; i++)
                                    {
                                        if (__0.Team.Structures[i].ToString().StartsWith(techStructureName))
                                        {
                                            techStructures++;
                                            __0.Team.Structures[i].RPC_SynchTechnologyTier();
                                        }
                                    }

                                    // notify all players
                                    HelperMethods.ReplyToCommand_Player(__0, "forced sync on " + techStructures.ToString() + " tech structure" + (techStructures > 1 ? "s" : ""));

                                    __0.Team.UpdateTechnologyTier();
                                }
                            }
                            else
                            {
                                // notify player on invalid usage
                                HelperMethods.ReplyToCommand_Player(__0, ": only commanders can use !techglitch");
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