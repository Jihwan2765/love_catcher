using UnityEngine;
using UnityEngine.Rendering;

namespace ClawMachine.Mechanics
{
    [DisallowMultipleComponent]
    public sealed class ClawTargetHighlight : MonoBehaviour
    {
        private Renderer sourceRenderer;
        private Renderer outlineRenderer;
        private Material[] originalMaterials;
        private Material[] highlightedMaterials;
        private Color emissionColor;
        private float pulseSpeed;
        private float pulseMin;
        private float pulseMax;
        private bool initialized;
        private bool highlighted;

        public bool Initialize(
            Renderer targetRenderer,
            Material outlineMaterial,
            Color glowColor,
            float glowPulseSpeed,
            float minimumGlow,
            float maximumGlow)
        {
            if (initialized)
            {
                return sourceRenderer != null && outlineRenderer != null;
            }

            if (targetRenderer == null || outlineMaterial == null)
            {
                return false;
            }

            MeshFilter sourceFilter = targetRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return false;
            }

            sourceRenderer = targetRenderer;
            emissionColor = glowColor;
            pulseSpeed = Mathf.Max(0f, glowPulseSpeed);
            pulseMin = Mathf.Max(0f, minimumGlow);
            pulseMax = Mathf.Max(pulseMin, maximumGlow);

            originalMaterials = sourceRenderer.sharedMaterials;
            highlightedMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material original = originalMaterials[i];
                if (original == null)
                {
                    continue;
                }

                Material highlightedMaterial = new Material(original)
                {
                    name = $"{original.name} (Claw Target Highlight)"
                };
                highlightedMaterial.EnableKeyword("_EMISSION");
                highlightedMaterials[i] = highlightedMaterial;
            }

            GameObject outlineObject = new GameObject("Claw Target Outline");
            outlineObject.layer = targetRenderer.gameObject.layer;
            outlineObject.transform.SetParent(targetRenderer.transform, false);

            MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer meshOutlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            meshOutlineRenderer.sharedMaterial = outlineMaterial;
            meshOutlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshOutlineRenderer.receiveShadows = false;
            meshOutlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshOutlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshOutlineRenderer.enabled = false;
            outlineRenderer = meshOutlineRenderer;

            initialized = true;
            return true;
        }

        public void SetHighlighted(bool shouldHighlight)
        {
            if (!initialized || highlighted == shouldHighlight)
            {
                return;
            }

            highlighted = shouldHighlight;
            outlineRenderer.enabled = highlighted;
            sourceRenderer.sharedMaterials = highlighted ? highlightedMaterials : originalMaterials;

            if (highlighted)
            {
                UpdateEmission();
            }
        }

        private void Update()
        {
            if (highlighted)
            {
                UpdateEmission();
            }
        }

        private void UpdateEmission()
        {
            float wave = pulseSpeed > 0f ? (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f : 0f;
            float intensity = Mathf.Lerp(pulseMin, pulseMax, wave);
            Color pulsedEmission = emissionColor * intensity;

            for (int i = 0; i < highlightedMaterials.Length; i++)
            {
                Material highlightedMaterial = highlightedMaterials[i];
                if (highlightedMaterial != null && highlightedMaterial.HasProperty("_EmissionColor"))
                {
                    highlightedMaterial.SetColor("_EmissionColor", pulsedEmission);
                }
            }
        }

        private void OnDisable()
        {
            if (initialized)
            {
                SetHighlighted(false);
            }
        }

        private void OnDestroy()
        {
            if (sourceRenderer != null && originalMaterials != null)
            {
                sourceRenderer.sharedMaterials = originalMaterials;
            }

            if (highlightedMaterials != null)
            {
                for (int i = 0; i < highlightedMaterials.Length; i++)
                {
                    if (highlightedMaterials[i] != null)
                    {
                        Destroy(highlightedMaterials[i]);
                    }
                }
            }
        }
    }
}
