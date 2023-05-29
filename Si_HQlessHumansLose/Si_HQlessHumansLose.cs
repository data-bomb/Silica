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
using UnityEngine;

[assembly: MelonInfo(typeof(HQlessHumansLose), "[Si] HQless Humans Lose", "1.0.0", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_HQlessHumansLose
{
    public class HQlessHumansLose : MelonMod
    {

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            private static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Structure __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                Il2Cpp.Team StructureTeam = __0.Team;

                String sStructureTeam = StructureTeam.TeamName;

                if (sStructureTeam.Contains("Human"))
                {
                    // did they just lose a headquarters?
                    if (__0.ToString().Contains("Headq"))
                    {
                        // find if it was the last headquarters
                        // start at -1 because destroyed HQ is counted
                        int iHeadquartersCount = -1;
                        for (int i = 0; i < StructureTeam.Structures.Count; i++)
                        {
                            if (StructureTeam.Structures[i].ToString().Contains("Headq"))
                            {
                                iHeadquartersCount++;
                            }
                        }

                        if (iHeadquartersCount == 0)
                        {
                            // end the game
                            for (int i = 0; i < StructureTeam.Structures.Count; i++)
                            {
                                StructureTeam.Structures[i].DamageManager.SetHealth01(0.0f);
                            }
                        }    
                    }
                }
            }
        }
    }
}