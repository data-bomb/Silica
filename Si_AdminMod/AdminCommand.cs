/*
Silica Admin Mod
Copyright (C) 2023 by databomb

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
    public class AdminCommand
    {
        private string _admincommandtext = null!;

        public String AdminCommandText
        {
            get => _admincommandtext;
            set => _admincommandtext = value ?? throw new ArgumentNullException("Command name is required.");
        }

        private HelperMethods.CommandCallback _commandCallback = null!;

        public HelperMethods.CommandCallback AdminCallback
        {
            get => _commandCallback;
            set => _commandCallback = value ?? throw new ArgumentNullException("Command callback is required.");
        }

        public Power AdminPower
        {
            get;
            set;
        }
    }
}