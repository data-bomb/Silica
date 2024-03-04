/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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
    public class PlayerCommand
    {
        private string _commandName = null!;

        public String CommandName
        {
            get => _commandName;
            set => _commandName = value ?? throw new ArgumentNullException("Command name is required.");
        }

        private HelperMethods.CommandCallback _commandCallback = null!;

        public HelperMethods.CommandCallback PlayerCommandCallback
        {
            get => _commandCallback;
            set => _commandCallback = value ?? throw new ArgumentNullException("Command callback is required.");
        }

        public bool HideChatMessage
        {
            get;
            set;
        }
    }
}