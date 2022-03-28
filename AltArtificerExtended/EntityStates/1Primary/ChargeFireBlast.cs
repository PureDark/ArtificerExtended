﻿using System;
using System.Collections.Generic;
using System.Text;
using EntityStates;
using EntityStates.Mage.Weapon;
using EntityStates.BeetleQueenMonster;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
//using AlternativeArtificer.States.Main;
using System.Threading.Tasks;
using AltArtificerExtended.Passive;
using AltArtificerExtended.Skills;

namespace AltArtificerExtended.EntityState
{
    class ChargeFireBlast : BaseSkillState
    {
        public GameObject projectilePrefabOuter = _2FireSkill2Skill.outerFireball;
        public GameObject projectilePrefabInner = _2FireSkill2Skill.innerFireball;
        public GameObject muzzleflashEffectPrefab = RoR2.LegacyResourcesAPI.Load<GameObject>("prefabs/effects/MuzzleflashMageFireLarge");
        public GameObject chargeEffectPrefab = RoR2.LegacyResourcesAPI.Load<GameObject>("prefabs/effects/ChargeMageFireBomb");
        public string chargeSoundString = "Play_mage_m2_charge";
        public float baseChargeDuration = 1f;
        public float baseWinddownDuration = 0.2f;

        public static float minRadius = 0;
        public static float maxRadius = 0.5f;

        public static float minDamageCoefficient = 1.2f;
        public static float maxDamageCoefficient = Main.artiBoltDamage * 1.5f;
        public static float procCoefficient = 1f;
        public float force = 0;
        public float selfForce = 150;
        public static float recoilAmplitude = 4f;

        public static GameObject crosshairOverridePrefab;

        private const float baseMinChargeDuration = 0.15f;
        private float stopwatch;
        private float timer;
        private float frequency = 0.25f;
        private float windDownDuration;
        private float chargeDuration;
        private float minChargeDuration;
        private bool hasFiredBomb;

        private string muzzleString;
        private GameObject defaultCrosshairPrefab;
        private uint soundID;

        private AltArtiPassive.BatchHandle handle;

        public override void OnEnter()
        {
            base.OnEnter();

            this.handle = new AltArtiPassive.BatchHandle();

            this.windDownDuration = this.baseWinddownDuration / this.attackSpeedStat;
            this.chargeDuration = this.baseChargeDuration / this.attackSpeedStat;
            this.minChargeDuration = baseMinChargeDuration / this.attackSpeedStat;

            this.soundID = Util.PlayAttackSpeedSound(this.chargeSoundString, base.gameObject, this.attackSpeedStat);
            base.characterBody.SetAimTimer(this.chargeDuration + this.windDownDuration + 2f);
            this.muzzleString = "MuzzleBetween";

            base.PlayAnimation("Gesture, Additive", "PrepFlamethrower", "Flamethrower.playbackRate", this.chargeDuration);
            this.defaultCrosshairPrefab = base.characterBody._defaultCrosshairPrefab;
            if (ChargeMeteors.crosshairOverridePrefab)
            {
                base.characterBody._defaultCrosshairPrefab = ChargeMeteors.crosshairOverridePrefab;
            }
        }

        public override void Update()
        {
            base.Update();

            base.characterBody.SetSpreadBloom(Util.Remap(this.GetChargeProgressSmooth(), 0f, 1f, minRadius, maxRadius), true);
        }
        private float GetChargeProgressSmooth()
        {
            return Mathf.Clamp01(this.stopwatch / this.chargeDuration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            this.stopwatch += Time.fixedDeltaTime;
            this.timer += Time.fixedDeltaTime * base.characterBody.attackSpeed;

            GameObject obj = base.outer.gameObject;
            if (AltArtiPassive.instanceLookup.TryGetValue(obj, out var passive))
            {
                while (this.timer > this.frequency)
                {
                    this.timer -= this.frequency;
                    passive.SkillCast(handle);
                }
            }

            if (!this.hasFiredBomb && (this.stopwatch >= chargeDuration || !IsKeyDownAuthority()) &&
                !this.hasFiredBomb && this.stopwatch >= minChargeDuration)
            {
                this.FireFireBlast();
            }
            if (this.stopwatch >= this.windDownDuration && this.hasFiredBomb && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public void FireFireBlast()
        {
            this.hasFiredBomb = true;

            float recoil = 1f * ChargeFireBlast.recoilAmplitude;
            base.AddRecoil(-recoil, -2f * recoil, -recoil, recoil);
            if (this.muzzleflashEffectPrefab)
            {
                EffectManager.SimpleMuzzleFlash(this.muzzleflashEffectPrefab, base.gameObject, this.muzzleString, false);
            }
            if (base.isAuthority)
            {
                float charge = this.GetChargeProgressSmooth();
                Ray aimRay = base.GetAimRay();
                if (this.projectilePrefabOuter != null && this.projectilePrefabInner != null)
                {
                    float damage = Util.Remap(charge, 0, 1, minDamageCoefficient, maxDamageCoefficient);
                    bool isCrit = Util.CheckRoll(this.critStat, base.characterBody.master);

                    FireOuterFireballs(aimRay, damage, isCrit);
                    FireInnerFireball(aimRay, damage, isCrit);
                }
                if (base.characterMotor)
                {
                    base.characterMotor.ApplyForce(aimRay.direction * (-this.selfForce * charge), false, false);
                }
            }

            base.characterBody._defaultCrosshairPrefab = this.defaultCrosshairPrefab;
            this.stopwatch = 0f;
            this.timer = 0f;
            this.handle.Fire(0f, 0.5f);
        }

        void FireOuterFireballs(Ray aimRay, float damage, bool isCrit)
        {
            var fireball = _2FireSkill2Skill.outerFireball;

            // outer left
            ProjectileManager.instance.FireProjectile(fireball, aimRay.origin,
                Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * damage,
                this.force, isCrit, DamageColorIndex.Default, null, -1f);
            //outer right
            ProjectileManager.instance.FireProjectile(fireball, aimRay.origin,
                Util.QuaternionSafeLookRotation(aimRay.direction, Vector3.down), base.gameObject, this.damageStat * damage,
                this.force, isCrit, DamageColorIndex.Default, null, -1f);
        }
        void FireInnerFireball(Ray aimRay, float damage, bool isCrit)
        {
            var fireball = _2FireSkill2Skill.innerFireball;

            // inner
            ProjectileManager.instance.FireProjectile(fireball, aimRay.origin,
                Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * damage,
                this.force, isCrit, DamageColorIndex.Default, null, -1f);
        }

        public override void OnExit()
        {
            base.PlayAnimation("Gesture, Additive", "FireWall");
            AkSoundEngine.StopPlayingID(this.soundID);
            base.characterBody._defaultCrosshairPrefab = this.defaultCrosshairPrefab;

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Pain;
        }
    }
}
