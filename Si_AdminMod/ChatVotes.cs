/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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

using MelonLoader;
using System;
using System.Linq;
using System.Timers;

namespace SilicaAdminMod
{
    public static class ChatVotes
    {
        private static bool voteInProgress = false;

        private static ChatVoteResults currentVoteResults = null!;

        public delegate void VoteHandler(ChatVoteResults results);

        private static Timer? Timer_VoteDuration;

        static HelperMethods.CommandCallback voteChatCallback = Command_VoteChat;

        public static void Command_VoteChat(Player callerPlayer, String args)
        {
            if (currentVoteResults == null)
            {
                MelonLogger.Warning("Current vote results unavailable.");
                return;
            }

            MelonLogger.Msg("Reached voteback callback for: " + args);

            foreach (OptionVoteResult currentVoteResult in currentVoteResults.DetailedResults)
            {
                if (currentVoteResult.Command == args)
                {
                    MelonLogger.Msg("Vote cast for ", args);
                    currentVoteResult.Votes++;
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

            // create the results we'll fill in as we go
            currentVoteResults = new ChatVoteResults();
            currentVoteResults.DetailedResults = new OptionVoteResult[ballot.Options.Length];
            currentVoteResults.VoteHandler = ballot.VoteHandler;

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

                PlayerMethods.RegisterPlayerPhrase(optionPair.Command, voteChatCallback, true);
            }

            // display message about vote to players

            


            StartVoteDurationTimer();
        }

        // listen to the option commands

        private static void StartVoteDurationTimer()
        {
            double interval = SiAdminMod.Pre_Admin_VoteDuration.Value * 1000.0f;
            Timer_VoteDuration = new Timer(interval);
            voteInProgress = true;
            Timer_VoteDuration.Elapsed += new ElapsedEventHandler(HandleVoteTimerExpired);
            Timer_VoteDuration.AutoReset = false;
            Timer_VoteDuration.Enabled = true;
        }

        // don't access anything from melonloader inside the timer callback
        private static void HandleVoteTimerExpired(object? source, ElapsedEventArgs e)
        {

            if (currentVoteResults == null)
            {
                //MelonLogger.Warning("Current vote results unavailable for timer expiration.");
                voteInProgress = false;
                return;
            }
            
            OptionVoteResult winningResult = new OptionVoteResult
            {
                Votes = -1
            };

            foreach (OptionVoteResult currentVoteResult in currentVoteResults.DetailedResults)
            {
                // determine winner
                if (currentVoteResult.Votes > winningResult.Votes)
                {
                    winningResult = currentVoteResult;
                }

                // unregister commands for current vote
                PlayerMethods.UnregisterPlayerPhrase(currentVoteResult.Command);
            }

            // call the vote handler
            currentVoteResults.VoteHandler(currentVoteResults);

            voteInProgress = false;
        }
    }
}