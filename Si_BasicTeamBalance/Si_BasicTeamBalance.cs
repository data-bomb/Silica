/*
 Silica Basic Team Balance Mod
 Copyright (C) 2023-2024 by databomb
 
 * Description *
 For Silica servers, allows server operators to configure the exact
 amount of team imbalance that is allowed. Additionally, it allows 
 a prevention from switching teams early in the match (to mitigate 
 a common cheating technique to find an enemy's position)

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
using Il2CppSteamworks;
#else
using Steamworks;
#endif

using HarmonyLib;
using MelonLoader;
using Si_BasicTeamBalance;
using UnityEngine;
using System;
using SilicaAdminMod;
using System.Linq;

[assembly: MelonInfo(typeof(BasicTeamBalance), "Basic Team Balance", "1.3.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_BasicTeamBalance
{
    public class BasicTeamBalance : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<float> _TwoTeamBalanceDivisor = null!;
        static MelonPreferences_Entry<float> _TwoTeamBalanceAddend = null!;
        static MelonPreferences_Entry<float> _ThreeTeamBalanceDivisor = null!;
        static MelonPreferences_Entry<float> _ThreeTeamBalanceAddend = null!;
        static MelonPreferences_Entry<bool> _PreventEarlyTeamSwitches = null!;
        static MelonPreferences_Entry<int> _AllowTeamSwitchAfterTime = null!;

        static Player? LastPlayerChatMessage;
        static bool preventTeamSwitches;

        private static float Timer_AllowTeamSwitches = HelperMethods.Timer_Inactive;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _TwoTeamBalanceDivisor ??= _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Divisor", 8.0f);
            _TwoTeamBalanceAddend ??= _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Addend", 1.0f);
            _ThreeTeamBalanceDivisor ??= _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Divisor", 10.0f);
            _ThreeTeamBalanceAddend ??= _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Addend", 0.0f);
            _PreventEarlyTeamSwitches ??= _modCategory.CreateEntry<bool>("TeamBalance_Prevent_EarlySwitching", false);
            _AllowTeamSwitchAfterTime ??= _modCategory.CreateEntry<int>("TeamBalance_Prevent_EarlySwitching_For_Seconds", 75);

            preventTeamSwitches = false;
        }

        
        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback teamCallback = Command_Team;
            HelperMethods.RegisterAdminCommand("team", teamCallback, Power.Teams, "Moves target player to target team. Usage: !team [optional:<player>] <teamname>");

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.FloatOption twoTeamDivisor = new(_TwoTeamBalanceDivisor, false, _TwoTeamBalanceDivisor.Value, 0.1f, 1000.0f);
            QList.OptionTypes.FloatOption twoTeamAddend = new(_TwoTeamBalanceAddend, false, _TwoTeamBalanceAddend.Value, 0.0f, 1000.0f);
            QList.OptionTypes.FloatOption threeTeamDivisor = new(_ThreeTeamBalanceDivisor, false, _ThreeTeamBalanceDivisor.Value, 0.1f, 1000.0f);
            QList.OptionTypes.FloatOption threeTeamAddend = new(_ThreeTeamBalanceAddend, false, _ThreeTeamBalanceAddend.Value, 0.0f, 1000.0f);
            QList.OptionTypes.BoolOption preventEarlyTeamSwitches = new(_PreventEarlyTeamSwitches, _PreventEarlyTeamSwitches.Value);
            QList.OptionTypes.IntOption preventEarlyTeamSwitchDuration = new(_AllowTeamSwitchAfterTime, true, _AllowTeamSwitchAfterTime.Value, 0, 300, 15);

            QList.Options.AddOption(twoTeamDivisor);
            QList.Options.AddOption(twoTeamAddend);
            QList.Options.AddOption(threeTeamDivisor);
            QList.Options.AddOption(threeTeamAddend);
            QList.Options.AddOption(preventEarlyTeamSwitches);
            QList.Options.AddOption(preventEarlyTeamSwitchDuration);
            #endif
        }


        public static void SendClearRequest(ulong thisPlayerSteam64, int thisPlayerChannel)
        {
            // send RPC_ClearRequest (3)
            GameByteStreamWriter clearWriteInstance = GameMode.CurrentGameMode.CreateRPCPacket((byte)MP_Strategy.ERPCs.CLEAR_REQUEST);
            if (clearWriteInstance != null)
            {
                clearWriteInstance.WriteUInt64(thisPlayerSteam64);
                clearWriteInstance.WriteByte((byte)thisPlayerChannel);
                GameMode.CurrentGameMode.SendRPCPacket(clearWriteInstance);
            }
        }

        public static bool OneFactionEliminated()
        {
            int TeamsWithMajorStructures = 0;
            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                Team? thisTeam = Team.Teams[i];
                int thisTeamMajorStructures = thisTeam.NumMajorStructures;
                if (thisTeamMajorStructures > 0)
                {
                    TeamsWithMajorStructures++;
                }
            }

            if (TeamsWithMajorStructures < 3)
            {
                return true;
            }

            return false;
        }

        public static int GetNumberOfActiveTeams(MP_Strategy.ETeamsVersus versusMode)
        {
            int NumActiveTeams = 0;
            switch (versusMode)
            {
                case MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS:
                case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS:
                    {
                        NumActiveTeams = 2;
                        break;
                    }
                case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS:
                    {
                        // need to determine if one faction has been eliminated
                        if (OneFactionEliminated())
                        {
                            NumActiveTeams = 2;
                        }
                        else
                        {
                            NumActiveTeams = 3;
                        }    
                        break;
                    }
            }

            return NumActiveTeams;
        }

        public static Team? FindLowestPopulationTeam(MP_Strategy.ETeamsVersus versusMode)
        {
            int LowestTeamNumPlayers = NetworkGameServer.GetPlayersMax() + 1;
            Team? LowestPopTeam = null;

            for (int i = 0; i < SiConstants.MaxPlayableTeams; i++)
            {
                Team? thisTeam = Team.Teams[i];
                if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS && i == (int)SiConstants.ETeam.Alien)
                {
                    continue;
                }
                else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS && i == (int)SiConstants.ETeam.Centauri)
                {
                    continue;
                }
                // has the team been eliminated?
                else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && !thisTeam.GetHasAnyCritical())
                {
                    continue;
                }

                int thisTeamNumPlayers = thisTeam.GetNumPlayers();
                if (thisTeamNumPlayers < LowestTeamNumPlayers)
                {
                    LowestTeamNumPlayers = thisTeamNumPlayers;
                    LowestPopTeam = thisTeam;
                }
            }

            return LowestPopTeam;
        }

        // Team Index 0 - Alien
        // Team Index 1 - Human (Centauri)
        // Team Index 2 - Human (Sol)
        public static bool JoinCausesImbalance(Team? TargetTeam)
        {
            if (TargetTeam == null)
            {
                return false;
            }

            MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();
            MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

            Team? LowestPopTeam = null;
            int NumActiveTeams = GetNumberOfActiveTeams(versusMode);
            LowestPopTeam = FindLowestPopulationTeam(versusMode);

            // are we already trying to join the team with lowest pop or did we have an error?
            if (LowestPopTeam == null || LowestPopTeam == TargetTeam)
            {
                return false;
            }

            // what's the player count difference?
            int TargetTeamPop = TargetTeam.GetNumPlayers();
            int PlayerDifference = TargetTeamPop - LowestPopTeam.GetNumPlayers();
            // as a positive number only
            if (PlayerDifference < 0)
            {
                PlayerDifference = -PlayerDifference;
            }

            // determine maximum allowed difference
            int TotalNumPlayers = Player.Players.Count;
            int MaxDifferenceAllowed;
            if (NumActiveTeams == 2)
            {
                MaxDifferenceAllowed = (int)Math.Ceiling((TotalNumPlayers / _TwoTeamBalanceDivisor.Value) + _TwoTeamBalanceAddend.Value);
            }
            // more strict enforcement for Humans vs Humans vs Aliens
            else
            {
                MaxDifferenceAllowed = (int)Math.Ceiling((TotalNumPlayers / _ThreeTeamBalanceDivisor.Value) + _ThreeTeamBalanceAddend.Value);
            }


            if (PlayerDifference > MaxDifferenceAllowed)
            {
                return true;
            }

            return false;
        }

        public static void Command_Team(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 2)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            string teamTarget = args.Split(' ')[(argumentCount == 1 ? 1 : 2)];
            Team team = Team.GetTeamByName(teamTarget, false);
            if (team == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous or invalid team name");
                return;
            }

            Player? player = null;
            if (argumentCount > 1)
            {
                string playerTarget = args.Split(' ')[1];
                player = HelperMethods.FindTargetPlayer(playerTarget);
            }
            else
            {
                player = callerPlayer;
            }

            if (player == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(player))
            {
                HelperMethods.ReplyToCommand_Player(player, "is immune due to level");
                return;
            }

            // check if they're already on the target team
            if (player.Team == team)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Player already on target team");
                return;
            }

            // swap and notify players
            SwapTeam(player, team);
            HelperMethods.AlertAdminAction(callerPlayer, "swapped " + player.PlayerName + " to " + HelperMethods.GetTeamColor(player) + team.TeamShortName + "</color>");
        }

        public static void SwapTeam(Player player, Team team)
        {
            MelonLogger.Msg("Swapping player (" + player.PlayerName + ") to team: " + team.TeamShortName);

            player.Team = team;
            NetworkLayer.SendPlayerSelectTeam(player, team);
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.ProcessNetRPC))]
        private static class ApplyPatch_MPStrategy_JoinTeam
        {
            public static bool Prefix(MP_Strategy __instance, GameByteStreamReader __0, byte __1)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only look at RPC_RequestJoinTeam bytes
                    if (__1 != (byte)MP_Strategy.ERPCs.REQUEST_JOIN_TEAM)
                    {
                        return true;
                    }

                    // if the game is over then don't run any balance checks
                    if (GameMode.CurrentGameMode.GameOver)
                    {
                        return true;
                    }

                    // after this point we will modify the read pointers so we have to return false
                    ulong PlayerSteam64 = __0.ReadUInt64();
                    CSteamID PlayerCSteamID = new CSteamID(PlayerSteam64);
                    PlayerCSteamID.m_SteamID = PlayerSteam64;
                    int PlayerChannel = __0.ReadByte();
                    Player JoiningPlayer = Player.FindPlayer(PlayerCSteamID, PlayerChannel);
                    Team? TargetTeam = __0.ReadTeam();

                    if (JoiningPlayer == null)
                    {
                        return false;
                    }

                    Team mTeam = JoiningPlayer.Team;

                    // requests to rejoin the same team
                    if (UnityEngine.Object.Equals(mTeam, TargetTeam))
                    {
                        SendClearRequest(PlayerSteam64, PlayerChannel);
                        return false;
                    }

                    // these would normally get processed at this point but check if early team switching is being stopped and the player already has a team
                    // if re-joining the current team would be considered as imbalanced, then override the prevention - player is trying to help
                    if (_PreventEarlyTeamSwitches.Value && GameMode.CurrentGameMode.GameOngoing && preventTeamSwitches && mTeam != null && !JoinCausesImbalance(mTeam))
                    {
                        // avoid chat spam
                        if (LastPlayerChatMessage != JoiningPlayer)
                        {
                            HelperMethods.ReplyToCommand_Player(JoiningPlayer, "'s switch was denied due to early game team lock");
                            LastPlayerChatMessage = JoiningPlayer;
                        }

                        MelonLogger.Msg(JoiningPlayer.PlayerName + "'s team switch was denied due to early game team lock");

                        SendClearRequest(PlayerSteam64, PlayerChannel);
                        return false;
                    }

                    MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();

                    // if there is some kind of game bug and the player is on an invalid team then let the change occur
                    if (JoiningPlayer.Team != null && strategyInstance.GetStrategyTeamSetup(JoiningPlayer.Team) == null)
                    {
                        MelonLogger.Warning("Found player on invalid team. Allowing role change.");
                        JoiningPlayer.Team = TargetTeam;
                        NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, TargetTeam);
                        return false;
                    }

                    // the team change should be permitted as it doesn't impact balance
                    if (!JoinCausesImbalance(TargetTeam))
                    {
                        JoiningPlayer.Team = TargetTeam;
                        NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, TargetTeam);
                        return false;
                    }

                    // if the player hasn't joined a team yet, force them to the team that needs it the most
                    if (JoiningPlayer.Team == null)
                    {
                        
                        MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;
                        Team? ForcedTeam = FindLowestPopulationTeam(versusMode);
                        if (ForcedTeam != null)
                        {
                            // avoid chat spam
                            if (LastPlayerChatMessage != JoiningPlayer)
                            {
                                HelperMethods.ReplyToCommand_Player(JoiningPlayer, " was forced to " + HelperMethods.GetTeamColor(ForcedTeam) + ForcedTeam.TeamShortName + "</color> to fix imbalance");
                                LastPlayerChatMessage = JoiningPlayer;
                            }

                            MelonLogger.Msg(JoiningPlayer.PlayerName + " was forced to " + ForcedTeam.TeamShortName + " to fix imbalance");

                            JoiningPlayer.Team = ForcedTeam;
                            NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, ForcedTeam);

                            return false;
                        }

                        MelonLogger.Warning("Error in FindLowestPopulationTeam(). Could not find a valid team.");
                        return false;
                    }
                        
                    // the player has already joined a team but the change would cause an imbalance
                                        
                    // avoid chat spam
                    if (LastPlayerChatMessage != JoiningPlayer)
                    {
                        HelperMethods.ReplyToCommand_Player(JoiningPlayer, "'s switch was denied due to imbalance");
                        LastPlayerChatMessage = JoiningPlayer;
                    }

                    MelonLogger.Msg(JoiningPlayer.PlayerName + "'s team switch was denied due to team imbalance");

                    SendClearRequest(PlayerSteam64, PlayerChannel);
                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::ProcessNetRPC");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0)
            {
                try
                {
                    if (_PreventEarlyTeamSwitches.Value)
                    {
                        preventTeamSwitches = true;

                        HelperMethods.StartTimer(ref Timer_AllowTeamSwitches);
                        MelonLogger.Msg("Early game team lock set for " + _AllowTeamSwitchAfterTime.Value.ToString() + " seconds.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.Update))]
        #else
        [HarmonyPatch(typeof(MusicJukeboxHandler), "Update")]
        #endif
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(MusicJukeboxHandler __instance)
            {
                try
                {
                    if (HelperMethods.IsTimerActive(Timer_AllowTeamSwitches))
                    {
                        Timer_AllowTeamSwitches += Time.deltaTime;

                        if (Timer_AllowTeamSwitches >= _AllowTeamSwitchAfterTime.Value)
                        {
                            Timer_AllowTeamSwitches = HelperMethods.Timer_Inactive;

                            preventTeamSwitches = false;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::Update");
                }
            }
        }

        // account for if the game ends before the timer expires
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                try
                {
                    if (!_PreventEarlyTeamSwitches.Value)
                    {
                        return;
                    }

                    if (HelperMethods.IsTimerActive(Timer_AllowTeamSwitches))
                    {
                        Timer_AllowTeamSwitches = HelperMethods.Timer_Inactive;

                        MelonLogger.Msg("Game ended before early game team lock expired. Forcing timer to expire.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }
    }
}
