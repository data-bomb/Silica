/*
Silica Early Encounters
Copyright (C) 2025 by databomb

* Description *
Sets up random early-game encounters for FPS players.

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
using Il2CppSilica.AI;
#else
using System.Reflection;
using Silica.AI;
#endif

using HarmonyLib;
using MelonLoader;
using SilicaAdminMod;
using System;
using Si_EarlyEncounters;
using System.Collections.Generic;
using UnityEngine;

[assembly: MelonInfo(typeof(EarlyEncounters), "Early Encounters", "0.9.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_EarlyEncounters
{
    public class EarlyEncounters : MelonMod
    {
        static MelonPreferences_Category _Pref_modCategory = null!;
        static MelonPreferences_Entry<int> _Pref_EarlyEncounters_Chance_TeamResourceEvent = null!;
        static MelonPreferences_Entry<int> _Pref_EarlyEncounters_Chance_FriendlyUnitEvent = null!;
        static MelonPreferences_Entry<int> _Pref_EarlyEncounters_Chance_EnemyWormEvent = null!;
        static MelonPreferences_Entry<int> _Pref_EarlyEncounters_Chance_EnemyUnitEvent = null!;
        static MelonPreferences_Entry<int> _Pref_EarlyEncounters_CratesPerTeam = null!;

        public static Dictionary<EEncounterType, MelonPreferences_Entry<int>> encounterProbabilities = new Dictionary<EEncounterType, MelonPreferences_Entry<int>>();
        public static string[] crateSpawnNames = { "SpecialContainer_Long_01_Large", "SpecialContainer_Long_01_Small", "SpecialContainer_Short_01_Small" };

        public override void OnInitializeMelon()
        {
            _Pref_modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Pref_EarlyEncounters_Chance_TeamResourceEvent ??= _Pref_modCategory.CreateEntry<int>("EarlyEncounter_TeamResourceEventChance", 100);
            _Pref_EarlyEncounters_Chance_FriendlyUnitEvent ??= _Pref_modCategory.CreateEntry<int>("EarlyEncounter_FriendlyUnitEventChance", 100);
            _Pref_EarlyEncounters_Chance_EnemyWormEvent ??= _Pref_modCategory.CreateEntry<int>("EarlyEncounter_EnemyWormEventChance", 100);
            _Pref_EarlyEncounters_Chance_EnemyUnitEvent ??= _Pref_modCategory.CreateEntry<int>("EarlyEncounter_EnemyUnitEventChance", 100);
            _Pref_EarlyEncounters_CratesPerTeam ??= _Pref_modCategory.CreateEntry<int>("EarlyEncounter_CratesPerTeam", 8);
        }
        public override void OnLateInitializeMelon()
        {
            FillEncounterDictionary();
        }

        public enum EEncounterType
        {
            TeamResources = 0,
            FriendlyUnit = 1,
            EnemyWorm = 2,
            EnemyUnit = 3
        }

        private static void FillEncounterDictionary()
        {
            encounterProbabilities.Clear();
            encounterProbabilities.Add(EEncounterType.TeamResources, _Pref_EarlyEncounters_Chance_TeamResourceEvent);
            encounterProbabilities.Add(EEncounterType.FriendlyUnit, _Pref_EarlyEncounters_Chance_FriendlyUnitEvent);
            encounterProbabilities.Add(EEncounterType.EnemyWorm, _Pref_EarlyEncounters_Chance_EnemyWormEvent);
            encounterProbabilities.Add(EEncounterType.EnemyUnit, _Pref_EarlyEncounters_Chance_EnemyUnitEvent);
        }

        private static int TotalProbabilityValue()
        {
            int totalValue = 0;

            foreach (var pairing in encounterProbabilities)
            {
                if (pairing.Value.Value <= 0)
                {
                    continue;
                }

                totalValue += pairing.Value.Value;
            }

            return totalValue;
        }

        // find random event
        public static EEncounterType FindEncounterType()
        {
            int maxValue = TotalProbabilityValue();
            int randomValue = UnityEngine.Random.Range(0, maxValue);

            MelonLogger.Msg("Crate Probability = (" + randomValue.ToString() + "/" + maxValue.ToString() + ")");

            // find paired encounter
            int currentValue = 0;
            foreach (var pairing in encounterProbabilities)
            {
                currentValue += pairing.Value.Value;
                if (pairing.Value.Value <= 0)
                {
                    continue;
                }

                if (randomValue <= currentValue)
                {
                    return pairing.Key;
                }
            }

            MelonLogger.Warning("Could not find a valid random encounter. Check probability parameters.");
            return EEncounterType.EnemyWorm;
        }

        private static string GetFriendlyUnitName(Team team)
        {
            switch (team.Index)
            {
                case (int)SiConstants.ETeam.Alien:
                    return "Wasp";
                case (int)SiConstants.ETeam.Centauri:
                    return "Cent_Soldier_Trooper";
                case (int)SiConstants.ETeam.Sol:
                default:
                    return "Sol_Soldier_Rifleman";
            }
        }

        // handle random event
        public static string HandleCrateEncounter(EEncounterType encounterType, Player player)
        {
            Vector3 targetPosition = player.ControlledUnit.WorldPhysicalCenter;
            Quaternion rotatedQuaternion = GameMath.GetRotatedQuaternion(Quaternion.identity, Vector3.up * UnityEngine.Random.Range(-180f, 180f));
            Target target = Target.GetTargetByNetID(player.ControlledUnit.NetworkComponent.NetID);
            Vector3 spawnVector = targetPosition + rotatedQuaternion * Vector3.forward * UnityEngine.Random.Range(10f, 20f);

            switch (encounterType)
            {
                case EEncounterType.TeamResources:
                    Team team = player.Team;
                    team.StoreResource(300);
                    return "a Team Resource Bonus (300)";
                case EEncounterType.FriendlyUnit:

                    HelperMethods.SpawnAtLocation(GetFriendlyUnitName(player.Team), spawnVector, rotatedQuaternion, (int)player.Team.Index);
                    return "a Friendly Defector";
                case EEncounterType.EnemyUnit:
                    HelperMethods.SpawnAtLocation("Sol_Soldier_Heavy", spawnVector, rotatedQuaternion, (int)SiConstants.ETeam.Wildlife);
                    rotatedQuaternion = GameMath.GetRotatedQuaternion(Quaternion.identity, Vector3.up * UnityEngine.Random.Range(-180f, 180f));
                    spawnVector = targetPosition + rotatedQuaternion * Vector3.forward * UnityEngine.Random.Range(10f, 20f);
                    HelperMethods.SpawnAtLocation("Sol_Soldier_Heavy", spawnVector, rotatedQuaternion, (int)SiConstants.ETeam.Wildlife);
                    return "Enemey Forces";
                case EEncounterType.EnemyWorm:
                default:
                    AmbientLife wildLifeInstance = GameObject.FindFirstObjectByType<AmbientLife>();
                    GameObject wormObject = Game.SpawnPrefab(wildLifeInstance.Basic[UnityEngine.Random.Range(0, wildLifeInstance.Basic.Count - 1)].Prefab, null, wildLifeInstance.Team, targetPosition, rotatedQuaternion, true, true);
                    Unit? wormUnit = wormObject.GetBaseGameObject() as Unit;
                    if (wormUnit == null)
                    {
                        return "Worm";
                    }
                    wormUnit.OnAttackOrder(target, target.transform.position, AgentMoveSpeed.Fast, true);
                    return "a Worm";
            }
        }

        private static void SpawnRandomCrate()
        {
            float x_coord = UnityEngine.Random.Range(0f, 1f);
            float y_coord = UnityEngine.Random.Range(0f, 1f);
            Vector2 mapPercentages = new Vector2(x_coord, y_coord);
            Vector2 mapCoords = GetWorldMapPositionFromPercentage(mapPercentages);
            Vector3 finalPosition = GetFinalWorldMapPosition(mapCoords);
            string crateTypeName = crateSpawnNames[UnityEngine.Random.Range(0, crateSpawnNames.Length)];

            Quaternion rotatedQuaternion = GameMath.GetRotatedQuaternion(Quaternion.identity, Vector3.up * UnityEngine.Random.Range(-180f, 180f));
            GameObject? spawnedObject = HelperMethods.SpawnAtLocation(crateTypeName, finalPosition, rotatedQuaternion, (int)SiConstants.ETeam.Wildlife);

            if (spawnedObject == null)
            {
                MelonLogger.Warning("Failed to spawn crate");
                return;
            }
        }

        // spawn containers at random locations
        [HarmonyPatch(typeof(StrategyTeamSetup), nameof(StrategyTeamSetup.SpawnAIUnits))]
        private static class EarlyEncounters_Patch_StrategyTeamSetup_SpawnAIUnits
        {
            public static void Postfix(StrategyTeamSetup __instance)
            {
                try
                {
                    MelonLogger.Msg("Setting up random crates");

                    for (int i = 0; i < _Pref_EarlyEncounters_CratesPerTeam.Value; i++)
                    {
                        SpawnRandomCrate();
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StrategyTeamSetup::SpawnAIUnits");
                }
            }
        }
        private static Vector2 GetWorldMapPositionFromPercentage(Vector2 mapPositionPercentages)
        {
            Bounds bounds = Game.MainTerrain.terrainData.bounds;
            Vector3 position = Game.MainTerrain.transform.position;
            return new Vector2(bounds.size.x * mapPositionPercentages.x + position.x, bounds.size.z * mapPositionPercentages.y + position.z);
        }

        private static Vector3 GetFinalWorldMapPosition(Vector2 mapPositions)
        {
            Vector3 finalPosition = new Vector3();
            finalPosition.x = mapPositions.x;
            finalPosition.y = 2000f;
            finalPosition.z = mapPositions.y;

            RaycastHit raycastHit;
            if (Physics.Raycast(finalPosition, Vector3.down, out raycastHit, 4000f, GamePhysics.TRACELAYERMASK_IGNOREDEBRIS, QueryTriggerInteraction.Ignore))
            {
                finalPosition = raycastHit.point;
            }
            else
            {
                MelonLogger.Warning("Could not find intercept with ground terrain for x,z (", (int)finalPosition.x, ", ", (int)finalPosition.z);
            }

            return finalPosition;
        }

        // handle the first player to open a container
        #if NET6_0
        [HarmonyPatch(typeof(OpenableBase), nameof(OpenableBase.OnUnitEnterZone))]
        #else
        [HarmonyPatch(typeof(OpenableBase), "OnUnitEnterZone")]
        #endif
        private static class EarlyEncounters_Patch_OpenableBase_OnUnitEnterZone
        {
            public static void Postfix(OpenableBase __instance, Zone __0, Unit __1)
            {
                try
                {
                    if (__instance == null || __instance.NetworkComponent == null || __1 == null || __1.NetworkComponent.Owner == null)
                    {
                        return;
                    }

                    // avoid any AI-controlled units
                    if (__1.NetworkComponent.OwnerPlayer == null)
                    {
                        // destroy crate
                        UnityEngine.Object.Destroy(__instance.NetworkComponent.gameObject);

                        return;
                    }

                    // avoid handling anything but the containers opening. 
                    if (!__instance.NetworkComponent.Owner.ObjectInfo.DisplayName.Equals("Container"))
                    {
                        return;
                    }

                    // find an encounter
                    EEncounterType encounter = FindEncounterType();
                    MelonLogger.Msg("Found player " + (__1.NetworkComponent.OwnerPlayer.PlayerName) + " opening container crate with " + __1.ToString() + " to find encounter " + encounter.ToString());
                    
                    // handle the encounter
                    string encounterText = HandleCrateEncounter(encounter, __1.NetworkComponent.OwnerPlayer);

                    HelperMethods.SendChatMessageToPlayer(__1.NetworkComponent.OwnerPlayer, HelperMethods.chatPrefix, " Unlocked a crate with ", encounterText);

                    // destroy crate
                    UnityEngine.Object.Destroy(__instance.NetworkComponent.gameObject);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OpenableBase::OnUnitEnterZone");
                }
            }
        }
    }
}