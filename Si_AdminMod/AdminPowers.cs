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

namespace SilicaAdminMod
{
    public enum Power
    {
        None = 0,
        Slot = (1 << 0),
        Vote = (1 << 1),
        Kick = (1 << 2),
        Ban = (1 << 3),
        Unban = (1 << 4),
        Slay = (1 << 5),
        Map = (1 << 6),
        Cheat = (1 << 7),
        Commander = (1 << 8),
        Skip = (1 << 9),
        End = (1 << 10),
        Eject = (1 << 11),
        Mute = (1 << 12),
        MuteForever = (1 << 13),
        Generic = (1 << 14),
        Teams = (1 << 15),
        Custom1 = (1 << 16),
        Custom2 = (1 << 17),
        Custom3 = (1 << 18),
        Custom4 = (1 << 19),
        Reserved1 = (1 << 21),
        Reserved2 = (1 << 22),
        Reserved3 = (1 << 23),
        Reserved4 = (1 << 24),
        Rcon = (1 << 25),
        Root = (1 << 26)
    }
}