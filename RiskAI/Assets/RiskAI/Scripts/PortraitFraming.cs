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

        /// <summary>
        /// v0.33 sizes from units.json presentation.portraitFraming.
        /// A portrait name wins over a unit id, so "Knight" is the footman's portrait (standard 1.4)
        /// and "MountedKnight" is the mounted mesh (1.85). An unknown name throws.
        /// </summary>
        public static Choice For(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new System.InvalidOperationException("Portrait framing needs a name from units.json.");
            if (!TryResolve(name, out var framing))
                throw new System.InvalidOperationException("Unknown portrait \"" + name + "\". Name a unit id, portrait or model from units.json.");
            return From(framing);
        }

        static bool TryResolve(string name, out Core.PortraitFraming framing)
        {
            framing = default;
            if (TryMatch(name, portrait: true, out framing)) return true;
            if (TryMatch(name, portrait: false, out framing)) return true;
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                ref readonly var type = ref UnitCatalog.Get(kind);
                if (type.Model != name) continue;
                // RoyalGuard.png is the base mesh, not the mounted portrait. v0.33 framed that
                // file with the land camera. The mounted camera belongs to the portrait name.
                framing = type.Portrait != name && type.Id != name
                    ? Core.PortraitFraming.Standard
                    : type.PortraitFraming;
                return true;
            }
            return false;
        }

        static bool TryMatch(string name, bool portrait, out Core.PortraitFraming framing)
        {
            framing = default;
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                ref readonly var type = ref UnitCatalog.Get(kind);
                bool hit = portrait ? type.Portrait == name : type.Id == name;
                if (!hit) continue;
                framing = type.PortraitFraming;
                return true;
            }
            return false;
        }

        static Choice From(Core.PortraitFraming framing)
        {
            switch (framing)
            {
                case Core.PortraitFraming.Mortar:
                    return Land(1.35f, 1.15f, false, .8f, new Vector3(4.5f, 1.6f, 3f));
                case Core.PortraitFraming.Siege:
                    return Land(1.7f, 1.15f, false, .8f, new Vector3(4.5f, 1.6f, 3f));
                case Core.PortraitFraming.Mounted:
                    return Land(1.85f, 1.75f, true, .8f, new Vector3(4.5f, 1.6f, 3f));
                case Core.PortraitFraming.General:
                    return Land(2.1f, 1.95f, true, .8f, new Vector3(4.5f, 1.6f, 3f));
                case Core.PortraitFraming.Command:
                    return Land(1.4f, 1.75f, true, .8f, new Vector3(4.5f, 1.6f, 3f));
                case Core.PortraitFraming.Roarer:
                    return Land(1.4f, 1.75f, true, .78f, new Vector3(2f, 1f, 5f));
                case Core.PortraitFraming.Frigate:
                    return Ship(3.35f);
                case Core.PortraitFraming.Warship:
                    return Ship(3.8f);
                case Core.PortraitFraming.Battleship:
                    return Ship(4.2f);
                case Core.PortraitFraming.Standard:
                    return Land(1.4f, 1.75f, false, .8f, new Vector3(4.5f, 1.6f, 3f));
                default:
                    throw new System.InvalidOperationException("Unknown portrait framing " + framing + ".");
            }
        }

        static Choice Land(float size, float focus, bool refit, float upper, Vector3 refitOffset) =>
            new Choice(false, size, focus, new Vector3(2f, 1f, 5f), refit, upper, refitOffset);

        static Choice Ship(float size) =>
            new Choice(true, size, 2.15f, new Vector3(4.8f, 3.1f, 6.8f), false, .8f, new Vector3(4.5f, 1.6f, 3f));

        public static bool FramesRenderer(Renderer renderer) =>
            renderer && renderer.enabled && !(renderer is LineRenderer) && renderer.name != "Soft ground shadow";
    }
}
