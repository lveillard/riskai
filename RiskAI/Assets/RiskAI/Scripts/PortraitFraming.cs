using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Camera numbers for a portrait render, read from units.json.
    /// A shared mesh such as RoyalGuard.png keeps the land camera of the Knight portrait:
    /// that file is the base mesh, and the mounted camera belongs to the portrait name.
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
        /// A portrait name wins over a unit id, so "Knight" is the footman's camera
        /// and "MountedKnight" is the mounted mesh. An unknown name throws.
        /// </summary>
        public static Choice For(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new System.InvalidOperationException("Portrait framing needs a name from units.json.");
            if (!TryResolve(name, out var view))
                throw new System.InvalidOperationException("Unknown portrait \"" + name + "\". Name a unit id, portrait or model from units.json.");
            return From(view);
        }

        static bool TryResolve(string name, out PortraitView view)
        {
            view = default;
            if (TryMatch(name, portrait: true, out view)) return true;
            if (TryMatch(name, portrait: false, out view)) return true;
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                ref readonly var type = ref UnitCatalog.Get(kind);
                if (type.Model != name) continue;
                // RoyalGuard.png is the base mesh, not the mounted portrait.
                view = type.PortraitName != name && type.Id != name ? LandDefault() : type.Portrait;
                return view.Exists;
            }
            return false;
        }

        static bool TryMatch(string name, bool portrait, out PortraitView view)
        {
            view = default;
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                ref readonly var type = ref UnitCatalog.Get(kind);
                bool hit = portrait ? type.PortraitName == name : type.Id == name;
                if (!hit) continue;
                view = type.Portrait;
                return view.Exists;
            }
            return false;
        }

        /// <summary>The land camera marked <c>landDefault</c> in units.json. A shared mesh uses it.</summary>
        static PortraitView LandDefault()
        {
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                ref readonly var type = ref UnitCatalog.Get(kind);
                if (!type.PortraitLandDefault) continue;
                if (!type.Portrait.Exists)
                    throw new System.InvalidOperationException("units.json marks " + type.Id + " landDefault but that unit has no portrait camera.");
                UnitVariantViews.RequirePortrait(kind);
                return type.Portrait;
            }
            throw new System.InvalidOperationException("units.json has no landDefault portrait camera.");
        }

        static Choice From(PortraitView view) =>
            new Choice(view.Ship, view.OrthographicSize, view.FocusHeight,
                new Vector3(view.OffsetX, view.OffsetY, view.OffsetZ), view.Refit, view.UpperFraction,
                new Vector3(view.RefitX, view.RefitY, view.RefitZ));

        public static bool FramesRenderer(Renderer renderer) =>
            renderer && renderer.enabled && !(renderer is LineRenderer) && renderer.name != "Soft ground shadow";
    }
}
