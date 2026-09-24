using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class LinkXmlContractTests
    {
        [Test]
        public void LinkXmlListsEveryTypeReachableFromTheUnitContract()
        {
            string xml = File.ReadAllText(Path.Combine(Application.dataPath, "RiskAI/link.xml"));
            var seen = new HashSet<string>();
            void Walk(Type type)
            {
                if (type == null) return;
                if (type.IsArray) { Walk(type.GetElementType()); return; }
                if (type.IsGenericType) { foreach (var argument in type.GetGenericArguments()) Walk(argument); return; }
                if (type.Namespace != "RiskAI.Core") return;
                if (!seen.Add(type.FullName)) return;
                Assert.That(xml, Does.Contain("fullname=\"" + type.FullName + "\""), type.FullName);
                if (type.IsEnum) return;
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Walk(field.FieldType);
            }
            Walk(typeof(UnitsFile));
            Walk(typeof(UnitConfigFieldAttribute));
            Assert.That(seen, Does.Contain("RiskAI.Core.WeaponSound"));
            Assert.That(seen, Does.Contain("RiskAI.Core.PortraitSource"));
        }
    }
}
