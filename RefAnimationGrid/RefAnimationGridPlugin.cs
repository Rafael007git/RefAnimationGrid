using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace RefAnimationGrid
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("bugerry.ModToolExtension")]
    [BepInProcess("She Will Punish Them.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.lorifel.refanimationgrid";
        public const string PluginName = "Reference Animation Grid";
        public const string PluginVersion = "1.0.0";

        private static string imagesFolder;
        private static List<Texture2D> loadedTextures = new List<Texture2D>();
        private float nextSearchTime = 0f;

        private void Awake()
        {
            string pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            imagesFolder = Path.Combine(pluginsDir, "RefAnimationGrid");

            if (!Directory.Exists(imagesFolder))
            {
                Directory.CreateDirectory(imagesFolder);
            }

            LoadAllPNGs();
        }

        private void Update()
        {
            // Сканируем сцену раз в 3 секунды на появление ЛЮБОЙ из стен
            if (Time.time > nextSearchTime)
            {
                nextSearchTime = Time.time + 3f;

                // Ищем префаб А2
                GameObject wallA2 = GameObject.Find("Animation grid A2(Clone)") ?? GameObject.Find("Animation grid A2");
                if (wallA2 != null && wallA2.GetComponent<WallController>() == null)
                {
                    wallA2.AddComponent<WallController>();
                    Logger.LogInfo("[RefAnimationGrid] Linked controller to Animation grid A2");
                }

                // Ищем префаб B2
                GameObject wallB2 = GameObject.Find("Animation grid B2(Clone)") ?? GameObject.Find("Animation grid B2");
                if (wallB2 != null && wallB2.GetComponent<WallController>() == null)
                {
                    wallB2.AddComponent<WallController>();
                    Logger.LogInfo("[RefAnimationGrid] Linked controller to Animation grid B2");
                }
            }
        }

        private void LoadAllPNGs()
        {
            loadedTextures.Clear();
            if (!Directory.Exists(imagesFolder)) return;

            string[] files = Directory.GetFiles(imagesFolder, "*.png");
            Array.Sort(files);

            foreach (string file in files)
            {
                byte[] fileData = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);

                if (tex.LoadImage(fileData))
                {
                    loadedTextures.Add(tex);
                }
            }
            Logger.LogInfo($"[RefAnimationGrid] Successfully loaded reference frames count: {loadedTextures.Count}");
        }

        // Внутренний динамический контроллер для конкретной стены
        public class WallController : MonoBehaviour
        {
            private Renderer frontRenderer;
            private Renderer backRenderer;
            private int currentFrameIndex = 0;
            private const string SHADER_PROPERTY_NAME = "_UserTex";

            private void Start()
            {
                Transform frontCanvas = transform.Find("PlaneV front");
                Transform backCanvas = transform.Find("PlaneV back");

                if (frontCanvas != null && backCanvas != null)
                {
                    frontRenderer = frontCanvas.GetComponent<Renderer>();
                    backRenderer = backCanvas.GetComponent<Renderer>();
                    ApplyTexture();
                }
            }

            private void Update()
            {
                if (frontRenderer == null || backRenderer == null || loadedTextures.Count == 0) return;

                // Перелистывание кадров (работает синхронно или независимо для каждой заспавненной стены)
                if (Input.GetKeyDown(KeyCode.RightBracket))
                {
                    currentFrameIndex = (currentFrameIndex + 1) % loadedTextures.Count;
                    ApplyTexture();
                }
                if (Input.GetKeyDown(KeyCode.LeftBracket))
                {
                    currentFrameIndex--;
                    if (currentFrameIndex < 0) currentFrameIndex = loadedTextures.Count - 1;
                    ApplyTexture();
                }
            }

            private void ApplyTexture()
            {
                if (loadedTextures.Count == 0) return;
                Texture2D currentTex = loadedTextures[currentFrameIndex];

                if (frontRenderer != null && frontRenderer.material != null && frontRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                    frontRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);

                if (backRenderer != null && backRenderer.material != null && backRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                    backRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);
            }
        }
    }
}
