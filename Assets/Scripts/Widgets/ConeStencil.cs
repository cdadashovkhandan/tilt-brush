// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class ConeStencil : StencilWidget
    {
        private Vector3 m_AspectRatio;
        public override Vector3 Extents
        {
            get
            {
                return m_Size * m_AspectRatio;
            }
            set
            {
                m_Size = 1f;
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        public override Vector3 CustomDimension
        {
            get { return m_AspectRatio; }
            set
            {
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            m_AspectRatio = Vector3.one;
            m_Type = StencilType.Cone;
        }

        public override void FindClosestPointOnSurface(Vector3 pos, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            Vector3 vCenterToPos = pos - transform.position;
            //Vector3 localPos = transform.InverseTransformPoint(pos);
            var collider = GetComponentInChildren<MeshCollider>();
            surfacePos = collider.ClosestPoint(pos);
            //surfaceNorm = -Vector3.forward;
            surfaceNorm = pos - surfacePos;
        }

        override public float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            float fRadius = Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
            float baseScore = (1.0f - (transform.position - vControllerPos).magnitude / fRadius);
            // don't try to scale if invalid; scaling by zero will make it look valid
            if (baseScore < 0) { return baseScore; }
            return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
        }

        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            if (secondaryHandInside)
            {
                return Axis.Invalid;
            }
            Vector3 vHandsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
            Vector3 vAbs = vHandsInObjectSpace.Abs();
            if (vAbs.x > vAbs.y && vAbs.x > vAbs.z)
            {
                return Axis.Invalid;
            }
            else if (vAbs.y > vAbs.z || vAbs.y > vAbs.x)
            {
                return Axis.Y;
            }
            else
            {
                return Axis.Invalid;
            }
        }

        // Apply scale and make it undo-able
        public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis)
        {
            if (m_RecordMovements)
            {
                Vector3 newDimensions = CustomDimension;
                newDimensions[(int)axis] *= deltaScale;
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(this, LocalTransform, newDimensions));
            }
            else
            {
                m_AspectRatio[(int)axis] *= deltaScale;
                UpdateScale();
            }
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            if (m_HighlightMeshFilters != null)
            {
                for (int i = 0; i < m_HighlightMeshFilters.Length; i++)
                {
                    App.Instance.SelectionEffect.RegisterMesh(m_HighlightMeshFilters[i]);
                }
            }
        }

        // Using the locked axis, get the scaled direction of the axis
        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            // Unexpected -- normally we're only called during a 2-handed manipulation
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            float parentScale = TrTransform.FromTransform(transform.parent).scale;

            // Fill in axisVec, extent
            switch (axis)
            {
                //case Axis.X:
                case Axis.Y:
                //case Axis.Z:
                    Vector3 axisVec_LS = Vector3.zero;
                    axisVec_LS[(int)axis] = 1;
                    axisVec = transform.TransformDirection(axisVec_LS);
                    extent = parentScale * Extents[(int)axis];
                    break;
                case Axis.Invalid:
                    axisVec = default(Vector3);
                    extent = default(float);
                    break;
                default:
                    throw new NotImplementedException(axis.ToString());
            }

            return axis;
        }

        public override Bounds GetBounds_SelectionCanvasSpace()
        {
            if (m_Collider != null)
            {
                MeshCollider collider = m_Collider as MeshCollider;
                TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_Collider.transform);
                Bounds bounds = new Bounds(colliderToCanvasXf * collider.bounds.center, Vector3.zero);

                // Polys are invariant with rotation, so take out the rotation from the transform and just
                // add the two opposing corners.
                colliderToCanvasXf.rotation = Quaternion.identity;
                bounds.Encapsulate(colliderToCanvasXf * (collider.bounds.center + collider.bounds.extents));
                bounds.Encapsulate(colliderToCanvasXf * (collider.bounds.center - collider.bounds.extents));

                return bounds;
            }
            return base.GetBounds_SelectionCanvasSpace();
        }

        protected override void UpdateScale()
        {
            float maxAspect = m_AspectRatio.Max();
            m_AspectRatio /= maxAspect;
            m_Size *= maxAspect;
            transform.localScale = m_Size * m_AspectRatio;
            UpdateMaterialScale();
        }
    }
} // namespace TiltBrush