using System;
using System.Globalization;
using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>窗口位置与尺寸的持久化（写在 PluginData 下，跨存档共用）。</summary>
    public static class WindowLayout
    {
        private static string Path
        {
            get
            {
                return System.IO.Path.Combine(
                    KSPUtil.ApplicationRootPath, "GameData", "ResupplyNetwork", "PluginData", "WindowLayout.cfg");
            }
        }

        public static void Load(string prefix, ref Rect rect, float minW, float minH, float maxW, float maxH)
        {
            try
            {
                if (!System.IO.File.Exists(Path)) return;
                ConfigNode root = ConfigNode.Load(Path);
                if (root == null) return;
                string data = root.GetValue(prefix);
                if (string.IsNullOrEmpty(data)) return;

                string[] parts = data.Split(',');
                if (parts.Length != 4) return;
                float x, y, w, h;
                if (!Try(parts[0], out x) || !Try(parts[1], out y) ||
                    !Try(parts[2], out w) || !Try(parts[3], out h)) return;

                rect = new Rect(x, y, Mathf.Clamp(w, minW, maxW), Mathf.Clamp(h, minH, maxH));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResupplyNetwork] 读取窗口布局失败: " + e.Message);
            }
        }

        public static void Save(string prefix, Rect rect)
        {
            try
            {
                ConfigNode root = System.IO.File.Exists(Path) ? ConfigNode.Load(Path) : new ConfigNode();
                if (root == null) root = new ConfigNode();
                root.SetValue(prefix,
                    rect.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                    rect.y.ToString("R", CultureInfo.InvariantCulture) + "," +
                    rect.width.ToString("R", CultureInfo.InvariantCulture) + "," +
                    rect.height.ToString("R", CultureInfo.InvariantCulture), true);
                root.Save(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResupplyNetwork] 保存窗口布局失败: " + e.Message);
            }
        }

        private static bool Try(string s, out float v)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
