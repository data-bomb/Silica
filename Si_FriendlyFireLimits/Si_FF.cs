/*
 Silica Friendly-Fire Adjustments Mod
 Copyright (C) 2023-2025 by databomb
 
 * Description *
 For Silica listen servers, adjust the amount of friendly fire damage 
 based on the unit and damage type.

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
using HarmonyLib;
using Si_FriendlyFireLimits;
using System;
using SilicaAdminMod;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;

[assembly: MelonInfo(typeof(FriendlyFireLimits), "Friendly Fire Limits", "1.3.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
#if NET6_0
[assembly: MelonOptionalDependencies("Admin Mod", "QList")]
#else
[assembly: MelonOptionalDependencies("Admin Mod")]
#endif

namespace Si_FriendlyFireLimits
{
    public class FriendlyFireLimits : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<float> _UnitOnUnitNonExplosionDamageMultipler = null!;
        static MelonPreferences_Entry<float> _UnitOnUnitExplosionDamageMultiplier = null!;
        static MelonPreferences_Entry<float> _UnitOnStructureExplosionDamageMultiplier = null!;
        static MelonPreferences_Entry<float> _UnitOnStructureNonExplosionDamageMultiplier = null!;
        static MelonPreferences_Entry<float> _StructuresAttackingUnitsDamageRatio = null!;
        static MelonPreferences_Entry<float> _StructuresAttackingStructuresDamageRatio = null!;
        static MelonPreferences_Entry<bool> _HarvesterPassthrough = null!;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _UnitOnUnitNonExplosionDamageMultipler ??= _modCategory.CreateEntry<float>("FriendlyFire_Unit_ATKs_Unit_DamageRatio", 0.5f);
            _UnitOnUnitExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_Unit_ATKs_Unit_DamageRatio_Explosion", 0.875f);
            _UnitOnStructureExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_Unit_ATKs_Structure_DamageRatio_Explosion", 0.625f);
            _UnitOnStructureNonExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_Unit_ATKs_Structure_DamageRatio", 0.0f);
            _StructuresAttackingUnitsDamageRatio ??= _modCategory.CreateEntry<float>("FriendlyFire_Structue_ATKs_Unit_DamageRatio", 0.25f);
            _StructuresAttackingStructuresDamageRatio ??= _modCategory.CreateEntry<float>("FriendlyFire_Structue_ATKs_Structure_DamageRatio", 0.25f);
            _HarvesterPassthrough ??= _modCategory.CreateEntry<bool>("FriendlyFire_Allow_Harvester_Damage", true);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public override void OnLateInitializeMelon()
        {
            Event_Damage.OnPrePlayerDamageReceived += OnPreDamageReceived_FriendlyFireCheck;

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (QListLoaded)
            {
                QListRegistration();
            }
            #endif
        }

        public void OnPreDamageReceived_FriendlyFireCheck(object? sender, OnPreDamageReceivedArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                // not team damage?
                if (!IsDamageFriendlyFire(args.DamageManager, args.Instigator))
                {
                    return;
                }

                // damaging self?
                if (IsDamageSelfInflicted(args.DamageManager, args.Instigator))
                {
                    return;
                }

                // adjust FF damage
                args.Damage = FindTeamDamage(args.DamageManager, args.Instigator, args.Damage, EDamageType.None, true);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnPreDamageReceived_FriendlyFireCheck");
            }
        }

        public static bool IsDamageSelfInflicted(DamageManager victimDamageManager, UnityEngine.GameObject instigatorObject)
        {
            return (instigatorObject.GetBaseGameObject() == victimDamageManager.Owner);
        }

        public static bool IsDamageFriendlyFire(DamageManager victimDamageManager, UnityEngine.GameObject instigatorObject)
        {
            BaseGameObject instigatorBase = GameFuncs.GetBaseGameObject(instigatorObject);
            if (instigatorBase == null || instigatorBase.Team == null || victimDamageManager.Owner == null ||victimDamageManager.Owner.Team == null)
            {
                return false;
            }

            return (instigatorBase.Team.Index == victimDamageManager.Owner.Team.Index);
        }

        public static float FindTeamDamage(DamageManager victimDamageManager, UnityEngine.GameObject instigatorObject, float originalDamage, EDamageType damageType = EDamageType.None, bool playerControlled = false)
        {
            // Victim/Attacker Object Types
            ObjectInfo victimObject = victimDamageManager.Owner.ObjectInfo;
            ObjectInfo attackerObject = instigatorObject.GetBaseGameObject().ObjectInfo;
            ObjectInfoType victimType = victimObject.ObjectType;
            ObjectInfoType attackerType = attackerObject.ObjectType;

            // Unit vs Unit Friendly Fire
            if (victimType == ObjectInfoType.Unit && attackerType == ObjectInfoType.Unit)
            {
                // check if we should skip harvester damage adjustments
                if (_HarvesterPassthrough.Value && victimObject.UnitType == UnitType.Harvester)
                {
                    return originalDamage;
                }

                // AoE explosion FF damage
                if (damageType == EDamageType.Explosion)
                {
                    return originalDamage * _UnitOnUnitExplosionDamageMultiplier.Value;
                }

                // any other Unit vs Unit FF damage
                return originalDamage * _UnitOnUnitNonExplosionDamageMultipler.Value;
            }

            // Unit vs Structure Friendly Fire
            if (victimType == ObjectInfoType.Structure && attackerType == ObjectInfoType.Unit)
            {
                // AoE explosion FF damage
                if (damageType == EDamageType.Explosion)
                {
                    return originalDamage * _UnitOnStructureExplosionDamageMultiplier.Value;
                }

                // any other Unit vs Structure FF damage
                return originalDamage * _UnitOnStructureNonExplosionDamageMultiplier.Value;
            }

            // Structure vs Unit Friendly Fire
            if (victimType == ObjectInfoType.Unit && attackerType == ObjectInfoType.Structure)
            {
                // check if we should skip harvester damage adjustments
                if (_HarvesterPassthrough.Value && victimObject.UnitType == UnitType.Harvester)
                {
                    return originalDamage;
                }

                // any other Structure vs Unit FF damage
                return originalDamage * _StructuresAttackingUnitsDamageRatio.Value;
            }

            // Structure vs Structure Friendly Fire
            if (victimType == ObjectInfoType.Structure && attackerType == ObjectInfoType.Structure)
            {
                // any Structure vs Structure FF damage
                return originalDamage * _StructuresAttackingStructuresDamageRatio.Value;
            }

            MelonLogger.Warning("Hit unexpected statement in FindTeamDamage");
            return originalDamage;
        }

        #if NET6_0
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void QListRegistration()
        {
            QList.Options.RegisterMod(this);

            QList.OptionTypes.FloatOption unitNonExplosion = new(_UnitOnUnitNonExplosionDamageMultipler, false, _UnitOnUnitNonExplosionDamageMultipler.Value, 0.0f, 2.0f);
            QList.OptionTypes.FloatOption unitExplosion = new(_UnitOnUnitExplosionDamageMultiplier, false, _UnitOnUnitExplosionDamageMultiplier.Value, 0.0f, 2.0f);
            QList.OptionTypes.FloatOption structureExplosion = new(_UnitOnStructureExplosionDamageMultiplier, false, _UnitOnStructureExplosionDamageMultiplier.Value, 0.0f, 2.0f);
            QList.OptionTypes.FloatOption structureNonExplosion = new(_UnitOnStructureNonExplosionDamageMultiplier, false, _UnitOnStructureNonExplosionDamageMultiplier.Value, 0.0f, 2.0f);
            QList.OptionTypes.FloatOption structureAttacksUnits = new(_StructuresAttackingUnitsDamageRatio, false, _UnitOnStructureNonExplosionDamageMultiplier.Value, 0.0f, 2.0f);
            QList.OptionTypes.FloatOption structureAttacksStructures = new(_StructuresAttackingStructuresDamageRatio, false, _UnitOnStructureNonExplosionDamageMultiplier.Value, 0.0f, 2.0f);
            QList.OptionTypes.BoolOption harvesterPassthrough = new(_HarvesterPassthrough, _HarvesterPassthrough.Value);

            QList.Options.AddOption(unitNonExplosion);
            QList.Options.AddOption(unitExplosion);
            QList.Options.AddOption(structureExplosion);
            QList.Options.AddOption(structureNonExplosion);
            QList.Options.AddOption(structureAttacksUnits);
            QList.Options.AddOption(structureAttacksStructures);
            QList.Options.AddOption(harvesterPassthrough);
        }
        #endif

        // use this patch for AI-instigated damage
        [HarmonyPatch(typeof(DamageManager), nameof(DamageManager.ApplyDamage))]
        static class ApplyPatchApplyDamage
        {
            public static bool Prefix(DamageManager __instance, float __result, UnityEngine.Collider __0, ref float __1, EDamageType __2, UnityEngine.GameObject __3, UnityEngine.Vector3 __4)
            {
                try
                {
                    // is the instigator AI or player-controlled?
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__3);
                    if (attackerBase.NetworkComponent.OwnerPlayer != null)
                    {
                        MelonLogger.Msg("Aborting for player-controlled damage.");
                        return true;
                    }

                    // not team damage?
                    if (!IsDamageFriendlyFire(__instance, __3))
                    {
                        return true;
                    }

                    // damaging self?
                    if (IsDamageSelfInflicted(__instance, __3))
                    {
                        return true;
                    }

                    // adjust FF damage
                    __1 = FindTeamDamage(__instance, __3, __1, __2);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run DamageManager::ApplyDamage");
                }

                return true;
            }
        }
    }
}