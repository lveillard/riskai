using UnityEngine;
using RiskAI.Core;

namespace RiskAI
{
    /// <summary>
    /// Maps a source attack timeline onto an authored attack clip. Simulation owns
    /// the strike; presentation samples the clip's contact pose at the same time.
    /// </summary>
    public static class AttackPresentationTiming
    {
        const float LegacyPresentationSeconds = .45f;

        public static string Clip(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Footman: return "1H_Melee_Attack_Slice_Horizontal";
                case UnitKind.Mage: return "Spellcast_Shoot";
                case UnitKind.Guard:
                case UnitKind.Mortar: return null;
                case UnitKind.MarineMajor:
                case UnitKind.MarineGeneral: return "2H_Melee_Attack_Slice";
                case UnitKind.MarinePrivate:return "1H_Ranged_Shoot";
                default: return "2H_Ranged_Shoot";
            }
        }

        public static float ContactNormalizedTime(UnitKind kind)
        {
            switch(kind)
            {
                // Keyframes are measured from the embedded 30 fps FBX takes.
                case UnitKind.Footman: return 8f/32f;
                case UnitKind.Mage: return 9f/28f;
                case UnitKind.MarineMajor:
                case UnitKind.MarineGeneral: return 13f/33f;
                // The ranged release starts its recoil after frame 8/32.
                case UnitKind.Archer:
                case UnitKind.Medic:
                case UnitKind.MarinePrivate: return 8f/32f;
                // Procedural mortar and mounted-lance curves define contact halfway.
                default: return .5f;
            }
        }

        public static float RecoverySeconds(float attackPoint, float backswing, float cooldown)
        {
            attackPoint = Mathf.Max(0, attackPoint);
            cooldown = Mathf.Max(attackPoint, cooldown);
            float recovery = backswing > 0
                ? backswing
                : Mathf.Max(0, LegacyPresentationSeconds - attackPoint);
            return Mathf.Min(recovery, cooldown - attackPoint);
        }

        public static float Duration(float attackPoint, float backswing, float cooldown)
            => Mathf.Max(0, attackPoint) + RecoverySeconds(attackPoint, backswing, cooldown);

        public static float NormalizedTime(float elapsed, float attackPoint, float recovery, float contactNormalizedTime)
        {
            if (elapsed <= 0) return 0;
            attackPoint = Mathf.Max(0, attackPoint);
            recovery = Mathf.Max(0, recovery);
            contactNormalizedTime = Mathf.Clamp01(contactNormalizedTime);
            if (attackPoint > 0 && elapsed < attackPoint)
                return contactNormalizedTime * Mathf.Clamp01(elapsed / attackPoint);
            if (recovery <= 0) return elapsed <= attackPoint ? contactNormalizedTime : 1;
            return Mathf.Lerp(contactNormalizedTime, 1,
                Mathf.Clamp01((elapsed - attackPoint) / recovery));
        }

        public static float ContactPose(float normalizedTime, float contactNormalizedTime)
        {
            if (normalizedTime < 0) return 0;
            contactNormalizedTime = Mathf.Clamp(contactNormalizedTime,.001f,.999f);
            return normalizedTime <= contactNormalizedTime
                ? Mathf.SmoothStep(0, 1, normalizedTime / contactNormalizedTime)
                : 1 - Mathf.SmoothStep(0, 1,
                    (normalizedTime - contactNormalizedTime) / (1 - contactNormalizedTime));
        }
    }
}
