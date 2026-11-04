using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video; // Критично для работы плеера

namespace RefAnimationGrid
{
    public abstract class BaseGridController : MonoBehaviour
    {
        protected abstract List<Texture2D> TargetTextures { get; }
        protected int currentFrameIndex = 0;
        protected const string SHADER_PROPERTY_NAME = "_UserTex";

        public bool IsVideoMode { get; protected set; } = false;

        public virtual void NextFrame()
        {
            if (IsVideoMode || TargetTextures.Count == 0) return;
            currentFrameIndex = (currentFrameIndex + 1) % TargetTextures.Count;
            ApplyTexture();
        }

        public virtual void PrevFrame()
        {
            if (IsVideoMode || TargetTextures.Count == 0) return;
            currentFrameIndex--;
            if (currentFrameIndex < 0) currentFrameIndex = TargetTextures.Count - 1;
            ApplyTexture();
        }

        public virtual void TogglePlayPause() { }
        protected abstract void ApplyTexture();
    }

    // ОБНОВЛЕННЫЙ КОНТРОЛЛЕР СТЕНЫ (ИСПРАВЛЕНЫ ПРОПОРЦИИ И ДОБАВЛЕНА СУПЕР-ПЕРЕМОТКА)
    public class WallController : BaseGridController
    {
        protected override List<Texture2D> TargetTextures => TextureRepository.WallTextures;
        private Renderer frontRenderer;
        private Renderer backRenderer;

        // Ссылки на трансформы холстов для изолированного масштабирования
        private Transform frontCanvasTransform;
        private Transform backCanvasTransform;

        // Video fields
        private VideoPlayer videoPlayer;
        private RenderTexture videoRenderTexture;
        private string activeVideoName = "";
        private List<long> savedFrames = new List<long>();
        private int currentKeyframeIndex = 0;

        // UI & Режимы: false = Play and Select (Mode 1), true = Reference (Mode 2)
        private bool isReferenceMode = false;
        private Rect guiWindowRect = new Rect(40, 40, 300, 160);

        private void Start()
        {
            Transform frontCanvas = transform.Find("PlaneV front");
            Transform backCanvas = transform.Find("PlaneV back");

            if (frontCanvas != null)
            {
                frontRenderer = frontCanvas.GetComponent<Renderer>();
                frontCanvasTransform = frontCanvas;
            }
            if (backCanvas != null)
            {
                backRenderer = backCanvas.GetComponent<Renderer>();
                backCanvasTransform = backCanvas;
            }

            string[] mp4Files = Directory.GetFiles(TextureRepository.VideoFolder, "*.mp4");
            if (mp4Files.Length > 0)
            {
                IsVideoMode = true;
                string videoPath = mp4Files[0];
                activeVideoName = Path.GetFileNameWithoutExtension(videoPath);
                savedFrames = TextureRepository.GetKeyframesForVideo(activeVideoName);

                SetupVideoPlayer(videoPath);
            }
            else
            {
                IsVideoMode = false;
                ApplyTexture();
            }
        }

        private void SetupVideoPlayer(string path)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = path;
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;

            videoRenderTexture = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
            videoRenderTexture.Create();
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;

            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            float aspectRatio = (float)source.texture.width / source.texture.height;

            // ИСПРАВЛЕНИЕ: корень transform не трогаем! Масштабируем ТОЛЬКО вертикальные холсты.
            // Высота остается строго 2 метра (y = 2), а ширина подстраивается под видео (x = 2 * aspectRatio)
            Vector3 targetCanvasScale = new Vector3(2.0f * aspectRatio, 2.0f, 1.0f);

            if (frontCanvasTransform != null) frontCanvasTransform.localScale = targetCanvasScale;
            if (backCanvasTransform != null) backCanvasTransform.localScale = targetCanvasScale;

            if (frontRenderer != null && frontRenderer.material != null)
                frontRenderer.material.SetTexture(SHADER_PROPERTY_NAME, videoRenderTexture);
            if (backRenderer != null && backRenderer.material != null)
                backRenderer.material.SetTexture(SHADER_PROPERTY_NAME, videoRenderTexture);

            videoPlayer.Pause();
            videoPlayer.frame = 0;
        }

        public override void TogglePlayPause()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) return;

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                SyncKeyframeIndex();
            }
            else
            {
                videoPlayer.Play();
            }
        }

        public override void NextFrame()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) { base.NextFrame(); return; }
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            if (isReferenceMode && savedFrames.Count > 0)
            {
                // MODE 2: Шаг по золотым кадрам
                currentKeyframeIndex = (currentKeyframeIndex + 1) % savedFrames.Count;
                videoPlayer.frame = savedFrames[currentKeyframeIndex];
            }
            else
            {
                // MODE 1: УМНАЯ СУПЕР-ПЕРЕМОТКА ВПЕРЕД
                long step = 1;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 100;
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 500;

                videoPlayer.frame = (videoPlayer.frame + step) % (long)videoPlayer.frameCount;
            }
        }

        public override void PrevFrame()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) { base.PrevFrame(); return; }
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            if (isReferenceMode && savedFrames.Count > 0)
            {
                // MODE 2: Шаг назад по золотым кадрам
                currentKeyframeIndex--;
                if (currentKeyframeIndex < 0) currentKeyframeIndex = savedFrames.Count - 1;
                videoPlayer.frame = savedFrames[currentKeyframeIndex];
            }
            else
            {
                // MODE 1: УМНАЯ СУПЕР-ПЕРЕМОТКА НАЗАД
                long step = 1;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 100;
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 500;

                long target = videoPlayer.frame - step;
                if (target < 0) target = (long)videoPlayer.frameCount + target; // Зацикливание с учетом большого шага
                if (target < 0) target = 0; // Дополнительная защита

                videoPlayer.frame = target;
            }
        }

        private void SyncKeyframeIndex()
        {
            if (savedFrames.Count == 0) return;
            long currentFrame = videoPlayer.frame;
            int closest = 0;
            long minDiff = long.MaxValue;
            for (int i = 0; i < savedFrames.Count; i++)
            {
                long diff = Math.Abs(savedFrames[i] - currentFrame);
                if (diff < minDiff) { minDiff = diff; closest = i; }
            }
            currentKeyframeIndex = closest;
        }

        private void OnGUI()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) return;

            var mainPlugin = FindObjectOfType<Plugin>();
            if (mainPlugin != null && mainPlugin.GetCurrentTarget() != this) return;

            guiWindowRect = GUI.Window(0, guiWindowRect, DrawControlWindow, "Video Projector Panel");
        }

        private void DrawControlWindow(int windowID)
        {
            long currentFrame = videoPlayer.frame;
            bool isSaved = savedFrames.Contains(currentFrame);

            GUILayout.Label($"Video: <b>{activeVideoName}</b>");
            GUILayout.Label($"Current Frame: <b>{currentFrame}</b> / {videoPlayer.frameCount}");
            GUILayout.Label($"Selected Frames Count: <b>{savedFrames.Count}</b>");

            GUILayout.Space(5);

            string buttonText = isSaved ? "❌ REMOVE CURRENT FRAME" : "⭐ ADD CURRENT FRAME";
            if (GUILayout.Button(buttonText))
            {
                if (isSaved) savedFrames.Remove(currentFrame);
                else { savedFrames.Add(currentFrame); savedFrames.Sort(); }
                TextureRepository.SaveDatabase();
                SyncKeyframeIndex();
            }

            GUILayout.Space(5);

            string modeLabel = isReferenceMode ? "MODE: [REFERENCE VIEW]" : "MODE: [PLAY AND SELECT]";
            if (GUILayout.Button(modeLabel))
            {
                isReferenceMode = !isReferenceMode;
                if (isReferenceMode) SyncKeyframeIndex();
            }

            GUI.DragWindow();
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

        private void OnDestroy()
        {
            if (videoRenderTexture != null)
            {
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
            }
        }
    }


    // Контроллеры мольбертов (Остаются без изменений)
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
