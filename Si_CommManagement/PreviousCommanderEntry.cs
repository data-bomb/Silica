/*
Silica Commander Management Mod
Copyright (C) 2023-2024 by databomb

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

namespace Si_CommanderManagement
{
    public class PreviousCommander
    {
        private Player _commander = null!;

        public Player Commander
        {
            get => _commander;
            set => _commander = value ?? throw new ArgumentNullException("Player is required.");
        }

        public int RoundsLeft
        {
            get;
            set;
        }
    }
}