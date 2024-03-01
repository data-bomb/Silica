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

using MelonLoader;
using System;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using System.Collections.Generic;
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class SiConstants
    {
        public const string SAM_AddAdmin_Usage = " usage: <player> <powers> <level>";

        public const int MaxPlayableTeams = 3;

        public enum ETeam
        {
            Alien = 0,
            Centauri = 1,
            Sol = 2,
            Wildlife = 3
        }
    }
}