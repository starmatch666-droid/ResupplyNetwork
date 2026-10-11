using System;

namespace ResupplyNetwork
{
    /// <summary>
    /// 下单前置条件。目前只有"必须有可用的通信链路"这一条——
    /// 对接口要求已在设计上取消（检查未加载舰船的对接口需要去读存档快照，
    /// 会把存档格式变成依赖，不值得）。
    /// </summary>
    public static class ShipChecks
    {
        /// <summary>返回 null 表示通过，否则返回不能下单的原因（已本地化）。</summary>
        public static string Check(Vessel v)
        {
            ResupplySettings s = ResupplySettings.Instance;
            if (v == null) return Loc.T("#LOC_RN_Err_NoVessel");

            if (s.RequireCommLink)
            {
                // connection 为 null 时无法判断（例如存档关闭了 CommNet），
                // 这种情况放行，避免把玩家卡死。
                if (v.connection != null && !v.connection.IsConnected)
                    return Loc.T("#LOC_RN_Err_NoLink");
            }
            return null;
        }
    }
}
