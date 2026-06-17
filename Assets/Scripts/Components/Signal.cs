using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Data;

namespace RailwayInterlock.Components
{
    public class Signal : MonoBehaviour, ISignal
    {
        [Header("Configuration")]
        public string signalId;
        public string displayName;
        public Direction direction = Direction.Up;
        public string protectingTrackId;

        [Header("Light References")]
        public Renderer redLight;
        public Renderer yellowLight;
        public Renderer greenLight;

        [Header("Emissive Materials")]
        public Color redEmissiveColor = new Color(1f, 0.2f, 0.2f);
        public Color yellowEmissiveColor = new Color(1f, 0.9f, 0.2f);
        public Color greenEmissiveColor = new Color(0.2f, 1f, 0.3f);
        public float lightIntensity = 2f;
        public float blinkFrequency = 1f;

        [Header("Detection Zone")]
        public Collider stopZone;
        public float detectionDistance = 50f;

        [Header("Debug")]
        [SerializeField] private SignalAspect _aspect = SignalAspect.Red;
        public SignalAspect Aspect => _aspect;

        private Material _redMaterial;
        private Material _yellowMaterial;
        private Material _greenMaterial;
        private Color _redBaseColor;
        private Color _yellowBaseColor;
        private Color _greenBaseColor;
        private float _blinkTimer;

        public event Action<string, SignalAspect> OnAspectChanged;

        private void Awake()
        {
            InitializeMaterials();
        }

        private void Start()
        {
            SetAspect(_aspect);
        }

        private void InitializeMaterials()
        {
            if (redLight != null)
            {
                _redMaterial = Instantiate(redLight.material);
                redLight.material = _redMaterial;
                _redBaseColor = _redMaterial.color;
            }
            if (yellowLight != null)
            {
                _yellowMaterial = Instantiate(yellowLight.material);
                yellowLight.material = _yellowMaterial;
                _yellowBaseColor = _yellowMaterial.color;
            }
            if (greenLight != null)
            {
                _greenMaterial = Instantiate(greenLight.material);
                greenLight.material = _greenMaterial;
                _greenBaseColor = _greenMaterial.color;
            }
        }

        public void SetAspect(SignalAspect aspect)
        {
            if (_aspect != aspect)
            {
                _aspect = aspect;
                OnAspectChanged?.Invoke(signalId, _aspect);
                Debug.Log($"[Signal] {signalId} 显示 {aspect} 灯");
            }
            UpdateLightVisuals();
        }

        private void UpdateLightVisuals()
        {
            _blinkTimer = 0f;
            SetLightState(_redMaterial, _aspect == SignalAspect.Red, redEmissiveColor, _redBaseColor, false);
            SetLightState(_yellowMaterial, _aspect == SignalAspect.Yellow, yellowEmissiveColor, _yellowBaseColor, _aspect == SignalAspect.Yellow);
            SetLightState(_greenMaterial, _aspect == SignalAspect.Green, greenEmissiveColor, _greenBaseColor, false);
        }

        private void SetLightState(Material mat, bool isOn, Color emissiveColor, Color baseColor, bool blink)
        {
            if (mat == null) return;

            if (isOn)
            {
                mat.color = baseColor;
                mat.SetColor("_EmissionColor", emissiveColor * lightIntensity);
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                mat.color = baseColor * 0.3f;
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }

        private void Update()
        {
            if (_aspect == SignalAspect.Yellow && _yellowMaterial != null)
            {
                _blinkTimer += Time.deltaTime;
                float blink = Mathf.Sin(_blinkTimer * Mathf.PI * 2 * blinkFrequency) * 0.5f + 0.5f;
                _yellowMaterial.SetColor("_EmissionColor", yellowEmissiveColor * lightIntensity * (0.5f + blink * 0.5f));
            }
        }

        public bool ShouldStopTrain()
        {
            return _aspect == SignalAspect.Red;
        }

        public SignalData ToData()
        {
            return new SignalData
            {
                Id = signalId,
                Name = displayName,
                Direction = direction,
                ProtectingTrackId = protectingTrackId
            };
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 dir = direction == Direction.Up ? transform.forward : -transform.forward;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, dir * detectionDistance);

            if (stopZone != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.matrix = stopZone.transform.localToWorldMatrix;
                BoxCollider box = stopZone as BoxCollider;
                if (box != null)
                    Gizmos.DrawCube(box.center, box.size);
            }

            Color labelColor = _aspect switch
            {
                SignalAspect.Red => Color.red,
                SignalAspect.Yellow => Color.yellow,
                SignalAspect.Green => Color.green,
                _ => Color.white
            };
        }
    }
}
