/*
 Silica Auto-Kick Negative Kills Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, automatically identifies players who fall
 below a certain negative kill threshold. When someone reaches the
 threshold then players are alerted in chat, hosts are alerted in their 
 log, and the player is kicked.

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
using Si_AutoKickNegativeKills;
using UnityEngine;

[assembly: MelonInfo(typeof(AutoKickNegativeKills), "[Si] Auto-Kick Negative Kills", "1.0.2", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
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
                                    String sPlayerNameToKick = attackerPlayer.PlayerName;
                                    MelonLogger.Msg("Kicked " + sPlayerNameToKick + " (" + attackerPlayer.ToString + ")");
                                    Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                                    Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, 0, sPlayerNameToKick + " was kicked for teamkilling.", false);
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