using System.Collections.Generic;

namespace ResupplyNetwork
{
    /// <summary>清单里的一项：物资名 + 数量。</summary>
    public sealed class CartItem
    {
        public string Resource;
        public double Amount;
    }

    /// <summary>把 KSP 侧的资源定义 / 目的地换算好，再交给纯计算层 CostCore。</summary>
    public static class CostModel
    {
        /// <summary>多物资一次性报价。排除表里的物资会被跳过。</summary>
        public static MultiQuote QuoteMulti(Vessel vessel, IList<CartItem> cart)
        {
            var s = ResupplySettings.Instance;
            if (vessel == null) return Fail("没有目标舰船");
            if (cart == null || cart.Count == 0) return Fail("清单是空的");

            DestinationZone zone = ZoneCalculator.Resolve(vessel);

            var items = new List<QuoteItem>(cart.Count);
            for (int i = 0; i < cart.Count; i++)
            {
                var c = cart[i];
                if (c == null || string.IsNullOrEmpty(c.Resource)) continue;
                if (c.Amount <= 0.0) continue;
                if (ResourceCatalog.IsExcluded(c.Resource)) continue;

                PartResourceDefinition def = ResourceCatalog.Definition(c.Resource);
                if (def == null) continue;

                var it = new QuoteItem();
                it.Resource = c.Resource;
                it.Amount = c.Amount;
                it.UnitPrice = ResourceCatalog.UnitPrice(c.Resource, def);
                it.Density = ResourceCatalog.FreightDensity(c.Resource, def);
                it.FreeCapacity = VesselResources.GetFreeCapacity(vessel, c.Resource);
                items.Add(it);
            }

            if (items.Count == 0) return Fail("清单里没有可交付的物资（可能被排除表过滤或数量为 0）");

            MultiQuote q = CostCore.QuoteMulti(
                items, zone.FreightRate, zone.TransferTime,
                s.BTrip, s.STrip, s.SMin, s.TBase);
            q.ZoneName = zone.Name;
            return q;
        }

        private static MultiQuote Fail(string reason)
        {
            var q = new MultiQuote();
            q.Reason = reason;
            return q;
        }

        public static DeliveryQuote Quote(Vessel vessel, string resource, double amount)
        {
            var s = ResupplySettings.Instance;
            var q = new DeliveryQuote();
            q.Resource = resource;
            q.Amount = amount;

            if (vessel == null) { q.Reason = "没有目标舰船"; return q; }
            if (string.IsNullOrEmpty(resource)) { q.Reason = "没有选择资源"; return q; }
            if (amount <= 0.0) { q.Reason = "数量必须大于 0"; return q; }
            if (ResourceCatalog.IsExcluded(resource)) { q.Reason = "\"" + resource + "\" 在排除表里"; return q; }

            PartResourceDefinition def = ResourceCatalog.Definition(resource);
            if (def == null) { q.Reason = "找不到资源定义: " + resource; return q; }

            double free = VesselResources.GetFreeCapacity(vessel, resource);
            DestinationZone zone = ZoneCalculator.Resolve(vessel);
            DeliveryQuote result = CostCore.Quote(
                resource, amount, free,
                ResourceCatalog.FreightDensity(resource, def),
                ResourceCatalog.UnitPrice(resource, def),
                zone.FreightRate, zone.TransferTime,
                s.BTrip, s.STrip, s.SMin, s.TBase);
            result.ZoneName = zone.Name;
            return result;
        }

        public static string Describe(DeliveryQuote q) { return CostCore.Describe(q); }

        public static string FormatDuration(double seconds) { return CostCore.FormatDuration(seconds); }
    }
}
