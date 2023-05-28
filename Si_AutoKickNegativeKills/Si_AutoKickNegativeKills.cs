using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_AutoKickNegativeKills;
using UnityEngine;

[assembly: MelonInfo(typeof(AutoKickNegativeKills), "[Si] Auto-Kick Negative Kills", "1.0.1", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AutoKickNegativeKills
{
    public class AutoKickNegativeKills : MelonMod
    {
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<int> _NegativeKillsThreshold;

        private const string ModCategory = "Silica";
        private const string ModEntryString = "AutoKickNegativeKillsThreshold";

        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_NegativeKillsThreshold == null)
            {
                _NegativeKillsThreshold = _modCategory.CreateEntry<int>(ModEntryString, -100);
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnUnitDestroyed))]
        private static class ApplyPatch_OnUnitDestroyed
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Unit __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                // Victim
                Il2Cpp.Team victimTeam = __0.Team;
                
                // Attacker
                Il2Cpp.BaseGameObject attackerBase = Il2Cpp.GameFuncs.GetBaseGameObject(__2);
                Il2Cpp.Team attackerTeam = attackerBase.Team;

                // don't check unless it was a team kill by a unit
                if (victimTeam == attackerTeam)
                {
                    Il2Cpp.ObjectInfoType attackerType = attackerBase.ObjectInfo.ObjectType;
                    if (attackerType == Il2Cpp.ObjectInfoType.Unit)
                    {
                        Il2Cpp.Player victimPlayer = __0.m_ControlledBy;
                        Il2Cpp.NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                        if (attackerNetComp != null)
                        {
                            Il2Cpp.Player attackerPlayer = attackerNetComp.OwnerPlayer;
                            if (victimPlayer != attackerPlayer)
                            {
                                // check score of attacker
                                short currentKillScore = attackerPlayer.m_Kills;
                                MelonLogger.Msg(attackerPlayer.PlayerName + " destroyed a friendly unit with kill score of " + currentKillScore.ToString());

                                if (currentKillScore < _NegativeKillsThreshold.Value)
                                {
                                    MelonLogger.Msg("Kicked " + attackerPlayer.PlayerName + " (" + attackerPlayer.ToString + ")");
                                    attackerPlayer.SendChatMessage("<<< violated team-killing rules and was kicked.");
                                    Il2Cpp.NetworkGameServer.KickPlayer(attackerPlayer);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}