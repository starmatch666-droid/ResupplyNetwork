using System;
using System.Collections.Generic;
using System.Text;

namespace ResupplyNetwork
{
    /// <summary>报价输入：一种物资及其单价、密度、可用容量。</summary>
    public sealed class QuoteItem
    {
        public string Resource;
        public double Amount;        // 申请数量
        public double UnitPrice;     // 资金/单位（物资）
        public double Density;       // t/单位（运费）
        public double FreeCapacity;  // 目标容器剩余空间
    }

    /// <summary>报价结果中的一行。</summary>
    public sealed class QuoteLine
    {
        public string Resource;
        public double Amount;        // 实际可交付量（可能被容量截断）
        public double Mass;
        public double MaterialCost;
        public bool Capped;
    }

    /// <summary>一次多物资订单的整体报价。</summary>
    public sealed class MultiQuote
    {
        public bool Ok;
        public string Reason;

        public readonly List<QuoteLine> Lines = new List<QuoteLine>();

        public double TotalMass;
        public double MaterialCost;
        public double FreightCost;
        public double ServiceCost;
        public double Total;

        public int Trips;
        public double FreightRate;
        public double TransferTime;
        public double LeadTime;

        public string ZoneName;
        public bool AnyCapped;
    }

    /// <summary>单物资报价（调试热键与离线报告仍在使用）。</summary>
    public struct DeliveryQuote
    {
        public bool Ok;
        public string Reason;

        public string Resource;
        public double Amount;
        public double UsableAmount;
        public double Mass;

        public double MaterialCost;
        public double FreightCost;
        public double ServiceCost;
        public double Total;

        public int Trips;
        public double FreightRate;
        public double TransferTime;
        public double LeadTime;
        public string ZoneName;
        public bool CappedByCapacity;
    }

    /// <summary>
    /// 纯计算层：只依赖 System，不碰任何 KSP 类型。
    /// 价格模型可以被离线工具（tools/PriceReport）直接执行和核对。
    /// </summary>
    public static class CostCore
    {
        public const double KerbinDay = 21600.0;
        public const double KerbinYear = 9203328.0;

        /// <summary>
        /// 一次订单可包含任意多种物资：运费按总质量算，服务费与趟数按总质量算，
        /// 交付时间是"一个批次"的时间，而不是每种物资各发一次。
        /// </summary>
        public static MultiQuote QuoteMulti(
            IList<QuoteItem> items,
            double freightRate,
            double transferTime,
            double bTrip,
            double sTrip,
            double sMin,
            double tBase)
        {
            var q = new MultiQuote();
            q.FreightRate = freightRate;
            q.TransferTime = transferTime;

            if (items == null || items.Count == 0) { q.Reason = "清单是空的"; return q; }

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || string.IsNullOrEmpty(it.Resource)) continue;
                if (it.Amount <= 0.0) continue;

                double usable = it.Amount;
                bool capped = false;
                if (it.FreeCapacity < usable) { usable = it.FreeCapacity; capped = true; }
                if (usable <= 0.0) continue;

                double density = it.Density > 0.0 ? it.Density : 0.0;
                var line = new QuoteLine();
                line.Resource = it.Resource;
                line.Amount = usable;
                line.Mass = usable * density;
                line.MaterialCost = usable * it.UnitPrice;
                line.Capped = capped;
                q.Lines.Add(line);

                q.TotalMass += line.Mass;
                q.MaterialCost += line.MaterialCost;
                q.AnyCapped |= capped;
            }

            if (q.Lines.Count == 0) { q.Reason = "清单里没有可交付的物资（可能容器已满）"; return q; }

            q.FreightCost = q.TotalMass * freightRate;

            int trips = (int)Math.Ceiling(q.TotalMass / Math.Max(1e-9, bTrip));
            if (trips < 1) trips = 1;
            q.Trips = trips;
            q.ServiceCost = trips * sTrip;

            double total = q.MaterialCost + q.FreightCost + q.ServiceCost;
            if (total < sMin) total = sMin;
            q.Total = total;

            q.LeadTime = tBase + trips * transferTime;
            q.Ok = true;
            return q;
        }

        public static DeliveryQuote Quote(
            string resource,
            double requestedAmount,
            double freeCapacity,
            double density,
            double unitPrice,
            double freightRate,
            double transferTime,
            double bTrip,
            double sTrip,
            double sMin,
            double tBase)
        {
            var one = new List<QuoteItem>(1);
            one.Add(new QuoteItem
            {
                Resource = resource,
                Amount = requestedAmount,
                UnitPrice = unitPrice,
                Density = density,
                FreeCapacity = freeCapacity,
            });

            var m = QuoteMulti(one, freightRate, transferTime, bTrip, sTrip, sMin, tBase);

            var q = new DeliveryQuote();
            q.Ok = m.Ok;
            q.Reason = m.Reason;
            q.Resource = resource;
            q.Amount = requestedAmount;
            q.FreightRate = freightRate;
            q.TransferTime = transferTime;
            q.Trips = m.Trips;
            q.MaterialCost = m.MaterialCost;
            q.FreightCost = m.FreightCost;
            q.ServiceCost = m.ServiceCost;
            q.Total = m.Total;
            q.LeadTime = m.LeadTime;
            q.ZoneName = m.ZoneName;
            if (m.Ok && m.Lines.Count > 0)
            {
                q.UsableAmount = m.Lines[0].Amount;
                q.Mass = m.Lines[0].Mass;
                q.CappedByCapacity = m.Lines[0].Capped;
            }
            return q;
        }

        /// <summary>运费率 = K_mass * (1 + dv/dv_ref) * (1 + K_time * 年数) * K_premium</summary>
        public static double FreightRate(double kMass, double dvPark, double dvRef,
                                         double kTime, double transferTime, double kPremium)
        {
            double years = transferTime / KerbinYear;
            return kMass * (1.0 + dvPark / dvRef) * (1.0 + kTime * years) * kPremium;
        }

        // ------------------------------------------------------------------
        //  定时补给：规则引擎的判定部分（纯函数，可离线验证）
        // ------------------------------------------------------------------

        /// <summary>
        /// 是否到了下一次检查时间。用显式标志表示"是否检查过"，
        /// 不能用 lastCheckUT &lt;= 0 当哨兵——新存档的 UT 恰好就是 0，会导致每帧都触发。
        /// </summary>
        public static bool ShouldCheck(double nowUT, double lastCheckUT, bool hasChecked, double interval)
        {
            if (interval <= 0.0) return false;
            if (!hasChecked) return true;
            return nowUT - lastCheckUT >= interval;
        }

        /// <summary>
        /// 余量低于阈值线（按容量比例）时，返回到目标存量所需的数量；否则返回 0。
        /// 这模拟现实里的补给飞船：只有在物资掉到线以下才发船，而不是随时待命。
        /// </summary>
        public static double TopUpAmount(double current, double capacity, double thresholdFraction, double targetFraction)
        {
            if (capacity <= 0.0) return 0.0;

            if (thresholdFraction < 0.0) thresholdFraction = 0.0;
            if (thresholdFraction > 1.0) thresholdFraction = 1.0;
            if (targetFraction < 0.0) targetFraction = 0.0;
            if (targetFraction > 1.0) targetFraction = 1.0;

            if (current / capacity >= thresholdFraction) return 0.0;

            double want = capacity * targetFraction - current;
            return want > 0.0 ? want : 0.0;
        }

        public static string Describe(DeliveryQuote q)
        {
            if (!q.Ok) return "无法报价: " + q.Reason;
            return string.Format(
                "{0} {1:N2} 单位 | {2:N3} t | 物资 {3:N0} + 运费 {4:N0} + 服务 {5:N0} = {6:N0} 资金 | {7} 趟 | 交付 {8}",
                q.Resource, q.UsableAmount, q.Mass, q.MaterialCost, q.FreightCost, q.ServiceCost, q.Total,
                q.Trips, FormatDuration(q.LeadTime));
        }

        public static string DescribeMulti(MultiQuote q)
        {
            if (!q.Ok) return "无法报价: " + q.Reason;
            var sb = new StringBuilder();
            sb.Append(q.Lines.Count).Append(" 种物资 | 合计 ").Append(q.TotalMass.ToString("N3")).Append(" t | ");
            sb.Append("物资 ").Append(q.MaterialCost.ToString("N0"));
            sb.Append(" + 运费 ").Append(q.FreightCost.ToString("N0"));
            sb.Append(" + 服务 ").Append(q.ServiceCost.ToString("N0"));
            sb.Append(" = ").Append(q.Total.ToString("N0")).Append(" 资金 | ");
            sb.Append(q.Trips).Append(" 趟 | 交付 ").Append(FormatDuration(q.LeadTime));
            return sb.ToString();
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds >= KerbinYear) return (seconds / KerbinYear).ToString("N2") + " 年";
            double days = seconds / KerbinDay;
            if (days >= 1.0) return days.ToString("N2") + " 天";
            return (seconds / 3600.0).ToString("N2") + " 小时";
        }
    }
}
