/*
 Silica Headquarterless Humans Lose Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, automatically detects when humans have 
 lost their last Headquarters and ends the game; similar to how the
 Alien team loses when their last Nest falls.

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
using Si_HQlessHumansLose;
using AdminExtension;
using System.Timers;
using static MelonLoader.MelonLogger;
using Il2CppSystem.IO;

[assembly: MelonInfo(typeof(HQlessHumansLose), "[Si] HQless Humans Lose", "1.2.0", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_HQlessHumansLose
{
    public class HQlessHumansLose : MelonMod
    {
        static bool lostMessageTimerExpired;
        static Team losingTeam;
        static System.Timers.Timer delayLostMessageTimer;

        public static void TeamLostMessage(Team team)
        {
            Player serverPlayer = NetworkGameServer.GetServerPlayer();
            String rootStructureName = team.TeamName.Contains("Human") ? "Headquarters" : "Nest";
            serverPlayer.SendChatMessage(HelperMethods.chatPrefix + HelperMethods.GetTeamColor(team) + team.TeamName + HelperMethods.defaultColor + " lost their last " + rootStructureName, false);
        }

        static String GetRootStructurePrefix(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headq" : "Nes";
        }

        static void HandleTimerSendLostMessage(object source, ElapsedEventArgs e)
        {
            lostMessageTimerExpired = true;
        }

        public static void TerminateRound(Team team)
        {
            HelperMethods.DestroyAllStructures(team);

            // introduce a delay so clients can see chat message after round ends
            lostMessageTimerExpired = false;
            losingTeam = team;

            double interval = 500.0;
            delayLostMessageTimer = new System.Timers.Timer(interval);
            delayLostMessageTimer.Elapsed += new ElapsedEventHandler(HandleTimerSendLostMessage);
            delayLostMessageTimer.AutoReset = false;
            delayLostMessageTimer.Enabled = true;
        }

        public static bool HasRootStructureRemaining(Team team, bool onDestroyedEventTrigger = true)
        {
            int iRootStructures;
            if (onDestroyedEventTrigger)
            {
                iRootStructures = -1;
            }
            else
            {
                iRootStructures = 0;
            }

            String rootStructureMatchText = GetRootStructurePrefix(team);

            for (int i = 0; i < team.Structures.Count; i++)
            {
                if (team.Structures[i].ToString().Contains(rootStructureMatchText))
                {
                    iRootStructures++;
                }
            }

            if (iRootStructures <= 0)
            {
                return false;
            }

            return true;
        }

        public static bool HasRootStructureUnderConstruction(Team team)
        {
            String rootStructureMatchText = GetRootStructurePrefix(team);
            
            int iRootStructuresUnderConstruction = 0;
            for (int i = 0; i < ConstructionSite.ConstructionSites.Count; i++)
            {
                if (ConstructionSite.ConstructionSites[i].Team.Index != team.Index)
                {
                    continue;
                }

                BaseGameObject constructionBase = Il2Cpp.GameFuncs.GetBaseGameObject(Il2Cpp.ConstructionSite.ConstructionSites[i].gameObject);
                if (constructionBase.ObjectInfo.ObjectType != Il2Cpp.ObjectInfoType.Structure)
                {
                    continue;
                }

                if (ConstructionSite.ConstructionSites[i].ToString().Contains(rootStructureMatchText))
                {
                    iRootStructuresUnderConstruction++;
                }
            }

            if (iRootStructuresUnderConstruction <= 0)
            {
                return false;
            }

            return true;
        }

        // check if last root structure that was under construction met an early demise
        [HarmonyPatch(typeof(Il2Cpp.ConstructionSite), nameof(Il2Cpp.ConstructionSite.Deinit))]
        private static class ApplyPatch_ConstructionSiteDeinit
        {
            private static void Postfix(Il2Cpp.ConstructionSite __instance, bool __0)
            {
                try
                {
                    // bool wasCompleted will be false if early demise was met
                    if (__0 == true)
                    {
                        return;
                    }

                    BaseGameObject constructionBase = GameFuncs.GetBaseGameObject(__instance.gameObject);
                    if (constructionBase.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                    {
                        return;
                    }

                    Team constructionSiteTeam = __instance.Team;
                    String rootStructureMatchText = GetRootStructurePrefix(constructionSiteTeam);
                    if (!__instance.ToString().Contains(rootStructureMatchText))
                    {
                        return;
                    }

                    if (HasRootStructureRemaining(constructionSiteTeam, false))
                    {
                        return;
                    }

                    if (HasRootStructureUnderConstruction(constructionSiteTeam))
                    {
                        return;
                    }

                    // the last Nest or Headquarters construction site was destroyed
                    TerminateRound(constructionSiteTeam);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run ConstructionSite::Deinit");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.ConstructionSite), nameof(Il2Cpp.ConstructionSite.Update))]
        private static class ApplyPatch_ConstructionSite_Update
        {
            private static void Postfix(Il2Cpp.ConstructionSite __instance)
            {
                try
                {
                    if (lostMessageTimerExpired)
                    {
                        lostMessageTimerExpired = false;

                        if (losingTeam != null)
                        {
                            TeamLostMessage(losingTeam);
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run ConstructionSite::Update");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            private static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Structure __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (GameMode.CurrentGameMode == null)
                    {
                        return;
                    }

                    if (!GameMode.CurrentGameMode.GameOngoing || (__0 == null))
                    {
                        return;
                    }

                    Team structureTeam = __0.Team;
                    if (structureTeam == null)
                    {
                        return;
                    }

                    
                    String rootStructureMatchText = GetRootStructurePrefix(structureTeam);
                    if (!__0.ToString().Contains(rootStructureMatchText))
                    {
                        return;
                    }

                    if (HasRootStructureRemaining(structureTeam, true))
                    {
                        return;
                    }

                    if (HasRootStructureUnderConstruction(structureTeam))
                    {
                        return;
                    }

                    // no HQ/nests left or being constructed, so end the round
                    TerminateRound(structureTeam);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnStructureDestroyed");
                }
            }
        }
    }
}