using System.Collections.Generic;
using UnityEngine;

namespace RefAnimationGrid
{
    public abstract class BaseGridController : MonoBehaviour
    {
        protected abstract List<Texture2D> TargetTextures { get; }
        protected int currentFrameIndex = 0;
        protected const string SHADER_PROPERTY_NAME = "_UserTex";

        public virtual void NextFrame()
        {
            if (TargetTextures.Count == 0) return;
            currentFrameIndex = (currentFrameIndex + 1) % TargetTextures.Count;
            ApplyTexture();
        }

        public virtual void PrevFrame()
        {
            if (TargetTextures.Count == 0) return;
            currentFrameIndex--;
            if (currentFrameIndex < 0) currentFrameIndex = TargetTextures.Count - 1;
            ApplyTexture();
        }

        protected abstract void ApplyTexture();
    }

    // 1. WALLS CONTROLLER (Strict direct child search for A2/B2 walls)
    public class WallController : BaseGridController
    {
        protected override List<Texture2D> TargetTextures => TextureRepository.WallTextures;
        private Renderer frontRenderer;
        private Renderer backRenderer;

        private void Start()
        {
            Transform frontCanvas = transform.Find("PlaneV front");
            Transform backCanvas = transform.Find("PlaneV back");
            if (frontCanvas != null) frontRenderer = frontCanvas.GetComponent<Renderer>();
            if (backCanvas != null) backRenderer = backCanvas.GetComponent<Renderer>();
            ApplyTexture();
        }

        protected override void ApplyTexture()
        {
            if (TargetTextures.Count == 0) return;
            Texture2D currentTex = TargetTextures[currentFrameIndex];

            if (frontRenderer != null && frontRenderer.material != null && frontRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                frontRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);

            if (backRenderer != null && backRenderer.material != null && backRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                backRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);
        }
    }

    // 2. VERTICAL CONTENT CONTROLLER (Universal auto-find by shader property)
    public class VerticalContentController : BaseGridController
    {
        protected override List<Texture2D> TargetTextures => TextureRepository.VerticalTextures;
        private Renderer targetRenderer;

        private void Start()
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
            foreach (var r in allRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty(SHADER_PROPERTY_NAME))
                {
                    targetRenderer = r;
                    break;
                }
            }

            if (targetRenderer == null)
                Debug.LogError($"[RefAnimationGrid] Vertical canvas target with property '{SHADER_PROPERTY_NAME}' not found in {gameObject.name}!");

            ApplyTexture();
        }

        protected override void ApplyTexture()
        {
            if (TargetTextures.Count == 0 || targetRenderer == null || targetRenderer.material == null) return;
            targetRenderer.material.SetTexture(SHADER_PROPERTY_NAME, TargetTextures[currentFrameIndex]);
        }
    }

    // 3. HORIZONTAL CONTENT CONTROLLER (Universal auto-find by shader property)
    public class HorizontalContentController : BaseGridController
    {
        protected override List<Texture2D> TargetTextures => TextureRepository.HorizontalTextures;
        private Renderer targetRenderer;

        private void Start()
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
            foreach (var r in allRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty(SHADER_PROPERTY_NAME))
                {
                    targetRenderer = r;
                    break;
                }
            }

            if (targetRenderer == null)
                Debug.LogError($"[RefAnimationGrid] Horizontal canvas target with property '{SHADER_PROPERTY_NAME}' not found in {gameObject.name}!");

            ApplyTexture();
        }

        protected override void ApplyTexture()
        {
            if (TargetTextures.Count == 0 || targetRenderer == null || targetRenderer.material == null) return;
            targetRenderer.material.SetTexture(SHADER_PROPERTY_NAME, TargetTextures[currentFrameIndex]);
        }
    }
}
