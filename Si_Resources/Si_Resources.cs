﻿/*
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

[assembly: MelonInfo(typeof(ResourceConfig), "Resource Configuration", "1.0.6", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Resources
{
    public class ResourceConfig : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> Pref_Resources_Humans_StartingAmount = null!;
        static MelonPreferences_Entry<int> Pref_Resources_Aliens_StartingAmount = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_Resources_Humans_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Humans_StartingAmount", 11000);
            Pref_Resources_Aliens_StartingAmount ??= _modCategory.CreateEntry<int>("Resources_Aliens_StartingAmount", 9000);
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

            QList.OptionTypes.IntOption humanStartingRes = new(Pref_Resources_Humans_StartingAmount, true, Pref_Resources_Humans_StartingAmount.Value, 3500, 50000, 500);
            QList.OptionTypes.IntOption alienStartingRes = new(Pref_Resources_Aliens_StartingAmount, true, Pref_Resources_Aliens_StartingAmount.Value, 3500, 50000, 500);

            QList.Options.AddOption(humanStartingRes);
            QList.Options.AddOption(alienStartingRes);
        }
        #endif

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