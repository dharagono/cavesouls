using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Pure static, deterministic Souls-like damage formula calculator.
    /// Zero GC allocations.
    /// </summary>
    public static class DamageCalculator
    {
        public static DamageResult Evaluate(
            in DamagePayload payload,
            in DefenseProfile defense,
            in ShieldBlockProfile shield,
            bool isBlocking,
            bool isFacingAttacker,
            float currentStamina,
            float criticalMultiplier = 2.5f)
        {
            DamageResult result = default;

            float phys = payload.PhysicalDamage;
            float magic = payload.MagicDamage;
            float fire = payload.FireDamage;
            float lightning = payload.LightningDamage;
            float dark = payload.DarkDamage;
            float poiseDmg = payload.PoiseDamage;

            // Check directional Shield Block (must be blocking, facing attacker, and not a Riposte/Backstab)
            if (isBlocking && isFacingAttacker && !payload.IsCriticalRiposteOrBackstab)
            {
                result.WasBlocked = true;
                result.ImpactedMaterial = shield.ShieldMaterial;

                phys *= (1f - Mathf.Clamp01(shield.PhysicalBlockPercent));
                magic *= (1f - Mathf.Clamp01(shield.MagicBlockPercent));
                fire *= (1f - Mathf.Clamp01(shield.FireBlockPercent));
                lightning *= (1f - Mathf.Clamp01(shield.LightningBlockPercent));
                dark *= (1f - Mathf.Clamp01(shield.DarkBlockPercent));

                float stabilityFactor = 1f - Mathf.Clamp(shield.Stability, 0f, 95f) * 0.01f;
                result.StaminaDrained = Mathf.Max(2f, payload.StaminaDrainOnBlock * stabilityFactor);

                if (currentStamina < result.StaminaDrained)
                {
                    result.TriggeredGuardBreak = true;
                    // Partial chip damage amplification on guard break
                    phys *= 1.35f;
                }
                else
                {
                    // Successful block negates poise damage
                    poiseDmg = 0f;
                }
            }

            // Apply Flat Defense Mitigation + Percentage Absorption per Element/SubType
            float physAbsorb = GetPhysicalAbsorption(in defense, payload.PhysicalSubType);
            float finalPhys = MitigateElement(phys, defense.FlatPhysical, physAbsorb);
            float finalMagic = MitigateElement(magic, defense.FlatMagic, defense.AbsorbMagic);
            float finalFire = MitigateElement(fire, defense.FlatFire, defense.AbsorbFire);
            float finalLightning = MitigateElement(lightning, defense.FlatLightning, defense.AbsorbLightning);
            float finalDark = MitigateElement(dark, defense.FlatDark, defense.AbsorbDark);

            float totalDamage = finalPhys + finalMagic + finalFire + finalLightning + finalDark;

            if (payload.IsCriticalRiposteOrBackstab)
            {
                totalDamage *= criticalMultiplier;
            }

            result.TotalDamageDealt = Mathf.Max(0f, totalDamage);
            result.PoiseDamageDealt = poiseDmg;
            return result;
        }

        private static float MitigateElement(float rawDamage, float flatDefense, float absorptionPercent)
        {
            if (rawDamage <= 0.001f) return 0f;

            // Souls-style piecewise soft-capped flat mitigation followed by percentage absorption
            float afterFlat = Mathf.Max(rawDamage * 0.15f, rawDamage - flatDefense);
            float absorbed = afterFlat * (1f - Mathf.Clamp01(absorptionPercent));
            return Mathf.Max(1f, absorbed);
        }

        private static float GetPhysicalAbsorption(in DefenseProfile defense, PhysicalDamageSubType subType)
        {
            switch (subType)
            {
                case PhysicalDamageSubType.Strike: return defense.AbsorbStrike;
                case PhysicalDamageSubType.Slash: return defense.AbsorbSlash;
                case PhysicalDamageSubType.Thrust: return defense.AbsorbThrust;
                default: return defense.AbsorbStandard;
            }
        }
    }
}
