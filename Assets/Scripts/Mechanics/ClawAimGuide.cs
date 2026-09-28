using UnityEngine;
using UnityEngine.Rendering;

namespace ClawMachine.Mechanics
{
    [DisallowMultipleComponent]
    public sealed class ClawAimGuide : MonoBehaviour
    {
        private const int HitBufferSize = 32;

        [Header("References")]
        [SerializeField] private ClawMachineController controller;
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private Material guideMaterial;

        [Header("Raycast")]
        [SerializeField] private LayerMask raycastMask = ~(1 << 7);
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;
        [SerializeField, Min(0f)] private float originClearance = 0.03f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.015f;

        [Header("Visuals")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.025f;
        [SerializeField, Min(0.01f)] private float markerRadius = 0.22f;
        [SerializeField, Range(12, 96)] private int markerSegments = 48;
        [SerializeField] private Color floorColor = new Color(0.1f, 1f, 0.9f, 0.72f);
        [SerializeField] private Color dollColor = new Color(1f, 0.82f, 0.1f, 0.9f);

        [Header("Target Highlight")]
        [SerializeField] private Material targetOutlineMaterial;
        [SerializeField] private Color targetGlowColor = new Color(1f, 0.65f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float targetGlowPulseSpeed = 6f;
        [SerializeField, Min(0f)] private float targetGlowMin = 0.15f;
        [SerializeField, Min(0f)] private float targetGlowMax = 0.55f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        private Collider[] selfColliders;
        private LineRenderer rayLine;
        private LineRenderer markerLine;
        private ClawTargetHighlight currentTargetHighlight;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<ClawMachineController>();
            }

            selfColliders = GetComponentsInChildren<Collider>(true);
            CreateVisuals();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!ShouldShowGuide())
            {
                SetVisible(false);
                return;
            }

            Vector3 origin = GetRayOrigin();
            if (!TryGetClosestHit(origin, out RaycastHit hit))
            {
                SetVisible(false);
                return;
            }

            bool isDoll = hit.collider.CompareTag("Doll") ||
                          (hit.rigidbody != null && hit.rigidbody.CompareTag("Doll"));
            Color color = isDoll ? dollColor : floorColor;
            Vector3 markerPosition = hit.point + hit.normal * surfaceOffset;

            UpdateTargetHighlight(hit, isDoll);
            UpdateRay(origin, markerPosition, color);
            UpdateMarker(markerPosition, hit.normal, color);
            SetVisible(true);
        }

        private bool ShouldShowGuide()
        {
            if (controller == null || !controller.isActiveAndEnabled)
            {
                return false;
            }

            return controller.currentState == ClawState.Idle ||
                   controller.currentState == ClawState.Moving;
        }

        private Vector3 GetRayOrigin()
        {
            if (rayOrigin != null)
            {
                return rayOrigin.position;
            }

            float lowestPoint = transform.position.y;
            for (int i = 0; i < selfColliders.Length; i++)
            {
                Collider selfCollider = selfColliders[i];
                if (selfCollider != null && selfCollider.enabled && selfCollider.gameObject.activeInHierarchy)
                {
                    lowestPoint = Mathf.Min(lowestPoint, selfCollider.bounds.min.y);
                }
            }

            return new Vector3(transform.position.x, lowestPoint - originClearance, transform.position.z);
        }

        private bool TryGetClosestHit(Vector3 origin, out RaycastHit closestHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                hitBuffer,
                maxDistance,
                raycastMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            int closestIndex = -1;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                closestHit = hitBuffer[closestIndex];
                return true;
            }

            closestHit = default;
            return false;
        }

        private void CreateVisuals()
        {
            GameObject rayObject = new GameObject("Aim Ray");
            rayObject.transform.SetParent(transform, false);
            rayLine = ConfigureLineRenderer(rayObject, false, lineWidth);
            rayLine.positionCount = 2;

            GameObject markerObject = new GameObject("Aim Marker");
            markerObject.transform.SetParent(transform, false);
            markerLine = ConfigureLineRenderer(markerObject, true, lineWidth * 1.5f);
            markerLine.positionCount = markerSegments;
        }

        private LineRenderer ConfigureLineRenderer(GameObject target, bool loop, float width)
        {
            LineRenderer renderer = target.AddComponent<LineRenderer>();
            renderer.sharedMaterial = guideMaterial;
            renderer.useWorldSpace = true;
            renderer.loop = loop;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.widthMultiplier = width;
            renderer.numCapVertices = 4;
            renderer.numCornerVertices = 4;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private void UpdateRay(Vector3 origin, Vector3 destination, Color color)
        {
            rayLine.startColor = color;
            rayLine.endColor = color;
            rayLine.SetPosition(0, origin);
            rayLine.SetPosition(1, destination);
        }

        private void UpdateMarker(Vector3 position, Vector3 surfaceNormal, Color color)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.001f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            markerLine.startColor = color;
            markerLine.endColor = color;
            if (markerLine.positionCount != markerSegments)
            {
                markerLine.positionCount = markerSegments;
            }

            for (int i = 0; i < markerSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / markerSegments;
                Vector3 offset = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * markerRadius;
                markerLine.SetPosition(i, position + offset);
            }
        }

        private void SetVisible(bool visible)
        {
            if (!visible)
            {
                ClearTargetHighlight();
            }

            if (rayLine != null)
            {
                rayLine.enabled = visible;
            }

            if (markerLine != null)
            {
                markerLine.enabled = visible;
            }
        }

        private void UpdateTargetHighlight(RaycastHit hit, bool isDoll)
        {
            if (!isDoll || targetOutlineMaterial == null)
            {
                ClearTargetHighlight();
                return;
            }

            GameObject target = hit.rigidbody != null ? hit.rigidbody.gameObject : hit.collider.gameObject;
            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                targetRenderer = target.GetComponentInChildren<Renderer>();
            }

            if (targetRenderer == null)
            {
                ClearTargetHighlight();
                return;
            }

            ClawTargetHighlight nextHighlight = target.GetComponent<ClawTargetHighlight>();
            if (nextHighlight == null)
            {
                nextHighlight = target.AddComponent<ClawTargetHighlight>();
            }

            if (nextHighlight == currentTargetHighlight)
            {
                return;
            }

            ClearTargetHighlight();
            if (nextHighlight.Initialize(
                    targetRenderer,
                    targetOutlineMaterial,
                    targetGlowColor,
                    targetGlowPulseSpeed,
                    targetGlowMin,
                    targetGlowMax))
            {
                currentTargetHighlight = nextHighlight;
                currentTargetHighlight.SetHighlighted(true);
            }
        }

        private void ClearTargetHighlight()
        {
            if (currentTargetHighlight == null)
            {
                return;
            }

            currentTargetHighlight.SetHighlighted(false);
            currentTargetHighlight = null;
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(0.1f, maxDistance);
            originClearance = Mathf.Max(0f, originClearance);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            lineWidth = Mathf.Max(0.001f, lineWidth);
            markerRadius = Mathf.Max(0.01f, markerRadius);
            markerSegments = Mathf.Clamp(markerSegments, 12, 96);
            targetGlowPulseSpeed = Mathf.Max(0f, targetGlowPulseSpeed);
            targetGlowMin = Mathf.Max(0f, targetGlowMin);
            targetGlowMax = Mathf.Max(targetGlowMin, targetGlowMax);
        }
    }
}
