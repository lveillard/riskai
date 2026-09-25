using NUnit.Framework;

namespace RiskAI.Tests
{
    public sealed class LocalizationAndAiPriorityTests
    {
        [TearDown]
        public void ResetLanguage() => ProbeHooks.SetLanguage(GameLanguage.English);

        [Test]
        public void EnglishIsDefaultAndSpanishCanBeSelected()
        {
            ProbeHooks.SetLanguage(GameLanguage.English);
            Assert.That(GameText.Localize("DESGLOSE DEL ORO"),Is.EqualTo("GOLD BREAKDOWN"));
            Assert.That(GameText.Localize("Las Marcas · 33 ciudades"),Is.EqualTo("The Marches · 33 cities"));
            Assert.That(GameText.Localize("Próxima ronda: +4 oro en 51 s"),Is.EqualTo("Next round: +4 gold in 51 s"));
            ProbeHooks.SetLanguage(GameLanguage.Spanish);
            Assert.That(GameText.Localize("DESGLOSE DEL ORO"),Is.EqualTo("DESGLOSE DEL ORO"));
            Assert.That(GameText.SwitchLabel,Is.EqualTo("EN"));
        }

        [Test]
        public void AiPrefersFinishingThenAdvancingAStartedCountry()
        {
            Assert.That(SkirmishCommander.CountryCompletionTier(5,4),Is.EqualTo(0));
            Assert.That(SkirmishCommander.CountryCompletionTier(5,2),Is.EqualTo(1));
            Assert.That(SkirmishCommander.CountryCompletionTier(5,0),Is.EqualTo(2));
            Assert.That(SkirmishCommander.CountryCompletionTier(0,0),Is.EqualTo(2));
        }
    }
}
