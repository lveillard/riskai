using System;

namespace RiskAI.Core
{
    /// <summary>
    /// Top-bar wording for the round timer. The timer counts down to the next income,
    /// so it is labelled as income rather than as elapsed round time. Spanish source
    /// strings; GameText localizes them.
    /// </summary>
    public static class IncomeCountdown
    {
        public static int SecondsRemaining(float elapsedInRound,float roundSeconds=BattleRules.RoundSeconds) =>
            Math.Max(0,(int)Math.Ceiling(Math.Max(0,roundSeconds-elapsedInRound)-1e-4));

        /// <summary>0 right after an income payment, 1 when the next one is due.</summary>
        public static float Progress(float elapsedInRound,float roundSeconds=BattleRules.RoundSeconds) =>
            roundSeconds<=0?1:Math.Min(1,Math.Max(0,elapsedInRound/roundSeconds));

        /// <summary>"R3 · Ingreso en 58 s" on desktop; "58 s" on compact bars.</summary>
        public static string Label(int round,float elapsedInRound,bool compact,float roundSeconds=BattleRules.RoundSeconds)
        {
            int seconds=SecondsRemaining(elapsedInRound,roundSeconds);
            return compact?seconds+" s":"R"+round+" · Ingreso en "+seconds+" s";
        }

        /// <summary>Long form for the tooltip / long-press.</summary>
        public static string Detail(int round,float elapsedInRound,int income,float roundSeconds=BattleRules.RoundSeconds) =>
            "Ronda "+round+" · próximo ingreso +"+income+" oro en "+SecondsRemaining(elapsedInRound,roundSeconds)+" s";
    }
}
