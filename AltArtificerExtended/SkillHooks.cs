﻿using System;
using System.Collections.Generic;
using System.Text;

using EntityStates.Mage.Weapon;

using Mono.Cecil.Cil;

using MonoMod.Cil;

using RoR2;
using RoR2.Skills;

using AltArtificerExtended.Passive;

using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using R2API;
using R2API.Utils;
using AltArtificerExtended;
using AltArtificerExtended.Components;
using static AltArtificerExtended.Passive.AltArtiPassive;
using static AltArtificerExtended.Components.ElementCounter;

namespace AltArtificerExtended
{
    public partial class Main
    {
        public delegate TCheese GiveCheese<TCheese>();

        public void DoHooks() => this.AddHooks();

        void RemoveHooks()
        {

        }

        void AddHooks()
        {
            On.EntityStates.Mage.Weapon.FireFireBolt.FireGauntlet += this.FireFireBolt_FireGauntlet;
            On.EntityStates.Mage.Weapon.BaseChargeBombState.OnEnter += this.BaseChargeBombState_OnEnter;
            On.EntityStates.Mage.Weapon.BaseChargeBombState.FixedUpdate += this.BaseChargeBombState_FixedUpdate;
            //On.EntityStates.Mage.Weapon.BaseThrowBombState.OnEnter += this.BaseChargeBombState_GetNextState;
            On.EntityStates.Mage.Weapon.BaseChargeBombState.OnExit += this.BaseChargeBombState_OnExit;
            On.EntityStates.Mage.Weapon.PrepWall.OnEnter += this.PrepWall_OnEnter;
            On.EntityStates.Mage.Weapon.PrepWall.OnExit += this.PrepWall_OnExit;
            On.EntityStates.Mage.Weapon.Flamethrower.OnEnter += this.Flamethrower_OnEnter;
            On.EntityStates.Mage.Weapon.Flamethrower.FixedUpdate += this.Flamethrower_FixedUpdate;
            On.EntityStates.Mage.Weapon.Flamethrower.OnExit += this.Flamethrower_OnExit;
            IL.RoR2.UI.CharacterSelectController.UpdateSurvivorInfoPanel/* was OnNetworkUserLoadoutChanged*/ += this.CharacterSelectController_OnNetworkUserLoadoutChanged; 
            On.RoR2.HealthComponent.TakeDamage += this.HealthComponent_TakeDamage;
            GlobalEventManager.onCharacterDeathGlobal += this.GlobalEventManager_OnCharacterDeath;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            On.RoR2.CharacterBody.AddBuff_BuffIndex += CharacterBody_AddBuff_BuffIndex;
            On.RoR2.CharacterMaster.OnBodyStart += CharacterMaster_OnBodyStart;
        }

        private void CharacterMaster_OnBodyStart(On.RoR2.CharacterMaster.orig_OnBodyStart orig, CharacterMaster self, CharacterBody body)
        {
            ElementCounter elements = body.GetComponent<ElementCounter>();
            if (elements != null)
            {
                //Debug.Log("Element counter on body start");

                EquipmentIndex equipIndex = EquipmentIndex.None;
                if (body.equipmentSlot != null)
                {
                    equipIndex = body.equipmentSlot.equipmentIndex;
                }

                elements.GetPowers(equipIndex, false, body.skillLocator);
            }

            orig(self, body);
        }

        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);
            /*if (self.HasBuff(manaBoosterBuff))
            {
                self.moveSpeed *= 1 + (NebulaPassive.manaBoosterMspdMultiplier * self.GetBuffCount(manaBoosterBuff));
            }*/
        }

        private void CharacterSelectController_OnNetworkUserLoadoutChanged(ILContext il)
        {
            return;
            void emittedAction(CharacterModel model, Loadout loadout, BodyIndex body)
            {
                GenericSkill[] skills = BodyCatalog.GetBodyPrefabSkillSlots(body);
                for (Int32 i = 0; i < skills.Length; i++)
                {
                    UInt32 selectedSkillIndex = loadout.bodyLoadoutManager.GetSkillVariant(body, i);
                    GenericSkill slot = skills[i];

                    for (Int32 j = 0; j < slot.skillFamily.variants.Length; j++)
                    {
                        SkillDef skillDef = slot.skillFamily.variants[j].skillDef;

                        if (skillDef != null && skillDef is PassiveSkillDef)
                        {
                            var passiveSkillDef = skillDef as PassiveSkillDef;
                        }
                    }
                }

            }

            ILCursor c = new ILCursor(il);

            _ = c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<RoR2.SkinDef>("Apply"));
            _ = c.Emit(OpCodes.Ldloc, 6);
            _ = c.Emit(OpCodes.Ldloc, 2);
            _ = c.Emit(OpCodes.Ldloc, 3);
            _ = c.EmitDelegate<Action<CharacterModel, Loadout, BodyIndex>>(emittedAction);
        }



        #region IceStuff + FireStuff
        private struct FreezeInfo
        {
            public GameObject frozenBy;
            public Vector3 frozenAt;

            public FreezeInfo(GameObject frozenBy, Vector3 frozenAt)
            {
                this.frozenAt = frozenAt;
                this.frozenBy = frozenBy;
            }
        }

        private readonly Dictionary<GameObject, GameObject> frozenBy = new Dictionary<GameObject, GameObject>();

        private void GlobalEventManager_OnCharacterDeath(DamageReport damageReport)
        {
            if (NetworkServer.active)
            {
                CharacterBody aBody = damageReport.attackerBody;
                if (damageReport != null && damageReport.victimBody && aBody && damageReport.victimBody.healthComponent)
                {
                    Power icePower = GetIcePowerLevelFromBody(aBody);

                    int chillDebuffCount = damageReport.victimBody.GetBuffCount(RoR2Content.Buffs.Slow80);

                    if (chillDebuffCount == 0 && (damageReport.damageInfo.damageType.HasFlag(DamageType.Freeze2s) || damageReport.damageInfo.damageType.HasFlag(DamageType.SlowOnHit)))
                        chillDebuffCount++;

                    if (chillDebuffCount > 0 && icePower > 0) //Arctic Blast
                    {
                        AltArtiPassive.DoNova(aBody, icePower, damageReport.victim.transform.position, chillDebuffCount);
                    }
                    #region old stuff
                    /*if (damageReport.victimBody.healthComponent.isInFrozenState)
                    {
                        if (this.frozenBy.ContainsKey(damageReport.victim.gameObject))
                        {
                            GameObject body = this.frozenBy[damageReport.victim.gameObject];
                            if (AltArtiPassive.instanceLookup.ContainsKey(body))
                            {
                                AltArtiPassive passive = AltArtiPassive.instanceLookup[body];
                                passive.DoExecute(damageReport);
                            }
                        }
                    }
                    else if (damageReport.damageInfo.damageType.HasFlag(DamageType.Freeze2s))
                    {
                        if (AltArtiPassive.instanceLookup.ContainsKey(damageReport.attacker))
                        {
                            AltArtiPassive.instanceLookup[damageReport.attacker].DoExecute(damageReport);
                        }
                    }*/
                    #endregion
                }
            }
        }
        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, RoR2.HealthComponent self, RoR2.DamageInfo damageInfo)
        {
            if (damageInfo.damageType.HasFlag(DamageType.Freeze2s))
            {
                this.frozenBy[self.gameObject] = damageInfo.attacker;
            }

            if (damageInfo.dotIndex == Main.burnDot || damageInfo.dotIndex == Main.strongBurnDot)
            {
                if (damageInfo.attacker)
                {
                    CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerBody)
                    {
                        Int32 buffCount = attackerBody.GetBuffCount(Main.meltBuff);

                        if (buffCount >= 0)
                        {
                            damageInfo.damage *= 1f + (AltArtiPassive.burnDamageMult * buffCount);

                            if (Util.CheckRoll((buffCount / 15) * 100, attackerBody.master))
                            {
                                EffectManager.SimpleImpactEffect(RoR2.LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/MagmaOrbExplosion"), damageInfo.position, Vector3.up, true);
                                //ImpactWispEmber MagmaOrbExplosion IgniteDirectionalExplosionVFX IgniteExplosionVFX FireMeatBallExplosion
                            }
                        }
                    }
                }
            }
            CharacterBody vBody = self.body;
            CharacterBody aBody = null;
            if (damageInfo.attacker != null)
                aBody = damageInfo.attacker.GetComponent<CharacterBody>();

            if (vBody != null && aBody != null && damageInfo.procCoefficient != 0 && !damageInfo.rejected)
            {
                Power icePower = GetIcePowerLevelFromBody(aBody);
                float debuffDuration = AltArtiPassive.chillProcDuration;

                if (damageInfo.damageType.HasFlag(DamageType.Freeze2s))
                {
                    float chillCount = AltArtiPassive.freezeProcCount;
                    if (damageInfo.damageType.HasFlag(DamageType.AOE))
                    {
                        chillCount -= 1;
                    }
                    for (int i = 0; i < chillCount; i++)
                    {
                        if (Util.CheckRoll(damageInfo.procCoefficient * 100, aBody.master))
                        {
                            vBody.AddTimedBuffAuthority(RoR2Content.Buffs.Slow80.buffIndex, debuffDuration);
                        }
                    }
                }
                else if(aBody.bodyIndex == BodyCatalog.FindBodyIndex(Main.mageBody))
                {
                    if (damageInfo.damageType.HasFlag(DamageType.SlowOnHit))
                    {
                        damageInfo.damageType = damageInfo.damageType & ~DamageType.SlowOnHit;
                        float procChance = Mathf.Min(1, AltArtiPassive.slowProcChance * damageInfo.procCoefficient * damageInfo.procCoefficient) * 100;

                        if (Util.CheckRoll(procChance, aBody.master))
                        {
                            vBody.AddTimedBuffAuthority(RoR2Content.Buffs.Slow80.buffIndex, debuffDuration);
                        }
                    }
                }


                int chillDebuffCount = vBody.GetBuffCount(RoR2Content.Buffs.Slow80);
                if (chillDebuffCount >= AltArtiPassive.novaDebuffThreshold && icePower > Power.None) //Arctic Blast
                {
                    vBody.ClearTimedBuffs(RoR2Content.Buffs.Slow80);
                    AltArtiPassive.DoNova(aBody, icePower, damageInfo.position, AltArtiPassive.novaDebuffThreshold);
                }
            }
           orig(self, damageInfo);
        }

        private void CharacterBody_AddBuff_BuffIndex(On.RoR2.CharacterBody.orig_AddBuff_BuffIndex orig, CharacterBody self, BuffIndex buffType)
        {
            if (buffType == RoR2Content.Buffs.Slow80.buffIndex && self.GetBuffCount(RoR2Content.Buffs.Slow80.buffIndex) >= AltArtiPassive.novaDebuffThreshold)
            {
                return;
            }
            orig(self, buffType);
        }

        #endregion
        #region Flamethrower
        private class FlamethrowerContext
        {
            public AltArtiPassive passive;
            public Single timer;

            public FlamethrowerContext(AltArtiPassive passive)
            {
                this.passive = passive;
                this.timer = 0f;
            }
        }
        private readonly Dictionary<Flamethrower, FlamethrowerContext> flamethrowerContext = new Dictionary<Flamethrower, FlamethrowerContext>();
        private void Flamethrower_OnEnter(On.EntityStates.Mage.Weapon.Flamethrower.orig_OnEnter orig, Flamethrower self)
        {
            orig(self);
            GameObject obj = self.outer.gameObject;
            if (AltArtiPassive.instanceLookup.ContainsKey(obj))
            {
                AltArtiPassive passive = AltArtiPassive.instanceLookup[obj];
                var context = new FlamethrowerContext(passive);
                passive.SkillCast();
                this.flamethrowerContext[self] = context;
            }
        }
        private void Flamethrower_FixedUpdate(On.EntityStates.Mage.Weapon.Flamethrower.orig_FixedUpdate orig, Flamethrower self)
        {
            orig(self);
            if (this.flamethrowerContext.ContainsKey(self))
            {
                FlamethrowerContext context = this.flamethrowerContext[self];
                context.timer += Time.fixedDeltaTime * context.passive.ext_attackSpeedStat;
                Int32 count = 0;
                while (context.timer >= context.passive.ext_flamethrowerInterval && count <= context.passive.ext_flamethrowerMaxPerTick)
                {
                    context.passive.SkillCast();
                    count++;
                    context.timer -= context.passive.ext_flamethrowerInterval;
                }
            }
        }
        private void Flamethrower_OnExit(On.EntityStates.Mage.Weapon.Flamethrower.orig_OnExit orig, Flamethrower self)
        {
            orig(self);
            if (this.flamethrowerContext.ContainsKey(self))
            {
                FlamethrowerContext context = this.flamethrowerContext[self];
                context.passive.SkillCast();
                _ = this.flamethrowerContext.Remove(self);
            }
        }
        #endregion
        #region Ice Wall
        private class PrepWallContext
        {
            public AltArtiPassive passive;
            public AltArtiPassive.BatchHandle handle;

            public PrepWallContext(AltArtiPassive passive, AltArtiPassive.BatchHandle handle)
            {
                this.passive = passive;
                this.handle = handle;
            }
        }
        private readonly Dictionary<PrepWall, PrepWallContext> prepWallContext = new Dictionary<PrepWall, PrepWallContext>();
        private void PrepWall_OnEnter(On.EntityStates.Mage.Weapon.PrepWall.orig_OnEnter orig, PrepWall self)
        {
            orig(self);
            GameObject obj = self.outer.gameObject;
            if (AltArtiPassive.instanceLookup.ContainsKey(obj))
            {
                AltArtiPassive passive = AltArtiPassive.instanceLookup[obj];
                var handle = new AltArtiPassive.BatchHandle();
                passive.SkillCast(handle);
                var context = new PrepWallContext(passive, handle);
                this.prepWallContext[self] = context;
            }
        }
        private void PrepWall_OnExit(On.EntityStates.Mage.Weapon.PrepWall.orig_OnExit orig, PrepWall self)
        {
            orig(self);
            if (this.prepWallContext.ContainsKey(self))
            {
                PrepWallContext context = this.prepWallContext[self];
                context.handle.Fire(context.passive.ext_prepWallMinDelay, context.passive.ext_prepWallMaxDelay);
                _ = this.prepWallContext.Remove(self);
            }
        }
        #endregion
        #region Nano Bomb/Spear
        private class NanoBombContext
        {
            public AltArtiPassive passive;
            public AltArtiPassive.BatchHandle handle;
            public Single timer;
            public NanoBombContext(AltArtiPassive passive, AltArtiPassive.BatchHandle handle)
            {
                this.passive = passive;
                this.handle = handle;
                this.timer = 0f;
            }
        }
        private readonly Dictionary<BaseChargeBombState, NanoBombContext> nanoBombContext = new Dictionary<BaseChargeBombState, NanoBombContext>();
        private void BaseChargeBombState_OnEnter(On.EntityStates.Mage.Weapon.BaseChargeBombState.orig_OnEnter orig, BaseChargeBombState self)
        {
            //Debug.Log(self.chargeEffectPrefab.name);
            orig(self);
            GameObject obj = self.outer.gameObject;
            if (AltArtiPassive.instanceLookup.ContainsKey(obj))
            {
                AltArtiPassive passive = AltArtiPassive.instanceLookup[obj];
                var handle = new AltArtiPassive.BatchHandle();
                var context = new NanoBombContext(passive, handle);
                this.nanoBombContext[self] = context;
                passive.SkillCast(handle);
            }
        }
        private void BaseChargeBombState_FixedUpdate(On.EntityStates.Mage.Weapon.BaseChargeBombState.orig_FixedUpdate orig, BaseChargeBombState self)
        {
            orig(self);
            if (this.nanoBombContext.ContainsKey(self))
            {
                NanoBombContext context = this.nanoBombContext[self];
                context.timer += Time.fixedDeltaTime * context.passive.ext_attackSpeedStat;
                Int32 count = 0;
                while (context.timer >= context.passive.ext_nanoBombInterval && count <= context.passive.ext_nanoBombMaxPerTick)
                {
                    count++;
                    context.passive.SkillCast(context.handle);
                    context.timer -= context.passive.ext_nanoBombInterval;
                }
            }
        }
        /*private void BaseThrowBombState_OnEnter(On.EntityStates.Mage.Weapon.BaseThrowBombState.orig_OnEnter orig, BaseThrowBombState self)
        {
            orig(self);
            if (this.nanoBombContext.ContainsKey(self))
            {
                NanoBombContext context = this.nanoBombContext[self];

                Int32 count = 0;
                while (context.timer >= context.passive.ext_nanoBombInterval && count <= context.passive.ext_nanoBombMaxPerTick)
                {
                    count++;
                    context.passive.SkillCast(context.handle);
                    context.timer -= context.passive.ext_nanoBombInterval;
                }

                context.handle.Fire(context.passive.ext_nanoBombMinDelay, context.passive.ext_nanoBombMaxDelay);
                _ = this.nanoBombContext.Remove(self);
            }
        }*/
        private void BaseChargeBombState_OnExit(On.EntityStates.Mage.Weapon.BaseChargeBombState.orig_OnExit orig, BaseChargeBombState self)
        {
            orig(self);
            if (this.nanoBombContext.ContainsKey(self))
            {
                NanoBombContext context = this.nanoBombContext[self];

                Int32 count = 0;
                while (context.timer >= context.passive.ext_nanoBombInterval && count <= context.passive.ext_nanoBombMaxPerTick)
                {
                    count++;
                    context.passive.SkillCast(context.handle);
                    context.timer -= context.passive.ext_nanoBombInterval;
                }

                context.handle.Fire(context.passive.ext_nanoBombMinDelay, context.passive.ext_nanoBombMaxDelay);
                _ = this.nanoBombContext.Remove(self);
            }
        }
        #endregion
        #region Fire/Lightning Bolt
        private void FireFireBolt_FireGauntlet(On.EntityStates.Mage.Weapon.FireFireBolt.orig_FireGauntlet orig, FireFireBolt self)
        {
            orig(self);
            GameObject obj = self.outer.gameObject;
            if (AltArtiPassive.instanceLookup.TryGetValue(obj, out AltArtiPassive passive))
            {
                passive.SkillCast();
            }
        }
        #endregion
    }
}
