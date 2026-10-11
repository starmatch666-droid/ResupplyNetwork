using KSP.Localization;

namespace ResupplyNetwork
{
    /// <summary>
    /// 本地化查询。键放在 GameData/ResupplyNetwork/Localization/*.cfg，
    /// 没有对应翻译时回退成键名本身（方便一眼看出漏了哪条）。
    /// </summary>
    public static class Loc
    {
        public static string T(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            // 注意：本地化文件里的占位符必须写成 KSP 原生的 <<1>> <<2>>（1 起），
            // 不能用 {0} —— 原版字典里一个 {0} 都没有，KSP 遇到 '{' 会把后面整段丢掉。
            bool hasArgs = args != null && args.Length > 0;
            string s;
            try
            {
                s = hasArgs ? Localizer.Format(key, args) : Localizer.Format(key);
            }
            catch
            {
                return key;
            }
            return string.IsNullOrEmpty(s) ? key : s;
        }
    }
}
