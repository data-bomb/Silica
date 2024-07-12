/*
 Silica Better Spawns Mod
 Copyright (C) 2024 by databomb
 
 * Description *
 For Silica servers, prioritizes better spawns so players can quickly
 change loadout or find better units.

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
using Si_BetterSpawns;
using SilicaAdminMod;
using System;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(BetterSpawns), "Better Spawns", "1.0.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_BetterSpawns
{
    public class BetterSpawns : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        public static MelonPreferences_Entry<bool> Pref_BetterSpawns_Human_ReselectSpawn_Enabled = null!;
        public static MelonPreferences_Entry<bool> Pref_BetterSpawns_Human_InitialSpawn_Enabled = null!;
        public static MelonPreferences_Entry<bool> Pref_BetterSpawns_Alien_ReselectSpawn_Enabled = null!;
        public static MelonPreferences_Entry<bool> Pref_BetterSpawns_Alien_InitialSpawn_Enabled = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_BetterSpawns_Human_ReselectSpawn_Enabled ??= _modCategory.CreateEntry<bool>("BetterSpawns_Humans_SetReselectSpawns", true);
            Pref_BetterSpawns_Human_InitialSpawn_Enabled ??= _modCategory.CreateEntry<bool>("BetterSpawns_Humans_SetInitialSpawns", true);
            Pref_BetterSpawns_Alien_ReselectSpawn_Enabled ??= _modCategory.CreateEntry<bool>("BetterSpawns_Aliens_SetReselectSpawns", true);
            Pref_BetterSpawns_Alien_InitialSpawn_Enabled ??= _modCategory.CreateEntry<bool>("BetterSpawns_Aliens_SetInitialSpawns", true);
        }

        // override GetSafeSpawnPoint to say nothing is ever safe, so revert to the random method to find the best spawn point instead
        [HarmonyPatch(typeof(SpawnPoint), nameof(SpawnPoint.GetSafeSpawnPoint))]
        private static class ApplyPatch_SpawnPoint_GetSafeSpawnPoint
        {
            public static bool Prefix(ref SpawnPoint __result, Team __0, float __1, float __2)
            {
                try
                {
                    // check if we're supposed to change the game code or not for the given team
                    // alien team
                    if (__0.Index == (int)SiConstants.ETeam.Alien)
                    {
                        if (!Pref_BetterSpawns_Alien_ReselectSpawn_Enabled.Value)
                        {
                            return true;
                        }
                    }
                    // human teams
                    else if (__0.Index == (int)SiConstants.ETeam.Sol || __0.Index == (int)SiConstants.ETeam.Centauri)
                    {
                        if (!Pref_BetterSpawns_Human_ReselectSpawn_Enabled.Value)
                        {
                            return true;
                        }
                    }
                    // ignore for non-playable teams
                    else
                    {
                        return true;
                    }

                    // suppress compiler warning since changing the prototype is less ideal
                    #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                    __result = null;
                    #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run SpawnPoint::GetSafeSpawnPoint");
                }

                return true;
            }
        }

        // this method is used for initial spawning only
        [HarmonyPatch(typeof(Structure), nameof(Structure.GetClosestStructureForRespawn))]
        private static class ApplyPatch_Structure_GetClosestStructureForRespawn
        {
            public static bool Prefix(ref Structure __result, Team __0, UnityEngine.Vector3 __1)
            {
                try
                {
                    if (__0 == null)
                    {
                        return true;
                    }

                    // check if we're supposed to change the game code or not for the given team
                    // alien team
                    if (__0.Index == (int)SiConstants.ETeam.Alien)
                    {
                        if (!Pref_BetterSpawns_Alien_InitialSpawn_Enabled.Value)
                        {
                            return true;
                        }

                        // TODO
                    }
                    // human teams
                    else if (__0.Index == (int)SiConstants.ETeam.Sol || __0.Index == (int)SiConstants.ETeam.Centauri)
                    {
                        if (!Pref_BetterSpawns_Human_InitialSpawn_Enabled.Value)
                        {
                            return true;
                        }

                        List<int> spawnableBarracks = FindAllSpawnableBarracks(__0);

                        // at the beginning of the game, use the game's default code to find a random spawn point at the HQ
                        if (spawnableBarracks.Count <= 0)
                        {
                            return true;
                        }

                        // if the position is invalid then fallback to selecting a random barracks instead
                        if (__1 == UnityEngine.Vector3.zero)
                        {
                            System.Random randomIndex = new System.Random();
                            int randomBarracks = spawnableBarracks[randomIndex.Next(0, spawnableBarracks.Count)];

                            int spawnPointsAtRandomBarracks = __0.Structures[randomBarracks].SpawnPoints.Count;
                            if (spawnPointsAtRandomBarracks <= 0)
                            {
                                return true;
                            }

                            __result = __0.Structures[randomBarracks];
                            return false;
                        }

                        Structure? closestBarracks = GetClosestBarracks(__0, __1);
                        if (closestBarracks == null)
                        {
                            return true;
                        }

                        // select to spawn at the closest barracks
                        __result = closestBarracks;

                        // if we got to this point then don't run the game code as we trust this structure is valid
                        return false;
                    }
                    // ignore for non-playable teams
                    else
                    {
                        return true;
                    }

                    // TODO
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Structure::GetClosestStructureForRespawn");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(SpawnPoint), nameof(SpawnPoint.GetRandomSpawnPoint))]
        private static class ApplyPatch_SpawnPoint_GetRandomSpawnPoint
        {
            public static bool Prefix(ref SpawnPoint __result, Team __0)
            {
                try
                {
                    if (__0 == null)
                    {
                        return true;
                    }

                    // only do something for playable teams for now
                    if (__0.Index == (int)SiConstants.ETeam.Wildlife)
                    {
                        return true;
                    }

                    int totalSpawnCount = __0.SpawnPoints.Count;
                    if (totalSpawnCount <= 0)
                    {
                        return true;
                    }

                    int totalStructures = __0.Structures.Count;
                    if (totalStructures <= 0)
                    {
                        return true;
                    }

                    // for human teams
                    if (__0.Index == (int)SiConstants.ETeam.Sol || __0.Index == (int)SiConstants.ETeam.Centauri)
                    {
                        SpawnPoint? spawnPointRandomBarracks = FindRandomHumanBarracksSpawn(__0);
                        if (spawnPointRandomBarracks == null)
                        {
                            return true;
                        }

                        // select random spawn point at barracks
                        __result = spawnPointRandomBarracks;

                        // if we got to this point then don't run the game code as we trust this spawn point is valid
                        return false;
                    }
                    // for alien team
                    else if (__0.Index == (int)SiConstants.ETeam.Alien)
                    {
                        // placeholder
                        return true;
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run SpawnPoint::GetRandomSpawnPoint");
                }

                return true;
            }
        }

        private static SpawnPoint? FindRandomHumanBarracksSpawn(Team team)
        {
            List<int> spawnableBarracks = FindAllSpawnableBarracks(team);

            // at the beginning of the game, use the game's default code to find a random spawn point at the HQ
            if (spawnableBarracks.Count <= 0)
            {
                return null;
            }

            // select random barracks
            System.Random randomIndex = new System.Random();
            int randomBarracks = spawnableBarracks[randomIndex.Next(0, spawnableBarracks.Count)];

            int spawnPointsAtRandomBarracks = team.Structures[randomBarracks].SpawnPoints.Count;
            if (spawnPointsAtRandomBarracks <= 0)
            {
                return null;
            }

            // select random spawn point at barracks
            return team.Structures[randomBarracks].SpawnPoints[randomIndex.Next(0, spawnPointsAtRandomBarracks)];
        }

        private static List<int> FindAllSpawnableBarracks(Team team)
        {
            List<int> spawnableBarracks = new List<int>();

            for (int i = 0; i < team.Structures.Count; i++)
            {
                if (team.Structures[i].ToString().StartsWith("Barracks"))
                {
                    if (team.Structures[i].DamageManager.IsDestroyed)
                    {
                        continue;
                    }

                    if (!team.Structures[i].HasSpawnPoints)
                    {
                        continue;
                    }

                    spawnableBarracks.Add(i);
                }
            }

            return spawnableBarracks;
        }

        private static Structure? GetClosestAlienBaseSpawn(Team team, UnityEngine.Vector3 location)
        {
            Structure? closestBaseSpawn = null;
            return closestBaseSpawn;
        }

        private static Structure? GetClosestBarracks(Team team, UnityEngine.Vector3 location)
        {
            Structure? closestBarracks = null;
            float lowestDistance = 99999f;

            foreach (Structure structure in team.Structures)
            {
                if (structure == null)
                {
                    continue;
                }

                if (structure.IsDestroyed || !structure.HasSpawnPoints)
                {
                    continue;
                }

                // ignore HQs, which have 16 spawn points
                if (structure.SpawnPoints.Count >= 15)
                {
                    continue;
                }

                float distanceToBarracks = GameMath.DistanceSq(location, structure.transform.position);
                if (distanceToBarracks < lowestDistance)
                {
                    closestBarracks = structure;
                    lowestDistance = distanceToBarracks;
                }
            }

            return closestBarracks;
        }
    }
}