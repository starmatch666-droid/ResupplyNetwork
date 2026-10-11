using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResupplyNetwork
{
    public sealed class DeliveryItem
    {
        public string Resource;
        public double Amount;
    }

    /// <summary>一笔订单可以包含多种物资，它们作为一个批次同时到达。</summary>
    public sealed class DeliveryRecord
    {
        public Guid VesselId;
        public string VesselName;
        public readonly List<DeliveryItem> Items = new List<DeliveryItem>();
        public double DueUT;
        public double OrderedUT;
        public double Paid;
        public string ZoneName;

        public int KindCount { get { return Items.Count; } }
        public double TotalAmount
        {
            get
            {
                double t = 0.0;
                for (int i = 0; i < Items.Count; i++) t += Items[i].Amount;
                return t;
            }
        }
    }

    /// <summary>
    /// 定时补给规则。规则可以只作用于一艘舰船，也可以作用于同类型的全部舰船，
    /// 或同一个天体上的全部舰船。
    /// </summary>
    public sealed class AutoRule
    {
        public const int ScopeVessel = 0;
        public const int ScopeVesselType = 1;
        public const int ScopeBody = 2;

        public Guid VesselId;          // ScopeVessel 时的目标
        public string VesselName;      // 展示用
        public int ScopeKind = ScopeVessel;
        public VesselType ScopeType;   // ScopeVesselType 时用
        public string ScopeBodyName;   // ScopeBody 时用

        public string Resource;
        public double Threshold = 0.3;
        public double Target = 1.0;
        public double Interval = 30.0 * 21600.0;
        public double Budget = 50000.0;
        public double LastCheckUT;
        public bool Checked;
        public double PrevLevel = -1.0;
        public bool Enabled = true;

        public int IntervalDays { get { return (int)Math.Round(Interval / 21600.0); } }
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        new GameScenes[] { GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION })]
    public class ResupplyQueue : ScenarioModule
    {
        public static ResupplyQueue Instance { get; private set; }

        public readonly List<DeliveryRecord> Pending = new List<DeliveryRecord>();
        public readonly List<AutoRule> AutoRules = new List<AutoRule>();

        public override void OnAwake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnSave(ConfigNode node)
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                var d = Pending[i];
                ConfigNode c = node.AddNode("DELIVERY");
                c.AddValue("vesselId", d.VesselId.ToString());
                c.AddValue("vesselName", d.VesselName ?? string.Empty);
                c.AddValue("dueUT", d.DueUT.ToString("R"));
                c.AddValue("orderedUT", d.OrderedUT.ToString("R"));
                c.AddValue("paid", d.Paid.ToString("R"));
                c.AddValue("zone", d.ZoneName ?? string.Empty);
                for (int j = 0; j < d.Items.Count; j++)
                {
                    ConfigNode item = c.AddNode("ITEM");
                    item.AddValue("resource", d.Items[j].Resource ?? string.Empty);
                    item.AddValue("amount", d.Items[j].Amount.ToString("R"));
                }
            }

            for (int i = 0; i < AutoRules.Count; i++)
            {
                var r = AutoRules[i];
                ConfigNode c = node.AddNode("RULE");
                c.AddValue("vesselId", r.VesselId.ToString());
                c.AddValue("vesselName", r.VesselName ?? string.Empty);
                c.AddValue("scope", r.ScopeKind.ToString());
                c.AddValue("scopeType", r.ScopeType.ToString());
                c.AddValue("scopeBody", r.ScopeBodyName ?? string.Empty);
                c.AddValue("resource", r.Resource ?? string.Empty);
                c.AddValue("threshold", r.Threshold.ToString("R"));
                c.AddValue("target", r.Target.ToString("R"));
                c.AddValue("interval", r.Interval.ToString("R"));
                c.AddValue("budget", r.Budget.ToString("R"));
                c.AddValue("lastCheckUT", r.LastCheckUT.ToString("R"));
                c.AddValue("checked", r.Checked ? "1" : "0");
                c.AddValue("enabled", r.Enabled ? "1" : "0");
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            Pending.Clear();
            foreach (var c in node.GetNodes("DELIVERY"))
            {
                DeliveryRecord d;
                if (!TryParse(c, out d)) continue;
                Pending.Add(d);
            }
            if (Pending.Count > 0)
                Debug.Log("[ResupplyNetwork] restored " + Pending.Count + " pending orders");

            AutoRules.Clear();
            foreach (var c in node.GetNodes("RULE"))
            {
                AutoRule r;
                if (!TryParseRule(c, out r)) continue;
                AutoRules.Add(r);
            }
            if (AutoRules.Count > 0)
                Debug.Log("[ResupplyNetwork] restored " + AutoRules.Count + " scheduled rules");
        }

        private static bool TryParseRule(ConfigNode c, out AutoRule r)
        {
            r = null;
            string res = c.GetValue("resource");
            if (string.IsNullOrEmpty(res)) return false;

            var rule = new AutoRule();
            Guid id;
            if (Guid.TryParse(c.GetValue("vesselId"), out id)) rule.VesselId = id;
            rule.VesselName = c.GetValue("vesselName") ?? string.Empty;
            rule.ScopeKind = (int)ParseD(c.GetValue("scope"), AutoRule.ScopeVessel);
            try { rule.ScopeType = (VesselType)Enum.Parse(typeof(VesselType), c.GetValue("scopeType") ?? "Ship"); }
            catch { rule.ScopeType = VesselType.Ship; }
            rule.ScopeBodyName = c.GetValue("scopeBody") ?? string.Empty;
            rule.Resource = res;
            rule.Threshold = ParseD(c.GetValue("threshold"), 0.3);
            rule.Target = ParseD(c.GetValue("target"), 1.0);
            rule.Interval = ParseD(c.GetValue("interval"), 30.0 * 21600.0);
            rule.Budget = ParseD(c.GetValue("budget"), 50000.0);
            rule.LastCheckUT = ParseD(c.GetValue("lastCheckUT"), 0.0);
            rule.Checked = (c.GetValue("checked") ?? "0").Trim() == "1";
            rule.Enabled = (c.GetValue("enabled") ?? "1").Trim() != "0";
            r = rule;
            return true;
        }

        private static bool TryParse(ConfigNode c, out DeliveryRecord d)
        {
            d = null;
            Guid id;
            if (!Guid.TryParse(c.GetValue("vesselId"), out id)) return false;

            var r = new DeliveryRecord();
            r.VesselId = id;
            r.VesselName = c.GetValue("vesselName") ?? string.Empty;
            r.DueUT = ParseD(c.GetValue("dueUT"), 0.0);
            r.OrderedUT = ParseD(c.GetValue("orderedUT"), 0.0);
            r.Paid = ParseD(c.GetValue("paid"), 0.0);
            r.ZoneName = c.GetValue("zone") ?? string.Empty;

            var nodes = c.GetNodes("ITEM");
            for (int i = 0; i < nodes.Length; i++)
            {
                string res = nodes[i].GetValue("resource");
                double amt = ParseD(nodes[i].GetValue("amount"), 0.0);
                if (string.IsNullOrEmpty(res) || amt <= 0.0) continue;
                r.Items.Add(new DeliveryItem { Resource = res, Amount = amt });
            }

            // 兼容旧存档：早期版本一单只有一种物资，写在 DELIVERY 节点自身。
            if (r.Items.Count == 0)
            {
                string legacyRes = c.GetValue("resource");
                double legacyAmt = ParseD(c.GetValue("amount"), 0.0);
                if (!string.IsNullOrEmpty(legacyRes) && legacyAmt > 0.0)
                    r.Items.Add(new DeliveryItem { Resource = legacyRes, Amount = legacyAmt });
            }

            if (r.Items.Count == 0) return false;
            d = r;
            return true;
        }

        private static double ParseD(string s)
        {
            return ParseD(s, 0.0);
        }

        private static double ParseD(string s, double fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            double d;
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        // ------------------------------------------------------------------

        public static Vessel FindVessel(Guid id)
        {
            var list = FlightGlobals.Vessels;
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id) return list[i];
            }
            return null;
        }

        public DeliveryRecord Enqueue(Vessel v, MultiQuote q)
        {
            if (v == null || q == null || !q.Ok || q.Lines.Count == 0) return null;

            var d = new DeliveryRecord();
            d.VesselId = v.id;
            d.VesselName = v.vesselName;
            d.OrderedUT = Planetarium.GetUniversalTime();
            d.DueUT = d.OrderedUT + q.LeadTime;
            d.Paid = q.Total;
            d.ZoneName = q.ZoneName;
            for (int i = 0; i < q.Lines.Count; i++)
            {
                d.Items.Add(new DeliveryItem { Resource = q.Lines[i].Resource, Amount = q.Lines[i].Amount });
            }
            Pending.Add(d);
            return d;
        }

        public double CancelWithRefund(int index)
        {
            if (index < 0 || index >= Pending.Count) return 0.0;
            var d = Pending[index];
            Pending.RemoveAt(index);
            return Refund(d.Paid, ResupplySettings.Instance.CancelRefund);
        }

        private static double Refund(double paid, double ratio)
        {
            if (ratio < 0.0) ratio = 0.0;
            if (ratio > 1.0) ratio = 1.0;
            double refund = paid * ratio;
            if (refund > 0.0 && Funding.Instance != null)
                Funding.Instance.AddFunds(refund, TransactionReasons.None);
            return refund;
        }

        // ------------------------------------------------------------------
        //  定时补给规则
        // ------------------------------------------------------------------

        public AutoRule AddRule(Vessel v, string resource, double threshold, double target,
                                double interval, double budget, int scopeKind)
        {
            if (v == null || string.IsNullOrEmpty(resource)) return null;

            for (int i = 0; i < AutoRules.Count; i++)
            {
                var e = AutoRules[i];
                if (e.Resource != resource) continue;
                if (e.ScopeKind != scopeKind) continue;
                if (scopeKind == AutoRule.ScopeVessel && e.VesselId != v.id) continue;
                if (scopeKind == AutoRule.ScopeVesselType && e.ScopeType != v.vesselType) continue;
                if (scopeKind == AutoRule.ScopeBody &&
                    !string.Equals(e.ScopeBodyName, v.mainBody != null ? v.mainBody.bodyName : "", StringComparison.OrdinalIgnoreCase)) continue;

                e.Threshold = threshold;
                e.Target = target;
                e.Interval = interval;
                e.Budget = budget;
                e.VesselName = v.vesselName;
                e.Enabled = true;
                return e;
            }

            var r = new AutoRule();
            r.VesselId = v.id;
            r.VesselName = v.vesselName;
            r.Resource = resource;
            r.Threshold = threshold;
            r.Target = target;
            r.Interval = interval;
            r.Budget = budget;
            r.ScopeKind = scopeKind;
            r.ScopeType = v.vesselType;
            r.ScopeBodyName = v.mainBody != null ? v.mainBody.bodyName : string.Empty;
            AutoRules.Add(r);
            return r;
        }

        public void RemoveRule(int index)
        {
            if (index >= 0 && index < AutoRules.Count) AutoRules.RemoveAt(index);
        }

        public bool HasPendingFor(Guid vesselId, string resource)
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                var d = Pending[i];
                if (d.VesselId != vesselId) continue;
                for (int j = 0; j < d.Items.Count; j++)
                    if (d.Items[j].Resource == resource) return true;
            }
            return false;
        }

        /// <summary>把规则展开成它作用的所有舰船。</summary>
        private static List<Vessel> TargetsOf(AutoRule r)
        {
            var list = new List<Vessel>();
            if (r.ScopeKind == AutoRule.ScopeVessel)
            {
                Vessel v = FindVessel(r.VesselId);
                if (v != null) list.Add(v);
                return list;
            }

            var all = FlightGlobals.Vessels;
            if (all == null) return list;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (v == null) continue;
                if (v.vesselType == VesselType.Debris || v.vesselType == VesselType.Flag ||
                    v.vesselType == VesselType.EVA || v.vesselType == VesselType.Unknown) continue;

                if (r.ScopeKind == AutoRule.ScopeVesselType)
                {
                    if (v.vesselType == r.ScopeType) list.Add(v);
                }
                else if (r.ScopeKind == AutoRule.ScopeBody)
                {
                    string body = v.mainBody != null ? v.mainBody.bodyName : string.Empty;
                    if (string.Equals(body, r.ScopeBodyName, StringComparison.OrdinalIgnoreCase)) list.Add(v);
                }
            }
            return list;
        }

        private void Update()
        {
            ProcessPending();
        }

        /// <summary>推进交付与定时补给规则。幂等，可被多处调用。</summary>
        public void ProcessPending()
        {
            double ut = Planetarium.GetUniversalTime();
            ProcessDeliveries(ut);
            ProcessAutoRules(ut);
        }

        private void ProcessDeliveries(double ut)
        {
            if (Pending.Count == 0) return;

            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var d = Pending[i];
                if (ut < d.DueUT) continue;

                Vessel v = FindVessel(d.VesselId);
                if (v == null)
                {
                    double refund = Refund(d.Paid, ResupplySettings.Instance.VesselLostRefund);
                    string lost = Loc.T("#LOC_RN_Msg_VesselGone", d.KindCount, refund.ToString("N0"));
                    Debug.LogWarning("[ResupplyNetwork] " + lost);
                    ScreenMessages.PostScreenMessage(lost, 8f, ScreenMessageStyle.UPPER_CENTER);
                    Pending.RemoveAt(i);
                    continue;
                }

                var sb = new System.Text.StringBuilder();
                for (int j = 0; j < d.Items.Count; j++)
                {
                    var it = d.Items[j];
                    double added = VesselResources.Add(v, it.Resource, it.Amount);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(ResourceCatalog.DisplayName(it.Resource)).Append(" +").Append(added.ToString("N1"));
                }
                Pending.RemoveAt(i);

                string name = string.IsNullOrEmpty(d.VesselName) ? v.vesselName : d.VesselName;
                string msg = Loc.T("#LOC_RN_Msg_Comm", name, sb.ToString());
                Debug.Log("[ResupplyNetwork] " + msg);
                ScreenMessages.PostScreenMessage(msg, 8f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void ProcessAutoRules(double ut)
        {
            if (AutoRules.Count == 0) return;
            var s = ResupplySettings.Instance;

            for (int i = AutoRules.Count - 1; i >= 0; i--)
            {
                var r = AutoRules[i];
                if (!r.Enabled) continue;
                if (!CostCore.ShouldCheck(ut, r.LastCheckUT, r.Checked, r.Interval)) continue;

                double prevCheckUT = r.LastCheckUT;
                bool firstCheck = !r.Checked;
                r.LastCheckUT = ut;
                r.Checked = true;

                List<Vessel> targets = TargetsOf(r);
                if (targets.Count == 0)
                {
                    if (r.ScopeKind == AutoRule.ScopeVessel)
                    {
                        r.Enabled = false;
                        Debug.LogWarning("[ResupplyNetwork] " + Loc.T("#LOC_RN_Msg_AutoDisabled",
                                          ResourceCatalog.DisplayName(r.Resource)));
                    }
                    continue;
                }

                for (int t = 0; t < targets.Count; t++)
                {
                    Vessel v = targets[t];
                    double current = VesselResources.GetAmount(v, r.Resource);
                    double capacity = current + VesselResources.GetFreeCapacity(v, r.Resource);

                    // 消耗速率预警只在单船规则下做（多目标时水位是混在一起的，估不准）
                    if (r.ScopeKind == AutoRule.ScopeVessel && !firstCheck && r.PrevLevel >= 0.0 && ut > prevCheckUT)
                    {
                        double consumed = r.PrevLevel - current;
                        if (consumed > 0.0)
                        {
                            double perSecond = consumed / (ut - prevCheckUT);
                            double endurance = perSecond > 0.0 ? current / perSecond : double.MaxValue;
                            if (endurance < r.Interval)
                                Debug.LogWarning("[ResupplyNetwork] " + Loc.T("#LOC_RN_Warn_Cadence",
                                                  v.vesselName, ResourceCatalog.DisplayName(r.Resource),
                                                  CostCore.FormatDuration(endurance), r.IntervalDays));
                        }
                    }
                    if (r.ScopeKind == AutoRule.ScopeVessel) r.PrevLevel = current;

                    if (HasPendingFor(v.id, r.Resource)) continue;

                    double need = CostCore.TopUpAmount(current, capacity, r.Threshold, r.Target);
                    if (need <= 0.0) continue;

                    var cart = new List<CartItem>(1);
                    cart.Add(new CartItem { Resource = r.Resource, Amount = need });
                    MultiQuote q = CostModel.QuoteMulti(v, cart);
                    if (!q.Ok)
                    {
                        Debug.LogWarning("[ResupplyNetwork] " + Loc.T("#LOC_RN_Msg_AutoNoQuote", q.Reason));
                        continue;
                    }
                    if (r.Budget > 0.0 && q.Total > r.Budget)
                    {
                        Debug.Log("[ResupplyNetwork] " + Loc.T("#LOC_RN_Msg_AutoSkipBudget",
                                          r.Budget.ToString("N0"), ResourceCatalog.DisplayName(r.Resource),
                                          q.Total.ToString("N0")));
                        continue;
                    }
                    if (Funding.Instance == null || Funding.Instance.Funds < q.Total)
                    {
                        Debug.Log("[ResupplyNetwork] " + Loc.T("#LOC_RN_Msg_AutoSkipFunds",
                                          q.Total.ToString("N0"), ResourceCatalog.DisplayName(r.Resource)));
                        continue;
                    }

                    Funding.Instance.AddFunds(-q.Total, TransactionReasons.None);
                    Enqueue(v, q);

                    double pct = capacity > 0.0 ? current / capacity * 100.0 : 0.0;
                    string msg = Loc.T("#LOC_RN_Msg_AutoOrder", v.vesselName,
                                       ResourceCatalog.DisplayName(r.Resource), need.ToString("N0"),
                                       pct.ToString("N0"));
                    Debug.Log("[ResupplyNetwork] " + msg + " (" + q.Total.ToString("N0") + " funds)");
                    ScreenMessages.PostScreenMessage(msg, 6f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
        }
    }
}
