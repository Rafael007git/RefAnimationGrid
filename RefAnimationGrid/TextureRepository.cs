using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RefAnimationGrid
{
    public static class TextureRepository
    {
        public static List<Texture2D> WallTextures { get; private set; } = new List<Texture2D>();
        public static List<Texture2D> VerticalTextures { get; private set; } = new List<Texture2D>();
        public static List<Texture2D> HorizontalTextures { get; private set; } = new List<Texture2D>();

        public static void Init()
        {
            string pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string wallsFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Walls");
            string vertFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Vertical");
            string horizFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Horizontal");

            LoadFolder(wallsFolder, WallTextures);
            LoadFolder(vertFolder, VerticalTextures);
            LoadFolder(horizFolder, HorizontalTextures);
        }

        private static void LoadFolder(string path, List<Texture2D> targetList)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            targetList.Clear();
            string[] files = Directory.GetFiles(path, "*.png");
            Array.Sort(files);

            foreach (string file in files)
            {
                byte[] fileData = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData))
                {
                    targetList.Add(tex);
                }
            }
            Debug.Log($"[RefAnimationGrid] Repository loaded {targetList.Count} images from {Path.GetFileName(path)}");
        }
    }
}
