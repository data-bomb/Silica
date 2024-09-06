/*
Silica Admin Mod
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

using MelonLoader;
using SilicaAdminMod;
using System.Drawing;

[assembly: MelonInfo(typeof(SiAdminMod), "Admin Mod", "2.0.965", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

// Color.Cyan
[assembly: MelonColor(255, 0, 255, 255)]
// Color.Green
[assembly: MelonAuthorColor(255, 0, 128, 0)]

// Instruct MelonLoader to load SAM before other mods
[assembly: MelonPriority(-100)]

[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]

[assembly: VerifyLoaderVersion(0, 6, 1, true)]

#if NET6_0
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
#else
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
#endif