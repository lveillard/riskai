using System;
using System.Collections.Generic;

namespace RiskAI.Core
{
    public enum DirectPointerActionKind { PrimaryTap, AreaBegin, AreaUpdate, AreaEnd, AreaCancel, Context, Pan, Pinch, Orbit }

    /// <summary>Engine-free screen point used by deterministic pointer arbitration.</summary>
    public readonly struct PointerPoint
    {
        public readonly float X, Y;
        public PointerPoint(float x, float y) { X=x; Y=y; }
        public static float Distance(PointerPoint a, PointerPoint b)
        {
            float x=a.X-b.X,y=a.Y-b.Y;return (float)Math.Sqrt(x*x+y*y);
        }
        public static float SqrDistance(PointerPoint a, PointerPoint b)
        {
            float x=a.X-b.X,y=a.Y-b.Y;return x*x+y*y;
        }
        public static PointerPoint Midpoint(PointerPoint a, PointerPoint b) => new PointerPoint((a.X+b.X)*.5f,(a.Y+b.Y)*.5f);
    }

    public readonly struct DirectPointerAction
    {
        public readonly DirectPointerActionKind Kind;
        public readonly PointerPoint Position, Previous;
        public readonly float Scale;
        public DirectPointerAction(DirectPointerActionKind kind, PointerPoint position, PointerPoint previous, float scale=1)
        { Kind=kind;Position=position;Previous=previous;Scale=scale; }
    }

    /// <summary>Pure touch/pen arbitration. One-pointer taps wait briefly so a double tap can become one context action.</summary>
    public sealed class DirectPointerGesture
    {
        public const float DragThreshold=12;
        public const float DoubleTapSeconds=.24f;
        public const float DoubleTapDistance=24;
        const float TwoFingerTapSlop=8;

        struct Contact
        {
            public PointerPoint Start,Position;
            public float StartedAt;
            public bool DoubleCandidate;
        }
        struct DeferredTap { public PointerPoint Position; public float ReleasedAt; public bool Valid; }

        readonly Dictionary<int,Contact> contacts=new Dictionary<int,Contact>(3);
        readonly List<DirectPointerAction> actions=new List<DirectPointerAction>(4);
        DeferredTap deferred;
        PointerPoint lastCenter,initialCenter;
        float lastDistance,initialDistance;
        bool threePointer,area,twoPointer,twoMoved,retiredTwo,cancelledGesture,blockedUntilAllReleased;

        public bool Active => contacts.Count>0||deferred.Valid||retiredTwo||blockedUntilAllReleased;
        public int ContactCount => contacts.Count;
        public float CoordinateScale { get; set; }=1;
        float Pixels(float logical) => logical*Math.Max(.5f,Math.Min(4,CoordinateScale));

        public void Begin(int id,PointerPoint position,float now)
        {
            if(blockedUntilAllReleased||retiredTwo)
            {
                contacts[id]=new Contact { Start=position,Position=position,StartedAt=now };
                blockedUntilAllReleased=true;
                return;
            }
            if(contacts.Count==2)
            {
                actions.RemoveAll(action=>action.Kind!=DirectPointerActionKind.AreaCancel);
                contacts[id]=new Contact { Start=position,Position=position,StartedAt=now };
                deferred.Valid=false;area=false;twoPointer=false;threePointer=true;
                lastCenter=ComputeCenter();return;
            }
            if(contacts.Count>=3)
            {
                if(area)actions.Add(new DirectPointerAction(DirectPointerActionKind.AreaCancel,position,position));
                actions.RemoveAll(action=>action.Kind!=DirectPointerActionKind.AreaCancel);
                contacts[id]=new Contact { Start=position,Position=position,StartedAt=now };
                deferred.Valid=false;area=false;cancelledGesture=true;blockedUntilAllReleased=true;
                return;
            }
            var contact=new Contact { Start=position,Position=position,StartedAt=now };
            if(contacts.Count==0&&deferred.Valid&&now-deferred.ReleasedAt<=DoubleTapSeconds&&PointerPoint.Distance(position,deferred.Position)<=Pixels(DoubleTapDistance))
            {
                contact.DoubleCandidate=true;
                // The first tap remains uncommitted for the entire candidate contact.
                deferred.Valid=false;
            }
            contacts[id]=contact;
            if(contacts.Count!=2)return;
            if(area)actions.Add(new DirectPointerAction(DirectPointerActionKind.AreaCancel,position,position));
            deferred.Valid=false;area=false;twoPointer=true;twoMoved=false;
            ComputePair(out initialCenter,out initialDistance);lastCenter=initialCenter;lastDistance=initialDistance;
        }

        public void Move(int id,PointerPoint position)
        {
            if(!contacts.TryGetValue(id,out var contact)||retiredTwo||blockedUntilAllReleased)return;
            contact.Position=position;contacts[id]=contact;
            if(threePointer)
            {
                var center=ComputeCenter();
                if(PointerPoint.SqrDistance(center,lastCenter)>.01f)
                    actions.Add(new DirectPointerAction(DirectPointerActionKind.Orbit,center,lastCenter));
                lastCenter=center;return;
            }
            if(twoPointer)
            {
                ComputePair(out var center,out var distance);
                if(!twoMoved)
                {
                    float slop=Pixels(TwoFingerTapSlop);
                    bool centerMoved=PointerPoint.SqrDistance(center,initialCenter)>slop*slop;
                    bool distanceChanged=Math.Abs(distance-initialDistance)>slop;
                    if(!centerMoved&&!distanceChanged){lastCenter=center;lastDistance=distance;return;}
                    twoMoved=true;
                }
                if(PointerPoint.SqrDistance(center,lastCenter)>.01f)
                    actions.Add(new DirectPointerAction(DirectPointerActionKind.Pan,center,lastCenter));
                if(lastDistance>.01f&&distance>.01f)
                {
                    float scale=distance/lastDistance;
                    if(Math.Abs(Math.Log(scale))>.002f)actions.Add(new DirectPointerAction(DirectPointerActionKind.Pinch,center,lastCenter,scale));
                }
                lastCenter=center;lastDistance=distance;return;
            }
            if(contacts.Count!=1)return;
            if(!area)
            {
                float threshold=Pixels(DragThreshold);
                if(PointerPoint.SqrDistance(contact.Position,contact.Start)<=threshold*threshold)return;
                area=true;deferred.Valid=false;
                actions.Add(new DirectPointerAction(DirectPointerActionKind.AreaBegin,contact.Start,contact.Start));
            }
            actions.Add(new DirectPointerAction(DirectPointerActionKind.AreaUpdate,contact.Position,contact.Start));
        }

        public void End(int id,PointerPoint position,float now,bool cancelled=false)
        {
            if(!contacts.TryGetValue(id,out var contact))return;
            if(cancelled)cancelledGesture=true;
            if(blockedUntilAllReleased||threePointer)
            {
                blockedUntilAllReleased=true;
                contacts.Remove(id);
                if(contacts.Count==0)ResetContacts();
                return;
            }
            Move(id,position);contact=contacts[id];contacts.Remove(id);
            if(twoPointer||retiredTwo)
            {
                if(contacts.Count>0){retiredTwo=true;return;}
                if(twoPointer&&!twoMoved&&!cancelledGesture)actions.Add(new DirectPointerAction(DirectPointerActionKind.Context,lastCenter,lastCenter));
                ResetContacts();return;
            }
            if(area)
            {
                actions.Add(new DirectPointerAction(cancelledGesture?DirectPointerActionKind.AreaCancel:DirectPointerActionKind.AreaEnd,contact.Position,contact.Start));
                ResetContacts();return;
            }
            if(!cancelledGesture)
            {
                if(contact.DoubleCandidate&&now-contact.StartedAt<=DoubleTapSeconds)
                    actions.Add(new DirectPointerAction(DirectPointerActionKind.Context,contact.Position,contact.Position));
                else deferred=new DeferredTap { Position=contact.Position,ReleasedAt=now,Valid=true };
            }
            ResetContacts(false);
        }

        public void Advance(float now)
        {
            if(!deferred.Valid||now-deferred.ReleasedAt<DoubleTapSeconds)return;
            actions.Add(new DirectPointerAction(DirectPointerActionKind.PrimaryTap,deferred.Position,deferred.Position));deferred.Valid=false;
        }
        public void Drain(Action<DirectPointerAction> consume) { for(int i=0;i<actions.Count;i++)consume(actions[i]);actions.Clear(); }
        public void Cancel() { contacts.Clear();actions.Clear();deferred.Valid=false;ResetContacts(); }
        void ResetContacts(bool clearContacts=true)
        {
            if(clearContacts)contacts.Clear();area=false;threePointer=false;twoPointer=false;twoMoved=false;retiredTwo=false;cancelledGesture=false;blockedUntilAllReleased=false;
        }
        PointerPoint ComputeCenter()
        {
            float x=0,y=0;foreach(var contact in contacts.Values){x+=contact.Position.X;y+=contact.Position.Y;}
            return new PointerPoint(x/contacts.Count,y/contacts.Count);
        }
        void ComputePair(out PointerPoint center,out float distance)
        {
            var enumerator=contacts.Values.GetEnumerator();enumerator.MoveNext();var a=enumerator.Current.Position;enumerator.MoveNext();var b=enumerator.Current.Position;
            center=PointerPoint.Midpoint(a,b);distance=PointerPoint.Distance(a,b);
        }
    }
}
