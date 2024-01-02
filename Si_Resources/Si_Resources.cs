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

[assembly: MelonInfo(typeof(ResourceConfig), "Resource Configuration", "1.0.3", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Resources
{
    public class ResourceConfig : MelonMod
    {
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<int> Pref_Resources_Humans_StartingAmount;
        static MelonPreferences_Entry<int> Pref_Resources_Aliens_StartingAmount;
        #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_Resources_Humans_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Humans_StartingAmount", 11000);
            Pref_Resources_Aliens_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Aliens_StartingAmount", 9000);
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.SetTeamVersusMode))]
        private static class Resources_Patch_MPStrategy_SetTeamVersusMode
        {
            public static void Postfix(MP_Strategy __instance, MP_Strategy.ETeamsVersus __0)
            {
                try
                {
                    switch (__0)
                    {
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS:
                        {
                            // Sol
                            Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;
                            // Centauri
                            Team.Teams[1].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

                            MelonLogger.Msg("Set starting resources. Humans: " + Pref_Resources_Humans_StartingAmount.Value.ToString());
                            break;
                        }
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS:
                        {
                            // Alien
                            Team.Teams[0].StartingResources = Pref_Resources_Aliens_StartingAmount.Value;
                            // Sol
                            Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

                            MelonLogger.Msg("Set starting resources. Aliens: " + Pref_Resources_Aliens_StartingAmount.Value.ToString() + " Humans: " + Pref_Resources_Humans_StartingAmount.Value.ToString());
                            break;
                        }
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS:
                        {
                            // Alien
                            Team.Teams[0].StartingResources = Pref_Resources_Aliens_StartingAmount.Value;
                            // Sol
                            Team.Teams[1].StartingResources = Pref_Resources_Humans_StartingAmount.Value;
                            // Centauri
                            Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

                            MelonLogger.Msg("Set starting resources. Aliens: " + Pref_Resources_Aliens_StartingAmount.Value.ToString() + " Humans: " + Pref_Resources_Humans_StartingAmount.Value.ToString());
                            break;
                        }
                    }

                    if (__0 != MP_Strategy.ETeamsVersus.NONE)
                    {
                        // set how many resources are in each resource area

                        // hook? ResourceArea.DistributeAllResources
                        // iterate and set ResourceArea.ResourceAmountMax = ?
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::SetTeamVersusMode");
                }
            }
        }
    }
}