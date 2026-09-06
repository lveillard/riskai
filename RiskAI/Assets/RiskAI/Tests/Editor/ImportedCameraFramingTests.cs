using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class ImportedCameraFramingTests
    {
        [TestCase(ScenarioMap.Europe)]
        [TestCase(ScenarioMap.NewWorld)]
        public void WholeMapViewFitsEverySourceCornerAboveTheHud(ScenarioMap scenario)
        {
            var previous=MapLayout.Scenario;
            var go=new GameObject("Camera framing test");
            try
            {
                MapLayout.Configure(scenario);
                var camera=go.AddComponent<Camera>();
                camera.fieldOfView=44;
                camera.aspect=Screen.width/(float)Screen.height;
                camera.transform.rotation=RtsCameraRig.DefaultRotation;
                var rig=go.AddComponent<RtsCameraRig>();rig.Initialize(camera);
                rig.SetHome(MapLayout.PlayableCenter);
                Assert.That(Vector3.Dot(rig.FocusPoint-camera.transform.position,camera.transform.forward),Is.EqualTo(80).Within(.001f));
                Assert.That(camera.transform.eulerAngles.x,Is.EqualTo(70).Within(.001f));
                Assert.That(camera.transform.eulerAngles.y,Is.EqualTo(0).Within(.001f));
                rig.FrameMap();
                camera.orthographicSize=rig.TargetZoom;
                rig.SetHome(MapLayout.PlayableCenter);
                for(int corner=0;corner<4;corner++)
                {
                    var min=MapLayout.PlayableMin;var max=MapLayout.PlayableMax;
                    var point=new Vector3((corner&1)==0?min.x:max.x,0,(corner&2)==0?min.y:max.y);
                    var screen=camera.WorldToScreenPoint(point);
                    Assert.That(screen.x,Is.InRange(0f,(float)Screen.width));
                    Assert.That(screen.y,Is.InRange(BattleHud.BottomPixels,Screen.height-BattleHud.TopPixels));
                    Assert.That(screen.z,Is.GreaterThan(0));
                }
            }
            finally { Object.DestroyImmediate(go);MapLayout.Configure(previous); }
        }
    }
}
