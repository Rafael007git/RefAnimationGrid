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

    // ОБНОВЛЕННЫЙ КОНТРОЛЛЕР СТЕНЫ С РЕЖИМАМИ «PLAY AND SELECT» И «REFERENCE»
    public class WallController : BaseGridController
    {
        protected override List<Texture2D> TargetTextures => TextureRepository.WallTextures;
        private Renderer frontRenderer;
        private Renderer backRenderer;

        // Video fields
        private VideoPlayer videoPlayer;
        private RenderTexture videoRenderTexture;
        private string activeVideoName = "";
        private List<long> savedFrames = new List<long>();
        private int currentKeyframeIndex = 0;
        private Vector3 originalScale;

        // UI & Режимы: false = Play and Select (Mode 1), true = Reference (Mode 2)
        private bool isReferenceMode = false;
        private Rect guiWindowRect = new Rect(40, 40, 300, 160);

        private void Start()
        {
            originalScale = transform.localScale;
            Transform frontCanvas = transform.Find("PlaneV front");
            Transform backCanvas = transform.Find("PlaneV back");
            if (frontCanvas != null) frontRenderer = frontCanvas.GetComponent<Renderer>();
            if (backCanvas != null) backRenderer = backCanvas.GetComponent<Renderer>();

            // Сканируем папку Video на наличие .mp4
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

            // Виртуальный холст под 1080p видеопоток
            videoRenderTexture = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
            videoRenderTexture.Create();
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;

            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            // Умное изменение масштаба холста по ширине под Aspect Ratio видео кадра
            float aspectRatio = (float)source.texture.width / source.texture.height;
            transform.localScale = new Vector3(originalScale.x * aspectRatio, originalScale.y, originalScale.z);

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
                // При остановке в режиме Play And Select пытаемся мягко сопоставить текущий кадр с индексами
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
                // MODE 2: Шагаем только по отобранным кадрам
                currentKeyframeIndex = (currentKeyframeIndex + 1) % savedFrames.Count;
                videoPlayer.frame = savedFrames[currentKeyframeIndex];
            }
            else
            {
                // MODE 1: Мотаем покадрово по всему видео
                videoPlayer.frame = (videoPlayer.frame + 1) % (long)videoPlayer.frameCount;
            }
        }

        public override void PrevFrame()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) { base.PrevFrame(); return; }
            if (videoPlayer.isPlaying) videoPlayer.Pause();

            if (isReferenceMode && savedFrames.Count > 0)
            {
                // MODE 2: Шагаем назад по отобранным кадрам
                currentKeyframeIndex--;
                if (currentKeyframeIndex < 0) currentKeyframeIndex = savedFrames.Count - 1;
                videoPlayer.frame = savedFrames[currentKeyframeIndex];
            }
            else
            {
                // MODE 1: Мотаем покадрово назад по всему видео
                long target = videoPlayer.frame - 1;
                if (target < 0) target = (long)videoPlayer.frameCount - 1;
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

        // ОТРИСОВКА ИНТЕРФЕЙСА ОКНА УПРАВЛЕНИЯ
        private void OnGUI()
        {
            if (!IsVideoMode || videoPlayer == null || !videoPlayer.isPrepared) return;

            // Ограничиваем отрисовку окон: рисуем интерфейс только если игрок стоит лицом к этой стене
            var mainPlugin = FindObjectOfType<Plugin>();
            if (mainPlugin != null && mainPlugin.GetCurrentTarget() != this) return;

            guiWindowRect = GUI.Window(0, guiWindowRect, DrawControlWindow, "Video Projector Panel");
        }

        private void DrawControlWindow(int windowID)
        {
            long currentFrame = videoPlayer.frame;
            bool isSaved = savedFrames.Contains(currentFrame);

            // Строка состояния кадра
            GUILayout.Label($"Video: <b>{activeVideoName}</b>");
            GUILayout.Label($"Current Frame: <b>{currentFrame}</b> / {videoPlayer.frameCount}");
            GUILayout.Label($"Selected Frames Count: <b>{savedFrames.Count}</b>");

            GUILayout.Space(5);

            // Кнопка Добавления / Удаления кадра из списка
            string buttonText = isSaved ? "❌ REMOVE CURRENT FRAME" : "⭐ ADD CURRENT FRAME";
            if (GUILayout.Button(buttonText))
            {
                if (isSaved) savedFrames.Remove(currentFrame);
                else { savedFrames.Add(currentFrame); savedFrames.Sort(); }
                TextureRepository.SaveDatabase(); // Мгновенный авто-сейв базы данных
                SyncKeyframeIndex();
            }

            GUILayout.Space(5);

            // Переключатель основных режимов мода
            string modeLabel = isReferenceMode ? "MODE: [REFERENCE VIEW]" : "MODE: [PLAY AND SELECT]";
            if (GUILayout.Button(modeLabel))
            {
                isReferenceMode = !isReferenceMode;
                if (isReferenceMode) SyncKeyframeIndex();
            }

            GUI.DragWindow(); // Разрешаем перетаскивать окошко по экрану мышкой
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
