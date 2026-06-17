using System;
using System.Collections;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Data;

namespace RailwayInterlock.Components
{
    public class SwitchPoint : MonoBehaviour, ISwitch
    {
        [Header("Configuration")]
        public string switchId;
        public string displayName;
        public SwitchType switchType = SwitchType.Single;
        public string commonTrackId;
        public string normalTrackId;
        public string reverseTrackId;

        [Header("Movement")]
        public Transform movingRail;
        public float switchTime = 1.5f;
        public Vector3 normalPositionOffset;
        public Vector3 reversePositionOffset;
        public Vector3 normalRotationEuler;
        public Vector3 reverseRotationEuler;

        [Header("Detection")]
        public TrackCircuit occupancyTrack;

        [Header("Debug")]
        [SerializeField] private SwitchPosition _position = SwitchPosition.Normal;
        [SerializeField] private SwitchPosition _targetPosition = SwitchPosition.Normal;
        public SwitchPosition TargetPosition => _targetPosition;
        [SerializeField] private bool _isMoving = false;
        [SerializeField] private float _moveProgress = 1f;

        public string Id => switchId;
        public SwitchPosition Position => _position;
        public SwitchType Type => switchType;
        public bool IsMoving => _isMoving;

        public event Action<string, SwitchPosition> OnPositionChanged;

        private Coroutine _moveCoroutine;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            if (movingRail != null)
            {
                _initialPosition = movingRail.localPosition;
                _initialRotation = movingRail.localRotation;
            }
        }

        private void Start()
        {
            ApplyPositionImmediate(_position);
            _targetPosition = _position;
        }

        public bool SetPosition(SwitchPosition position)
        {
            if (IsOccupied())
            {
                Debug.LogWarning($"[SwitchPoint] {switchId} 被占用，无法转换道岔");
                return false;
            }

            if (_isMoving)
            {
                StopCoroutine(_moveCoroutine);
            }

            if (_position == position)
            {
                _targetPosition = position;
                return true;
            }

            _targetPosition = position;
            _moveCoroutine = StartCoroutine(MoveToPosition(position));
            return true;
        }

        public bool IsInConsistentPosition()
        {
            return !_isMoving && _position == _targetPosition;
        }

        public bool IsOccupied()
        {
            if (occupancyTrack != null)
                return occupancyTrack.State == TrackState.Occupied;
            return false;
        }

        private IEnumerator MoveToPosition(SwitchPosition target)
        {
            _isMoving = true;
            float elapsed = 0f;
            SwitchPosition startPos = _position;

            Vector3 startPosOffset = startPos == SwitchPosition.Normal ? normalPositionOffset : reversePositionOffset;
            Vector3 endPosOffset = target == SwitchPosition.Normal ? normalPositionOffset : reversePositionOffset;
            Vector3 startRot = startPos == SwitchPosition.Normal ? normalRotationEuler : reverseRotationEuler;
            Vector3 endRot = target == SwitchPosition.Normal ? normalRotationEuler : reverseRotationEuler;

            while (elapsed < switchTime)
            {
                elapsed += Time.deltaTime;
                _moveProgress = Mathf.Clamp01(elapsed / switchTime);
                float t = Mathf.SmoothStep(0f, 1f, _moveProgress);

                if (movingRail != null)
                {
                    movingRail.localPosition = _initialPosition + Vector3.Lerp(startPosOffset, endPosOffset, t);
                    movingRail.localRotation = _initialRotation * Quaternion.Euler(Vector3.Lerp(startRot, endRot, t));
                }

                yield return null;
            }

            _position = target;
            _isMoving = false;
            _moveProgress = 1f;
            ApplyPositionImmediate(target);

            Debug.Log($"[SwitchPoint] {switchId} 转换到 {target} 位置");
            OnPositionChanged?.Invoke(switchId, _position);
        }

        private void ApplyPositionImmediate(SwitchPosition pos)
        {
            if (movingRail != null)
            {
                Vector3 offset = pos == SwitchPosition.Normal ? normalPositionOffset : reversePositionOffset;
                Vector3 rot = pos == SwitchPosition.Normal ? normalRotationEuler : reverseRotationEuler;
                movingRail.localPosition = _initialPosition + offset;
                movingRail.localRotation = _initialRotation * Quaternion.Euler(rot);
            }
            _position = pos;
        }

        public void TogglePosition()
        {
            SwitchPosition target = _position == SwitchPosition.Normal ? SwitchPosition.Reverse : SwitchPosition.Normal;
            SetPosition(target);
        }

        public SwitchData ToData()
        {
            return new SwitchData
            {
                Id = switchId,
                Name = displayName,
                Type = switchType,
                CommonTrack = commonTrackId,
                NormalConnection = normalTrackId,
                ReverseConnection = reverseTrackId
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _position == SwitchPosition.Normal ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 1f);

            string label = $"{switchId}\n{_position}";
            if (_isMoving)
                label += $" (移动中 {(_moveProgress * 100):F0}%)";
        }
    }
}
