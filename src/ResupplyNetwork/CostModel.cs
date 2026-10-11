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
        public static MultiQuote QuoteMulti(Vessel vessel, IList<CartItem> cart)
        {
            return QuoteMulti(vessel, cart, false);
        }

        /// <summary>多物资一次性报价。排除表里的物资会被跳过。rush = 加急订单。</summary>
        public static MultiQuote QuoteMulti(Vessel vessel, IList<CartItem> cart, bool rush)
        {
            var s = ResupplySettings.Instance;
            if (vessel == null) return Fail(Loc.T("#LOC_RN_Err_NoVessel"));
            if (cart == null || cart.Count == 0) return Fail(Loc.T("#LOC_RN_Err_EmptyCart"));

            string blocked = ShipChecks.Check(vessel);
            if (blocked != null) return Fail(blocked);

            DestinationZone zone = ZoneCalculator.Resolve(vessel);

            var items = new List<QuoteItem>(cart.Count);
            string lastExcluded = null;
            for (int i = 0; i < cart.Count; i++)
            {
                var c = cart[i];
                if (c == null || string.IsNullOrEmpty(c.Resource)) continue;
                if (c.Amount <= 0.0) continue;
                if (ResourceCatalog.IsExcluded(c.Resource)) { lastExcluded = c.Resource; continue; }

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

            if (items.Count == 0)
            {
                if (lastExcluded != null)
                    return Fail(Loc.T("#LOC_RN_Err_Excluded", ResourceCatalog.DisplayName(lastExcluded)));
                return Fail(Loc.T("#LOC_RN_Err_NoDeliverable"));
            }

            MultiQuote q = CostCore.QuoteMulti(
                items, zone.FreightRate, zone.TransferTime,
                s.BTrip, s.STrip, s.SMin, s.TBase);
            if (!q.Ok)
            {
                if (!string.IsNullOrEmpty(q.ReasonKey)) q.Reason = Loc.T(q.ReasonKey);
                return q;
            }
            q.ZoneName = zone.Name;
            if (rush) CostCore.ApplyRush(q, s.RushCostMultiplier, s.RushTimeFactor);
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

            if (vessel == null)
            {
                q.Reason = Loc.T("#LOC_RN_Err_NoVessel"); q.ReasonKey = "#LOC_RN_Err_NoVessel"; return q;
            }
            if (string.IsNullOrEmpty(resource))
            {
                q.Reason = Loc.T("#LOC_RN_Err_NoResource"); q.ReasonKey = "#LOC_RN_Err_NoResource"; return q;
            }
            if (amount <= 0.0)
            {
                q.Reason = Loc.T("#LOC_RN_Err_Amount"); q.ReasonKey = "#LOC_RN_Err_Amount"; return q;
            }
            if (ResourceCatalog.IsExcluded(resource))
            {
                q.Reason = Loc.T("#LOC_RN_Err_Excluded", ResourceCatalog.DisplayName(resource));
                q.ReasonKey = "#LOC_RN_Err_Excluded";
                return q;
            }

            PartResourceDefinition def = ResourceCatalog.Definition(resource);
            if (def == null)
            {
                q.Reason = Loc.T("#LOC_RN_Err_NoDef", resource);
                q.ReasonKey = "#LOC_RN_Err_NoDef";
                return q;
            }

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
