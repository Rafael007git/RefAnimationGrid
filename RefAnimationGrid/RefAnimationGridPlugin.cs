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

        private string imagesFolder;
        private List<Texture2D> loadedTextures = new List<Texture2D>();
        private int currentFrameIndex = 0;

        private GameObject activeWall;
        private Renderer frontRenderer;
        private Renderer backRenderer;

        private const string SHADER_PROPERTY_NAME = "_UserTex";

        // Timer to avoid spamming the log every frame
        private float nextLogTime = 0f;

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
            // Radar search for the spawned wall object
            if (activeWall == null)
            {
                // METHOD A: Try to find by name in the scene (safer for Mod Tool objects)
                GameObject wallObj = GameObject.Find("Animation grid A2(Clone)") ?? GameObject.Find("Animation grid A2");

                if (wallObj != null)
                {
                    Logger.LogInfo($"[RefAnimationGrid] Found wall object by name: {wallObj.name}");
                    InitializeWall(wallObj);
                }
                else
                {
                    // Periodic log tracking (once every 5 seconds) so it won't flood the console
                    if (Time.time > nextLogTime)
                    {
                        Logger.LogInfo("[RefAnimationGrid] Searching for 'Animation grid A2' in the scene...");
                        nextLogTime = Time.time + 5f;
                    }
                }
            }

            if (activeWall == null || frontRenderer == null || backRenderer == null || loadedTextures.Count == 0) return;

            // Frame switching controls with explicit feedback logs
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                currentFrameIndex = (currentFrameIndex + 1) % loadedTextures.Count;
                Logger.LogInfo($"[RefAnimationGrid] Next key pressed. Target index: {currentFrameIndex}");
                ApplyCurrentTexture();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                currentFrameIndex--;
                if (currentFrameIndex < 0) currentFrameIndex = loadedTextures.Count - 1;
                Logger.LogInfo($"[RefAnimationGrid] Prev key pressed. Target index: {currentFrameIndex}");
                ApplyCurrentTexture();
            }
        }

        private void InitializeWall(GameObject wallObj)
        {
            activeWall = wallObj;

            // Searching strictly for child objects PlaneV front and PlaneV back
            Transform frontCanvas = activeWall.transform.Find("PlaneV front");
            Transform backCanvas = activeWall.transform.Find("PlaneV back");

            if (frontCanvas != null && backCanvas != null)
            {
                frontRenderer = frontCanvas.GetComponent<Renderer>();
                backRenderer = backCanvas.GetComponent<Renderer>();

                Logger.LogInfo("[RefAnimationGrid] Success: Meshes 'PlaneV front' and 'PlaneV back' linked successfully!");
                ApplyCurrentTexture();
            }
            else
            {
                Logger.LogError("[RefAnimationGrid] Error: Failed to find child meshes 'PlaneV front' or 'PlaneV back' inside the root object!");
                // Reset so it tries to find it again if hierarchy wasn't ready
                activeWall = null;
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

        private void ApplyCurrentTexture()
        {
            if (frontRenderer == null || backRenderer == null || loadedTextures.Count == 0) return;

            Texture2D currentTex = loadedTextures[currentFrameIndex];
            Logger.LogInfo($"[RefAnimationGrid] Applying texture index [{currentFrameIndex}] to materials...");

            // Checking Shader Graph property presence with logs
            if (frontRenderer.material != null)
            {
                if (frontRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                {
                    frontRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);
                    Logger.LogInfo("[RefAnimationGrid] Texture sent to PlaneV front (_UserTex)");
                }
                else
                {
                    Logger.LogWarning($"[RefAnimationGrid] Warning: PlaneV front material does NOT have property '{SHADER_PROPERTY_NAME}'!");
                }
            }

            if (backRenderer.material != null)
            {
                if (backRenderer.material.HasProperty(SHADER_PROPERTY_NAME))
                {
                    backRenderer.material.SetTexture(SHADER_PROPERTY_NAME, currentTex);
                    Logger.LogInfo("[RefAnimationGrid] Texture sent to PlaneV back (_UserTex)");
                }
                else
                {
                    Logger.LogWarning($"[RefAnimationGrid] Warning: PlaneV back material does NOT have property '{SHADER_PROPERTY_NAME}'!");
                }
            }
        }
    }
}
