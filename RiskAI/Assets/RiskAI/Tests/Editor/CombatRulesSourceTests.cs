using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class CombatRulesSourceTests
    {
        // Byte-for-byte extracted evidence, not a second authored matrix:
        // saran/world war3mapMisc.txt SHA256 ACFC8258A69E588C925AC4605E176D48F42DF45C55C03CFD49650686285592E1.
        // Owned RoC WAR3.MPQ Units\MiscData.txt SHA256 5582ED5CDA90A86FA5023A4144A36266F532877A8837AB8D8EDB7CBC770E8006.
        static Dictionary<string,string> ReadMisc(string filename)
        {
            var result=new Dictionary<string,string>();
            string path=Path.Combine(Application.dataPath,"RiskAI/Tests/Editor/Fixtures",filename);
            Assert.That(File.Exists(path),Is.True,"Missing extracted source fixture: "+path);
            bool misc=false;
            foreach(string sourceLine in File.ReadAllLines(path))
            {
                string line=sourceLine.Split(new[]{"//"},StringSplitOptions.None)[0].Trim();
                if(line.StartsWith("[")){misc=line=="[Misc]";continue;}
                int equals=line.IndexOf('=');
                if(misc&&equals>0)result[line.Substring(0,equals).Trim()]=line.Substring(equals+1).Trim();
            }
            return result;
        }
        static float Number(string value)=>float.Parse(value,CultureInfo.InvariantCulture);
        static readonly ArmorKind[] SourceColumns={ArmorKind.Light,ArmorKind.Medium,ArmorKind.Heavy,ArmorKind.Fortified,ArmorKind.Normal,ArmorKind.Hero,ArmorKind.Divine,ArmorKind.Unarmored};

        [TestCase(AttackKind.Normal,"Normal")]
        [TestCase(AttackKind.Piercing,"Pierce")]
        [TestCase(AttackKind.Siege,"Siege")]
        [TestCase(AttackKind.Magic,"Magic")]
        [TestCase(AttackKind.Chaos,"Chaos")]
        [TestCase(AttackKind.Hero,"Hero")]
        [TestCase(AttackKind.Spells,"Spells")]
        public void EveryAttackRowMatchesAllEightExtractedMapColumns(AttackKind attack,string sourceName)
        {
            var source=ReadMisc("SaranWar3MapMisc.txt");
            string[] row=source["DamageBonus"+sourceName].Split(',');
            Assert.That(row.Length,Is.EqualTo(SourceColumns.Length));
            for(int column=0;column<row.Length;column++)
            {
                float expected=Number(row[column]);var armor=SourceColumns[column];
                Assert.That(CombatRules.DamageMultiplier(attack,armor),Is.EqualTo(expected),sourceName+" vs source column "+column);
                Assert.That(CombatRules.ResolveDamage(100,attack,armor,0),Is.EqualTo(100*expected).Within(.0001f));
            }
        }

        [Test]
        public void AppendedCategoriesRetainExistingEnumIdentities()
        {
            Assert.That((int)AttackKind.Normal,Is.EqualTo(0));Assert.That((int)AttackKind.Piercing,Is.EqualTo(1));
            Assert.That((int)AttackKind.Siege,Is.EqualTo(2));Assert.That((int)AttackKind.Magic,Is.EqualTo(3));
            Assert.That((int)ArmorKind.Unarmored,Is.EqualTo(0));Assert.That((int)ArmorKind.Light,Is.EqualTo(1));
            Assert.That((int)ArmorKind.Medium,Is.EqualTo(2));Assert.That((int)ArmorKind.Heavy,Is.EqualTo(3));Assert.That((int)ArmorKind.Fortified,Is.EqualTo(4));
            Assert.That(Enum.GetValues(typeof(AttackKind)).Length,Is.EqualTo(7));
            Assert.That(Enum.GetValues(typeof(ArmorKind)).Length,Is.EqualTo(8));
        }

        [Test]
        public void NumericArmorCoefficientMatchesOwnedRocBaselineAndComposesWithMapMatrix()
        {
            float coefficient=Number(ReadMisc("RocUnitsMiscData.txt")["DefenseArmor"]);
            Assert.That(CombatRules.ArmorCoefficient,Is.EqualTo(coefficient));
            foreach(float armor in new[]{0f,.5f,1f,7f,30f})
                Assert.That(CombatRules.ArmorMultiplier(armor)*(1+coefficient*armor),Is.EqualTo(1).Within(.00001f));
            float divine=Number(ReadMisc("SaranWar3MapMisc.txt")["DamageBonusChaos"].Split(',')[6]);
            Assert.That(CombatRules.ResolveDamage(100,AttackKind.Chaos,ArmorKind.Divine,30),Is.EqualTo(100*divine/(1+coefficient*30)).Within(.00001f));
        }

        [Test]
        public void ExistingNegativeArmorCurveRetainsItsGeometricSeriesAndFiniteLimit()
        {
            // Regression of the existing engine adapter, not independent proof
            // of a Reforged executable's negative-armor algorithm.
            float coefficient=Number(ReadMisc("RocUnitsMiscData.txt")["DefenseArmor"]);
            float expected=1,increment=coefficient;
            for(int points=1;points<=30;points++)
            {
                expected+=increment;increment*=1-coefficient;
                Assert.That(CombatRules.ArmorMultiplier(-points),Is.EqualTo(expected).Within(.00001f));
            }
            Assert.That(CombatRules.ArmorMultiplier(-10000),Is.EqualTo(2).Within(.00001f));
        }
    }
}
