/*
 Silica Anti-Grief Mod
 Copyright (C) 2024 by databomb
 
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

#if NET6_0
using Il2Cpp;
#endif

using System;
using HarmonyLib;
using MelonLoader;
using Si_AntiGrief;
using SilicaAdminMod;
using System.Linq;

[assembly: MelonInfo(typeof(AntiGrief), "Anti-Grief", "1.1.6", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_AntiGrief
{
    public class AntiGrief : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _NegativeKillsThreshold = null!;
        static MelonPreferences_Entry<bool> _NegativeKills_Penalty_Ban = null!;
        static MelonPreferences_Entry<bool> _StructureAntiGrief_IgnoreNodes = null!;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _NegativeKillsThreshold ??= _modCategory.CreateEntry<int>("Grief_NegativeKills_Threshold", -125);
            _NegativeKills_Penalty_Ban ??= _modCategory.CreateEntry<bool>("Grief_NegativeKills_Penalty_Ban", true);
            _StructureAntiGrief_IgnoreNodes ??= _modCategory.CreateEntry<bool>("Grief_IgnoreFriendlyNodesDestroyed", true);
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

            QList.OptionTypes.IntOption negativeThreshold = new(_NegativeKillsThreshold, true, _NegativeKillsThreshold.Value, -3000, -100, 25);
            QList.OptionTypes.BoolOption banGriefers = new(_NegativeKills_Penalty_Ban, _NegativeKills_Penalty_Ban.Value);

            QList.Options.AddOption(negativeThreshold);
            QList.Options.AddOption(banGriefers);
        }
        #endif

        [HarmonyPatch(typeof(StrategyMode), nameof(StrategyMode.OnUnitDestroyed))]
        private static class ApplyPatch_StrategyMode_OnUnitDestroyed
        {
            public static void Postfix(StrategyMode __instance, Unit __0, EDamageType __1, UnityEngine.GameObject __2)
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

                    Player victimPlayer = __0.ControlledBy;
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
                    short currentKillScore = attackerPlayer.Kills;
                    MelonLogger.Msg(attackerPlayer.PlayerName + " destroyed a friendly unit with kill score of " + currentKillScore.ToString());

                    // check if another player was the victim
                    if (victimPlayer != null)
                    {
                        MelonLogger.Msg(attackerPlayer.PlayerName + " team killed " + victimPlayer.PlayerName);
                        HelperMethods.ReplyToCommand_Player(attackerPlayer, "team killed " + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName);
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

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatch_OnStructureDestroyed
        {
            public static void Postfix(MP_Strategy __instance, Structure __0, EDamageType __1, UnityEngine.GameObject __2)
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

                    string structureName = GetStructureDisplayName(__0.ToString());

                    // should we ignore the message for this particular type of structure?
                    if (!DisplayTeamKillForStructure(structureName))
                    {
                        return;
                    }

                    MelonLogger.Msg(attackerPlayer.PlayerName + " team killed a structure " + structureName);
                    HelperMethods.ReplyToCommand_Player(attackerPlayer, "killed a friendly structure (" + HelperMethods.GetTeamColor(attackerPlayer) + structureName + HelperMethods.defaultColor + ")");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::OnStructureDestroyed");
                }
            }
        }

        private static bool DisplayTeamKillForStructure(string structureName)
        {
            // has the server set it so Nodes should be ignored?
            if (_StructureAntiGrief_IgnoreNodes.Value)
            {
                if (structureName == "Node")
                {
                    return false;
                }
            }

            return true;
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