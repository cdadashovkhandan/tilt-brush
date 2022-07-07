// Copyright 2022 Vlad Corchis
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
    public class CylinderStencil : StencilWidget
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
            m_Type = StencilType.Cylinder;
        }

        // This is the key method that allows the pointer to snap on the shape's surface
        // Note the collider is of type MeshCollider and it needs to be retreived
        // Without this, the pointer does not snap on the shape
        public override void FindClosestPointOnSurface(Vector3 pos, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            Vector3 vCenterToPos = pos - transform.position;
            var collider = GetComponentInChildren<MeshCollider>();
            surfacePos = collider.ClosestPoint(pos);
            surfaceNorm = pos - surfacePos;
        }

        // This method activates the shape, allowing it to actually be picked up and scaled
        // Without it, the shape remains frozen
        override public float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            float fRadius = Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
            float baseScore = (1.0f - (transform.position - vControllerPos).magnitude / fRadius);
            if (baseScore < 0) { return baseScore; }
            return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
        }

        // This method checks the available transformation on the axis. In this case, the shape can be scaled uniformly or on the y-axis
        // Without it, it will be impossible to scale the shape
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

        // This registers and highlights the shape's components (depending on the axis on which the shape will be scaled) in case the shape is composed by more meshes but it is not the case here
        // However, this method cannot be null, the axis must be registered, and since the behavior is the same for all axis, all axis are registered and the whole shape is highlighted
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
        // If the shape can be, for example, scaled on the y-axis, this is where the transofrmation happens
        // However, it has to be in accordance to the above method "GetInferredManipulationAxis"
        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            float parentScale = TrTransform.FromTransform(transform.parent).scale;

            switch (axis)
            {
                case Axis.Y:
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

        // This method simply updates the scale and makes the change visible
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