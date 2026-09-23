using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    /// <summary>v0.30 roster: h00F, h00I, h00J, h00M, h01A plus the Mortar/Medic reviews.</summary>
    public sealed class SourceRosterV030Tests
    {
        // gold, hp, base, dice, sides, range, cooldown, speed, armor (explicit W3U overrides or RoC inheritance).
        [TestCase(UnitKind.EliteRifleman, "h00F", 6, 450f, 36f, 2, 4, 7f, 1f, 5.4f, 1f, AttackKind.Piercing, ArmorKind.Light)]
        [TestCase(UnitKind.Roarer, "h00I", 4, 400f, 29f, 1, 3, 10f, 2f, 5.4f, 1f, AttackKind.Piercing, ArmorKind.Light)]
        [TestCase(UnitKind.ArmyGeneral, "h00J", 10, 800f, 55f, 2, 5, 2f, 1.45f, 7f, 10f, AttackKind.Normal, ArmorKind.Heavy)]
        [TestCase(UnitKind.Artillery, "h00M", 15, 900f, 55f, 1, 13, 20f, 3f, 4f, 3f, AttackKind.Piercing, ArmorKind.Unarmored)]
        [TestCase(UnitKind.Tank, "h01A", 25, 1500f, 80f, 1, 11, 10f, 1.8f, 5.2f, 9f, AttackKind.Siege, ArmorKind.Fortified)]
        public void NewCityUnitsResolveTheirSourceProfile(UnitKind kind, string rawId, int gold, float health, float baseDamage,
            int dice, int sides, float range, float cooldown, float speed, float armor, AttackKind attack, ArmorKind defense)
        {
            var profile = UnitCatalog.Get(kind);
            Assert.That(UnitCatalog.Get(kind).SourceRawcode, Is.EqualTo(rawId));
            Assert.That(UnitCatalog.Get(kind).Cost, Is.EqualTo(gold));
            Assert.That(UnitCatalog.Get(kind).Points, Is.EqualTo(gold), "upoi equals ugol for every v0.30 land unit.");
            Assert.That(profile.MaxHealth, Is.EqualTo(health));
            Assert.That(profile.Weapon.Base, Is.EqualTo(baseDamage));
            Assert.That(profile.Weapon.Dice, Is.EqualTo(dice));
            Assert.That(profile.Weapon.Sides, Is.EqualTo(sides));
            Assert.That(profile.Weapon.Range, Is.EqualTo(range));
            Assert.That(profile.Weapon.Cooldown, Is.EqualTo(cooldown).Within(.0001f));
            Assert.That(profile.Speed, Is.EqualTo(speed).Within(.0001f));
            Assert.That(profile.Armor, Is.EqualTo(armor));
            Assert.That(profile.AttackType, Is.EqualTo(attack));
            Assert.That(profile.ArmorType, Is.EqualTo(defense));
            Assert.That(ProductionHotkeys.GridKeys, Does.Contain(ProductionHotkeys.Hotkey(kind)));
            Assert.That(UnitCatalog.Get(kind).TrainSeconds, Is.EqualTo(1f), "Every v0.30 unit overrides ubld=1.");
            Assert.That(UnitCatalog.Get(kind).Level, Is.EqualTo(1));
            Assert.That((UnitCatalog.Get(kind).Building==UnitBuilding.City), Is.True);
            Assert.That(UnitCatalog.Get(kind).Weapon.Ranged, Is.EqualTo(range > 2));
        }

        [Test]
        public void NewKindsAreAppendedWithoutMovingCatalogOrdinals()
        {
            Assert.That((int)UnitKind.MarineGeneral, Is.EqualTo(8));
            Assert.That((int)UnitKind.EliteRifleman, Is.EqualTo(9));
            Assert.That((int)UnitKind.Roarer, Is.EqualTo(10));
            Assert.That((int)UnitKind.ArmyGeneral, Is.EqualTo(11));
            Assert.That((int)UnitKind.Artillery, Is.EqualTo(12));
            Assert.That((int)UnitKind.Tank, Is.EqualTo(13));
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind))) Assert.That(UnitCatalog.Get(kind).Id, Is.EqualTo(kind.ToString()));
        }

        [Test]
        public void NewWeaponsFollowSourceDelivery()
        {
            var artillery = UnitCatalog.Get(UnitKind.Artillery).Weapon;
            Assert.That(artillery.Delivery, Is.EqualTo(WeaponDelivery.Artillery));
            Assert.That(artillery.Targeting, Is.EqualTo(WeaponTargeting.LaunchPoint));
            Assert.That(artillery.ProjectileSpeed, Is.EqualTo(900f / 50f));
            Assert.That(artillery.FullDamageRadius, Is.EqualTo(25f / 50f));
            Assert.That(artillery.MediumDamageRadius, Is.EqualTo(100f / 50f));
            Assert.That(artillery.SmallDamageRadius, Is.EqualTo(170f / 50f).Within(.0001f));
            Assert.That(artillery.SplashFactor(3f), Is.EqualTo(.1f));
            Assert.That(UnitCatalog.Get(UnitKind.Artillery).Acquisition.RadiusHostile, Is.EqualTo(20));
            Assert.That(UnitCatalog.Get(UnitKind.Artillery).Mechanical, Is.True);
            Assert.That(UnitCatalog.Get(UnitKind.Artillery).Weapon.MinRange, Is.Zero, "hmtt has no minimum range.");

            var tank = UnitCatalog.Get(UnitKind.Tank).Weapon;
            Assert.That(tank.IsProjectile, Is.True);
            Assert.That(tank.HasSplash, Is.False, "h01A msplash inherits no hfoo splash areas.");
            Assert.That(tank.ProjectileSpeed, Is.EqualTo(1000f / 50f));
            Assert.That(UnitCatalog.Get(UnitKind.Tank).Mechanical, Is.True);

            Assert.That(UnitCatalog.Get(UnitKind.EliteRifleman).Weapon.IsProjectile, Is.False);
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Weapon.ProjectileSpeed, Is.EqualTo(900f / 50f));
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Acquisition.RadiusHostile, Is.EqualTo(8));
            Assert.That(UnitCatalog.Get(UnitKind.EliteRifleman).Acquisition.RadiusHostile, Is.EqualTo(12));
            Assert.That(UnitCatalog.Get(UnitKind.ArmyGeneral).Acquisition.RadiusHostile, Is.EqualTo(10));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Mechanical, Is.False);
        }

        [Test]
        public void MortarMatchesH00HAndHmtmSource()
        {
            var profile = UnitCatalog.Get(UnitKind.Mortar);
            var weapon = UnitCatalog.Get(UnitKind.Mortar).Weapon;
            Assert.That(profile.AttackType, Is.EqualTo(AttackKind.Siege));
            Assert.That(profile.ArmorType, Is.EqualTo(ArmorKind.Medium), "RoC hmtm defType=medium (runtime baseline).");
            Assert.That(profile.Speed, Is.EqualTo(230f / 50f).Within(.0001f));
            Assert.That(profile.Weapon.Cooldown, Is.EqualTo(3.5f));
            Assert.That(profile.Weapon.AttackPoint, Is.EqualTo(1f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Weapon.MinRange, Is.EqualTo(250f / 50f));
            Assert.That(UnitCatalog.Get(UnitKind.Mortar).Acquisition.RadiusHostile, Is.EqualTo(900f / 50f));
            Assert.That(weapon.Delivery, Is.EqualTo(WeaponDelivery.Artillery));
            Assert.That(weapon.ProjectileSpeed, Is.EqualTo(900f / 50f));
            Assert.That(weapon.FullDamageRadius, Is.EqualTo(.5f));
            Assert.That(weapon.MediumDamageRadius, Is.EqualTo(3f));
            Assert.That(weapon.SmallDamageRadius, Is.EqualTo(5f));
            Assert.That(weapon.SplashFactor(.4f), Is.EqualTo(1f));
            Assert.That(weapon.SplashFactor(2f), Is.EqualTo(.35f).Within(.0001f), "h00H explicit uhd1.");
            Assert.That(weapon.SplashFactor(4f), Is.EqualTo(.1f).Within(.0001f));
            Assert.That(weapon.SplashFactor(5.1f), Is.Zero);
        }

        [Test]
        public void MedicHasSourceManaAndLocalHealthAdaptation()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Medic).MaxHealth, Is.EqualTo(220f), "Local v0.30 product decision (h00E explicit uhpm=250).");
            var mana = UnitCatalog.Get(UnitKind.Medic).Mana;
            Assert.That(mana.Maximum, Is.EqualTo(200f));
            Assert.That(mana.Initial, Is.EqualTo(75f));
            Assert.That(mana.Regeneration, Is.EqualTo(1.5f));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Heal.Amount, Is.EqualTo(25f));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Heal.ManaCost, Is.EqualTo(5f));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Heal.Cooldown, Is.EqualTo(1f));
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Heal.Range, Is.EqualTo(250f / 50f));
        }

        [Test]
        public void ManaPoolRegeneratesClampsAndRefusesUnaffordableCasts()
        {
            var pool = new ManaPool();
            pool.Reset(UnitCatalog.Get(UnitKind.Medic).Mana);
            int heals = 0;
            while (pool.TrySpend(UnitCatalog.Get(UnitKind.Medic).Heal.ManaCost)) heals++;
            Assert.That(heals, Is.EqualTo(15), "75 initial mana pays fifteen 5-mana heals.");
            Assert.That(pool.CanSpend(UnitCatalog.Get(UnitKind.Medic).Heal.ManaCost), Is.False);
            pool.Tick(2f);
            Assert.That(pool.Current, Is.EqualTo(3f).Within(.0001f));
            pool.Tick(1000f);
            Assert.That(pool.Current, Is.EqualTo(200f));
            pool.Tick(float.NaN);
            Assert.That(pool.Current, Is.EqualTo(200f));

            var none = new ManaPool();
            none.Reset(UnitCatalog.Get(UnitKind.Archer).Mana);
            Assert.That(none.Enabled, Is.False);
            Assert.That(none.TrySpend(0), Is.False);
        }

        [Test]
        public void RoarUsesAroaSourceValues()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Roar.Enabled, Is.True);
            Assert.That(UnitCatalog.Get(UnitKind.ArmyGeneral).Roar.Enabled, Is.True, "h00J also attaches Aroa.");
            Assert.That(UnitCatalog.Get(UnitKind.Medic).Roar.Enabled, Is.False);
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Roar.Area, Is.EqualTo(700f / 50f), "war3map.w3a Aroa aare=700.");
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Roar.ManaCost, Is.EqualTo(100f));
            Assert.That(UnitCatalog.Get(UnitKind.Roarer).Roar.Duration, Is.EqualTo(45f));
            Assert.That(1 + UnitCatalog.Get(UnitKind.Roarer).Roar.DamageBonus, Is.EqualTo(1.25f));
            var roarer = UnitCatalog.Get(UnitKind.Roarer).Mana;
            Assert.That(roarer.Maximum, Is.EqualTo(300f)); Assert.That(roarer.Regeneration, Is.EqualTo(2f)); Assert.That(roarer.Initial, Is.EqualTo(75f));
            var general = UnitCatalog.Get(UnitKind.ArmyGeneral).Mana;
            Assert.That(general.Maximum, Is.EqualTo(300f)); Assert.That(general.Regeneration, Is.EqualTo(3f)); Assert.That(general.Initial, Is.Zero);
        }
    }
}
