using KSP.Localization;

namespace ResupplyNetwork
{
    /// <summary>
    /// 本地化查询。键放在 GameData/ResupplyNetwork/Localization/*.cfg，
    /// 没有对应翻译时回退成键名本身（方便一眼看出漏了哪条）。
    /// </summary>
    public static class Loc
    {
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string s = Localizer.Format(key);
            return string.IsNullOrEmpty(s) ? key : s;
        }

        public static string T(string key, params object[] args)
        {
            string fmt = T(key);
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }
    }
}
