using System;
using System.Collections.Generic;
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
        public const string PluginVersion = "1.0.4";

        private float nextSearchTime = 0f;
        private Camera mainCamera;
        private BaseGridController currentTargetInFront;

        private static readonly Dictionary<string, Type> ObjectConfig = new Dictionary<string, Type>
        {
            { "Animation grid A2", typeof(WallController) },
            { "Animation grid B2", typeof(WallController) },
            { "Painting vertical", typeof(VerticalContentController) },
            { "Painting horizontal", typeof(HorizontalContentController) }
        };

        private void Awake()
        {
            TextureRepository.Init();
        }

        private void Update()
        {
            if (Time.time > nextSearchTime)
            {
                nextSearchTime = Time.time + 3f;
                FindAndLinkObjectsOptimized();
            }

            // Рассчитываем цель в полусфере один раз за кадр
            currentTargetInFront = FindClosestObjectInFront();

            if (currentTargetInFront != null)
            {
                if (Input.GetKeyDown(KeyCode.RightBracket)) currentTargetInFront.NextFrame();
                if (Input.GetKeyDown(KeyCode.LeftBracket)) currentTargetInFront.PrevFrame();

                // Клавиша P для запуска / паузы видеопотока на стене
                if (Input.GetKeyDown(KeyCode.P)) currentTargetInFront.TogglePlayPause();
            }
        }

        // Позволяет окну OnGUI узнать, видит ли игрок этот конкретный холст
        public BaseGridController GetCurrentTarget()
        {
            return currentTargetInFront;
        }

        private void FindAndLinkObjectsOptimized()
        {
            foreach (var config in ObjectConfig)
            {
                string baseName = config.Key;
                Type controllerType = config.Value;

                GameObject obj = GameObject.Find($"{baseName}(Clone)") ?? GameObject.Find(baseName);
                if (obj != null && obj.GetComponent(controllerType) == null)
                {
                    obj.AddComponent(controllerType);
                    Logger.LogInfo($"[RefAnimationGrid] SUCCESS: Linked {controllerType.Name} to object: {obj.name}");
                }
            }
        }

        private BaseGridController FindClosestObjectInFront()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;

            BaseGridController[] allControllers = FindObjectsOfType<BaseGridController>();
            BaseGridController closestTarget = null;
            float minDistance = float.MaxValue;

            Vector3 camPos = mainCamera.transform.position;
            Vector3 camForward = mainCamera.transform.forward;

            foreach (var controller in allControllers)
            {
                if (controller == null) continue;

                Vector3 targetPos = controller.transform.position;
                Vector3 toTarget = targetPos - camPos;

                float dot = Vector3.Dot(camForward, toTarget.normalized);
                if (dot > 0f)
                {
                    float distance = toTarget.sqrMagnitude;
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestTarget = controller;
                    }
                }
            }
            return closestTarget;
        }
    }
}