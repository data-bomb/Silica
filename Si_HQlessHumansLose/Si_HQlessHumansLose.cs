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
using System.Collections;
using System.Collections.Generic;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(HQlessHumansLose), "HQless Humans Lose", "1.4.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_HQlessHumansLose
{
    public class HQlessHumansLose : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        public static MelonPreferences_Entry<bool> Pref_HQ_RemoveStructuresOnElimination = null!;
        public static MelonPreferences_Entry<bool> Pref_HQ_DespawnUnitsOnElimination = null!;
        public static MelonPreferences_Entry<int> Pref_HQ_EliminationUnitsPerFrame = null!;
        public static MelonPreferences_Entry<float> Pref_HQ_EliminationSweepInterval = null!;

        // teams that still need their leftovers removed; empty outside of an elimination
        static readonly List<Team> teamsToClean = new List<Team>();
        static readonly List<Unit> unitBatch = new List<Unit>();
        static readonly List<Structure> structureBatch = new List<Structure>();
        static bool cleanupRunning;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_HQ_RemoveStructuresOnElimination ??= _modCategory.CreateEntry<bool>("HQ_RemoveStructuresOnElimination", false);
            Pref_HQ_DespawnUnitsOnElimination ??= _modCategory.CreateEntry<bool>("HQ_DespawnUnitsOnElimination", true);
            Pref_HQ_EliminationUnitsPerFrame ??= _modCategory.CreateEntry<int>("HQ_EliminationUnitsPerFrame", 8);
            Pref_HQ_EliminationSweepInterval ??= _modCategory.CreateEntry<float>("HQ_EliminationSweepInterval", 5f);
        }

        public static void TeamLostMessage(Team team)
        {
            String rootStructureName = GetRootCriticalFullName(team);
            HelperMethods.ReplyToCommand(HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color> lost their " + rootStructureName);
        }

        static String GetRootCriticalPrefix(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headq" : "Que";
        }

        static String GetRootCriticalFullName(Team team)
        {
            return team.TeamName.Contains("Human") ? "Headquarters" : "Queen";
        }

        static bool IsGameOngoing()
        {
            GameMode gameMode = GameMode.CurrentGameMode;
            return gameMode != null && gameMode.GameOngoing;
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

            return TeamsWithCriticalObjects < 2;
        }

        public static void EliminateTeam(Team team)
        {
            MP_Strategy strategyInstance = GameObject.FindFirstObjectByType<MP_Strategy>();
            if (strategyInstance == null)
            {
                return;
            }

            MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;
            bool oneFactionAlreadyEliminated = OneFactionAlreadyEliminated();

            MelonLogger.Msg("Eliminating team " + team.TeamShortName + " on versus mode " + versusMode.ToString() + " (one faction already eliminated: " + oneFactionAlreadyEliminated + ")");

            // are there still two remaining factions after this one is eliminated?
            if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && !oneFactionAlreadyEliminated)
            {
                // remove the units (otherwise AI will roam around doing odd things), and the
                // structures if configured, a few per frame so the server never stalls on it
                QueueTeamCleanup(team);
            }
            else
            {
                HelperMethods.DestroyAllStructures(team);
            }

            TeamLostMessage(team);
        }

        private static void QueueTeamCleanup(Team team)
        {
            if (!teamsToClean.Contains(team))
            {
                teamsToClean.Add(team);
            }

            if (!cleanupRunning)
            {
                cleanupRunning = true;
                MelonCoroutines.Start(CleanupEliminatedTeams());
            }
        }

        // Runs only while an eliminated team still needs watching. Costs nothing before the first
        // elimination and stops by itself when the round ends.
        private static IEnumerator CleanupEliminatedTeams()
        {
            // start on the next frame: we are queued from inside the game's own destroy handling
            yield return null;

            try
            {
                while (teamsToClean.Count > 0 && IsGameOngoing())
                {
                    bool removedSomething = false;

                    for (int i = teamsToClean.Count - 1; i >= 0; i--)
                    {
                        Team team = teamsToClean[i];

                        // gone, or back in the game (new round started, or a new HQ/Queen was finished)
                        if (team == null || team.GetHasAnyCritical())
                        {
                            teamsToClean.RemoveAt(i);
                            continue;
                        }

                        if (RemoveSomeUnits(team))
                        {
                            removedSomething = true;
                        }
                        else if (Pref_HQ_RemoveStructuresOnElimination.Value && RemoveOneStructure(team))
                        {
                            removedSomething = true;
                        }
                    }

                    if (removedSomething)
                    {
                        // next batch on the next frame
                        yield return null;
                    }
                    else
                    {
                        // nothing left right now. look again later: structures that were left standing
                        // can still produce units for the eliminated team
                        yield return new WaitForSeconds(Mathf.Clamp(Pref_HQ_EliminationSweepInterval.Value, 1f, 10f));
                    }
                }
            }
            finally
            {
                teamsToClean.Clear();
                unitBatch.Clear();
                structureBatch.Clear();
                cleanupRunning = false;
            }
        }

        // removes up to HQ_EliminationUnitsPerFrame units; returns false when the team has none left
        private static bool RemoveSomeUnits(Team team)
        {
            int budget = Mathf.Max(1, Pref_HQ_EliminationUnitsPerFrame.Value);

            unitBatch.Clear();
            for (int i = team.Units.Count - 1; i >= 0 && unitBatch.Count < budget; i--)
            {
                Unit unit = team.Units[i];
                if (unit != null && !unit.IsDestroyed)
                {
                    unitBatch.Add(unit);
                }
            }

            bool despawn = Pref_HQ_DespawnUnitsOnElimination.Value;
            for (int i = 0; i < unitBatch.Count; i++)
            {
                Unit unit = unitBatch[i];
                try
                {
                    // a unit earlier in the batch can take this one with it (e.g. a transport and its passengers)
                    if (unit == null || unit.IsDestroyed)
                    {
                        continue;
                    }

                    if (despawn)
                    {
                        // plain removal, synced to clients by the NetworkComponent: no wreck, no ragdoll,
                        // no death effects and no kill events
                        UnityEngine.Object.Destroy(unit.gameObject);
                    }
                    else
                    {
                        unit.DamageManager.SetHealth(0.0f);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to remove unit of eliminated team");
                }
            }

            bool removedAny = unitBatch.Count > 0;
            unitBatch.Clear();
            return removedAny;
        }

        // destroys one structure per call; returns false when there is nothing left to destroy
        private static bool RemoveOneStructure(Team team)
        {
            structureBatch.Clear();
            for (int i = team.Structures.Count - 1; i >= 0 && structureBatch.Count < 1; i--)
            {
                Structure structure = team.Structures[i];
                if (structure == null || structure.IsDestroyed)
                {
                    continue;
                }

                // but don't destroy bunkers if there's still 2 teams left
                if (structure.ToString().StartsWith("Bunk"))
                {
                    continue;
                }

                structureBatch.Add(structure);
            }

            bool removedAny = false;
            for (int i = 0; i < structureBatch.Count; i++)
            {
                try
                {
                    structureBatch[i].DamageManager.SetHealth01(0.0f);
                    removedAny = true;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to destroy structure of eliminated team");
                }
            }

            structureBatch.Clear();
            return removedAny;
        }

        public static bool HasRootCriticalsRemaining(Team team)
        {
            return team.GetHasAnyCritical();
        }

        public static bool HasRootStructureUnderConstruction(Team team)
        {
            String rootStructureMatchText = GetRootCriticalPrefix(team);

            for (int i = 0; i < ConstructionSite.ConstructionSites.Count; i++)
            {
                ConstructionSite site = ConstructionSite.ConstructionSites[i];
                if (site == null || site.Team == null || site.Team.Index != team.Index)
                {
                    continue;
                }

                BaseGameObject constructionBase = GameFuncs.GetBaseGameObject(site.gameObject);
                if (constructionBase == null || constructionBase.ObjectInfo == null || constructionBase.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                {
                    continue;
                }

                if (site.ToString().Contains(rootStructureMatchText))
                {
                    return true;
                }
            }

            return false;
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

                    // sites are also torn down when the round ends or the map changes
                    if (!IsGameOngoing())
                    {
                        return;
                    }

                    BaseGameObject constructionBase = GameFuncs.GetBaseGameObject(__instance.gameObject);
                    if (constructionBase == null || constructionBase.ObjectInfo == null || constructionBase.ObjectInfo.ObjectType != ObjectInfoType.Structure)
                    {
                        return;
                    }

                    Team constructionSiteTeam = __instance.Team;
                    if (constructionSiteTeam == null)
                    {
                        return;
                    }

                    String rootStructureMatchText = GetRootCriticalPrefix(constructionSiteTeam);
                    if (!__instance.ToString().Contains(rootStructureMatchText))
                    {
                        return;
                    }

                    if (HasRootCriticalsRemaining(constructionSiteTeam))
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

        // an elimination never carries over into the next round (one call per round each)
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0)
            {
                teamsToClean.Clear();
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                teamsToClean.Clear();
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            private static void Postfix(MP_Strategy __instance, Structure __0)
            {
                try
                {
                    // almost every destroyed structure leaves here: only a critical one can eliminate its team
                    if (__0 == null || __0.ObjectInfo == null || !__0.ObjectInfo.Critical)
                    {
                        return;
                    }

                    OnCriticalObjectDestroyed(__instance, __0, __0.Team, true);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnStructureDestroyed");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnUnitDestroyed))]
        private static class ApplyPatch_OnUnitDestroyed
        {
            private static void Postfix(MP_Strategy __instance, Unit __0)
            {
                try
                {
                    // almost every destroyed unit leaves here: only a critical one can eliminate its team
                    if (__0 == null || __0.ObjectInfo == null || !__0.ObjectInfo.Critical)
                    {
                        return;
                    }

                    OnCriticalObjectDestroyed(__instance, __0, __0.Team, false);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnUnitDestroyed");
                }
            }
        }

        private static void OnCriticalObjectDestroyed(MP_Strategy strategyInstance, BaseGameObject destroyedObject, Team team, bool checkConstructionSites)
        {
            if (team == null || !strategyInstance.GameOngoing)
            {
                return;
            }

            String rootCriticalMatchText = GetRootCriticalPrefix(team);
            if (!destroyedObject.ToString().Contains(rootCriticalMatchText))
            {
                return;
            }

            if (HasRootCriticalsRemaining(team))
            {
                return;
            }

            if (checkConstructionSites && HasRootStructureUnderConstruction(team))
            {
                return;
            }

            // no HQ/nests left or being constructed
            EliminateTeam(team);
        }
    }
}
