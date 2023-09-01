using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_Resources;
using UnityEngine;
using AdminExtension;

[assembly: MelonInfo(typeof(ResourceConfig), "Resource Configuration", "1.0.1", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_Resources
{
    public class ResourceConfig : MelonMod
    {
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<int> Pref_Resources_Humans_StartingAmount;
        static MelonPreferences_Entry<int> Pref_Resources_Aliens_StartingAmount;

        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory("Silica");
            }
            if (Pref_Resources_Humans_StartingAmount == null)
            {
                Pref_Resources_Humans_StartingAmount = _modCategory.CreateEntry<int>("Resources_Humans_StartingAmount", 11000);
            }
            if (Pref_Resources_Aliens_StartingAmount == null)
            {
                Pref_Resources_Aliens_StartingAmount = _modCategory.CreateEntry<int>("Resources_Aliens_StartingAmount", 11000);
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.SetTeamVersusMode))]
        private static class Resources_Patch_MPStrategy_SetTeamVersusMode
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.MP_Strategy.ETeamsVersus __0)
            {
                try
                {
                    switch (__0)
                    {
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS:
                        {
                            // Sol
                            Il2Cpp.Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;
                            // Centauri
                            Il2Cpp.Team.Teams[1].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

                            MelonLogger.Msg("Set starting resources. Humans: " + Pref_Resources_Humans_StartingAmount.Value.ToString());
                            break;
                        }
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS:
                        {
                            // Alien
                            Il2Cpp.Team.Teams[0].StartingResources = Pref_Resources_Aliens_StartingAmount.Value;
                            // Sol
                            Il2Cpp.Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

                            MelonLogger.Msg("Set starting resources. Aliens: " + Pref_Resources_Aliens_StartingAmount.Value.ToString() + " Humans: " + Pref_Resources_Humans_StartingAmount.Value.ToString());
                            break;
                        }
                        case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS:
                        {
                            // Alien
                            Il2Cpp.Team.Teams[0].StartingResources = Pref_Resources_Aliens_StartingAmount.Value;
                            // Sol
                            Il2Cpp.Team.Teams[1].StartingResources = Pref_Resources_Humans_StartingAmount.Value;
                            // Centauri
                            Il2Cpp.Team.Teams[2].StartingResources = Pref_Resources_Humans_StartingAmount.Value;

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