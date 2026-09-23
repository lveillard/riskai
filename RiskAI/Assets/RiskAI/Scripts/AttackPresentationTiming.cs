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
        // Swing length for profiles without a source backswing.
        const float DefaultPresentationSeconds = .45f;

        // The clip and its contact time are units.json presentation (attackClip, contact).

        public static float RecoverySeconds(float attackPoint, float backswing, float cooldown)
        {
            attackPoint = Mathf.Max(0, attackPoint);
            cooldown = Mathf.Max(attackPoint, cooldown);
            float recovery = backswing > 0
                ? backswing
                : Mathf.Max(0, DefaultPresentationSeconds - attackPoint);
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
