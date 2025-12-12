/*
 Silica Anti-Grief Mod
 Copyright (C) 2023-2025 by databomb
 
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
using System.Runtime.CompilerServices;

[assembly: MelonInfo(typeof(AntiGrief), "Anti-Grief", "1.5.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
#if NET6_0
[assembly: MelonOptionalDependencies("Admin Mod", "QList")]
#else
[assembly: MelonOptionalDependencies("Admin Mod")]
#endif

namespace Si_AntiGrief
{
    public class AntiGrief : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _NegativeKillsThreshold = null!;
        static MelonPreferences_Entry<bool> _NegativeKills_Penalty_Ban = null!;
        static MelonPreferences_Entry<bool> _StructureAntiGrief_IgnoreNodes = null!;
        static MelonPreferences_Entry<bool> _BlockShrimpControllers = null!;
        static MelonPreferences_Entry<bool> _BlockCommanderRemovingLastSpawn = null!;
        static MelonPreferences_Entry<bool> _BlockCommanderRemovingLastResearch = null!;

        static uint[] playerTransferCount = new uint[] {};

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _NegativeKillsThreshold ??= _modCategory.CreateEntry<int>("Grief_NegativeKills_Threshold", -9);
            _NegativeKills_Penalty_Ban ??= _modCategory.CreateEntry<bool>("Grief_NegativeKills_Penalty_Ban", true);
            _StructureAntiGrief_IgnoreNodes ??= _modCategory.CreateEntry<bool>("Grief_IgnoreFriendlyNodesDestroyed", true);
            _BlockShrimpControllers ??= _modCategory.CreateEntry<bool>("Grief_BlockShrimpTakeOver", false);
            _BlockCommanderRemovingLastSpawn ??= _modCategory.CreateEntry<bool>("Grief_BlockRemoveLastSpawn", true);
            _BlockCommanderRemovingLastResearch ??= _modCategory.CreateEntry<bool>("Grief_BlockRemoveLastResearch", true);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            InitializeTransferCountArray();
        }

        public void InitializeTransferCountArray()
        {
            int maxValue = NetworkGameServer.GetPlayersMax() + 1;
            playerTransferCount = new uint[maxValue];
            MelonLogger.Msg("Setting Anti-Grief transfer count array using max array index of: " + maxValue.ToString());
            Array.Fill(playerTransferCount, 0u);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public override void OnLateInitializeMelon()
        {
            //subscribing to the events
            Event_Roles.OnRoleChanged += OnRoleChanged;
            Event_Units.OnRequestEnterUnit += OnRequestEnterUnit;
            Event_Structures.OnRequestDestroyStructure += OnRequestDestroyStructure_GriefCheck;

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (QListLoaded)
            {
                QListRegistration();
            }
#endif
        }

        #if NET6_0
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void QListRegistration()
        {
            QList.Options.RegisterMod(this);

            QList.OptionTypes.IntOption negativeThreshold = new(_NegativeKillsThreshold, true, _NegativeKillsThreshold.Value, -3000, -100, 25);
            QList.OptionTypes.BoolOption banGriefers = new(_NegativeKills_Penalty_Ban, _NegativeKills_Penalty_Ban.Value);

            QList.Options.AddOption(negativeThreshold);
            QList.Options.AddOption(banGriefers);
        }
        #endif

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

                    // don't check unless it was a team kill
                    Team attackerTeam = attackerBase.Team;
                    if (attackerTeam == null || (attackerTeam.Index != victimTeam.Index))
                    {
                        return;
                    }

                    NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                    if (attackerNetComp == null)
                    {
                        return;
                    }

                    // was teamkiller a playable character?
                    Player attackerPlayer = attackerNetComp.OwnerPlayer;
                    if (attackerPlayer == null)
                    {
                        return;
                    }

                    // structure processing
                    if (__0.Owner is Structure || __0.Owner is ConstructionSite)
                    {
                        string structureName = GetDisplayName(__0);

                        // should we ignore the message for this particular type of structure?
                        if (!DisplayTeamKillForStructure(structureName))
                        {
                            return;
                        }

                        MelonLogger.Msg(attackerPlayer.PlayerName + " team killed a structure " + structureName);
                        HelperMethods.ReplyToCommand_Player(attackerPlayer, "killed a friendly " + (__0.OwnerConstructionSite == null ? "structure" : "construction site") + " (" + HelperMethods.GetTeamColor(attackerPlayer) + structureName + "</color>)");
                    }
                    // unit processing
                    else if (__0.Owner is Unit)
                    {
                        // don't need to worry about fall damage or other self-inflicted damage
                        Player victimPlayer = __0.OwnerUnit.ControlledBy;
                        if (victimPlayer == attackerPlayer)
                        {
                            return;
                        }

                        MelonLogger.Msg(attackerPlayer.PlayerName + " destroyed a friendly unit with kill score of " + attackerPlayer.Kills);

                        // check if another player was the victim
                        if (victimPlayer != null)
                        {
                            MelonLogger.Msg(attackerPlayer.PlayerName + " team killed " + victimPlayer.PlayerName);
                            HelperMethods.ReplyToCommand_Player(attackerPlayer, "team killed " + HelperMethods.GetTeamColor(victimPlayer) + victimPlayer.PlayerName + "</color>");
                        }
                    }

                    EvaluatePunishment(attackerPlayer);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Player::OnTargetDestroyed");
                }
            }
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.SpawnUnitForPlayer), new Type[] { typeof(Player), typeof(GameObject), typeof(Vector3), typeof(Quaternion) })]
        private static class AntiGrief_Patch_GameMode_SpawnUnitForPlayer
        {
            public static void Postfix(GameMode __instance, Unit __result, Player __0, UnityEngine.GameObject __1, UnityEngine.Vector3 __2, UnityEngine.Quaternion __3)
            {
                try
                {
                    if (__0 == null)
                    {
                        return;
                    }

                    // GetIndex() will return -1 if this is not a valid player on the list
                    int playerIndex = __0.GetIndex();
                    if (!IsPlayerIndexValid(playerIndex))
                    {
                        MelonLogger.Error("Player Index found outside of bounds (SpawnUnitForPlayer): " + playerIndex);
                        return;
                    }

                    playerTransferCount[playerIndex] = 0;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::SpawnUnitForPlayer");
                }
            }
        }

        public void OnRequestDestroyStructure_GriefCheck(object? sender, OnRequestDestroyStructureArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                Structure structure = args.Structure;
                Team team = args.Team;
                
                if (structure == null || team == null)
                {
                    return;
                }

                MelonLogger.Msg("OnRequestDestroyStructure StructureType: " + structure.ObjectInfo.StructureType + " SelectionType: " + structure.ObjectInfo.StructureSelectionType);

                // did the commander try and sell a barracks/lesser spawn or research facility?
                if ((_BlockCommanderRemovingLastSpawn.Value && structure.ObjectInfo.StructureSelectionType == StructureSelectionType.Units1) || 
                    (_BlockCommanderRemovingLastResearch.Value && structure.ObjectInfo.StructureType == StructureType.Research))
                {
                    int remainingStructures = team.GetStructureCount(structure.ObjectInfo);
                    MelonLogger.Msg("OnRequestDestroyStructure Remaining Structures: " + remainingStructures);

                    // prevent getting rid of the last structure
                    if (remainingStructures <= 1)
                    {
                        MelonLogger.Msg(team.TeamShortName + "'s commander tried to sell the last structure: ", structure.ObjectInfo.DisplayName);

                        // find if team has commander
                        Player? commander = null;
                        if (GameMode.CurrentGameMode is GameModeExt gameModeExt)
                        {
                            commander = gameModeExt.GetCommanderForTeam(team);
                        }

                        if (commander != null)
                        {
                            HelperMethods.ReplyToCommand_Player(commander, "tried to destroy the last ", structure.ObjectInfo.DisplayName);
                        }

                        args.Block = true;
                    }

                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestDestroyStructure_GriefCheck");
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

                Player player = args.Player;
                Unit unit = args.Unit;

                if (player == null || player.Team == null || unit == null)
                {
                    return;
                }

                // if the player isn't on the alien team, we can skip these checks
                if (player.Team.Index != (int)SiConstants.ETeam.Alien)
                {
                    return;
                }

                if (_BlockShrimpControllers.Value)
                {
                    // is it a shrimp?
                    if (unit.IsResourceHolder)
                    {
                        MelonLogger.Msg("Found " + player.PlayerName + " trying to use an Alien shrimp.");
                        HelperMethods.SendChatMessageToPlayer(player, HelperMethods.chatPrefix, " use of shrimp is not permitted on this server.");
                        args.Block = true;
                        return;
                    }
                }

                // check if the unit the player is in right now needs to get deleted
                if (ShouldDeletePriorUnit(player))
                {
                    MelonLogger.Msg("Player " + player.PlayerName + " found switching from spawn unit. Taking action to prevent inf creatures.");
                    DeletePriorUnit(player, player.ControlledUnit);
                }

                // GetIndex() will return -1 if this is not a valid player on the list
                int playerIndex = player.GetIndex();
                if (!IsPlayerIndexValid(playerIndex))
                {
                    MelonLogger.Error("Player Index found outside of bounds (OnRequestEnterUnit): " + playerIndex);
                    return;
                }

                playerTransferCount[playerIndex]++;
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestEnterUnit");
            }
        }

        public static void DeletePriorUnit(Player player, Unit unit)
        {
            for (int i = NetworkComponent.NetworkComponents.Count - 1; i >= 0; i--)
            {
                NetworkComponent networkComponent = NetworkComponent.NetworkComponents[i];
                if (networkComponent != null && networkComponent.OwnerPlayer == player && networkComponent.IsValid && networkComponent == unit.NetworkComponent)
                {
                    MelonLogger.Msg("Removing old unit (" + unit.name + ") from " + player.PlayerName + " before allowing transfer.");
                    UnityEngine.Object.Destroy(networkComponent.gameObject);
                }
            }
        }

        public static bool ShouldDeletePriorUnit(Player player)
        {
            Unit? currentUnit = player.ControlledUnit;
            Team? playerTeam = player.Team;
            if (currentUnit == null || playerTeam == null)
            {
                return false;
            }

            // we don't want the user to be able to delete an endless supply of starting units
            if (GameMode.CurrentGameMode.GetCanDeletePlayerControlledUnit(player, currentUnit))
            {
                // GetIndex() will return -1 if this is not a valid player on the list
                int playerIndex = player.GetIndex();
                if (!IsPlayerIndexValid(playerIndex))
                {
                    MelonLogger.Error("Player Index found outside of bounds (ShouldDeletePriorUnit): " + playerIndex);
                    return false;
                }

                if (playerTransferCount[playerIndex] >= 1)
                {
                    return false;
                }

                return true;
            }

            return false;
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
                if (args.Role == GameModeExt.ETeamRole.UNIT)
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

        public static void EvaluatePunishment(Player player)
        {
            short currentKillScore = player.Kills;
            if (currentKillScore >= _NegativeKillsThreshold.Value)
            {
                return;
            }

            String sPlayerNameToKick = player.PlayerName;

            if (_NegativeKills_Penalty_Ban.Value)
            {
                MelonLogger.Msg("Banned " + sPlayerNameToKick + " (" + player.ToString() + ") for griefing (negative kills)");
                HelperMethods.ReplyToCommand_Player(player, "was banned for griefing (negative kills)");
                // this uses the default in-game kick button response, which at least imposes a temp-ban for the life of the server (if not more)
                NetworkGameServer.KickPlayer(player);
            }
            else
            {
                MelonLogger.Msg("Kicked " + sPlayerNameToKick + " (" + player.ToString() + ") for griefing (negative kills)");
                HelperMethods.ReplyToCommand_Player(player, "was kicked for griefing (negative kills)");
                HelperMethods.KickPlayer(player);
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

        private static bool IsPlayerIndexValid(int playerIndex)
        {
            if (playerIndex == -1)
            {
                return false;
            }

            return true;
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
            return target.ObjectInfo.DisplayName.Replace(" ", "").Replace("-", "");
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