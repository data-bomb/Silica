/*
 Silica Headquarterless Humans Lose Mod
 Copyright (C) 2023-2024 by databomb
 
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
using UnityEngine;
using System;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(HQlessHumansLose), "HQless Humans Lose", "1.3.9", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_HQlessHumansLose
{
    public class HQlessHumansLose : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        public static MelonPreferences_Entry<bool> Pref_HQ_RemoveStructuresOnElimination = null!;

        static Team? losingTeam;
        static Player? destroyerOfWorlds;
        static float Timer_SendTeamLostMessage = HelperMethods.Timer_Inactive;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_HQ_RemoveStructuresOnElimination ??= _modCategory.CreateEntry<bool>("HQ_RemoveStructuresOnElimination", false);
        }

        public static void TeamLostMessage(Team team)
        {
            Player broadcastPlayer = HelperMethods.FindBroadcastPlayer();
            String rootStructureName = GetRootCriticalFullName(team);
            broadcastPlayer.SendChatMessage(HelperMethods.chatPrefix + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color> lost their " + rootStructureName, false);
        }

        public static void TeamLostByPlayerMessage(Team team, Player player)
        {
            Player broadcastPlayer = HelperMethods.FindBroadcastPlayer();
            String rootStructureName = GetRootCriticalFullName(team);
            broadcastPlayer.SendChatMessage(HelperMethods.chatPrefix + HelperMethods.GetTeamColor(player) + player.PlayerName + "</color> destroyed " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>'s " + rootStructureName, false);
        }

        static String GetRootCriticalPrefix(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headq" : "Que";
        }

        static String GetRootCriticalFullName(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headquarters" : "Queen";
        }

        public static bool OneFactionAlreadyEliminated()
        {
            int TeamsWithCriticalObjects = 0;
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                Team? thisTeam = Team.Teams[i];
                if (thisTeam == null)
                {
                    continue;
                }

                if (thisTeam.GetHasAnyCritical())
                {
                    TeamsWithCriticalObjects++;
                }
            }

            if (TeamsWithCriticalObjects < 2)
            {
                MelonLogger.Msg("OneFactionAlreadyEliminated: true");
                return true;
            }

            MelonLogger.Msg("OneFactionAlreadyEliminated: false");
            return false;
        }

        public static void EliminateTeam(Team team)
        {
            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
            MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

            MelonLogger.Msg("Eliminating team " + team.TeamShortName + " on versus mode " + versusMode.ToString());

            // are there still two remaining factions after this one is eliminated?
            if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && !OneFactionAlreadyEliminated())
            {
                // check if we should destroy all structures
                if (Pref_HQ_RemoveStructuresOnElimination.Value)
                {
                    for (int i = 0; i < team.Structures.Count; i++)
                    {
                        // but don't destroy bunkers if there's still 2 teams left
                        if (team.Structures[i].ToString().StartsWith("Bunk"))
                        {
                            continue;
                        }

                        team.Structures[i].DamageManager.SetHealth01(0.0f);
                    }
                }
                
                // destroy units (otherwise AI will roam around doing odd things)
                DestroyAllUnits(team);
            }
            else
            {
                HelperMethods.DestroyAllStructures(team);
            }

            TeamLostMessage(team);
        }

        private static void DestroyAllUnits(Team team)
        {
            for (int i = 0; i < team.Units.Count; i++)
            {
                if (!team.Units[i].IsDestroyed)
                {
                    team.Units[i].DamageManager.SetHealth(0.0f);
                }
            }
        }

        // introduce a delay so clients can see chat message after round ends
        private static void DelayTeamLostMessage(Team team)
        {
            MelonLogger.Msg("Starting delay lost timer for team " + team.TeamShortName);

            HelperMethods.StartTimer(ref Timer_SendTeamLostMessage);
            losingTeam = team;
        }

        public static bool HasRootCriticalsRemaining(Team team, bool onDestroyedEventTrigger = true)
        {
            return team.GetHasAnyCritical();
            #if false
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
            #endif
        }

        public static bool HasRootStructureUnderConstruction(Team team)
        {
            String rootStructureMatchText = GetRootCriticalPrefix(team);
            
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

                    MelonLogger.Msg("Structure construction destroyed: " + __instance.name);

                    Team constructionSiteTeam = __instance.Team;
                    String rootStructureMatchText = GetRootCriticalPrefix(constructionSiteTeam);
                    if (!__instance.ToString().Contains(rootStructureMatchText))
                    {
                        return;
                    }

                    if (HasRootCriticalsRemaining(constructionSiteTeam, false))
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
        // disabled for now because the game clients independently calculate the end of game conditions (not just the server) :(
        #if false
        #if NET6_0
        [HarmonyPatch(typeof(Team), nameof(Team.UpdateMajorStructuresCount))]
        #else
        [HarmonyPatch(typeof(Team), "UpdateMajorStructuresCount")]
        #endif
        public static class ApplyPatch_Team_UpdateMajorStructuresCount
        {
            public static bool Prefix(Team __instance)
            {
                if (__instance == null)
                {
                    MelonLogger.Error("Team is null for UpdateMajorStructuresCount.");
                    return false;
                }

                int numMajorStructures = 0;

                foreach (Structure structure in __instance.Structures)
                {
                    if (structure == null)
                    {
                        Debug.LogError("UpdateMajorStructuresCount: A structure is NULL for team '" + __instance.TeamShortName + "', skipping it...");
                        continue;
                    }

                    if (!structure.IsDestroyed && structure.ObjectInfo && structure.ObjectInfo.StructureMajor)
                    {
                        numMajorStructures++;
                    }
                }

                if (numMajorStructures == 0)
                {
                    if (HasRootStructureUnderConstruction(__instance))
                    {
                        MelonLogger.Msg("Found Major Structure under Construction. Adjusting count.");
                        __instance.NumMajorStructures = 1;
                        return false;
                    }
                }

                __instance.NumMajorStructures = numMajorStructures;
                return false;
            }
        }

        // don't count it as a loss if HQ/Nest is under construction
        #if NET6_0
        [HarmonyPatch(typeof(Team), nameof(Team.GetHasAnyMajorStructures))]
        #else
        [HarmonyPatch(typeof(Team), "GetHasAnyMajorStructures")]
        #endif
        public static class ApplyPatch_Team_GetHasAnyMajorStructures
        {
            public static bool Prefix(Team __instance, ref bool __result)
            {
                foreach (Structure structure in __instance.Structures)
                {
                    if (structure == null)
                    {
                        Debug.LogError("GetHasAnyMajorStructures: A structure is NULL for team '" + __instance.TeamShortName + "', skipping it...");
                        continue;
                    }

                    if (structure.ObjectInfo && structure.ObjectInfo.StructureMajor && !structure.IsDestroyed)
                    {
                        __result = true;
                        return false;
                    }
                }

                if (HasRootStructureUnderConstruction(__instance))
                {
                    MelonLogger.Msg("Found Major Structure under Construction. Preventing loss.");
                    __result = true;
                    return false;
                }

                __result = false;
                return false;
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(Team), nameof(Team.GetHasAnyMajorStructures))]
        #else
        [HarmonyPatch(typeof(Team), "GetHasAnyMajorStructures")]
        #endif
        public static class ApplyPatch_GetHasAnyMajorStructures
        {
            public static void Postfix(Team __instance, ref bool __result)
            {
                // only spend the CPU if the team is about to lose
                if (__result == false && GameMode.CurrentGameMode.GameOngoing)
                {
                    if (HasRootStructureUnderConstruction(__instance))
                    {
                        MelonLogger.Msg("Found Major Structure under Construction. Preventing loss...");
                        __result = true;
                    }
                }
            }
        }
        #endif

        #if NET6_0
        [HarmonyPatch(typeof(ConstructionSite), nameof(ConstructionSite.Update))]
        #else
        [HarmonyPatch(typeof(ConstructionSite), "Update")]
        #endif
        public static class ApplyPatch_ConstructionSite_Update
        {
            public static void Postfix(ConstructionSite __instance)
            {
                try
                {
                    if (HelperMethods.IsTimerActive(Timer_SendTeamLostMessage))
                    {
                        Timer_SendTeamLostMessage += Time.deltaTime;

                        if (Timer_SendTeamLostMessage >= 1f)
                        {
                            Timer_SendTeamLostMessage = HelperMethods.Timer_Inactive;

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
            private static void Postfix(MP_Strategy __instance, Structure __0, UnityEngine.GameObject __1)
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

                    //MelonLogger.Msg("Structure destroyed: " + __0.name);

                    Team structureTeam = __0.Team;
                    if (structureTeam == null)
                    {
                        return;
                    }
                    
                    String rootCriticalMatchText = GetRootCriticalPrefix(structureTeam);
                    if (!__0.ToString().Contains(rootCriticalMatchText))
                    {
                        return;
                    }

                    if (HasRootCriticalsRemaining(structureTeam, true))
                    {
                        return;
                    }

                    if (HasRootStructureUnderConstruction(structureTeam))
                    {
                        return;
                    }

                    // was a human-controlled player responsible for the destruction?
                    destroyerOfWorlds = null;
                    if (__1 != null)
                    {
                        BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);
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

        [HarmonyPatch(typeof(StrategyMode), nameof(StrategyMode.OnUnitDestroyed))]
        private static class ApplyPatch_StrategyMode_OnUnitDestroyed
        {
            public static void Postfix(StrategyMode __instance, Unit __0, UnityEngine.GameObject __1)
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

                    //MelonLogger.Msg("Unit destroyed: " + __0.name);

                    Team unitTeam = __0.Team;
                    if (unitTeam == null)
                    {
                        return;
                    }

                    String rootCriticalMatchText = GetRootCriticalPrefix(unitTeam);
                    if (!__0.ToString().Contains(rootCriticalMatchText))
                    {
                        return;
                    }

                    if (HasRootCriticalsRemaining(unitTeam, true))
                    {
                        return;
                    }

                    // was a human-controlled player responsible for the destruction?
                    destroyerOfWorlds = null;
                    if (__1 != null)
                    {
                        BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);
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
                    EliminateTeam(unitTeam);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StrategyMode::OnUnitDestroyed");
                }
            }
        }
    }
}