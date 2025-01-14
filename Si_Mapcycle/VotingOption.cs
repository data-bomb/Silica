/*
Silica Map Cycle
Copyright (C) 2023-2025 by databomb

01/11/2025: DrMuck: Added Option to specify GameModes for maps in mapcycle.txt and rtv

* Description *
Provides map management and cycles to a server.

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Si_Mapcycle
{
    public class VotingOption
    {
        public string MapName { get; set; }
        public string GameMode { get; set; }

        public VotingOption(string mapName, string gameMode)
        {
            MapName = mapName;
            GameMode = gameMode;
        }
    }
}
