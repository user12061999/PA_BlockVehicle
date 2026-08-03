using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class CarView : MonoBehaviour
    {
        [Serializable]
        private sealed class AttachInfo
        {
            [SerializeField] private PartAttachType _partType;
            [SerializeField] private PartAttachSideType _sideType;
            [SerializeField] private Transform[] _roots;

            public PartAttachType PartType => _partType;
            public PartAttachSideType SideType => _sideType;
            public Transform[] Roots => _roots;
        }

        [SerializeField] private Transform[] _defaultWheels;
        [SerializeField] private AttachInfo[] _attachInfos;
        [SerializeField] private Transform[] _idlingTransforms;
        [SerializeField] private Transform _roof;
        [SerializeField] private float _maxSinkDistance = 0.5f;

#if !RELEASE
        [SerializeField] private bool _isDrawGizmos = true;
#endif

        private readonly List<PartView> _attachedParts = new List<PartView>();
        private Vector3 _defaultOffset;

        public Transform T => transform;
        public Transform[] DefaultWheels => _defaultWheels;
        public Transform[] IdlingTransforms => _idlingTransforms;
        public Transform Roof => _roof;
        public IReadOnlyList<PartView> AttachedParts => _attachedParts;
        public float BodyTiltAngle { get; private set; }
        public float WheelSteerAngle { get; private set; }

        void Awake()
        {
            _defaultOffset = transform.localPosition;
            foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.receiveShadows = false;
            }
        }

#if !RELEASE
        void OnDrawGizmos()
        {
            if (!_isDrawGizmos || _attachInfos == null) return;
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            foreach (AttachInfo attachInfo in _attachInfos)
            {
                if (attachInfo == null || attachInfo.Roots == null) continue;
                foreach (Transform root in attachInfo.Roots)
                {
                    if (root != null) Gizmos.DrawSphere(root.position, 0.25f);
                }
            }
        }
#endif

        public List<PartView> GetPartViews(string uniqueId)
        {
            return _attachedParts.FindAll(part => part != null && part.UniqueId == uniqueId);
        }

        public string AttachPart(PartView partPrefab, int partId, string uniqueId = "")
        {
            if (partPrefab == null) return string.Empty;
            if (string.IsNullOrEmpty(uniqueId)) uniqueId = Guid.NewGuid().ToString();

            AttachInfo[] infos = GetAttachInfos(partPrefab.AttachType);
            if (infos.Length == 0) return uniqueId;

            foreach (AttachInfo info in infos)
            {
                Transform root = FirstFreeRoot(info);
                if (root == null) root = ReuseRoot(partPrefab.AttachType);
                if (root == null) continue;

                PartView part = Instantiate(partPrefab, root);
                part.Initialize(partId, uniqueId, info.SideType);
                _attachedParts.Add(part);
            }

            RefreshDefaultWheel();
            return uniqueId;
        }

        public void DetachPart(string uniqueId)
        {
            for (int i = _attachedParts.Count - 1; i >= 0; i--)
            {
                PartView part = _attachedParts[i];
                if (part == null || part.UniqueId != uniqueId) continue;
                _attachedParts.RemoveAt(i);
                Destroy(part.gameObject);
            }

            RefreshDefaultWheel();
        }

        public void DetachPart(int partId)
        {
            for (int i = _attachedParts.Count - 1; i >= 0; i--)
            {
                PartView part = _attachedParts[i];
                if (part == null || part.PartId != partId) continue;
                _attachedParts.RemoveAt(i);
                Destroy(part.gameObject);
            }

            RefreshDefaultWheel();
        }

        public List<Transform> GetWheels()
        {
            List<Transform> wheels = new List<Transform>();
            if (_defaultWheels != null) wheels.AddRange(_defaultWheels);
            if (_attachInfos == null) return wheels;

            foreach (AttachInfo info in _attachInfos)
            {
                if (info == null || info.PartType != PartAttachType.Wheel || info.Roots == null) continue;
                wheels.AddRange(info.Roots);
            }

            return wheels;
        }

        public void ActivatePart(int partId, float time)
        {
            foreach (PartView part in _attachedParts)
            {
                if (part != null && part.PartId == partId) part.Activate(time);
            }
        }

        public void SwitchActivateParts(TerrainType terrainType, PartDataAsset partDataAsset)
        {
            foreach (PartView part in _attachedParts)
            {
                if (part == null) continue;
                PerformanceData performanceData = partDataAsset == null ? null : partDataAsset.GetPerformanceData(part.PartId);
                if (performanceData != null && performanceData.TerrainType == terrainType) part.Activate(float.MaxValue);
                else part.Inactivate();
            }
        }

        public void InactivateAllParts()
        {
            foreach (PartView part in _attachedParts)
            {
                if (part != null) part.Inactivate();
            }
        }

        public void SetTiltBody(float angle)
        {
            BodyTiltAngle = angle;
            if (_idlingTransforms == null) return;
            foreach (Transform target in _idlingTransforms)
            {
                if (target == null) continue;
                Vector3 euler = target.localEulerAngles;
                euler.z = angle;
                target.localEulerAngles = euler;
            }
        }

        public void SetWheelSteerAngle(float angle)
        {
            WheelSteerAngle = angle;
            List<Transform> wheels = GetWheels();
            for (int i = 0; i < wheels.Count; i++)
            {
                Transform wheel = wheels[i];
                if (wheel == null) continue;
                if (i == 0 || i == 3 || i == 4 || i == 7)
                {
                    Vector3 euler = wheel.localEulerAngles;
                    euler.y = angle;
                    wheel.localEulerAngles = euler;
                }
            }
        }

        public void SetSink(bool isSink, bool immediate = false)
        {
            Vector3 position = _defaultOffset;
            if (isSink) position.y -= _maxSinkDistance;
            transform.localPosition = position;
        }

        AttachInfo[] GetAttachInfos(PartAttachType attachType)
        {
            if (_attachInfos == null) return new AttachInfo[0];
            return Array.FindAll(_attachInfos, info => info != null && info.PartType == attachType);
        }

        static Transform FirstFreeRoot(AttachInfo info)
        {
            if (info == null || info.Roots == null) return null;
            foreach (Transform root in info.Roots)
            {
                if (root != null && root.childCount == 0) return root;
            }

            return null;
        }

        Transform ReuseRoot(PartAttachType attachType)
        {
            PartView oldPart = _attachedParts.Find(part => part != null && part.AttachType == attachType);
            if (oldPart == null) return null;
            Transform root = oldPart.transform.parent;
            DetachPart(oldPart.UniqueId);
            return root;
        }

        void RefreshDefaultWheel()
        {
            bool hasWheelPart = _attachedParts.Exists(part => part != null && part.AttachType == PartAttachType.Wheel);
            if (_defaultWheels == null) return;
            foreach (Transform wheel in _defaultWheels)
            {
                if (wheel != null) wheel.gameObject.SetActive(!hasWheelPart);
            }
        }
    }
}
