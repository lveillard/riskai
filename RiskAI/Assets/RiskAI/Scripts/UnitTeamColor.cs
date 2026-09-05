using System;
using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Applies team colours to the cloth portions of an original KayKit model.</summary>
    public static class UnitTeamColor
    {
        readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            readonly int source;
            readonly int kind;
            readonly int team;
            readonly bool forceTeam;
            public MaterialKey(int sourceId, UnitKind unitKind, int owner, bool forced)
            { source=sourceId;kind=(int)unitKind;team=owner;forceTeam=forced; }
            public bool Equals(MaterialKey other) => source==other.source&&kind==other.kind&&team==other.team&&forceTeam==other.forceTeam;
            public override bool Equals(object obj) => obj is MaterialKey&&Equals((MaterialKey)obj);
            public override int GetHashCode() { unchecked { return (((source*397+kind)*397+team)*397)+(forceTeam?1:0); } }
        }

        static readonly Dictionary<MaterialKey,Material> Materials = new Dictionary<MaterialKey,Material>();
        static Shader teamShader;
        static bool shaderChecked;

        public static void Apply(GameObject model, UnitKind kind, int team)
        {
            if (!model) return;
            var shader=FindTeamShader();
            if (!shader || !shader.isSupported)
            {
                AddFallbackTabard(model,team);
                return;
            }

            Color teamColor=VisualFactory.TeamColor(team);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var source=renderer.sharedMaterial;
                if (!source) continue;
                bool forceTeam=renderer.name.IndexOf("Cape",StringComparison.OrdinalIgnoreCase)>=0||renderer.name.IndexOf("Shield",StringComparison.OrdinalIgnoreCase)>=0;
                var key=new MaterialKey(source.GetInstanceID(),kind,team,forceTeam);
                if (!Materials.TryGetValue(key,out var material)||!material)
                {
                    material=new Material(source) { name=source.name+" · team "+team };
                    material.shader=shader;
                    material.SetColor("_TeamColor",teamColor);
                    material.SetFloat("_SourceHue",SourceHue(kind));
                    material.SetFloat("_HueWidth",HueWidth(kind));
                    material.SetFloat("_ForceTeam",forceTeam?1:0);
                    material.SetFloat("_MedicUniform",kind==UnitKind.Medic?1:0);
                    Materials[key]=material;
                }
                renderer.sharedMaterial=material;
            }
        }

        static Shader FindTeamShader()
        {
            if (!shaderChecked) { shaderChecked=true;teamShader=Shader.Find("RiskAI/UnitTeam"); }
            return teamShader;
        }

        static float SourceHue(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Archer:return .42f; // green hood and tunic
                case UnitKind.Medic:
                case UnitKind.Mage:return .69f; // violet cloth
                default:return .98f; // knight's red cloth accents
            }
        }

        static float HueWidth(UnitKind kind) => (kind==UnitKind.Mage||kind==UnitKind.Medic)?.16f:kind==UnitKind.Archer?.14f:.085f;

        static void AddFallbackTabard(GameObject model,int team)
        {
            var tabard=VisualFactory.Shape(model.transform,PrimitiveType.Cube,"Team tabard",
                new Vector3(0,1.04f,.36f),new Vector3(.42f,.5f,.035f),VisualFactory.TeamColor(team));
            tabard.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
