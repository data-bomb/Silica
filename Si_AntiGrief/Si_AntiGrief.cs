/*
 Silica Anti-Grief Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica servers, automatically identifies players who fall below a 
 certain negative kill threshold. When someone reaches the threshold 
 then players are alerted in chat, hosts are alerted in their log, 
 and the player is kicked.

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
using Si_AntiGrief;
using AdminExtension;

[assembly: MelonInfo(typeof(AntiGrief), "[Si] Anti-Grief", "1.1.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AntiGrief
{
    public class AntiGrief : MelonMod
    {
        static MelonPreferences_Category? _modCategory;
        static MelonPreferences_Entry<int>? _NegativeKillsThreshold;
        static MelonPreferences_Entry<bool>? _NegativeKills_Penalty_Ban;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _NegativeKillsThreshold ??= _modCategory.CreateEntry<int>("Grief_NegativeKills_Threshold", -120);
            _NegativeKills_Penalty_Ban ??= _modCategory.CreateEntry<bool>("Grief_NegativeKills_Penalty_Ban", true);
        }

        [HarmonyPatch(typeof(Il2Cpp.StrategyMode), nameof(Il2Cpp.StrategyMode.OnUnitDestroyed))]
        private static class ApplyPatch_StrategyMode_OnUnitDestroyed
        {
            public static void Postfix(Il2Cpp.StrategyMode __instance, Il2Cpp.Unit __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 == null || __2 == null || _NegativeKillsThreshold == null || _NegativeKills_Penalty_Ban == null)
                    {
                        return;
                    }

                    // Victim
                    Team victimTeam = __0.Team;
                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__2);

                    if (attackerBase == null || victimTeam == null)
                    {
                        return;
                    }

                    Team attackerTeam = attackerBase.Team;

                    // don't check unless it was a team kill by a unit
                    if (attackerTeam == null || (attackerTeam.Index != victimTeam.Index))
                    {
                        return;
                    }


                    ObjectInfo attackerObjectInfo = attackerBase.ObjectInfo;
                    if (attackerObjectInfo == null)
                    {
                        return;
                    }

                    ObjectInfoType? attackerType = attackerObjectInfo.ObjectType;

                    if (attackerType == null || attackerType != ObjectInfoType.Unit)
                    {
                        return;
                    }

                    Player victimPlayer = __0.m_ControlledBy;
                    NetworkComponent attackerNetComp = attackerBase.NetworkComponent;

                    // was teamkiller a playable character?
                    if (attackerNetComp == null)
                    {
                        return;
                    }

                    Player attackerPlayer = attackerNetComp.OwnerPlayer;
                    // don't need to worry about fall damage or other self-inflicted damage
                    if (attackerPlayer == null || (victimPlayer == attackerPlayer))
                    {
                        return;
                    }

                    // check score of attacker
                    short currentKillScore = attackerPlayer.m_Kills;
                    MelonLogger.Msg(attackerPlayer.PlayerName + " destroyed a friendly unit with kill score of " + currentKillScore.ToString());

                    // check if another player was the victim
                    Player serverPlayer = NetworkGameServer.GetServerPlayer();
                    if (victimPlayer != null)
                    {
                        MelonLogger.Msg(attackerPlayer.PlayerName + " team killed " + victimPlayer.PlayerName);
                        NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(attackerPlayer) + attackerPlayer.PlayerName + HelperMethods.defaultColor + " team killed " + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName, false);
                    }

                    if (currentKillScore >= _NegativeKillsThreshold.Value)
                    {
                        return;
                    }

                    String sPlayerNameToKick = attackerPlayer.PlayerName;

                    if (_NegativeKills_Penalty_Ban.Value)
                    {
                        MelonLogger.Msg("Banned " + sPlayerNameToKick + " (" + attackerPlayer.ToString() + ") for griefing (negative kills)");
                        HelperMethods.ReplyToCommand_Player(attackerPlayer, "was banned for griefing (negative kills)");
                        // this uses the default in-game kick button response, which at least imposes a temp-ban for the life of the server (if not more)
                        NetworkGameServer.KickPlayer(attackerPlayer);
                    }
                    else
                    {
                        MelonLogger.Msg("Kicked " + sPlayerNameToKick + " (" + attackerPlayer.ToString() + ") for griefing (negative kills)");
                        HelperMethods.ReplyToCommand_Player(attackerPlayer, "was kicked for griefing (negative kills)");
                        HelperMethods.KickPlayer(attackerPlayer);
                    }                        
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StrategyMode::OnUnitDestroyed");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Structure __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 == null || __2 == null)
                    {
                        return;
                    }

                    // Victim
                    Team victimTeam = __0.Team;
                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__2);

                    if (attackerBase == null || victimTeam == null)
                    {
                        return;
                    }

                    Team attackerTeam = attackerBase.Team;

                    // don't check unless it was a team kill
                    if (attackerTeam == null || (attackerTeam.Index != victimTeam.Index))
                    {
                        return;
                    }

                    NetworkComponent attackerNetComp = attackerBase.NetworkComponent;

                    // was teamkiller a playable character?
                    if (attackerNetComp == null)
                    {
                        return;
                    }

                    Player attackerPlayer = attackerNetComp.OwnerPlayer;
                    if (attackerPlayer == null)
                    {
                        return;
                    }

                    string structName = GetStructureDisplayName(__0.ToString());

                    Player serverPlayer = NetworkGameServer.GetServerPlayer();
                    MelonLogger.Msg(attackerPlayer.PlayerName + " team killed a structure " + structName);
                    NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(attackerPlayer) + attackerPlayer.PlayerName + HelperMethods.defaultColor + " killed a friendly structure (" + HelperMethods.GetTeamColor(attackerPlayer) + structName + HelperMethods.defaultColor + ")", false);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnStructureDestroyed");
                }
            }
        }

        private static string GetStructureDisplayName(string structureFullName)
        {
            if (structureFullName.Contains('_'))
            {
                return structureFullName.Split('_')[0];
            }
            else if (structureFullName.Contains('('))
            {
                return structureFullName.Split('(')[0];
            }
            
            return structureFullName;
        }
    }
}