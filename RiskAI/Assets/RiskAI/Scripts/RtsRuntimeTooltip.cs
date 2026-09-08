using System;
using UnityEngine;
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

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerOverEvent>(OnPointerOver);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            label.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>Call before a HUD SetContent rebuild clears its tooltip-bearing descendants.</summary>
        public void SetContentChanged() => Hide();

        void OnPointerMove(PointerMoveEvent evt)
        {
            if(!SupportsHover(evt.pointerType)) return;
            SetPending(evt.target as VisualElement, evt.position);
        }

        void OnPointerOver(PointerOverEvent evt)
        {
            if(!SupportsHover(evt.pointerType)) return;
            SetPending(evt.target as VisualElement, evt.position);
        }

        void OnPointerDown(PointerDownEvent evt) => Hide();
        void OnWheel(WheelEvent evt) => Hide();
        void OnGeometry(GeometryChangedEvent evt) => Place();
        void OnTargetDetached(DetachFromPanelEvent evt) => Hide();
        void OnPointerLeave(PointerLeaveEvent evt) => Hide();
        void OnDetach(DetachFromPanelEvent evt) => Hide();

        void SetPending(VisualElement target, Vector2 position)
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
            pendingShow = root.schedule.Execute(ShowPending).StartingIn(DelayMilliseconds);
        }

        void ShowPending()
        {
            pendingShow = null;
            if(disposed || pendingTarget == null || pendingTarget.panel!=root.panel || !root.Contains(pendingTarget) || string.IsNullOrWhiteSpace(pendingText)) return;
            label.style.maxWidth=Mathf.Max(1,Mathf.Min(MaximumWidth,root.worldBound.width-Edge*2));
            label.text = pendingText;
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
            Hide();
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            root.UnregisterCallback<PointerOverEvent>(OnPointerOver);
            root.UnregisterCallback<PointerDownEvent>(OnPointerDown,TrickleDown.TrickleDown);
            root.UnregisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            label.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            label.RemoveFromHierarchy();
        }
    }
}
