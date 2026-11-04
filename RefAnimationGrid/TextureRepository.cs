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

        public static string VideoFolder { get; private set; }
        private static string jsonPath;
        private static Dictionary<string, List<long>> keyframesDB = new Dictionary<string, List<long>>();

        public static void Init()
        {
            string pluginsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string wallsFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Walls");
            string vertFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Vertical");
            string horizFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Horizontal");
            VideoFolder = Path.Combine(pluginsDir, "RefAnimationGrid", "Video");
            jsonPath = Path.Combine(VideoFolder, "keyframes_db.json");

            LoadFolder(wallsFolder, WallTextures);
            LoadFolder(vertFolder, VerticalTextures);
            LoadFolder(horizFolder, HorizontalTextures);

            if (!Directory.Exists(VideoFolder)) Directory.CreateDirectory(VideoFolder);
            LoadJSONDatabase();
        }

        private static void LoadFolder(string path, List<Texture2D> targetList)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            targetList.Clear();
            string[] files = Directory.GetFiles(path, "*.png");
            Array.Sort(files);

            foreach (string file in files)
            {
                byte[] fileData = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData)) targetList.Add(tex);
            }
        }

        // БЕЗОПАСНЫЙ ЛЕГКИЙ JSON ПАРСЕР (Без внешних зависимостей)
        private static void LoadJSONDatabase()
        {
            keyframesDB.Clear();
            if (!File.Exists(jsonPath)) return;

            try
            {
                string json = File.ReadAllText(jsonPath);
                // Простейший ручной парсинг структуры "имя": [кадры]
                string[] entries = json.Split(new string[] { "]," }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    string[] kv = entry.Split(':');
                    if (kv.Length < 2) continue;
                    string key = kv[0].Replace("\"", "").Replace("{", "").Replace("}", "").Trim();
                    string vals = kv[1].Replace("[", "").Replace("]", "").Replace("}", "").Trim();

                    List<long> frames = new List<long>();
                    if (!string.IsNullOrEmpty(vals))
                    {
                        foreach (var v in vals.Split(','))
                        {
                            if (long.TryParse(v.Trim(), out long f)) frames.Add(f);
                        }
                    }
                    keyframesDB[key] = frames;
                }
            }
            catch (Exception e) { Debug.LogError($"[RefAnimationGrid] JSON Load Error: {e.Message}"); }
        }

        public static List<long> GetKeyframesForVideo(string videoName)
        {
            if (!keyframesDB.ContainsKey(videoName)) keyframesDB[videoName] = new List<long>();
            return keyframesDB[videoName];
        }

        public static void SaveDatabase()
        {
            try
            {
                List<string> jsonParts = new List<string>();
                foreach (var kvp in keyframesDB)
                {
                    string framesStr = string.Join(", ", kvp.Value);
                    jsonParts.Add($"  \"{kvp.Key}\": [{framesStr}]");
                }
                string fullJson = "{\n" + string.Join(",\n", jsonParts) + "\n}";
                File.WriteAllText(jsonPath, fullJson);
            }
            catch (Exception e) { Debug.LogError($"[RefAnimationGrid] JSON Save Error: {e.Message}"); }
        }
    }
}
