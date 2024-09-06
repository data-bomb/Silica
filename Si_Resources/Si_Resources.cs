/*
Silica Resources Mod
Copyright (C) 2024 by databomb

* Description *
Provides a server host the ability to configure different starting 
resource amounts for humans vs alien teams.

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
using Si_Resources;
using SilicaAdminMod;
using System;
using System.Linq;

[assembly: MelonInfo(typeof(ResourceConfig), "Resource Configuration", "1.2.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Resources
{
    public class ResourceConfig : MelonMod
    {
        const int defaultStartingResources = 8000;

        enum EResource
        {
            Balterium = 0,
            Biotics = 1
        }

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> Pref_Resources_Centauri_StartingAmount = null!;
        static MelonPreferences_Entry<int> Pref_Resources_Sol_StartingAmount = null!;
        static MelonPreferences_Entry<int> Pref_Resources_Aliens_StartingAmount = null!;
        static MelonPreferences_Entry<bool> Pref_Resources_Aliens_RevealClosestArea = null!;
        static MelonPreferences_Entry<bool> Pref_Resources_Humans_RevealClosestArea = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_Resources_Centauri_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Centauri_StartingAmount", defaultStartingResources);
            Pref_Resources_Sol_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Sol_StartingAmount", defaultStartingResources);
            Pref_Resources_Aliens_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Aliens_StartingAmount", defaultStartingResources);
            Pref_Resources_Aliens_RevealClosestArea ??= _modCategory.CreateEntry<bool>("Resources_Aliens_RevealClosestAreaOnStart", false);
            Pref_Resources_Humans_RevealClosestArea ??= _modCategory.CreateEntry<bool>("Resources_Humans_RevealClosestAreaOnStart", true);
        }
       
        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback resourcesCallback = Command_Resources;
            HelperMethods.RegisterAdminCommand("resources", resourcesCallback, Power.Cheat, "Provides resources to a team. Usage: !resources <amount> [optional:<teamname>]");

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.IntOption centauriStartingRes = new(Pref_Resources_Centauri_StartingAmount, true, Pref_Resources_Centauri_StartingAmount.Value, 8000, 50000, 500);
            QList.OptionTypes.IntOption solStartingRes = new(Pref_Resources_Sol_StartingAmount, true, Pref_Resources_Sol_StartingAmount.Value, 8000, 50000, 500);
            QList.OptionTypes.IntOption alienStartingRes = new(Pref_Resources_Aliens_StartingAmount, true, Pref_Resources_Aliens_StartingAmount.Value, 8000, 50000, 500);

            QList.Options.AddOption(centauriStartingRes);
            QList.Options.AddOption(solStartingRes);
            QList.Options.AddOption(alienStartingRes);
            #endif
        }

        public static void Command_Resources(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 2)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            Team? team = null;
            if (argumentCount == 1)
            {
                if (callerPlayer != null)
                {
                    team = callerPlayer.Team;
                }
            }
            else
            {
                string teamTarget = args.Split(' ')[2];
                team = Team.GetTeamByName(teamTarget, false);
            }

            if (team == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous or invalid team name");
                return;
            }

            string amountText = args.Split(' ')[1];
            if (!int.TryParse(amountText, out int amount))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Invalid amount specified");
                return;
            }
            else if (amount == 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": No amount specified");
                return;
            }
            // 0 resource capacity happens in the early game and is interpretted as inf resource capacity
            else if (amount > team.RemainingResourceCapacity && team.RemainingResourceCapacity > 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Amount specified is higher than team resource capacity");
                return;
            }
            else if (amount + team.StoredResources < 0)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Amount specified would give team negative resources");
                return;
            }

            // change resource amount and notify players
            if (amount >= 0)
            {
                team.StoreResource(amount);
                HelperMethods.AlertAdminAction(callerPlayer, "granted " + amountText + " resources to " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>");
            }
            else
            {
                team.RetrieveResource(-amount);
                HelperMethods.AlertAdminAction(callerPlayer, "took " + amountText + " resources from " + HelperMethods.GetTeamColor(team) + team.TeamShortName + "</color>");
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.SetTeamVersusMode))]
        private static class Resources_Patch_MPStrategy_SetTeamVersusMode
        {
            public static void Postfix(MP_Strategy __instance, GameModeExt.ETeamsVersus __0)
            {
                try
                {
                    foreach (StrategyTeamSetup strategyTeamSetup in __instance.TeamSetups)
                    {
                        if (!__instance.GetTeamSetupActive(strategyTeamSetup))
                        {
                            continue;
                        }

                        // re-adjust starting resources immediately after the game sets it
                        strategyTeamSetup.Team.StartingResources = GetTeamStartingResources(strategyTeamSetup.Team);
                        MelonLogger.Msg("Set starting resources for Team (" + strategyTeamSetup.Team.TeamShortName + ") to " + strategyTeamSetup.Team.StartingResources);

                        // check if we should make the first biotics/balterium resource area visible to the team
                        if (!MakeClosestResearchAreaVisible(strategyTeamSetup.Team))
                        {
                            continue;
                        }

                        Resource? resourceType = GetTeamResourceType(strategyTeamSetup.Team);
                        if (resourceType == null)
                        {
                            MelonLogger.Warning("Could not find default resource type for team: " + strategyTeamSetup.Team.TeamShortName);
                            continue;
                        }

                        if (ResourceArea.GetNumKnownResourcesAreas(strategyTeamSetup.Team, resourceType) > 0)
                        {
                            MelonLogger.Msg("Already a visible resource type for team: " + strategyTeamSetup.Team.TeamShortName);
                            continue;
                        }

                        ResourceArea? closestStartingResourceArea = GetTeamClosestResourceArea(strategyTeamSetup.Team, resourceType);
                        if (closestStartingResourceArea == null)
                        {
                            MelonLogger.Warning("Could not find closest resource area for team: " + strategyTeamSetup.Team.TeamShortName);
                            continue;
                        }

                        UnityEngine.Vector3 unitSpawnPosition = closestStartingResourceArea.transform.position;
                        unitSpawnPosition[1] += 7f;

                        // make this visible by spawning a starting unit
                        string prefabName = (strategyTeamSetup.Team.Index == (int)SiConstants.ETeam.Alien ? "Crab" : "Soldier_Scout");
                        HelperMethods.SpawnAtLocation(prefabName, unitSpawnPosition, UnityEngine.Quaternion.identity, strategyTeamSetup.Team.Index);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::SetTeamVersusMode");
                }
            }
        }

        static bool MakeClosestResearchAreaVisible(Team team)
        {
            if (team.Index == (int)SiConstants.ETeam.Alien)
            {
                return Pref_Resources_Aliens_RevealClosestArea.Value;
            }
            else if (team.Index == (int)SiConstants.ETeam.Sol || team.Index == (int)SiConstants.ETeam.Centauri)
            {
                return Pref_Resources_Humans_RevealClosestArea.Value;
            }

            return false;
        }

        static ResourceArea? GetTeamClosestResourceArea(Team team, Resource type)
        {
            UnityEngine.Vector3 position = GetTeamStartingPosition(team);

            GameListCache.QueryResourceAreas.Clear();
            foreach (ResourceArea resourceArea in ResourceArea.ResourceAreas)
            {
                if (resourceArea.ResourceType != type)
                {
                    continue;
                }

                if (resourceArea.ResourceAmountCurrent <= 0)
                {
                    continue;
                }

                GameListCache.QueryResourceAreas.Add(resourceArea);
            }

            ResourceArea? closestArea = null;
            float closestDistance = float.MaxValue;
            foreach (ResourceArea resourceArea in GameListCache.QueryResourceAreas)
            {
                float sqrMagnitude = (resourceArea.SignalCenter - position).sqrMagnitude;
                if (sqrMagnitude < closestDistance)
                {
                    closestArea = resourceArea;
                    closestDistance = sqrMagnitude;
                }
            }

            return closestArea;
        }

        static UnityEngine.Vector3 GetTeamStartingPosition(Team team)
        {
            if (team.Structures.Count <= 0)
            {
                MelonLogger.Warning("Could not determine starting position for team: " + team.TeamShortName);
                return UnityEngine.Vector3.zero;
            }

            return team.Structures[0].transform.position;
        }

        static Resource? GetTeamResourceType(Team team)
        {
            switch (team.Index)
            {
                case (int)SiConstants.ETeam.Alien:
                    // "Biotics"
                    #if NET6_0
                    return Resource.Resources[(int)EResource.Biotics];
                    #else
                    return Resource.Resources.Find(r => r.ResourceName.StartsWith("Bi"));
                    #endif               
                case (int)SiConstants.ETeam.Sol:
                case (int)SiConstants.ETeam.Centauri:
                    // "Balterium"
                    #if NET6_0
                    return Resource.Resources[(int)EResource.Balterium];
                    #else
                    return Resource.Resources.Find(r => r.ResourceName.StartsWith("Ba"));
                    #endif
                default:
                    MelonLogger.Warning("Could not determine default resource type for team: " + team.TeamShortName);
                    return null;
            }
        }

        static int GetTeamStartingResources(Team team)
        {
            switch (team.Index)
            {
                case (int)SiConstants.ETeam.Alien:
                    return Pref_Resources_Aliens_StartingAmount.Value;
                case (int)SiConstants.ETeam.Centauri:
                    return Pref_Resources_Centauri_StartingAmount.Value;
                case (int)SiConstants.ETeam.Sol:
                    return Pref_Resources_Sol_StartingAmount.Value;
                default:
                    MelonLogger.Warning("Could not determine starting resources for team: " + team.TeamShortName);
                    return defaultStartingResources;
            }
        }
    }
}