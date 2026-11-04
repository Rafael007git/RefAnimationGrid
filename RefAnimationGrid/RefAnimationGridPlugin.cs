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
        public const string PluginVersion = "1.0.2";

        private float nextSearchTime = 0f;
        private Camera mainCamera;

        // Config dictionary mapping the exact root prefab names to their respective controllers
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
            // 1. Scene scanning loop (Triggers every 3 seconds natively via MeshRenderers)
            if (Time.time > nextSearchTime)
            {
                nextSearchTime = Time.time + 3f;
                FindAndLinkObjectsOptimized();
            }

            // 2. Front hemisphere input router
            BaseGridController target = FindClosestObjectInFront();
            if (target != null)
            {
                if (Input.GetKeyDown(KeyCode.RightBracket)) target.NextFrame();
                if (Input.GetKeyDown(KeyCode.LeftBracket)) target.PrevFrame();
            }
        }

        private void FindAndLinkObjectsOptimized()
        {
            // Возвращаемся к старой доброй логике прямого поиска по именам корней префабов!
            foreach (var config in ObjectConfig)
            {
                string baseName = config.Key;
                Type controllerType = config.Value;

                // Ищем объект напрямую по имени, как в старой версии мода
                GameObject obj = GameObject.Find($"{baseName}(Clone)") ?? GameObject.Find(baseName);

                if (obj != null)
                {
                    // Если объект найден и на нём ещё нет нужного контроллера — вешаем его
                    if (obj.GetComponent(controllerType) == null)
                    {
                        obj.AddComponent(controllerType);
                        Logger.LogInfo($"[RefAnimationGrid] SUCCESS: Linked {controllerType.Name} to found object: {obj.name}");
                    }
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

                // Hemisphere verification via dot product
                float dot = Vector3.Dot(camForward, toTarget.normalized);
                if (dot > 0f)
                {
                    float distance = toTarget.sqrMagnitude; // Extremely fast check without square roots
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
