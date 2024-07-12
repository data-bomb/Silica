/*
 Silica Default Spawn Units
 Copyright (C) 2024 by databomb
 
 * Description *
 For Silica servers, allows hosts to modify the default spawn units at
 the various technology tier levels.

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
#else
using System.Reflection;
#endif

using HarmonyLib;
using MelonLoader;
using Si_DefaultUnits;
using UnityEngine;
using System;
using SilicaAdminMod;
using System.Linq;

[assembly: MelonInfo(typeof(DefaultUnits), "Default Spawn Units", "1.0.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_DefaultUnits
{
    public class DefaultUnits : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        private const string ModCategory = "Silica";

        static MelonPreferences_Entry<string> _Human_Unit_Tier_0 = null!;
        static MelonPreferences_Entry<string> _Human_Unit_Tier_I = null!;
        static MelonPreferences_Entry<string> _Human_Unit_Tier_II = null!;
        static MelonPreferences_Entry<string> _Human_Unit_Tier_III = null!;
        static MelonPreferences_Entry<string> _Human_Unit_Tier_IV = null!;

        static MelonPreferences_Entry<string> _Alien_Unit_Tier_0 = null!;
        static MelonPreferences_Entry<string> _Alien_Unit_Tier_I = null!;
        static MelonPreferences_Entry<string> _Alien_Unit_Tier_II = null!;
        static MelonPreferences_Entry<string> _Alien_Unit_Tier_III = null!;
        static MelonPreferences_Entry<string> _Alien_Unit_Tier_IV = null!;

        static int[]? teamTechTiers;
        static bool[]? teamFirstSpawn;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);

            _Human_Unit_Tier_0 ??= _modCategory.CreateEntry<string>("DefaultSpawn_Human_TechTier_0", "Soldier_Scout");
            _Human_Unit_Tier_I ??= _modCategory.CreateEntry<string>("DefaultSpawn_Human_TechTier_I", "Soldier_Rifleman");
            _Human_Unit_Tier_II ??= _modCategory.CreateEntry<string>("DefaultSpawn_Human_TechTier_II", "Soldier_Rifleman");
            _Human_Unit_Tier_III ??= _modCategory.CreateEntry<string>("DefaultSpawn_Human_TechTier_III", "Soldier_Rifleman");
            _Human_Unit_Tier_IV ??= _modCategory.CreateEntry<string>("DefaultSpawn_Human_TechTier_IV", "Soldier_Commando");

            _Alien_Unit_Tier_0 ??= _modCategory.CreateEntry<string>("DefaultSpawn_Alien_TechTier_0", "Crab");
            _Alien_Unit_Tier_I ??= _modCategory.CreateEntry<string>("DefaultSpawn_Alien_TechTier_I", "Crab_Horned");
            _Alien_Unit_Tier_II ??= _modCategory.CreateEntry<string>("DefaultSpawn_Alien_TechTier_II", "Wasp");
            _Alien_Unit_Tier_III ??= _modCategory.CreateEntry<string>("DefaultSpawn_Alien_TechTier_III", "Wasp");
            _Alien_Unit_Tier_IV ??= _modCategory.CreateEntry<string>("DefaultSpawn_Alien_TechTier_IV", "Wasp");

            teamTechTiers = new int[SiConstants.MaxPlayableTeams];
            teamFirstSpawn = new bool[SiConstants.MaxPlayableTeams];
        }

        #if NET6_0
        public override void OnLateInitializeMelon()
        {
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.StringOption humanTier0 = new(_Human_Unit_Tier_0, _Human_Unit_Tier_0.Value);
            QList.OptionTypes.StringOption humanTier1 = new(_Human_Unit_Tier_I, _Human_Unit_Tier_I.Value);
            QList.OptionTypes.StringOption humanTier2 = new(_Human_Unit_Tier_II, _Human_Unit_Tier_II.Value);
            QList.OptionTypes.StringOption humanTier3 = new(_Human_Unit_Tier_III, _Human_Unit_Tier_III.Value);
            QList.OptionTypes.StringOption humanTier4 = new(_Human_Unit_Tier_IV, _Human_Unit_Tier_IV.Value);

            QList.Options.AddOption(humanTier0);
            QList.Options.AddOption(humanTier1);
            QList.Options.AddOption(humanTier2);
            QList.Options.AddOption(humanTier3);
            QList.Options.AddOption(humanTier4);

            QList.OptionTypes.StringOption alienTier0 = new(_Alien_Unit_Tier_0, _Alien_Unit_Tier_0.Value);
            QList.OptionTypes.StringOption alienTier1 = new(_Alien_Unit_Tier_I, _Alien_Unit_Tier_I.Value);
            QList.OptionTypes.StringOption alienTier2 = new(_Alien_Unit_Tier_II, _Alien_Unit_Tier_II.Value);
            QList.OptionTypes.StringOption alienTier3 = new(_Alien_Unit_Tier_III, _Alien_Unit_Tier_III.Value);
            QList.OptionTypes.StringOption alienTier4 = new(_Alien_Unit_Tier_IV, _Alien_Unit_Tier_IV.Value);

            QList.Options.AddOption(alienTier0);
            QList.Options.AddOption(alienTier1);
            QList.Options.AddOption(alienTier2);
            QList.Options.AddOption(alienTier3);
            QList.Options.AddOption(alienTier4);
        }
        #endif

        // patch as close to where it's used
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.GetUnitPrefabForPlayer))]
        private static class ApplyPatch_MP_Strategy_GetUnitPrefabForPlayer
        {
            public static void Prefix(MP_Strategy __instance, UnityEngine.GameObject __result, Player __0)
            {
                try
                {
                    if (teamTechTiers == null || teamFirstSpawn == null)
                    {
                        return;
                    }

                    Team playerTeam = __0.Team;

                    // was there a change in tech tier or is it the first spawn of the round for this team?
                    if (!TechTierUpdated(playerTeam) && !teamFirstSpawn[playerTeam.Index])
                    {
                        return;
                    }

                    if (teamFirstSpawn[playerTeam.Index])
                    {
                        MelonLogger.Msg("First player spawn of the round for Team: " + playerTeam.TeamName);
                        teamFirstSpawn[playerTeam.Index] = false;
                    }

                    // update the tech tier
                    teamTechTiers[playerTeam.Index] = playerTeam.CurrentTechnologyTier;

                    BaseTeamSetup teamSetup = __instance.GetTeamSetup(playerTeam);
                    if (teamSetup == null)
                    {
                        MelonLogger.Warning("Could not find StrategyTeamSetup for Team: " + playerTeam.TeamName);
                        return;
                    }

                    // remove the extended player spawn list since this has higher priority over PlayerSpawn
                    if (teamSetup.PlayerSpawnExt != null)
                    {
                        teamSetup.PlayerSpawnExt = null;
                    }

                    // determine what the new spawn object will be
                    ObjectInfo? updatedPlayerSpawnObjInfo = GetPlayerSpawnObjInfo(playerTeam);
                    if (updatedPlayerSpawnObjInfo == null)
                    {
                        return;
                    }

                    // update the default unit for the team
                    teamSetup.PlayerSpawn = updatedPlayerSpawnObjInfo;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::GetUnitPrefabForPlayer");
                }
            }
        }

        // reset tech tiers back to 0
        // note: players join and spawn *before* OnGameStarted fires, so it's important to reset back to 0 as soon as the game ends
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    if (teamTechTiers == null || teamFirstSpawn == null)
                    {
                        return;
                    }

                    for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
                    {
                        teamTechTiers[i] = 0;
                        teamFirstSpawn[i] = true;

                        if (Team.Teams[i] == null)
                        {
                            continue;
                        }

                        if (Team.Teams[i].CurrentTechnologyTier != 0)
                        {
                            MelonLogger.Warning("Manually resetting tech tier level to 0 for team: " + Team.Teams[i].TeamName);

                            #if NET6_0
                            Team.Teams[i].CurrentTechnologyTier = 0;
                            #else
                            Type teamType = typeof(Team);
                            PropertyInfo currentTechTierProperty = teamType.GetProperty("CurrentTechnologyTier");
                            currentTechTierProperty.SetValue(Team.Teams[i], 0);
                            #endif
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        static bool TechTierUpdated(Team team)
        {
            if (teamTechTiers == null)
            {
                return false;
            }

            return team.CurrentTechnologyTier != teamTechTiers[team.Index];
        }

        static ObjectInfo? GetPlayerSpawnObjInfo(Team team)
        {
            string desiredSpawn = GetDesiredSpawnConfig(team);
            if (desiredSpawn.Length == 0)
            {
                MelonLogger.Warning("Could not find valid spawn string for team " + team.TeamName + " and tech tier " + team.CurrentTechnologyTier.ToString());
                return null;
            }

            ObjectInfo? updateObject = GetSpawnObject(desiredSpawn);
            if (updateObject == null)
            {
                MelonLogger.Warning("Could not generate default spawn objectinfo for team " + team.TeamName + " and tech tier " + team.CurrentTechnologyTier.ToString());
                return null;
            }

            MelonLogger.Msg("Found new default spawn object for " + team.TeamName + ": " + desiredSpawn);

            return updateObject;
        }

        static string GetDesiredSpawnConfig(Team team)
        {
            bool humanTeam = (team.Index == (int)SiConstants.ETeam.Sol) || (team.Index == (int)SiConstants.ETeam.Centauri);

            if (teamTechTiers == null)
            {
                return "";
            }

            switch (teamTechTiers[team.Index])
            {
                case 0:
                {
                    return humanTeam ? _Human_Unit_Tier_0.Value : _Alien_Unit_Tier_0.Value;
                }
                case 1:
                {
                    return humanTeam ? _Human_Unit_Tier_I.Value : _Alien_Unit_Tier_I.Value;
                }
                case 2:
                {
                    return humanTeam ? _Human_Unit_Tier_II.Value : _Alien_Unit_Tier_II.Value;
                }
                case 3:
                {
                    return humanTeam ? _Human_Unit_Tier_III.Value : _Alien_Unit_Tier_III.Value;
                }
                case 4:
                {
                    return humanTeam ? _Human_Unit_Tier_IV.Value : _Alien_Unit_Tier_IV.Value;
                }
            }

            return "";
        }
        
        static ObjectInfo? GetSpawnObject(string prefabName)
        {
            int prefabIndex = GameDatabase.GetSpawnablePrefabIndex(prefabName);
            if (prefabIndex <= -1)
            {
                return null;
            }

            GameObject prefabObject = GameDatabase.GetSpawnablePrefab(prefabIndex);
            if (prefabObject == null)
            {
                return null;
            }

            BaseGameObject prefabBase = GameFuncs.GetBaseGameObject(prefabObject);
            if (prefabBase == null)
            {
                return null;
            }

            ObjectInfo spawnObject = prefabBase.ObjectInfo;
            if (spawnObject == null)
            {
                return null;
            }

            return spawnObject;
        }
    }
}