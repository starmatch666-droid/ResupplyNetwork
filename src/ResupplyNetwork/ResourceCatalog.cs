using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>资源经济换算：单价、计价密度、排除判断。</summary>
    public static class ResourceCatalog
    {
        public static PartResourceDefinition Definition(string resource)
        {
            if (string.IsNullOrEmpty(resource)) return null;
            var lib = PartResourceLibrary.Instance;
            if (lib == null) return null;
            return lib.GetDefinition(resource);
        }

        public static bool IsExcluded(string resource)
        {
            return ResupplySettings.Instance.Excluded.Contains(resource);
        }

        /// <summary>
        /// 本地化显示名。KSP 会把 RESOURCE_DEFINITION 里的 displayName 解析成本地语言
        /// （原版走 #autoLOC，CommunityResourcePack 等 mod 走自己的 Localization/*.cfg）。
        /// 解析失败或为空时回退到内部名——内部名是订单、存档与规则的唯一键，绝不能改。
        /// </summary>
        public static string DisplayName(string resource)
        {
            if (string.IsNullOrEmpty(resource)) return string.Empty;
            PartResourceDefinition def = Definition(resource);
            if (def != null)
            {
                string dn = def.displayName;
                if (!string.IsNullOrEmpty(dn) && !dn.StartsWith("#", System.StringComparison.Ordinal))
                    return dn;
            }
            return resource;
        }

        /// <summary>单价（资金/单位）= unitCost * 稀缺倍数，或人工覆盖价；不低于最低单价。</summary>
        public static double UnitPrice(string resource, PartResourceDefinition def)
        {
            var s = ResupplySettings.Instance;
            double price;
            double over;
            if (s.UnitPriceOverride.TryGetValue(resource, out over))
            {
                price = over;
            }
            else
            {
                double unitCost = def != null ? def.unitCost : 0.0;
                price = unitCost * s.ScarcityOf(resource);
            }
            return price < s.MinUnitPrice ? s.MinUnitPrice : price;
        }

        /// <summary>
        /// 计入运费的密度（t/单位）。
        /// 只有真正零质量的资源才用兜底值；轻但非零的资源必须按真实密度算，
        /// 否则会把 LqdHydrogen / LqdHe3 这类低温推进剂的运费抬高。
        /// </summary>
        public static double FreightDensity(string resource, PartResourceDefinition def)
        {
            var s = ResupplySettings.Instance;
            double d = def != null ? def.density : 0.0;
            return d > 0.0 ? d : s.MinDensity;
        }
    }
}
