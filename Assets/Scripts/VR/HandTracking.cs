using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace NeonBreach
{
    /// <summary>
    /// Controller-free play with the XR Hands package (OpenXR hand tracking, e.g. Meta Quest).
    /// Reads the 26 joints of each hand every frame and recognises simple gestures from joint distances:
    ///   RIGHT hand  pinch (thumb tip touches index tip)  -> fire     |  open palm held 0.4 s -> reload
    ///   LEFT hand   pinch                                -> grenade  |  open palm held 0.4 s -> use medkit
    /// The weapon follows the right hand's aim pose (wrist -> middle knuckle), and a right pinch also clicks VR menus.
    /// Uses hysteresis (2 cm to start a pinch, 3.5 cm to release) so tracking jitter does not cause double shots.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class HandTracking : MonoBehaviour
    {
        public class HandState
        {
            public bool tracked;
            public bool pinch, pinchDown, pinchUp;
            public float pinchDistance;
            public float palmOpenTime;
            public bool palmOpen;
            public bool palmTriggered;    // true for one frame when an open palm has been held long enough
            bool latched;
            public Pose aim;

            public void Latch(bool open)
            {
                palmTriggered = false;
                if (open && palmOpenTime >= 0.4f && !latched) { palmTriggered = true; latched = true; }
                if (!open) latched = false;
            }
        }

        public readonly HandState Left = new HandState();
        public readonly HandState Right = new HandState();

        static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
        XRHandSubsystem sub;
        float searchTimer;

        public bool Available { get { return sub != null && sub.running; } }
        public bool AnyTracked { get { return Left.tracked || Right.tracked; } }

        void Update()
        {
            if (!GameSettings.HandTracking)
            {
                Clear(Left); Clear(Right);
                return;
            }
            if (sub == null || !sub.running)
            {
                sub = null;
                searchTimer -= Time.unscaledDeltaTime;
                if (searchTimer > 0f) { Clear(Left); Clear(Right); return; }
                searchTimer = 1f;
                subs.Clear();
                SubsystemManager.GetSubsystems(subs);
                for (int i = 0; i < subs.Count; i++) if (subs[i] != null && subs[i].running) { sub = subs[i]; break; }
                if (sub == null) { Clear(Left); Clear(Right); return; }
            }
            UpdateHand(sub.leftHand, Left);
            UpdateHand(sub.rightHand, Right);
        }

        static void Clear(HandState s)
        {
            s.tracked = false; s.pinch = false; s.pinchDown = false; s.pinchUp = false;
            s.palmOpen = false; s.palmOpenTime = 0f; s.Latch(false);
        }

        static bool Joint(XRHand h, XRHandJointID id, out Vector3 p)
        {
            Pose pose;
            if (h.GetJoint(id).TryGetPose(out pose)) { p = pose.position; return true; }
            p = Vector3.zero;
            return false;
        }

        void UpdateHand(XRHand hand, HandState st)
        {
            bool wasPinch = st.pinch;
            st.pinchDown = false;
            st.pinchUp = false;
            st.tracked = hand.isTracked;

            Vector3 thumb, index, middle, ring, little, palm, wrist, knuckle;
            if (!st.tracked ||
                !Joint(hand, XRHandJointID.ThumbTip, out thumb) || !Joint(hand, XRHandJointID.IndexTip, out index) ||
                !Joint(hand, XRHandJointID.MiddleTip, out middle) || !Joint(hand, XRHandJointID.RingTip, out ring) ||
                !Joint(hand, XRHandJointID.LittleTip, out little) || !Joint(hand, XRHandJointID.Palm, out palm) ||
                !Joint(hand, XRHandJointID.Wrist, out wrist) || !Joint(hand, XRHandJointID.MiddleProximal, out knuckle))
            {
                Clear(st);
                if (wasPinch) st.pinchUp = true;
                return;
            }

            // pinch with hysteresis
            st.pinchDistance = Vector3.Distance(thumb, index);
            st.pinch = wasPinch ? st.pinchDistance < 0.035f : st.pinchDistance < 0.02f;
            st.pinchDown = st.pinch && !wasPinch;
            st.pinchUp = !st.pinch && wasPinch;

            // open palm: every fingertip extended away from the palm and the thumb spread
            st.palmOpen = Vector3.Distance(index, palm) > 0.075f && Vector3.Distance(middle, palm) > 0.08f &&
                          Vector3.Distance(ring, palm) > 0.075f && Vector3.Distance(little, palm) > 0.06f &&
                          st.pinchDistance > 0.06f;
            st.palmOpenTime = st.palmOpen ? st.palmOpenTime + Time.unscaledDeltaTime : 0f;
            st.Latch(st.palmOpen);

            // aim pose: along the back of the hand, from the wrist towards the middle knuckle
            Vector3 fwd = knuckle - wrist;
            if (fwd.sqrMagnitude < 0.000001f) fwd = Vector3.forward;
            st.aim = new Pose(Vector3.Lerp(palm, knuckle, 0.5f), Quaternion.LookRotation(fwd.normalized, Vector3.up));
        }
    }
}
