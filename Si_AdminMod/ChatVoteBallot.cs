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

using System;

namespace SilicaAdminMod
{
    public class ChatVoteBallot
    {
        private String _question = null!;

        public String Question
        {
            get => _question;
            set => _question = value ?? throw new ArgumentNullException("Question is required.");
        }

        private ChatVotes.VoteHandler _voteHandler = null!;

        public ChatVotes.VoteHandler VoteHandler
        {
            get => _voteHandler;
            set => _voteHandler = value ?? throw new ArgumentNullException("Vote handler is required.");
        }

        private OptionPair[] _options = null!;

        public OptionPair[] Options
        {
            get => _options;
            set => _options = value ?? throw new ArgumentNullException("Options are required.");
        }
    }

    public class OptionPair
    {
        private String _command = null!;

        public String Command
        {
            get => _command;
            set => _command = value ?? throw new ArgumentNullException("Command is required.");
        }

        private String _description = null!;

        public String Description
        {
            get => _description;
            set => _description = value ?? throw new ArgumentNullException("Description is required.");
        }
    }
}