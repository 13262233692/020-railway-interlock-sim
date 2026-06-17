using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Data;

namespace RailwayInterlock.Components
{
    [RequireComponent(typeof(BoxCollider))]
    public class TrackCircuit : MonoBehaviour, ITrackCircuit
    {
        [Header("Configuration")]
        public string trackId;
        public string displayName;
        public float length = 100f;
        public List<string> adjacentTrackIds = new List<string>();

        [Header("Visuals")]
        public Renderer trackRenderer;
        public Color clearColor = new Color(0.3f, 0.3f, 0.3f);
        public Color occupiedColor = new Color(0.8f, 0.2f, 0.2f);

        [Header("Debug")]
        [SerializeField] private TrackState _state = TrackState.Clear;
        [SerializeField] private int _occupancyCount = 0;

        private BoxCollider _collider;
        private readonly HashSet<GameObject> _occupyingObjects = new HashSet<GameObject>();

        public string Id => trackId;

        public TrackState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged?.Invoke(trackId, _state);
                    UpdateVisuals();
                }
            }
        }

        public event Action<string, TrackState> OnStateChanged;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
            _collider.isTrigger = true;
        }

        private void Start()
        {
            UpdateVisuals();
        }

        public void SetOccupied()
        {
            _occupancyCount++;
            if (_occupancyCount > 0)
                State = TrackState.Occupied;
        }

        public void SetClear()
        {
            _occupancyCount = Mathf.Max(0, _occupancyCount - 1);
            if (_occupancyCount == 0)
                State = TrackState.Clear;
        }

        private void OnTriggerEnter(Collider other)
        {
            var train = other.GetComponent<Train>();
            if (train != null && _occupyingObjects.Add(other.gameObject))
            {
                SetOccupied();
                Debug.Log($"[TrackCircuit] {trackId} 被列车 {train.trainId} 占用");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var train = other.GetComponent<Train>();
            if (train != null && _occupyingObjects.Remove(other.gameObject))
            {
                SetClear();
                Debug.Log($"[TrackCircuit] {trackId} 列车 {train.trainId} 离开");
            }
        }

        private void UpdateVisuals()
        {
            if (trackRenderer != null)
            {
                trackRenderer.material.color = State == TrackState.Occupied ? occupiedColor : clearColor;
                trackRenderer.material.SetColor("_EmissionColor", State == TrackState.Occupied ? occupiedColor * 0.5f : Color.black);
            }
        }

        public TrackCircuitData ToData()
        {
            return new TrackCircuitData
            {
                Id = trackId,
                Name = displayName,
                Length = length,
                AdjacentTracks = new List<string>(adjacentTrackIds)
            };
        }

        public void ResetOccupancy()
        {
            _occupancyCount = 0;
            _occupyingObjects.Clear();
            State = TrackState.Clear;
        }

        private void OnDrawGizmosSelected()
        {
            if (_collider == null)
                _collider = GetComponent<BoxCollider>();

            if (_collider != null)
            {
                Gizmos.color = State == TrackState.Occupied ? new Color(1f, 0.3f, 0.3f, 0.3f) : new Color(0.3f, 0.8f, 0.3f, 0.2f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(_collider.center, _collider.size);
            }

            GUIStyle style = new GUIStyle
            {
                fontSize = 14,
                normal = { textColor = State == TrackState.Occupied ? Color.red : Color.white }
            };
        }
    }
}
