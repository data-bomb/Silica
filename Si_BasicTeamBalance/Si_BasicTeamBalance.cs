/*
 Silica Auto Teams Select Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, only permit players to cause a moderate
 team imbalance when attempting to join other teams.

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
using Il2CppSteamworks;
using MelonLoader;
using Si_BasicTeamBalance;
using System.Linq.Expressions;
using UnityEngine;

[assembly: MelonInfo(typeof(BasicTeamBalance), "[Si] Basic Team Balance", "0.9.1", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_BasicTeamBalance
{
    public class BasicTeamBalance : MelonMod
    {
        public static void PrintError(Exception exception, string message=null)
        {
            if (message != null)
            {
                MelonLogger.LogError(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.LogError(error);
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.ProcessNetRPC))]
        private static class ApplyPatchJoinTeamPacket
        {
            public static bool Prefix(Il2Cpp.MP_Strategy __instance, Il2Cpp.GameByteStreamReader __0, byte __1)
            {
                if (__0 != null)
                {
                    // check for RPC_RequestJoinTeam byte
                    if (__1 == 1)
                    {
                        try
                        {
                            MelonLogger.Msg("RPC_RequestJoinTeam received");

                            ulong PlayerSteam64 = __0.ReadUInt64();
                            int PlayerChannel = __0.ReadByte();
                            Il2CppSteamworks.CSteamID PlayerCSteamID = new CSteamID(PlayerSteam64);
                            PlayerCSteamID.m_SteamID = PlayerSteam64;
                            Il2Cpp.Player JoiningPlayer = Il2Cpp.Player.FindPlayer(PlayerCSteamID, PlayerChannel);
                            Il2Cpp.Team TargetTeam = __0.ReadTeam();
                            if (JoiningPlayer != null && TargetTeam != null)
                            {
                                // get team counts
                                int iTargetTeamCount = TargetTeam.GetNumPlayers();
                                int iTargetTeamIndex = TargetTeam.Index;
                                MelonLogger.Msg(TargetTeam.TeamName + " (target) has playercount: " + iTargetTeamCount.ToString());

                                Il2Cpp.Team CurrentTeam = JoiningPlayer.m_Team;
                                int iCurrentTeamCount = 0;
                                if (CurrentTeam != null)
                                {
                                    iCurrentTeamCount = JoiningPlayer.m_Team.GetNumPlayers();
                                    MelonLogger.Msg(JoiningPlayer.m_Team.TeamName + " (current) has playercount: " + iCurrentTeamCount.ToString());
                                }
                                else
                                {
                                    // grab opposing team player count
                                    // Team Index 0 - Alien
                                    // Team Index 1 - Human (Centauri)
                                    // Team Index 2 - Human (Sol)
                                    int iOtherTeamIndex = (iTargetTeamIndex == 0) ? 2 : 0;

                                    // TODO: Account for HvHvA mode
                                    /* int iOtherTeamCount = 0;
                                    for (int i = 0; i < Il2Cpp.Team.Teams.Count; i++)
                                    {
                                        if (i == iTargetTeamIndex)
                                        {
                                            continue;
                                        }

                                        iOtherTeamCount = Il2Cpp.Team.Teams[i].GetNumPlayers();
                                    }
                                    */

                                    iCurrentTeamCount = Il2Cpp.Team.Teams[iOtherTeamIndex].GetNumPlayers();
                                    MelonLogger.Msg(JoiningPlayer.PlayerName + " joined from null team so used team index " + iOtherTeamIndex.ToString() + " and found playercount: " + iCurrentTeamCount.ToString());
                                }

                                // would this cause an imbalance? or are they already on the team?
                                // TODO: make the amount of imbalance a MelonLoader configuration option
                                if ((iTargetTeamCount - iCurrentTeamCount > 2) || (CurrentTeam == TargetTeam))
                                {
                                    MelonLogger.Msg("Sending Clear Request");
                                    // send RPC_ClearRequest
                                    Il2Cpp.GameByteStreamWriter clearWriteInstance = Il2Cpp.GameMode.CurrentGameMode.CreateRPCPacket(3);
                                    clearWriteInstance.WriteUInt64(PlayerSteam64);
                                    clearWriteInstance.WriteByte((byte)JoiningPlayer.PlayerChannel);
                                    Il2Cpp.GameMode.CurrentGameMode.SendRPCPacket(clearWriteInstance);
                                    return false;
                                }

                                MelonLogger.Msg("Allow the join...");
                                JoiningPlayer.Team = TargetTeam;
                                Il2Cpp.NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, TargetTeam);
                            }

                            return false;
                        }
                        catch (Exception error)
                        {
                            PrintError(error, "Failed to run ProcessNetRPC");
                        }
                    }
                }

                return true;
            }
        }
    }
}