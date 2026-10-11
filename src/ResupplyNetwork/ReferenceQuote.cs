using System;
using System.Collections.Generic;

namespace ResupplyNetwork
{
    /// <summary>
    /// 给设置页用的参考报价：把 250 km 轨道上的一个 2.5m 最大油罐加满 LFOX。
    ///
    /// 用的是游戏里最常见的那个罐子：2.5m 的 Jumbo-64，
    /// 2880 单位液体燃料 + 3520 单位氧化剂 = 32 吨推进剂。
    /// 走的是和正式下单完全相同的 CostCore 路径，所以改系数时这行字会实时跟着变。
    /// </summary>
    public static class ReferenceQuote
    {
        private const double RefLiquidFuel = 2880.0;
        private const double RefOxidizer = 3520.0;
        private const string RefBody = "Kerbin";
        private const string RefSituation = "Orbiting";
        private const double FallbackTransferTime = 21600.0;

        public static string Build()
        {
            ResupplySettings s = ResupplySettings.Instance;
            try
            {
                var items = new List<QuoteItem>(2);
                AddItem(items, "LiquidFuel", RefLiquidFuel);
                AddItem(items, "Oxidizer", RefOxidizer);
                if (items.Count == 0) return "参考：读不到 LiquidFuel / Oxidizer 的资源定义";

                double dv = 0.0;
                double transfer = FallbackTransferTime;
                double multiplier = 1.0;
                double hardRate = -1.0;
                for (int i = 0; i < s.Zones.Count; i++)
                {
                    var z = s.Zones[i];
                    if (!string.Equals(z.Body, RefBody, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(z.Situation, RefSituation, StringComparison.OrdinalIgnoreCase)) continue;
                    dv = z.Dv;
                    transfer = z.TransferTime;
                    multiplier = z.RateMultiplier;
                    if (z.HasRate) hardRate = z.Rate;
                    break;
                }

                double rate = hardRate >= 0.0
                    ? hardRate
                    : CostCore.FreightRate(s.KMass, dv, s.DvRef, s.KTime, transfer, s.KPremium) * multiplier;

                MultiQuote q = CostCore.QuoteMulti(
                    items, rate, transfer, s.BTrip, s.STrip, s.SMin, s.TBase);
                if (!q.Ok) return "参考：无法计算（" + q.Reason + "）";

                string line1 = Loc.T("#LOC_RN_Ref_Head", q.TotalMass.ToString("N1"));
                string line2 = Loc.T("#LOC_RN_Ref_Cost", q.Total.ToString("N0"), CostCore.FormatDuration(q.LeadTime));
                string line3 = Loc.T("#LOC_RN_Ref_Rate", rate.ToString("N0"), q.Trips, q.MaterialCost.ToString("N0"));
                string line4 = Loc.T("#LOC_RN_Ref_Rest", q.FreightCost.ToString("N0"), q.ServiceCost.ToString("N0"));
                return line1 + "\n" + line2 + "\n" + line3 + "\n" + line4;
            }
            catch (Exception e)
            {
                return "参考：计算失败 - " + e.Message;
            }
        }

        private static void AddItem(List<QuoteItem> items, string name, double amount)
        {
            PartResourceDefinition def = ResourceCatalog.Definition(name);
            if (def == null) return;
            var it = new QuoteItem();
            it.Resource = name;
            it.Amount = amount;
            it.UnitPrice = ResourceCatalog.UnitPrice(name, def);
            it.Density = ResourceCatalog.FreightDensity(name, def);
            it.FreeCapacity = double.MaxValue;
            items.Add(it);
        }
    }
}
