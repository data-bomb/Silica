/*
Silica Admin Mod
Copyright (C) 2024-2025 by databomb

* Description *
Provides basic admin mod system to allow additional admins beyond
the host.

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

using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilicaAdminMod
{
    public static class ChatVotes
    {
        public static bool voteInProgress = false;

        public static ChatVoteResults currentVoteResults = null!;

        public delegate void VoteHandler(ChatVoteResults results);

        static HelperMethods.CommandCallback voteChatCallback = Command_VoteChat;

        static List<Player> voters = null!;

        public static void Command_VoteChat(Player? callerPlayer, String args)
        {
            if (currentVoteResults == null || voters == null)
            {
                MelonLogger.Warning("Current vote results unavailable.");
                return;
            }

            if (callerPlayer == null)
            {
                MelonLogger.Warning("Console not supported.");
                return;
            }

            if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
            {
                MelonLogger.Msg("Reached vote callback for: " + args);
            }

            foreach (OptionVoteResult currentVoteResult in currentVoteResults.DetailedResults)
            {
                if (currentVoteResult.Command == args)
                {
                    if (voters.Contains(callerPlayer))
                    {
                        HelperMethods.SendChatMessageToPlayer(callerPlayer, "Vote already cast.");
                        return;
                    }

                    currentVoteResult.Votes++;
                    voters.Add(callerPlayer);
                    
                    HelperMethods.SendChatMessageToPlayer(callerPlayer, "Vote cast for " + args);

                    break;
                }
            }
        }

        public static bool IsVoteInProgress()
        {
            return voteInProgress;
        }

        public static void HoldVote(ChatVoteBallot ballot)
        {
            if (IsVoteInProgress())
            {
                throw new InvalidOperationException("Cannot start a new vote when one is in progress.");
            }

            // display question to players
            HelperMethods.ReplyToCommand(ballot.Question);
            voters = new List<Player>();

            // create the results we'll fill in as we go
            currentVoteResults = new ChatVoteResults
            {
                DetailedResults = new OptionVoteResult[ballot.Options.Length],
                VoteHandler = ballot.VoteHandler
            };

            int index = 0;
            // register the commands we need to but just while the vote is active
            foreach (OptionPair optionPair in ballot.Options)
            {
                OptionVoteResult currentResult = new OptionVoteResult
                {
                    Command = optionPair.Command,
                    Votes = 0
                };

                currentVoteResults.DetailedResults[index] = currentResult;
                index++;

                // listen to the option commands
                PlayerMethods.RegisterPlayerPhrase(optionPair.Command, voteChatCallback, true);

                // TODO: consider sending these at a slower pace than all at once
                HelperMethods.ReplyToCommand(optionPair.Command, " -- ", optionPair.Description);
            }

            StartVoteDurationTimer();
        }
        
        private static void StartVoteDurationTimer()
        {
            if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
            {
                MelonLogger.Msg("Starting vote timer with duration " + SiAdminMod.Pre_Admin_VoteDuration.Value.ToString());
            }
            voteInProgress = true;
            HelperMethods.StartTimer(ref SiAdminMod.Timer_TallyVote);
        }


    }

    public partial class SiAdminMod
    {
        public static float Timer_TallyVote = HelperMethods.Timer_Inactive;

        public override void OnUpdate()
        {
            try
            {
                // check if timer expired while the game is in-progress
                if (HelperMethods.IsTimerActive(Timer_TallyVote))
                {
                    Timer_TallyVote += Time.deltaTime;

                    if (Timer_TallyVote >= SiAdminMod.Pre_Admin_VoteDuration.Value)
                    {
                        Timer_TallyVote = HelperMethods.Timer_Inactive;

                        if (ChatVotes.currentVoteResults == null)
                        {
                            MelonLogger.Warning("Current vote results unavailable for timer expiration.");
                            ChatVotes.voteInProgress = false;
                            return;
                        }

                        OptionVoteResult winningResult = new OptionVoteResult
                        {
                            Votes = -1,
                            Command = "invalid"
                        };

                        foreach (OptionVoteResult currentVoteResult in ChatVotes.currentVoteResults.DetailedResults)
                        {
                            // determine winner
                            if (currentVoteResult.Votes > winningResult.Votes)
                            {
                                winningResult = currentVoteResult;
                            }

                            if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                            {
                                MelonLogger.Msg("Found command " + currentVoteResult.Command + " with votes " + currentVoteResult.Votes);
                            }

                            // unregister commands for current vote
                            PlayerMethods.UnregisterPlayerPhrase(currentVoteResult.Command);
                        }

                        // assign winner
                        ChatVotes.currentVoteResults.WinningCommand = winningResult.Command;

                        // call the vote handler
                        ChatVotes.voteInProgress = false;
                        ChatVotes.currentVoteResults.VoteHandler(ChatVotes.currentVoteResults);
                    }

                }
            }
            catch (Exception exception)
            {
                HelperMethods.PrintError(exception, "Failed in OnUpdate");
            }
        }
    }
}