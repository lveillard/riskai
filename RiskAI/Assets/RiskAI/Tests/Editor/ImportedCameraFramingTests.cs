using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedCameraFramingTests
    {
        [TestCase(ScenarioMap.Classic)]
        [TestCase(ScenarioMap.Riverlands)]
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void WholeMapViewFitsEverySourceCornerInsideTheUsableHudViewport(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            var go=new GameObject("Camera framing test");
            try
            {
                MapLayout.Configure(scenario);
                foreach(float aspect in new[]{16f/9f,21f/9f,9f/16f})
                foreach(float yaw in new[]{0f,45f,135f,270f})
                {
                    var camera=go.AddComponent<Camera>();
                    camera.fieldOfView=44;camera.aspect=aspect;
                    camera.transform.rotation=RtsCameraRig.DefaultRotation;
                    var rig=go.AddComponent<RtsCameraRig>();rig.Initialize(camera);
                    rig.SetHome(MapLayout.PlayableCenter);
                    Assert.That(Vector3.Dot(rig.FocusPoint-camera.transform.position,camera.transform.forward),Is.EqualTo(RtsCameraRig.DefaultZoom/Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)).Within(.001f));
                    rig.Orbit(new Vector2(yaw*Mathf.Max(1,Mathf.Min(Screen.width,Screen.height))/180f,0));
                    rig.FrameMap();camera.orthographicSize=rig.TargetZoom;rig.SetHome(MapLayout.PlayableCenter);
                    float left=RtsCameraRig.MapFramePaddingPixels,right=Screen.width-RtsCameraRig.MapFramePaddingPixels;
                    float bottom=BattleHud.BottomPixels+RtsCameraRig.MapFramePaddingPixels,top=Screen.height-BattleHud.TopPixels-RtsCameraRig.MapFramePaddingPixels;
                    for(int corner=0;corner<4;corner++)
                    {
                        var min=MapLayout.PlayableMin;var max=MapLayout.PlayableMax;
                        var point=new Vector3((corner&1)==0?min.x:max.x,0,(corner&2)==0?min.y:max.y);
                        var screen=camera.WorldToScreenPoint(point);
                        Assert.That(screen.x,Is.InRange(left,right),$"{scenario}, aspect {aspect}, corner {corner}");
                        Assert.That(screen.y,Is.InRange(bottom,top),$"{scenario}, aspect {aspect}, corner {corner}");
                        Assert.That(screen.z,Is.GreaterThan(0));
                    }
                    Object.DestroyImmediate(rig);Object.DestroyImmediate(camera);
                }
            }
            finally { Object.DestroyImmediate(go);MapLayout.Configure(previous); }
        }
    }
}
