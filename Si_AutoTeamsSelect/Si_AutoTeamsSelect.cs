using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using VersusTeamsAutoSelect;

[assembly: MelonInfo(typeof(VersusTeamsAutoSelectMod), "[Si] Versus Auto-Select Team", "1.0.0", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace VersusTeamsAutoSelect
{

    public class VersusTeamsAutoSelectMod : MelonMod
    {
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
                Il2Cpp.MP_Strategy.ETeamsVersus versusMode = VersusTeamsAutoSelectMod._versusAutoSelectMode.Value;
                __instance.SetTeamVersusMode(versusMode);
            }
        }
    }
}