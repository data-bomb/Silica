using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using VersusTeamsAutoSelect;

[assembly: MelonInfo(typeof(VersusTeamsAutoSelectMod), "[Si] Versus Auto-Select Team", "1.0.1", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace VersusTeamsAutoSelect
{

    public class VersusTeamsAutoSelectMod : MelonMod
    {
        public static void PrintError(Exception exception, string message = null)
        {
            if (message != null)
            {
                MelonLogger.Error(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.Error(error);
        }

        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<Il2Cpp.MP_Strategy.ETeamsVersus> _versusAutoSelectMode;

        private const string ModCategory = "Silica";
        private const string AutoSelectMode = "VersusAutoSelectMode";
        
        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_versusAutoSelectMode == null)
            {
                _versusAutoSelectMode = _modCategory.CreateEntry<Il2Cpp.MP_Strategy.ETeamsVersus>(AutoSelectMode, Il2Cpp.MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS, "Valid choices are HUMANS_VS_HUMANS, HUMANS_VS_ALIENS, or HUMANS_VS_HUMANS_VS_ALIENS");
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.Restart))]
        public static class ApplyPatchSelectHumansVersusAliens
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance)
            {
                try
                {
                    Il2Cpp.MP_Strategy.ETeamsVersus versusMode = VersusTeamsAutoSelectMod._versusAutoSelectMode.Value;
                    __instance.SetTeamVersusMode(versusMode);
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run Restart");
                }
            }
        }
    }
}