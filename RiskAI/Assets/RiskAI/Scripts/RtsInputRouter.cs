using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RiskAI
{
    /// <summary>Converts EnhancedTouch and Pen input into the same controller actions used by mouse input.</summary>
    public sealed class RtsInputRouter
    {
        const int PenPointerId=-817;
        readonly RtsController controller;
        readonly DirectPointerGesture gesture=new DirectPointerGesture();
        readonly HashSet<int> ownedTouches=new HashSet<int>();
        readonly HashSet<int> ignoredTouches=new HashSet<int>();
        readonly HashSet<int> armedTouches=new HashSet<int>();
        readonly HashSet<int> blockedTouches=new HashSet<int>();
        bool penOwned,penArmed,penBlocked,barrelHeld;
        float suppressMouseUntil;

        public bool OwnsDirectPointer => gesture.Active||penOwned||penArmed||penBlocked||barrelHeld||ownedTouches.Count>0||ignoredTouches.Count>0||armedTouches.Count>0||blockedTouches.Count>0||Time.unscaledTime<suppressMouseUntil;

        public RtsInputRouter(RtsController owner)
        {
            controller=owner;
            EnhancedTouchSupport.Enable();
        }

        public void Dispose()
        {
            ForceReset();
            EnhancedTouchSupport.Disable();
        }

        /// <summary>Cancels gameplay interpretation but quarantines contacts until their actual release.</summary>
        public void Cancel()
        {
            bool live=gesture.Active||penOwned||penArmed||penBlocked||barrelHeld||ownedTouches.Count>0||ignoredTouches.Count>0||armedTouches.Count>0||blockedTouches.Count>0;
            gesture.Cancel();controller.CancelAreaSelection();
            MoveToBlocked(ownedTouches);MoveToBlocked(ignoredTouches);MoveToBlocked(armedTouches);
            ownedTouches.Clear();ignoredTouches.Clear();armedTouches.Clear();
            if(penOwned||penArmed)penBlocked=true;
            penOwned=false;penArmed=false;
            if(live)SuppressSyntheticMouse();
        }

        void ForceReset()
        {
            gesture.Cancel();controller.CancelAreaSelection();ownedTouches.Clear();ignoredTouches.Clear();armedTouches.Clear();blockedTouches.Clear();
            penOwned=false;penArmed=false;penBlocked=false;barrelHeld=false;SuppressSyntheticMouse();
        }

        public void Tick()
        {
            ReconcileQuarantine();
            if(!controller.AcceptsDirectPointerInput) { Cancel();return; }
            gesture.CoordinateScale=UiViewport.Scale;
            TickTouches();TickPen();
            gesture.Advance(Time.unscaledTime);gesture.Drain(Dispatch);
        }

        void ReconcileQuarantine()
        {
            if(Touch.activeTouches.Count==0)
            {
                // A removed touchscreen may never deliver Ended. Clear only touch ownership; pen has a separate lifecycle.
                if(ownedTouches.Count>0||ignoredTouches.Count>0||armedTouches.Count>0)
                {
                    gesture.Cancel();controller.CancelAreaSelection();ownedTouches.Clear();ignoredTouches.Clear();armedTouches.Clear();
                }
                blockedTouches.Clear();
            }
            var pen=Pen.current;
            if(pen==null&&(penOwned||penArmed||penBlocked||barrelHeld)) { ForceReset();return; }
            if(penBlocked&&!pen.tip.isPressed&&!barrelHeld)penBlocked=false;
        }
        void TickTouches()
        {
            foreach(var touch in Touch.activeTouches)
            {
                int id=touch.touchId;Vector2 position=touch.screenPosition;
                bool ended=touch.phase==UnityEngine.InputSystem.TouchPhase.Ended||touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled;
                if(ended)
                {
                    if(ownedTouches.Remove(id))gesture.End(id,Point(position),Time.unscaledTime,touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled);
                    ignoredTouches.Remove(id);armedTouches.Remove(id);blockedTouches.Remove(id);SuppressSyntheticMouse();
                    continue;
                }
                if(blockedTouches.Contains(id)||ignoredTouches.Contains(id)||armedTouches.Contains(id)) { SuppressSyntheticMouse();continue; }
                if(ownedTouches.Contains(id))
                {
                    SuppressSyntheticMouse();if(touch.phase==UnityEngine.InputSystem.TouchPhase.Moved)gesture.Move(id,Point(position));continue;
                }
                if(touch.phase!=UnityEngine.InputSystem.TouchPhase.Began)
                {
                    // A resumed app can observe a still-held contact without its Began event. It remains quarantined.
                    blockedTouches.Add(id);SuppressSyntheticMouse();continue;
                }
                if(penOwned||penArmed||penBlocked||barrelHeld)
                {
                    blockedTouches.Add(id);SuppressSyntheticMouse();continue;
                }
                if(controller.BlocksWorldInput(position))
                {
                    if(gesture.Active||ownedTouches.Count>0)Cancel();
                    ignoredTouches.Add(id);SuppressSyntheticMouse();continue;
                }
                controller.PrepareDirectPointerInput();
                if(controller.OrderCursor)
                {
                    controller.ExecuteArmedPointer(position);armedTouches.Add(id);SuppressSyntheticMouse();continue;
                }
                ownedTouches.Add(id);gesture.Begin(id,Point(position),Time.unscaledTime);SuppressSyntheticMouse();
            }
        }

        void TickPen()
        {
            var pen=Pen.current;
            if(pen==null)
            {
                if(penOwned||penArmed||penBlocked||barrelHeld)ForceReset();
                return;
            }
            Vector2 position=pen.position.ReadValue();bool tipDown=pen.tip.isPressed;bool barrelDown=pen.firstBarrelButton.isPressed;
            if(barrelDown)SuppressSyntheticMouse();
            if(barrelDown&&!barrelHeld)
            {
                barrelHeld=true;Cancel();penBlocked=tipDown;SuppressSyntheticMouse();
                // A held button first observed after pause/focus loss is not a new command.
                if(pen.firstBarrelButton.wasPressedThisFrame&&!controller.BlocksWorldInput(position))controller.ContextAction(position);
                return;
            }
            if(barrelHeld)
            {
                SuppressSyntheticMouse();
                if(!barrelDown){barrelHeld=false;penBlocked=tipDown;}
                return;
            }
            if(penBlocked)
            {
                SuppressSyntheticMouse();if(!tipDown)penBlocked=false;return;
            }
            if(penArmed)
            {
                SuppressSyntheticMouse();if(!tipDown)penArmed=false;return;
            }
            if(!penOwned&&tipDown)
            {
                if(!pen.tip.wasPressedThisFrame||controller.BlocksWorldInput(position)||ownedTouches.Count>0||ignoredTouches.Count>0||blockedTouches.Count>0||armedTouches.Count>0)
                {
                    penBlocked=true;SuppressSyntheticMouse();return;
                }
                controller.PrepareDirectPointerInput();
                if(controller.OrderCursor)
                {
                    controller.ExecuteArmedPointer(position);penArmed=true;SuppressSyntheticMouse();return;
                }
                penOwned=true;gesture.Begin(PenPointerId,Point(position),Time.unscaledTime);SuppressSyntheticMouse();
            }
            if(!penOwned)return;
            SuppressSyntheticMouse();
            if(tipDown)gesture.Move(PenPointerId,Point(position));
            else
            {
                gesture.End(PenPointerId,Point(position),Time.unscaledTime);penOwned=false;
            }
        }
        void MoveToBlocked(HashSet<int> source) { foreach(var id in source)blockedTouches.Add(id); }
        void SuppressSyntheticMouse() => suppressMouseUntil=Mathf.Max(suppressMouseUntil,Time.unscaledTime+.5f);
        static PointerPoint Point(Vector2 point) => new PointerPoint(point.x,point.y);
        static Vector2 Vector(PointerPoint point) => new Vector2(point.X,point.Y);

        void Dispatch(DirectPointerAction action)
        {
            switch(action.Kind)
            {
                case DirectPointerActionKind.PrimaryTap: controller.PrimaryTap(Vector(action.Position),controller.ShiftHeld,false);break;
                case DirectPointerActionKind.AreaBegin: controller.BeginAreaSelection(Vector(action.Position));break;
                case DirectPointerActionKind.AreaUpdate: controller.UpdateAreaSelection(Vector(action.Position));break;
                case DirectPointerActionKind.AreaEnd: controller.EndAreaSelection(Vector(action.Position),controller.ShiftHeld);break;
                case DirectPointerActionKind.AreaCancel: controller.CancelAreaSelection();break;
                case DirectPointerActionKind.Context: controller.ContextAction(Vector(action.Position));break;
                case DirectPointerActionKind.Pan: controller.CameraRig.Drag(Vector(action.Previous),Vector(action.Position));break;
                case DirectPointerActionKind.Orbit: controller.CameraRig.Orbit(Vector(action.Position)-Vector(action.Previous));break;
                case DirectPointerActionKind.Pinch: controller.CameraRig.ZoomByRatio(action.Scale,Vector(action.Position));break;
            }
        }
    }
}
