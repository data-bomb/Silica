/*
 Silica Anti-Grief Mod
 Copyright (C) 2023-2024 by databomb
 
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
using UnityEngine;

[assembly: MelonInfo(typeof(AntiGrief), "Anti-Grief", "1.3.4", "databomb", "https://github.com/data-bomb/Silica")]
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
        static MelonPreferences_Entry<bool> _BlockShrimpControllers = null!;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _NegativeKillsThreshold ??= _modCategory.CreateEntry<int>("Grief_NegativeKills_Threshold", -9);
            _NegativeKills_Penalty_Ban ??= _modCategory.CreateEntry<bool>("Grief_NegativeKills_Penalty_Ban", true);
            _StructureAntiGrief_IgnoreNodes ??= _modCategory.CreateEntry<bool>("Grief_IgnoreFriendlyNodesDestroyed", true);
            _BlockShrimpControllers ??= _modCategory.CreateEntry<bool>("Grief_BlockShrimpTakeOver", false);
        }

        public override void OnLateInitializeMelon()
        {
            //subscribing to the events
            Event_Roles.OnRoleChanged += OnRoleChanged;
            Event_Units.OnRequestEnterUnit += OnRequestEnterUnit;

            #if NET6_0
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
            #endif
        }


        [HarmonyPatch(typeof(StrategyMode), nameof(StrategyMode.OnUnitDestroyed))]
        private static class ApplyPatch_StrategyMode_OnUnitDestroyed
        {
            public static void Postfix(StrategyMode __instance, Unit __0, UnityEngine.GameObject __1)
            {
                try
                {
                    if (__0 == null || __1 == null || _NegativeKillsThreshold == null || _NegativeKills_Penalty_Ban == null)
                    {
                        return;
                    }

                    // Victim
                    Team victimTeam = __0.Team;
                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);

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
                        HelperMethods.ReplyToCommand_Player(attackerPlayer, "team killed " + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName + "</color>");
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

        [HarmonyPatch(typeof(Player), nameof(Player.OnTargetDestroyed))]
        private static class ApplyPatch_OnTargetDestroyed
        {
            public static void Postfix(Target __0, GameObject __1)
            {
                try
                {
                    if (__0 == null || __1 == null)
                    {
                        return;
                    }

                    // Victim
                    Team victimTeam = __0.Team;
                    // Attacker
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__1);

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

                    string structureName = GetDisplayName(__0);

                    // should we ignore the message for this particular type of structure?
                    if (!DisplayTeamKillForStructure(structureName))
                    {
                        return;
                    }

                    MelonLogger.Msg(attackerPlayer.PlayerName + " team killed a structure " + structureName);
                    HelperMethods.ReplyToCommand_Player(attackerPlayer, "killed a friendly " + (__0.OwnerConstructionSite == null ? "structure" : "construction site") + " (" + HelperMethods.GetTeamColor(attackerPlayer) + structureName + "</color>)");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Player::OnTargetDestroyed");
                }
            }
        }

        public void OnRequestEnterUnit(object? sender, OnRequestEnterUnitArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                if (!_BlockShrimpControllers.Value)
                {
                    return;
                }

                Player player = args.Player;
                Unit unit = args.Unit;

                if (player == null || player.Team == null || unit == null)
                {
                    return;
                }

                // if the player isn't on the alien team, we can skip this check
                if (player.Team.Index != (int)SiConstants.ETeam.Alien)
                {
                    return;
                }

                // is it a shrimp?
                if (unit.IsResourceHolder)
                {
                    MelonLogger.Msg("Found " + player.PlayerName + " trying to use an Alien shrimp.");
                    HelperMethods.SendChatMessageToPlayer(player, HelperMethods.chatPrefix, " use of shrimp is not permitted on this server.");
                    args.Block = true;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestEnterUnit");
            }
        }

        // hook DestroyAllUnitsForPlayer pre and override game code to prevent aliens from dying
        public void OnRoleChanged(object? sender, OnRoleChangedArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                Player player = args.Player;

                if (player == null || player.Team == null)
                {
                    return;
                }

                // if the player isn't on the alien team, we can skip this check
                if (player.Team.Index != (int)SiConstants.ETeam.Alien)
                {
                    return;
                }

                // if a player switches to non-infantry it will despawn their alien unit
                if (args.Role == GameModeExt.ETeamRole.INFANTRY)
                {
                    return;
                }

                Unit unit = player.ControlledUnit;
                if (unit == null)
                {
                    return;
                }

                if (unit.DamageManager.IsDestroyed)
                {
                    return;
                }

                // if this is a low tier unit then we don't care if it disappears
                if (!IsHighValueAlienUnit(unit))
                {
                    return;
                }

                string unitName = GetDisplayName(unit.name);

                MelonLogger.Msg(player.PlayerName + " tried to despawn a non-trivial Alien unit (" + unitName + ")");
                HelperMethods.ReplyToCommand_Player(player, "tried to despawn a unit (" + HelperMethods.GetTeamColor(player) + unitName + "</color>)");

                // work-around to spawn a temp replacement before all the player's units are taken by DestroyAllUnitsForPlayer
                GameMode.CurrentGameMode.SpawnUnitForPlayer(player, player.Team);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRoleChanged");
            }
        }

        public static bool IsHighValueAlienUnit(Unit unit)
        {
            BaseGameObject? unitBase = GameFuncs.GetBaseGameObject(unit.gameObject);
            if (unitBase == null)
            {
                return false;
            }

            ConstructionData? unitConstructionData = unitBase.ConstructionData;
            if (unitConstructionData == null)
            {
                return false;
            }

            if (unitConstructionData.ResourceCost >= 500)
            {
                return true;
            }

            return false;
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

        private static string GetDisplayName(Target target)
        {
            if (target.ToString().Contains('_'))
            {
                // is this a construction site or not?
                if (target.OwnerConstructionSite == null)
                {
                    return target.ToString().Split('_')[0];
                }
                else
                {
                    return target.ToString().Split('_')[1];
                }
            }
            else if (target.ToString().Contains('('))
            {
                return target.ToString().Split('(')[0];
            }

            return target.ToString();
        }

        private static string GetDisplayName(string fullName)
        {
            if (fullName.Contains('_'))
            {
                return fullName.Split('_')[0];
            }
            else if (fullName.Contains('('))
            {
                return fullName.Split('(')[0];
            }
            
            return fullName;
        }
    }
}