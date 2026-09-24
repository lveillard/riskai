using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Camera numbers for a portrait render. A name is a ship only when that unit's domain is sea.
    /// UnitKind now includes land ids, so "is this name a UnitKind" is not a ship test.
    /// </summary>
    public static class PortraitFraming
    {
        public readonly struct Choice
        {
            public readonly bool Ship;
            public readonly float OrthographicSize;
            public readonly float FocusHeight;
            public readonly Vector3 Offset;
            public readonly bool Refit;
            public readonly float UpperFraction;
            public readonly Vector3 RefitOffset;

            public Choice(bool ship, float orthographicSize, float focusHeight, Vector3 offset, bool refit, float upperFraction, Vector3 refitOffset)
            {
                Ship = ship; OrthographicSize = orthographicSize; FocusHeight = focusHeight; Offset = offset;
                Refit = refit; UpperFraction = upperFraction; RefitOffset = refitOffset;
            }
        }

        public static bool IsShipPortrait(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                if (kind.ToString() != name) continue;
                return UnitCatalog.Get(kind).Domain == UnitDomain.Sea;
            }
            return false;
        }

        /// <summary>v0.33 sizes: land 1.4, mortar 1.35, artillery/tank 1.7, frigate 3.35, warship 3.8, battleship 4.2.</summary>
        public static Choice For(string name)
        {
            bool ship = IsShipPortrait(name);
            bool siege = name == "Mortar" || name == "Artillery" || name == "Tank";
            float size = ship ? (name == "Battleship" ? 4.2f : name == "Warship" ? 3.8f : 3.35f)
                : siege ? (name == "Mortar" ? 1.35f : 1.7f)
                : name == "MountedKnight" ? 1.85f : name == "ArmyGeneral" ? 2.1f : 1.4f;
            float focus = ship ? 2.15f : siege ? 1.15f : name == "ArmyGeneral" ? 1.95f : 1.75f;
            bool refit = !ship && (name == "MountedKnight" || name == "ArmyGeneral" || name == "MarineMajor" || name == "MarineGeneral" || name == "Roarer");
            var offset = ship ? new Vector3(4.8f, 3.1f, 6.8f) : new Vector3(2f, 1f, 5f);
            var refitOffset = name == "Roarer" ? new Vector3(2f, 1f, 5f) : new Vector3(4.5f, 1.6f, 3f);
            return new Choice(ship, size, focus, offset, refit, name == "Roarer" ? .78f : .8f, refitOffset);
        }

        public static bool FramesRenderer(Renderer renderer) =>
            renderer && renderer.enabled && !(renderer is LineRenderer) && renderer.name != "Soft ground shadow";
    }
}
