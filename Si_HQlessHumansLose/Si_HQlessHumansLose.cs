/*
 Silica Headquarterless Humans Lose Mod
 Copyright (C) 2024 by databomb
 
 * Description *
 For Silica servers, automatically detects when humans have lost their 
 last Headquarters and ends the game; similar to how the Alien team 
 loses when their last Nest falls.

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

using HarmonyLib;
using MelonLoader;
using Si_HQlessHumansLose;
using System.Timers;
using UnityEngine;
using System;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(HQlessHumansLose), "[Si] HQless Humans Lose", "1.2.6", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_HQlessHumansLose
{
    public class HQlessHumansLose : MelonMod
    {
        static bool lostMessageTimerExpired;
        static Team? losingTeam;
        static Player? destroyerOfWorlds;
        static System.Timers.Timer? delayLostMessageTimer;

        public static void TeamLostMessage(Team team)
        {
            Player serverPlayer = NetworkGameServer.GetServerPlayer();
            String rootStructureName = GetRootStructureFullName(team);
            serverPlayer.SendChatMessage(HelperMethods.chatPrefix + HelperMethods.GetTeamColor(team) + team.TeamName + HelperMethods.defaultColor + " lost their last " + rootStructureName, false);
        }

        public static void TeamLostByPlayerMessage(Team team, Player player)
        {
            Player serverPlayer = NetworkGameServer.GetServerPlayer();
            String rootStructureName = GetRootStructureFullName(team);
            serverPlayer.SendChatMessage(HelperMethods.chatPrefix + HelperMethods.GetTeamColor(player) + player.PlayerName + HelperMethods.defaultColor + " destroyed " + HelperMethods.GetTeamColor(team) + team.TeamName + "'s last " + rootStructureName, false);
        }

        static String GetRootStructurePrefix(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headq" : "Nes";
        }

        static String GetRootStructureFullName(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headquarters" : "Nest";
        }

        static void HandleTimerSendLostMessage(object? source, ElapsedEventArgs e)
        {
            lostMessageTimerExpired = true;
        }

        public static bool OneFactionEliminated()
        {
            int TeamsWithMajorStructures = 0;
            for (int i = 0; i < Team.Teams.Count; i++)
            {
                Team? thisTeam = Team.Teams[i];
                int thisTeamMajorStructures = thisTeam.NumMajorStructures;
                if (thisTeamMajorStructures > 0)
                {
                    TeamsWithMajorStructures++;
                }
            }

            if (TeamsWithMajorStructures < 3)
            {
                return true;
            }

            return false;
        }

        public static void EliminateTeam(Team team)
        {
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
            MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

            MelonLogger.Msg("Eliminating team " + team.TeamName + " on versus mode " + versusMode.ToString());

            // are there still two remaining factions after this one is eliminated?
            if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && !OneFactionEliminated())
            {
                // destroy structures
                for (int i = 0; i < team.Structures.Count; i++)
                {
                    // but don't destroy bunkers if there's still 2 teams left
                    if (team.Structures[i].ToString().StartsWith("Bunk"))
                    {
                        continue;
                    }

                    team.Structures[i].DamageManager.SetHealth01(0.0f);
                }

                // destroy units (otherwise AI will roam around doing odd things)
                DestroyAllUnits(team);
            }
            else
            {
                HelperMethods.DestroyAllStructures(team);
            }

            DelayTeamLostMessage(team);
        }

        private static void DestroyAllUnits(Team team)
        {
            for (int i = 0; i < team.Units.Count; i++)
            {
                team.Units[i].DamageManager.SetHealth01(0.0f);
            }
        }

        // introduce a delay so clients can see chat message after round ends
        private static void DelayTeamLostMessage(Team team)
        {
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

                BaseGameObject constructionBase = GameFuncs.GetBaseGameObject(ConstructionSite.ConstructionSites[i].gameObject);
                if (constructionBase.ObjectInfo.ObjectType != ObjectInfoType.Structure)
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
        [HarmonyPatch(typeof(ConstructionSite), nameof(ConstructionSite.Deinit))]
        private static class ApplyPatch_ConstructionSiteDeinit
        {
            private static void Postfix(ConstructionSite __instance, bool __0)
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
                    EliminateTeam(constructionSiteTeam);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run ConstructionSite::Deinit");
                }
            }
        }

        // don't let the structure count reach 0 if HQ/Nest is under construction
        #if NET6_0
        [HarmonyPatch(typeof(Team), nameof(Team.UpdateMajorStructuresCount))]
        #else
        [HarmonyPatch(typeof(Team), "UpdateMajorStructuresCount")]
        #endif
        private static class ApplyPatch_UpdateMajorStructuresCount
        {
            private static void Postfix(Team __instance)
            {
                // only spend the CPU if the team is about to lose
                if (__instance.NumMajorStructures == 0 && GameMode.CurrentGameMode.GameOngoing)
                {
                    if (HasRootStructureUnderConstruction(__instance))
                    {
                        MelonLogger.Msg("Found Major Structure under Construction. Adjusting count.");
                        __instance.NumMajorStructures = 1;
                    }
                }
            }
        }

        // don't count it as a loss if HQ/Nest is under construction
        #if NET6_0
        [HarmonyPatch(typeof(StrategyTeamSetup), nameof(StrategyTeamSetup.GetHasLost))]
        #else
        [HarmonyPatch(typeof(StrategyTeamSetup), "GetHasLost")]
        #endif
        private static class ApplyPatch_GetHasLost
        {
            private static void Postfix(StrategyTeamSetup __instance, bool __result)
            {
                // only spend the CPU if the team is about to lose
                if (__result == true && GameMode.CurrentGameMode.GameOngoing)
                {
                    if (HasRootStructureUnderConstruction(__instance.Team))
                    {
                        MelonLogger.Msg("Found Major Structure under Construction. Preventing loss.");
                        __result = false;
                    }
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(ConstructionSite), nameof(ConstructionSite.Update))]
        #else
        [HarmonyPatch(typeof(ConstructionSite), "Update")]
        #endif
        private static class ApplyPatch_ConstructionSite_Update
        {
            private static void Postfix(ConstructionSite __instance)
            {
                try
                {
                    if (lostMessageTimerExpired)
                    {
                        lostMessageTimerExpired = false;

                        if (losingTeam != null)
                        {
                            if (destroyerOfWorlds == null)
                            {
                                TeamLostMessage(losingTeam);
                            }
                            else
                            {
                                TeamLostByPlayerMessage(losingTeam, destroyerOfWorlds);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run ConstructionSite::Update");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            private static void Postfix(MP_Strategy __instance, Structure __0, EDamageType __1, UnityEngine.GameObject __2)
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

                    // was a human-controlled player responsible for the destruction?
                    destroyerOfWorlds = null;
                    if (__2 != null)
                    {
                        BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__2);
                        if (attackerBase != null)
                        {
                            NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                            if (attackerNetComp != null && attackerNetComp.OwnerPlayer != null)
                            {
                                destroyerOfWorlds = attackerNetComp.OwnerPlayer;
                            }
                        }
                    }

                    // no HQ/nests left or being constructed
                    EliminateTeam(structureTeam);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnStructureDestroyed");
                }
            }
        }
    }
}