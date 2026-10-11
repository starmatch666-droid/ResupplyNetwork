using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>Kerbin 系目的地覆盖项（来自 cfg）。</summary>
    public sealed class ZoneOverride
    {
        public string Body;
        public string Situation;
        public double Dv;
        public double TransferTime;
        public double Rate;
        public bool HasRate;
    }

    /// <summary>跨恒星系 / 虫洞路线覆盖项。</summary>
    public sealed class RouteOverride
    {
        public string From;
        public string To;
        public double Rate;
        public double TransferTime;
    }

    public sealed class ResupplySettings
    {
        // ---- 全局系数 ----
        public double KMass = 4000.0;
        public double DvRef = 1000.0;
        public double KTime = 0.5;
        public double KPremium = 1.0;
        public double BTrip = 20.0;
        public double STrip = 800.0;
        public double SMin = 1000.0;
        public double TBase = 7200.0;
        public double CancelRefund = 0.8;
        // ---- 定时补给默认值 ----
        public double AutoThreshold = 0.3;      // 余量低于容量的 30% 才发船
        public double AutoTarget = 1.0;         // 补到容量的 100%
        public double AutoIntervalDays = 30.0;  // 每 30 Kerbin 日检查一次
        public double AutoMaxFunds = 50000.0;   // 单笔预算上限，<=0 表示不限
        public double MinDensity = 0.0001;
        public double MinUnitPrice = 0.01;
        public bool AutoComputeZones = true;

        public readonly Dictionary<string, double> UnitPriceOverride = new Dictionary<string, double>();
        public readonly Dictionary<string, double> Scarcity = new Dictionary<string, double>();
        public readonly HashSet<string> Excluded = new HashSet<string>();
        public readonly List<ZoneOverride> Zones = new List<ZoneOverride>();
        public readonly List<RouteOverride> Routes = new List<RouteOverride>();

        private static ResupplySettings _instance;

        public static ResupplySettings Instance
        {
            get
            {
                if (_instance == null) Load();
                return _instance;
            }
        }

        public static void Load()
        {
            var s = new ResupplySettings();
            try
            {
                if (GameDatabase.Instance != null)
                {
                    ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("RESUPPLY_CONFIG");
                    if (nodes != null && nodes.Length > 0)
                    {
                        s.Parse(nodes[0]);
                        Debug.Log("[ResupplyNetwork] 设置已加载: 覆盖" + s.UnitPriceOverride.Count
                                  + " 溢价" + s.Scarcity.Count
                                  + " 排除" + s.Excluded.Count
                                  + " 目的地" + s.Zones.Count
                                  + " 路线" + s.Routes.Count);
                    }
                    else
                    {
                        Debug.LogWarning("[ResupplyNetwork] 找不到 RESUPPLY_CONFIG，使用内置默认值");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ResupplyNetwork] 设置加载失败，使用默认值: " + e);
            }
            _instance = s;
        }

        private void Parse(ConfigNode n)
        {
            KMass = GetD(n, "K_mass", KMass);
            DvRef = GetD(n, "dv_ref", DvRef);
            KTime = GetD(n, "K_time", KTime);
            KPremium = GetD(n, "K_premium", KPremium);
            BTrip = GetD(n, "B_trip", BTrip);
            STrip = GetD(n, "S_trip", STrip);
            SMin = GetD(n, "S_min", SMin);
            TBase = GetD(n, "T_base", TBase);
            CancelRefund = GetD(n, "cancelRefund", CancelRefund);
            AutoThreshold = GetD(n, "autoThreshold", AutoThreshold);
            AutoTarget = GetD(n, "autoTarget", AutoTarget);
            AutoIntervalDays = GetD(n, "autoIntervalDays", AutoIntervalDays);
            AutoMaxFunds = GetD(n, "autoMaxFunds", AutoMaxFunds);
            MinDensity = GetD(n, "minDensity", MinDensity);
            MinUnitPrice = GetD(n, "minUnitPrice", MinUnitPrice);

            string ac = n.GetValue("autoComputeZones");
            if (!string.IsNullOrEmpty(ac)) AutoComputeZones = ac.Trim().ToLowerInvariant() != "false";

            foreach (var c in n.GetNodes("UNIT_PRICE_OVERRIDE"))
            {
                string name = c.GetValue("name");
                if (!string.IsNullOrEmpty(name)) UnitPriceOverride[name.Trim()] = GetD(c, "price", 0.0);
            }
            foreach (var c in n.GetNodes("SCARCITY"))
            {
                string name = c.GetValue("name");
                if (!string.IsNullOrEmpty(name)) Scarcity[name.Trim()] = GetD(c, "multiplier", 1.0);
            }
            foreach (var c in n.GetNodes("EXCLUDE"))
            {
                string name = c.GetValue("name");
                if (!string.IsNullOrEmpty(name)) Excluded.Add(name.Trim());
            }
            foreach (var c in n.GetNodes("ZONE_OVERRIDE"))
            {
                var z = new ZoneOverride();
                z.Body = (c.GetValue("body") ?? string.Empty).Trim();
                z.Situation = (c.GetValue("situation") ?? string.Empty).Trim();
                z.Dv = GetD(c, "dv", 0.0);
                z.TransferTime = GetD(c, "transferTime", TBase);
                string rate = c.GetValue("rate");
                if (!string.IsNullOrEmpty(rate)) { z.Rate = GetD(c, "rate", 0.0); z.HasRate = true; }
                Zones.Add(z);
            }
            foreach (var c in n.GetNodes("ROUTE_OVERRIDE"))
            {
                var r = new RouteOverride();
                r.From = (c.GetValue("from") ?? string.Empty).Trim();
                r.To = (c.GetValue("to") ?? string.Empty).Trim();
                r.Rate = GetD(c, "rate", 0.0);
                r.TransferTime = GetD(c, "transferTime", 0.0);
                Routes.Add(r);
            }
        }

        private static double GetD(ConfigNode n, string key, double fallback)
        {
            string v = n.GetValue(key);
            if (string.IsNullOrEmpty(v)) return fallback;
            double d;
            if (double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        public double ScarcityOf(string resource)
        {
            double m;
            return Scarcity.TryGetValue(resource, out m) ? m : 1.0;
        }
    }
}
