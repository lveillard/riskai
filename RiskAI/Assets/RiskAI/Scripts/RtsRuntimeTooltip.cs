using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>
    /// Runtime tooltip overlay for UIToolkit players. VisualElement.tooltip is
    /// editor-only, so this listens at the panel root and draws one ignore-pick
    /// label above the rebuilt HUD content.
    /// </summary>
    public sealed class RtsRuntimeTooltip : IDisposable
    {
        const long DelayMilliseconds = 350;
        const long HoldMilliseconds = 500;
        const float HoldSlop = 12f;
        const float Offset = 14f;
        const float Edge = 8f;
        const float MaximumWidth = 320f;

        readonly VisualElement root;
        readonly Label label;
        IVisualElementScheduledItem pendingShow;
        VisualElement pendingTarget;
        string pendingText;
        Vector2 pointerPosition;
        bool disposed;
        int holdPointer = -1;
        int suppressedPointer = -1;
        Vector2 holdPosition;
        PointerCancelEvent cancelPress;
        ButtonControl holdPress;
        TouchControl holdTouch;
        VisualElement captureTarget;
        bool dispatchingCancel;

        public RtsRuntimeTooltip(VisualElement rootVisualElement)
        {
            root = rootVisualElement ?? throw new ArgumentNullException(nameof(rootVisualElement));
            label = RtsUiStyle.Label(string.Empty, "Runtime tooltip", 12);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.display = DisplayStyle.None;
            label.style.maxWidth = MaximumWidth;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.backgroundColor = RtsUiStyle.PanelColor;
            label.style.color = RtsUiStyle.Text;
            label.style.borderTopColor = RtsUiStyle.Bronze;
            label.style.borderBottomColor = RtsUiStyle.Bronze;
            label.style.borderLeftColor = RtsUiStyle.Bronze;
            label.style.borderRightColor = RtsUiStyle.Bronze;
            label.style.borderTopWidth = 1;
            label.style.borderBottomWidth = 1;
            label.style.borderLeftWidth = 1;
            label.style.borderRightWidth = 1;
            label.style.paddingLeft = 9;
            label.style.paddingRight = 9;
            label.style.paddingTop = 6;
            label.style.paddingBottom = 6;
            root.Add(label);

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOverEvent>(OnPointerOver);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnPointerCancel,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureEvent>(OnCapture,TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnClick,TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            label.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            Application.focusChanged += OnFocusChanged;
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
        }

        /// <summary>Call before a HUD SetContent rebuild clears its tooltip-bearing descendants.</summary>
        public void SetContentChanged() => CancelHold();

        void OnPointerMove(PointerMoveEvent evt)
        {
            if(evt.pointerId == holdPointer)
            {
                if(evt.pressedButtons != 1 || ((Vector2)evt.position-holdPosition).sqrMagnitude > HoldSlop*HoldSlop)
                    EndHold(); // Leave native drag/scroll handling intact.
                return;
            }
            if(!SupportsHover(evt.pointerType) || evt.pressedButtons != 0 || holdPointer >= 0) return;
            SetPending(evt.target as VisualElement, evt.position);
        }

        void OnPointerOver(PointerOverEvent evt)
        {
            if(!SupportsHover(evt.pointerType) || evt.pressedButtons != 0 || holdPointer >= 0) return;
            SetPending(evt.target as VisualElement, evt.position);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            bool anotherHold = holdPointer >= 0;
            CancelHold();
            if(evt.pointerId == suppressedPointer) suppressedPointer = -1;
            if(anotherHold || evt.button != 0 || evt.pressedButtons != 1 ||
                (evt.pointerType != UnityEngine.UIElements.PointerType.touch && evt.pointerType != UnityEngine.UIElements.PointerType.pen)) return;
            var target = evt.target as VisualElement;
            if(string.IsNullOrWhiteSpace(ResolveText(target)) || ContactCount() > 1) return;
            holdPointer = evt.pointerId;
            holdPosition = evt.position;
            FindHeldControl(evt.pointerType);
            cancelPress = PointerCancelEvent.GetPooled(evt);
            if(HasBarrel(holdPress?.device as Pen))
            {
                // Default UI bindings do not expose barrel buttons. Reject a
                // modified tip press before it arms the target's Clickable.
                evt.StopImmediatePropagation();
                CancelHold();
                return;
            }
            SetPending(target, evt.position, HoldMilliseconds);
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if(evt.pointerId == suppressedPointer) evt.StopImmediatePropagation();
            if(evt.pointerId == holdPointer) EndHold();
        }

        void OnClick(ClickEvent evt)
        {
            if(evt.pointerId == suppressedPointer) evt.StopImmediatePropagation();
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            if(!dispatchingCancel && evt.pointerId == holdPointer) EndHold();
        }

        void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if(!dispatchingCancel && evt.pointerId == holdPointer) EndHold();
        }

        void OnCapture(PointerCaptureEvent evt)
        {
            if(evt.pointerId != holdPointer || dispatchingCancel) return;
            UnwatchCapture();
            captureTarget = evt.target as VisualElement;
            if(captureTarget == null || captureTarget == root) { captureTarget = null; return; }
            // Captured pointer events skip ancestors in runtime UI Toolkit.
            // Observe the owner's target phase without changing native capture.
            captureTarget.RegisterCallback<PointerMoveEvent>(OnPointerMove,TrickleDown.TrickleDown);
            captureTarget.RegisterCallback<PointerUpEvent>(OnPointerUp,TrickleDown.TrickleDown);
            captureTarget.RegisterCallback<PointerCancelEvent>(OnPointerCancel,TrickleDown.TrickleDown);
        }

        void UnwatchCapture()
        {
            if(captureTarget == null) return;
            captureTarget.UnregisterCallback<PointerMoveEvent>(OnPointerMove,TrickleDown.TrickleDown);
            captureTarget.UnregisterCallback<PointerUpEvent>(OnPointerUp,TrickleDown.TrickleDown);
            captureTarget.UnregisterCallback<PointerCancelEvent>(OnPointerCancel,TrickleDown.TrickleDown);
            captureTarget = null;
        }

        void OnWheel(WheelEvent evt) => CancelHold();
        void OnGeometry(GeometryChangedEvent evt) => Place();
        void OnTargetDetached(DetachFromPanelEvent evt) => CancelHold();
        void OnPointerLeave(PointerLeaveEvent evt) { if(!dispatchingCancel) EndHold(); }
        void OnDetach(DetachFromPanelEvent evt) => CancelHold();
        void OnFocusChanged(bool focused) { if(!focused) CancelHold(); }

        void OnAfterInputUpdate()
        {
            if(holdPointer < 0) return;
            // A second finger may land outside this panel. InputSystem also maps
            // canceled contacts to pointer-up, so observe cancellation before UI dispatch.
            if(ContactCount() > 1 || (holdPress != null && !holdPress.device.added) ||
                (holdTouch != null && holdTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled) ||
                HasBarrel(holdPress?.device as Pen))
                CancelHold();
        }

        static bool HasBarrel(Pen pen) => pen != null &&
            (pen.firstBarrelButton.isPressed || pen.secondBarrelButton.isPressed ||
             pen.thirdBarrelButton.isPressed || pen.fourthBarrelButton.isPressed);

        static int ContactCount()
        {
            int count = 0;
            foreach(var device in InputSystem.devices)
            {
                if(device is Touchscreen screen)
                    foreach(var touch in screen.touches) { if(touch.press.isPressed) count++; }
                else if(device is Pen pen && pen.tip.isPressed) count++;
            }
            return count;
        }

        void FindHeldControl(string pointerType)
        {
            foreach(var device in InputSystem.devices)
            {
                if(pointerType == UnityEngine.UIElements.PointerType.touch && device is Touchscreen screen)
                    foreach(var touch in screen.touches)
                        if(touch.press.isPressed) { holdTouch = touch; holdPress = touch.press; return; }
                if(pointerType == UnityEngine.UIElements.PointerType.pen && device is Pen pen && pen.tip.isPressed)
                { holdPress = pen.tip; return; }
            }
        }

        void CancelNativePress()
        {
            if(cancelPress == null) return;
            var cancellation = cancelPress;
            cancelPress = null;
            suppressedPointer = holdPointer;
            dispatchingCancel = true;
            try
            {
                // Clickable invokes on pointer-up, whereas ClickDetector independently
                // synthesizes ClickEvent in PostDispatch. A real cancel resets both,
                // including when this helper is disposed before the physical release.
                if(root.panel != null)
                {
                    var capture = root.panel.GetCapturingElement(holdPointer) as VisualElement;
                    cancellation.target = capture ?? (pendingTarget?.panel == root.panel ? pendingTarget : root);
                    ((VisualElement)cancellation.target).SendEvent(cancellation);
                }
            }
            finally { dispatchingCancel = false; cancellation.Dispose(); }
        }

        void CancelHold()
        {
            if(dispatchingCancel) return;
            CancelNativePress();
            EndHold();
        }

        void EndHold()
        {
            UnwatchCapture();
            holdPointer = -1;
            holdPress = null;
            holdTouch = null;
            cancelPress?.Dispose();
            cancelPress = null;
            Hide();
        }

        void SetPending(VisualElement target, Vector2 position, long delay = DelayMilliseconds)
        {
            pointerPosition=position;
            if(target==pendingTarget&&pendingText!=null)
            {
                if(label.resolvedStyle.display==DisplayStyle.Flex)Place();
                return;
            }
            string text = ResolveText(target);
            if(string.IsNullOrWhiteSpace(text)) { Hide(); return; }
            if(label.resolvedStyle.display == DisplayStyle.Flex && text == label.text)
            {
                Place();
                return;
            }
            if(target == pendingTarget && text == pendingText) return;
            PausePending();
            label.style.display = DisplayStyle.None;
            if(pendingTarget!=null)pendingTarget.UnregisterCallback<DetachFromPanelEvent>(OnTargetDetached);
            pendingTarget = target;
            pendingTarget.RegisterCallback<DetachFromPanelEvent>(OnTargetDetached);
            pendingText = text;
            pendingShow = root.schedule.Execute(ShowPending).StartingIn(delay);
        }

        void ShowPending()
        {
            pendingShow = null;
            if(disposed || pendingTarget == null || pendingTarget.panel!=root.panel || !root.Contains(pendingTarget) || string.IsNullOrWhiteSpace(pendingText)) return;
            if(holdPointer >= 0)
            {
                if(holdPress != null && !holdPress.isPressed) { EndHold(); return; }
                CancelNativePress();
            }
            label.style.maxWidth=Mathf.Max(1,Mathf.Min(MaximumWidth,root.worldBound.width-Edge*2));
            label.text = GameText.Localize(pendingText);
            label.BringToFront();
            label.style.display = DisplayStyle.Flex;
            Place();
        }

        void Place()
        {
            if(disposed || label.resolvedStyle.display != DisplayStyle.Flex) return;
            Rect panel = root.worldBound;
            float width = Mathf.Min(MaximumWidth, Mathf.Max(1f, label.worldBound.width));
            float height = Mathf.Max(1f, label.worldBound.height);
            Vector2 local = pointerPosition - panel.position;
            float x = Mathf.Clamp(local.x + Offset, Edge, Mathf.Max(Edge, panel.width - width - Edge));
            float y = Mathf.Clamp(local.y + Offset, Edge, Mathf.Max(Edge, panel.height - height - Edge));
            label.style.left = x;
            label.style.top = y;
        }

        static bool SupportsHover(string pointerType) => pointerType == UnityEngine.UIElements.PointerType.mouse || pointerType == UnityEngine.UIElements.PointerType.pen;

        static string ResolveText(VisualElement target)
        {
            string nearest=null;
            for(var current=target;current!=null;current=current.parent)
            {
                string text=current.tooltip;
                if(string.IsNullOrWhiteSpace(text))continue;
                if(nearest==null){nearest=text;continue;}
                if(text!=nearest)return nearest+"\n"+text;
            }
            return nearest;
        }

        void Hide()
        {
            PausePending();
            if(pendingTarget!=null)pendingTarget.UnregisterCallback<DetachFromPanelEvent>(OnTargetDetached);
            pendingTarget = null;
            pendingText = null;
            if(label != null) label.style.display = DisplayStyle.None;
        }

        void PausePending()
        {
            if(pendingShow == null) return;
            pendingShow.Pause();
            pendingShow = null;
        }

        public void Dispose()
        {
            if(disposed) return;
            disposed = true;
            CancelHold();
            Application.focusChanged -= OnFocusChanged;
            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove,TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerOverEvent>(OnPointerOver);
            root.UnregisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerUpEvent>(OnPointerUp,TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerCancelEvent>(OnPointerCancel,TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut,TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerCaptureEvent>(OnCapture,TrickleDown.TrickleDown);
            root.UnregisterCallback<ClickEvent>(OnClick,TrickleDown.TrickleDown);
            root.UnregisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            label.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            label.RemoveFromHierarchy();
        }
    }
}
