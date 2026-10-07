using System;
using TrashPandas.Core.Trenchcoat;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// What clients need to animate the trenchcoat locally (limbs, head, reaching hands). The host's
    /// physics and jumps are not part of it: position and rotation travel through NetworkTransform.
    /// </summary>
    public struct BodyVisualState : INetworkSerializable, IEquatable<BodyVisualState>
    {
        [Flags]
        enum F : ushort
        {
            Crouch = 1 << 0, Collapsed = 1 << 1, LeftLegLimp = 1 << 2, RightLegLimp = 1 << 3,
            LeftArmLimp = 1 << 4, RightArmLimp = 1 << 5, LeftReach = 1 << 6, RightReach = 1 << 7,
            HeadSlumped = 1 << 8, HasLeftPoint = 1 << 9, HasRightPoint = 1 << 10,
        }

        ushort _flags;
        public Vector2 Move;
        public float Discord;
        public Vector3 LeftPoint, RightPoint, LeftAim, RightAim, HeadAim;

        public static BodyVisualState From(in BodyIntent i)
        {
            F f = 0;
            if (i.Crouch) f |= F.Crouch;
            if (i.Collapsed) f |= F.Collapsed;
            if (i.LeftLegLimp) f |= F.LeftLegLimp;
            if (i.RightLegLimp) f |= F.RightLegLimp;
            if (i.LeftArmLimp) f |= F.LeftArmLimp;
            if (i.RightArmLimp) f |= F.RightArmLimp;
            if (i.LeftReach) f |= F.LeftReach;
            if (i.RightReach) f |= F.RightReach;
            if (i.HeadSlumped) f |= F.HeadSlumped;
            if (i.HasLeftPoint) f |= F.HasLeftPoint;
            if (i.HasRightPoint) f |= F.HasRightPoint;
            return new BodyVisualState
            {
                _flags = (ushort)f, Move = i.Move, Discord = i.Discord,
                LeftPoint = i.LeftPoint, RightPoint = i.RightPoint,
                LeftAim = i.LeftAim, RightAim = i.RightAim, HeadAim = i.HeadAim,
            };
        }

        public BodyIntent ToIntent()
        {
            var f = (F)_flags;
            return new BodyIntent
            {
                Move = Move, Discord = Discord,
                Crouch = (f & F.Crouch) != 0, Collapsed = (f & F.Collapsed) != 0,
                LeftLegLimp = (f & F.LeftLegLimp) != 0, RightLegLimp = (f & F.RightLegLimp) != 0,
                LeftArmLimp = (f & F.LeftArmLimp) != 0, RightArmLimp = (f & F.RightArmLimp) != 0,
                LeftReach = (f & F.LeftReach) != 0, RightReach = (f & F.RightReach) != 0,
                HeadSlumped = (f & F.HeadSlumped) != 0,
                HasLeftPoint = (f & F.HasLeftPoint) != 0, HasRightPoint = (f & F.HasRightPoint) != 0,
                LeftPoint = LeftPoint, RightPoint = RightPoint, LeftAim = LeftAim, RightAim = RightAim, HeadAim = HeadAim,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref _flags);
            s.SerializeValue(ref Move);
            s.SerializeValue(ref Discord);
            s.SerializeValue(ref LeftPoint);
            s.SerializeValue(ref RightPoint);
            s.SerializeValue(ref LeftAim);
            s.SerializeValue(ref RightAim);
            s.SerializeValue(ref HeadAim);
        }

        public bool Equals(BodyVisualState o) =>
            _flags == o._flags && Move == o.Move && Discord == o.Discord && LeftPoint == o.LeftPoint &&
            RightPoint == o.RightPoint && LeftAim == o.LeftAim && RightAim == o.RightAim && HeadAim == o.HeadAim;
    }
}
