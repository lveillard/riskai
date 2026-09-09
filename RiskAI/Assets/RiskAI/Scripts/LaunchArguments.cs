using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    /// <summary>One launch contract for desktop CLI and explicit browser diagnostics URLs.</summary>
    public static class LaunchArguments
    {
        static string[] cached;
        public static string[] Get()
        {
            if(cached!=null)return cached;
#if UNITY_WEBGL && !UNITY_EDITOR
            return cached=FromUrl(Application.absoluteURL);
#else
            return cached=Environment.GetCommandLineArgs();
#endif
        }
        public static string[] FromUrl(string url)
        {
            var args=new List<string>();
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||string.IsNullOrEmpty(uri.Query))return args.ToArray();
            foreach(string pair in uri.Query.TrimStart('?').Split('&'))
            {
                var parts=pair.Split(new[]{'='},2);
                string key=Uri.UnescapeDataString(parts[0]);
                string value=parts.Length>1?Uri.UnescapeDataString(parts[1]):"1";
                switch(key)
                {
                    case "riskai-map":case "riskai-seed":case "riskai-players":case "riskai-ui-capture":
                    case "riskai-probe-seconds":case "riskai-probe-warmup":case "riskai-probe-recruits":case "riskai-restart-cycles":case "riskai-path-budget":
                        args.Add("--"+key);args.Add(value);break;
                    case "riskai-probe":case "riskai-probe-sustained":case "riskai-probe-warmup-commander":case "riskai-frame-trace":case "riskai-restart-probe":case "riskai-play":
                        if(value=="1"||value=="true")args.Add("--"+key);break;
                }
            }
            return args.ToArray();
        }
    }
}
