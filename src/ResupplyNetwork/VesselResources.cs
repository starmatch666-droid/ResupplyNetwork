using System.Collections.Generic;

namespace ResupplyNetwork
{
    /// <summary>
    /// 已加载舰船走 PartResource，未加载舰船走 ProtoPartResourceSnapshot。
    /// 这是 KSP 里最容易出 bug 的一处，两条路径都必须覆盖。
    /// </summary>
    public static class VesselResources
    {
        // 注意：这一层会被 UI 每个资源种类、每帧多次调用，因此全部写成无委托的直接循环，
        // 避免闭包分配带来的 GC 压力。
        public static double GetAmount(Vessel v, string resource)
        {
            if (v == null || string.IsNullOrEmpty(resource)) return 0.0;
            double total = 0.0;

            if (v.loaded)
            {
                var parts = v.parts;
                if (parts == null) return 0.0;
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (part == null || part.Resources == null) continue;
                    foreach (PartResource r in part.Resources)
                    {
                        if (r == null || !string.Equals(r.resourceName, resource)) continue;
                        total += r.amount;
                    }
                }
            }
            else
            {
                var pv = v.protoVessel;
                if (pv == null || pv.protoPartSnapshots == null) return 0.0;
                for (int i = 0; i < pv.protoPartSnapshots.Count; i++)
                {
                    var pps = pv.protoPartSnapshots[i];
                    if (pps == null || pps.resources == null) continue;
                    for (int j = 0; j < pps.resources.Count; j++)
                    {
                        var prs = pps.resources[j];
                        if (prs == null || !string.Equals(prs.resourceName, resource)) continue;
                        total += prs.amount;
                    }
                }
            }
            return total;
        }

        public static double GetFreeCapacity(Vessel v, string resource)
        {
            if (v == null || string.IsNullOrEmpty(resource)) return 0.0;
            double total = 0.0;

            if (v.loaded)
            {
                var parts = v.parts;
                if (parts == null) return 0.0;
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (part == null || part.Resources == null) continue;
                    foreach (PartResource r in part.Resources)
                    {
                        if (r == null || !string.Equals(r.resourceName, resource)) continue;
                        total += r.maxAmount - r.amount;
                    }
                }
            }
            else
            {
                var pv = v.protoVessel;
                if (pv == null || pv.protoPartSnapshots == null) return 0.0;
                for (int i = 0; i < pv.protoPartSnapshots.Count; i++)
                {
                    var pps = pv.protoPartSnapshots[i];
                    if (pps == null || pps.resources == null) continue;
                    for (int j = 0; j < pps.resources.Count; j++)
                    {
                        var prs = pps.resources[j];
                        if (prs == null || !string.Equals(prs.resourceName, resource)) continue;
                        total += prs.maxAmount - prs.amount;
                    }
                }
            }
            return total;
        }

        /// <summary>按容器剩余空间注入资源，返回实际注入量。</summary>
        public static double Add(Vessel v, string resource, double amount)
        {
            if (v == null || amount <= 0.0) return 0.0;
            double remaining = amount;
            double added = 0.0;

            if (v.loaded)
            {
                var parts = v.parts;
                if (parts == null) return 0.0;
                for (int i = 0; i < parts.Count && remaining > 0.0; i++)
                {
                    var part = parts[i];
                    if (part == null || part.Resources == null) continue;
                    foreach (PartResource r in part.Resources)
                    {
                        if (r == null || !string.Equals(r.resourceName, resource)) continue;
                        double room = r.maxAmount - r.amount;
                        if (room <= 0.0) continue;
                        double put = room < remaining ? room : remaining;
                        r.amount += put;
                        remaining -= put;
                        added += put;
                        if (remaining <= 0.0) break;
                    }
                }
            }
            else
            {
                var pv = v.protoVessel;
                if (pv == null || pv.protoPartSnapshots == null) return 0.0;
                for (int i = 0; i < pv.protoPartSnapshots.Count && remaining > 0.0; i++)
                {
                    var pps = pv.protoPartSnapshots[i];
                    if (pps == null || pps.resources == null) continue;
                    for (int j = 0; j < pps.resources.Count && remaining > 0.0; j++)
                    {
                        var prs = pps.resources[j];
                        if (prs == null || !string.Equals(prs.resourceName, resource)) continue;
                        double room = prs.maxAmount - prs.amount;
                        if (room <= 0.0) continue;
                        double put = room < remaining ? room : remaining;
                        prs.amount += put;
                        remaining -= put;
                        added += put;
                    }
                }
            }
            return added;
        }

        /// <summary>列出舰船上所有可补给的资源名（去重）。</summary>
        public static List<string> ListResources(Vessel v)
        {
            var set = new HashSet<string>();
            if (v != null)
            {
                if (v.loaded)
                {
                    var parts = v.parts;
                    if (parts != null)
                    {
                        for (int i = 0; i < parts.Count; i++)
                        {
                            var part = parts[i];
                            if (part == null || part.Resources == null) continue;
                            foreach (PartResource r in part.Resources)
                            {
                                if (r != null) set.Add(r.resourceName);
                            }
                        }
                    }
                }
                else
                {
                    var pv = v.protoVessel;
                    if (pv != null && pv.protoPartSnapshots != null)
                    {
                        for (int i = 0; i < pv.protoPartSnapshots.Count; i++)
                        {
                            var pps = pv.protoPartSnapshots[i];
                            if (pps == null || pps.resources == null) continue;
                            for (int j = 0; j < pps.resources.Count; j++)
                            {
                                var prs = pps.resources[j];
                                if (prs != null) set.Add(prs.resourceName);
                            }
                        }
                    }
                }
            }
            var list = new List<string>(set);
            list.Sort();
            return list;
        }
    }
}
