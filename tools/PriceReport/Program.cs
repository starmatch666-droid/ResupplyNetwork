using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ResupplyNetwork;

namespace ResupplyNetwork.Tools
{
    internal sealed class CfgNode
    {
        public string Name = "";
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<CfgNode> Children = new List<CfgNode>();

        public CfgNode Child(string name)
        {
            foreach (var c in Children) if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        public IEnumerable<CfgNode> All(string name)
        {
            foreach (var c in Children) if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) yield return c;
        }

        public string Get(string key)
        {
            string v;
            return Values.TryGetValue(key, out v) ? v : null;
        }
    }

    internal sealed class ResDef
    {
        public string Name;
        public double Density;
        public double UnitCost;
        public string Source;
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            string gd = args.Length > 0 ? args[0]
                : @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData";
            string cfgPath = args.Length > 1 ? args[1]
                : Path.Combine(AppContext.BaseDirectory,
                    @"..\..\..\..\GameData\ResupplyNetwork\Config\ResupplySettings.cfg");

            Console.OutputEncoding = Encoding.UTF8;

            if (!Directory.Exists(gd)) { Console.WriteLine("GameData 路径不存在: " + gd); return 2; }
            cfgPath = Path.GetFullPath(cfgPath);
            if (!File.Exists(cfgPath)) { Console.WriteLine("配置不存在: " + cfgPath); return 2; }

            // ---- 1. 解析我们自己的 config（这同时验证了 cfg 是否写得能被解析） ----
            var cfgRoot = Parse(File.ReadAllText(cfgPath));
            var conf = cfgRoot.Child("RESUPPLY_CONFIG");
            if (conf == null) { Console.WriteLine("配置里找不到 RESUPPLY_CONFIG 节点"); return 3; }

            double kMass = Num(conf.Get("K_mass"), 4000);
            double dvRef = Num(conf.Get("dv_ref"), 1000);
            double kTime = Num(conf.Get("K_time"), 0.5);
            double kPremium = Num(conf.Get("K_premium"), 1.0);
            double bTrip = Num(conf.Get("B_trip"), 20);
            double sTrip = Num(conf.Get("S_trip"), 800);
            double sMin = Num(conf.Get("S_min"), 1000);
            double tBase = Num(conf.Get("T_base"), 7200);
            double minDensity = Num(conf.Get("minDensity"), 0.0001);
            double minUnitPrice = Num(conf.Get("minUnitPrice"), 0.01);

            var overrides = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in conf.All("UNIT_PRICE_OVERRIDE")) overrides[c.Get("name") ?? ""] = Num(c.Get("price"), 0);

            var scarcity = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in conf.All("SCARCITY")) scarcity[c.Get("name") ?? ""] = Num(c.Get("multiplier"), 1);

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in conf.All("EXCLUDE")) excluded.Add(c.Get("name") ?? "");

            var zones = new List<Tuple<string, string, double, double, double>>();
            foreach (var c in conf.All("ZONE_OVERRIDE"))
                zones.Add(Tuple.Create(c.Get("body") ?? "", c.Get("situation") ?? "", Num(c.Get("dv"), 0),
                                      Num(c.Get("transferTime"), 0), Num(c.Get("rate"), 0)));

            Console.WriteLine("=== 配置解析结果（" + cfgPath + "）===");
            Console.WriteLine("K_mass=" + kMass + "  dv_ref=" + dvRef + "  K_time=" + kTime + "  K_premium=" + kPremium);
            Console.WriteLine("B_trip=" + bTrip + "  S_trip=" + sTrip + "  S_min=" + sMin + "  T_base=" + tBase);
            Console.WriteLine("单位价覆盖=" + overrides.Count + "  稀缺溢价=" + scarcity.Count
                              + "  排除=" + excluded.Count + "  目的地覆盖=" + zones.Count);
            Console.WriteLine();

            // ---- 2. 扫游戏里的真实资源定义 ----
            var defs = new Dictionary<string, ResDef>(StringComparer.OrdinalIgnoreCase);
            ScanResources(gd, defs);
            Console.WriteLine("=== 扫描到 " + defs.Count + " 个 RESOURCE_DEFINITION ===");
            Console.WriteLine();

            // ---- 样例目的地（来自设计表） ----
            var sampleZones = new (string Name, double Rate)[]
            {
                ("LKO(70-250km)", 4000),
                ("Mun 表面", 11000),
                ("Jool 系统", 42000),
            };

            Func<string, ResDef, double> unitPrice = (name, d) =>
            {
                double p;
                if (overrides.TryGetValue(name, out p)) return Math.Max(p, minUnitPrice);
                double m;
                p = d.UnitCost * (scarcity.TryGetValue(name, out m) ? m : 1.0);
                return Math.Max(p, minUnitPrice);
            };
            Func<ResDef, double> freightDensity = d => d.Density > 0.0 ? d.Density : minDensity;

            // ---- 3. 精选资源价格表 ----
            string[] curated =
            {
                "LiquidFuel", "Oxidizer", "SolidFuel", "MonoPropellant", "XenonGas", "Ore", "Ablator",
                "LqdHydrogen", "LqdMethane", "LqdOxygen", "ArgonGas", "LqdXenon", "Hydrazine", "Aerozine50",
                "Supplies", "Mulch", "Fertilizer", "MaterialKits", "Machinery", "SpecializedParts",
                "Metals", "MetallicOre", "Minerals", "Water", "Food", "ColonySupplies", "Glykerol",
                "EnrichedUranium", "DepletedUranium", "Thorium", "UF4", "Plutonium-238",
                "LqdHe3", "FusionPellets", "Karborundum", "NuclearSaltWater", "Antimatter", "AntiHydrogen",
            };

            Console.WriteLine("=== 精选资源价格（单价 = 物资价 + 运费，不含服务费）===");
            Console.WriteLine(Fmt("资源", -18) + Fmt("密度t/u", 12) + Fmt("unitCost", 12)
                              + Fmt("实际单价", 12) + Fmt("LKO/u", 12) + Fmt("Mun/u", 12) + Fmt("Jool/u", 12) + "  运费/u@LKO");
            Console.WriteLine(new string('-', 110));
            foreach (var name in curated)
            {
                ResDef d;
                if (!defs.TryGetValue(name, out d)) { Console.WriteLine(Fmt(name, -18) + "  (未找到)"); continue; }
                double up = unitPrice(name, d);
                double fd = freightDensity(d);
                string flag = excluded.Contains(name) ? "  [已排除]" : "";
                Console.WriteLine(
                    Fmt(name, -18) + Fmt(d.Density.ToString("G6", CultureInfo.InvariantCulture), 12)
                    + Fmt(d.UnitCost.ToString("G6", CultureInfo.InvariantCulture), 12)
                    + Fmt(up.ToString("N4"), 12)
                    + Fmt((up + fd * 4000).ToString("N4"), 12)
                    + Fmt((up + fd * 11000).ToString("N4"), 12)
                    + Fmt((up + fd * 42000).ToString("N4"), 12)
                    + "  " + (fd * 4000).ToString("N0") + flag);
            }
            Console.WriteLine();

            // ---- 4. 用 CostCore 跑几个实例报价 ----
            Console.WriteLine("=== 实例报价（CostCore，与游戏内同一份代码）===");
            Sample(defs, "LiquidFuel", 2880, 4000, 21600, kMass, dvRef, kTime, kPremium, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity);
            Sample(defs, "LqdHydrogen", 70572, 9100, 47520, kMass, dvRef, kTime, kPremium, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity);
            Sample(defs, "Supplies", 10000, 42000, 17280000, kMass, dvRef, kTime, kPremium, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity);
            Sample(defs, "ElectricCharge", 5000, 4000, 21600, kMass, dvRef, kTime, kPremium, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity);
            Console.WriteLine();
            SampleMulti(defs, new (string, double)[]
            {
                ("LiquidFuel", 2880),
                ("Oxidizer", 3520),
            }, 4000, 21600, kMass, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity, "LKO 加满一罐 Jumbo-64 等量");
            SampleMulti(defs, new (string, double)[]
            {
                ("Supplies", 10000),
                ("Water", 2000),
                ("MaterialKits", 500),
            }, 42000, 17280000, kMass, bTrip, sTrip, sMin, tBase, minDensity, minUnitPrice, overrides, scarcity, "Jool 站补 补给品+水+材料");
            Console.WriteLine();

            // ---- 5. 反物质专项核对 ----
            Console.WriteLine("=== 反物质 / 反氢 专项核对 ===");
            PerGram(defs, "Antimatter", overrides, scarcity, minUnitPrice);
            PerGram(defs, "AntiHydrogen", overrides, scarcity, minUnitPrice);
            Console.WriteLine("（CRP 作者原意：两者都是 100000 资金/克；本配置按 x" 
                              + (scarcity.ContainsKey("Antimatter") ? scarcity["Antimatter"].ToString() : "1") + " 抬价）");
            Console.WriteLine();

            SimulateAutoRules();
            Console.WriteLine();

            PerfProbe();
            Console.WriteLine();

            // ---- 6. 健康检查 ----
            Console.WriteLine("=== 健康检查 ===");
            int zeroDensity = 0, zeroCost = 0;
            foreach (var kv in defs)
            {
                if (kv.Value.Density <= 0) zeroDensity++;
                if (kv.Value.UnitCost <= 0) zeroCost++;
            }
            Console.WriteLine("零质量资源: " + zeroDensity + " 个（靠 minDensity 兜底）");
            Console.WriteLine("零价资源:   " + zeroCost + " 个（靠 minUnitPrice / 覆盖价兜底）");
            Console.WriteLine("被排除:     " + excluded.Count + " 条规则");
            foreach (var e in excluded)
            {
                Console.WriteLine("   " + (defs.ContainsKey(e) ? "存在  " : "缺失  ") + e);
            }
            return 0;
        }

        private sealed class SimOrder
        {
            public double OrderDay;
            public double ArriveDay;
            public double Amount;
            public double LevelAtTrigger;
            public bool Arrived;
        }

        /// <summary>
        /// 按代码里实际的循环形状模拟每帧开销。只覆盖数据扫描部分，
        /// IMGUI 的绘制开销无法离线模拟，另行说明。
        /// </summary>
        private static void PerfProbe()
        {
            Console.WriteLine("=== 性能探针（按代码里的循环形状模拟）===");

            const int vesselCount = 200;   // 舰队规模
            const int parts = 120;         // 选中舰船的部件数
            const int perPart = 4;         // 每个部件的资源条目数
            const int resTypes = 18;       // 选中舰船的资源种类数
            const int eventsPerFrame = 3;  // IMGUI 每帧大约调用 OnGUI 的次数
            const int frames = 600;        // 10 秒 @60fps

            var res = new string[parts][];
            var amt = new double[parts][];
            var max = new double[parts][];
            for (int p = 0; p < parts; p++)
            {
                res[p] = new string[perPart];
                amt[p] = new double[perPart];
                max[p] = new double[perPart];
                for (int q = 0; q < perPart; q++)
                {
                    res[p][q] = "Res" + ((p * perPart + q) % resTypes);
                    amt[p][q] = 50.0;
                    max[p][q] = 100.0;
                }
            }

            double sink = 0.0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                for (int e = 0; e < eventsPerFrame; e++)
                {
                    // EligibleVessels()：遍历整支舰队 + 新建 List
                    var list = new List<int>(vesselCount);
                    for (int v = 0; v < vesselCount; v++) list.Add(v);
                    sink += list.Count * 1e-12;

                    // ListResources()：一次全扫描 + 去重
                    var set = new HashSet<string>();
                    for (int p = 0; p < parts; p++)
                        for (int q = 0; q < perPart; q++) set.Add(res[p][q]);

                    // 每个资源种类：GetAmount + GetFreeCapacity 各一次全扫描
                    foreach (var name in set)
                    {
                        double total = 0.0, free = 0.0;
                        for (int p = 0; p < parts; p++)
                        {
                            for (int q = 0; q < perPart; q++)
                            {
                                if (!string.Equals(res[p][q], name)) continue;
                                total += amt[p][q];
                                free += max[p][q] - amt[p][q];
                            }
                        }
                        sink += total + free;
                    }
                }
            }
            sw.Stop();

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perFrameUs = totalMs * 1000.0 / frames;
            Console.WriteLine("  模型: " + vesselCount + " 艘舰船, 选中舰船 " + parts + " 部件 × " + perPart
                              + " 资源条目, " + resTypes + " 种资源, 每帧 " + eventsPerFrame + " 次 OnGUI");
            Console.WriteLine("  窗口打开时数据扫描: " + perFrameUs.ToString("N1") + " µs/帧"
                              + "  （16.67ms 帧预算的 " + (perFrameUs / 16670.0 * 100.0).ToString("N3") + "%）");
            Console.WriteLine("  窗口关闭时: 只有两个早退判断，量级为 0");
            Console.WriteLine("  （sink=" + sink.ToString("N1") + "，防止被优化掉）");
        }

        /// <summary>离线验证定时补给规则引擎：判定函数边界 + 班期仿真。</summary>
        private static void SimulateAutoRules()
        {
            Console.WriteLine("=== 定时补给规则引擎验证（离线仿真）===");
            double day = CostCore.KerbinDay;

            Console.WriteLine("  判定函数边界:");
            Console.WriteLine("    首次（未检查过）              = " + CostCore.ShouldCheck(0, 0, false, 30 * day));
            Console.WriteLine("    UT=0 且已检查过               = " + CostCore.ShouldCheck(0, 0, true, 30 * day)
                              + "   <- 必须是 False（否则每帧都会触发）");
            Console.WriteLine("    距上次 29 天（间隔 30 天）     = " + CostCore.ShouldCheck(29 * day, 0, true, 30 * day));
            Console.WriteLine("    距上次 30 天（间隔 30 天）     = " + CostCore.ShouldCheck(30 * day, 0, true, 30 * day));
            Console.WriteLine("    余量 25%、阈值 30%、目标 100%  -> 补 "
                              + CostCore.TopUpAmount(500, 2000, 0.30, 1.00).ToString("N0") + " / 2000");
            Console.WriteLine("    余量 60%、阈值 30%             -> 补 "
                              + CostCore.TopUpAmount(1200, 2000, 0.30, 1.00).ToString("N0") + "（低于线才发船）");
            Console.WriteLine("    容器已满、阈值 30%             -> 补 "
                              + CostCore.TopUpAmount(2000, 2000, 0.30, 1.00).ToString("N0"));
            Console.WriteLine();

            Console.WriteLine("  场景：空间站补给品容量 2000，日均消耗 40，阈值 30%，补到 100%，航程 3 天");
            RunSchedule("班期 30 天", 2000, 40, 0.30, 1.00, 30, 3, 400);
            RunSchedule("班期 20 天", 2000, 40, 0.30, 1.00, 20, 3, 400);
        }

        private static void RunSchedule(string title, double capacity, double perDay,
            double threshold, double target, double intervalDays, double leadDays, int totalDays)
        {
            double day = CostCore.KerbinDay;
            double level = capacity;
            double lastCheck = 0.0;
            bool hasChecked = false;
            double minLevel = level;
            var orders = new List<SimOrder>();

            for (int d = 0; d <= totalDays; d++)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    var o = orders[i];
                    if (o.Arrived) continue;
                    if (Math.Abs(o.ArriveDay - d) < 1e-9)
                    {
                        level = Math.Min(capacity, level + o.Amount);
                        o.Arrived = true;
                    }
                }

                if (CostCore.ShouldCheck(d * day, lastCheck, hasChecked, intervalDays * day))
                {
                    hasChecked = true;
                    lastCheck = d * day;
                    double need = CostCore.TopUpAmount(level, capacity, threshold, target);
                    if (need > 0.0)
                    {
                        orders.Add(new SimOrder
                        {
                            OrderDay = d,
                            ArriveDay = d + leadDays,
                            Amount = need,
                            LevelAtTrigger = level,
                        });
                    }
                }

                level -= perDay;
                if (level < 0.0) level = 0.0;
                if (level < minLevel) minLevel = level;
            }

            Console.WriteLine();
            Console.WriteLine("    [" + title + "] " + totalDays + " 天内发船 " + orders.Count + " 次，最低余量 "
                              + (minLevel / capacity * 100.0).ToString("N0") + "%"
                              + (minLevel <= 0.0 ? "   ← 断货了" : ""));
            for (int i = 0; i < orders.Count; i++)
            {
                var o = orders[i];
                Console.WriteLine("        第 " + o.OrderDay.ToString("N0").PadLeft(4) + " 天下单，触发时余量 "
                                  + (o.LevelAtTrigger / capacity * 100.0).ToString("N0").PadLeft(3) + "%"
                                  + "，补 " + o.Amount.ToString("N0").PadLeft(5)
                                  + "，第 " + o.ArriveDay.ToString("N0").PadLeft(4) + " 天到货");
            }
        }

        private static void SampleMulti(Dictionary<string, ResDef> defs, (string Name, double Amount)[] items,
            double rate, double transferTime, double kMass, double bTrip, double sTrip, double sMin, double tBase,
            double minDensity, double minUnitPrice, Dictionary<string, double> overrides, Dictionary<string, double> scarcity,
            string title)
        {
            var list = new System.Collections.Generic.List<QuoteItem>();
            foreach (var it in items)
            {
                ResDef d;
                if (!defs.TryGetValue(it.Name, out d))
                {
                    Console.WriteLine("  " + title + " -> " + it.Name + " 未找到");
                    return;
                }
                double m; double p = d.UnitCost * (scarcity.TryGetValue(it.Name, out m) ? m : 1.0);
                double over;
                if (overrides.TryGetValue(it.Name, out over)) p = over;
                if (p < minUnitPrice) p = minUnitPrice;
                list.Add(new QuoteItem
                {
                    Resource = it.Name,
                    Amount = it.Amount,
                    UnitPrice = p,
                    Density = d.Density > 0.0 ? d.Density : minDensity,
                    FreeCapacity = double.MaxValue,
                });
            }

            var q = CostCore.QuoteMulti(list, rate, transferTime, bTrip, sTrip, sMin, tBase);
            Console.WriteLine("  [" + title + "] " + CostCore.DescribeMulti(q));
            if (q.Ok)
            {
                foreach (var line in q.Lines)
                    Console.WriteLine("      " + line.Resource.PadRight(16) + " " + line.Amount.ToString("N0").PadLeft(12)
                                      + " 单位  " + line.Mass.ToString("N3").PadLeft(10) + " t  物资 "
                                      + line.MaterialCost.ToString("N0").PadLeft(12));
            }
        }

        private static void PerGram(Dictionary<string, ResDef> defs, string name,
            Dictionary<string, double> overrides, Dictionary<string, double> scarcity, double minUnitPrice)
        {
            ResDef d;
            if (!defs.TryGetValue(name, out d)) { Console.WriteLine(Fmt(name, -14) + "未找到"); return; }
            double m; double p = d.UnitCost * (scarcity.TryGetValue(name, out m) ? m : 1.0);
            double over;
            if (overrides.TryGetValue(name, out over)) p = over;
            if (p < minUnitPrice) p = minUnitPrice;
            double grams = d.Density * 1e6;   // 1 单位 = density 吨 = density*1e6 克
            double perGram = grams > 0 ? p / grams : 0;
            Console.WriteLine(Fmt(name, -14) + " 单价 " + p.ToString("N0").PadLeft(14)
                              + " /单位 | 1 单位 = " + grams.ToString("G6") + " 克 | 每克 "
                              + perGram.ToString("N0").PadLeft(12) + " 资金");
        }

        private static void Sample(Dictionary<string, ResDef> defs, string name, double amount, double rate,
            double transferTime, double kMass, double dvRef, double kTime, double kPremium,
            double bTrip, double sTrip, double sMin, double tBase, double minDensity, double minUnitPrice,
            Dictionary<string, double> overrides, Dictionary<string, double> scarcity)
        {
            ResDef d;
            if (!defs.TryGetValue(name, out d)) { Console.WriteLine(name + " 未找到"); return; }
            double m; double p = d.UnitCost * (scarcity.TryGetValue(name, out m) ? m : 1.0);
            double over;
            if (overrides.TryGetValue(name, out over)) p = over;
            if (p < minUnitPrice) p = minUnitPrice;
            double dens = d.Density > 0.0 ? d.Density : minDensity;

            var q = CostCore.Quote(name, amount, double.MaxValue, dens, p, rate, transferTime,
                                   bTrip, sTrip, sMin, tBase);
            Console.WriteLine("  " + CostCore.Describe(q));
        }

        // ------------------------------------------------------------------
        //  极简 KSP ConfigNode 解析器（chars 级，支持单行与多行节点）
        // ------------------------------------------------------------------
        private static CfgNode Parse(string text)
        {
            var root = new CfgNode();
            var stack = new Stack<CfgNode>();
            stack.Push(root);
            var token = new StringBuilder();
            var value = new StringBuilder();
            string key = null;
            bool inValue = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '\r') continue;

                if (inValue)
                {
                    if (c == '\n')
                    {
                        stack.Peek().Values[key] = value.ToString().Trim();
                        key = null; inValue = false; value.Clear();
                    }
                    else value.Append(c);
                    continue;
                }

                if (c == '{')
                {
                    var n = new CfgNode { Name = token.ToString().Trim() };
                    token.Clear();
                    stack.Peek().Children.Add(n);
                    stack.Push(n);
                    continue;
                }
                if (c == '}')
                {
                    token.Clear();
                    if (stack.Count > 1) stack.Pop();
                    continue;
                }
                if (c == '=')
                {
                    key = token.ToString().Trim();
                    token.Clear();
                    inValue = true;
                    value.Clear();
                    continue;
                }
                if (c == '\n') continue;   // 保留 token：节点名可能写在 '{' 的上一行
                token.Append(c);
            }
            if (inValue && key != null) stack.Peek().Values[key] = value.ToString().Trim();
            return root;
        }

        // ------------------------------------------------------------------
        private static void ScanResources(string gd, Dictionary<string, ResDef> into)
        {
            var nameRx = new Regex(@"(?<![\w])name\s*=\s*([^\r\n]+)", RegexOptions.Compiled);
            var densRx = new Regex(@"(?<![\w])density\s*=\s*([^\r\n]+)", RegexOptions.Compiled);
            var costRx = new Regex(@"(?<![\w])unitCost\s*=\s*([^\r\n]+)", RegexOptions.Compiled);

            foreach (var path in Directory.EnumerateFiles(gd, "*.cfg", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }
                if (text.IndexOf("RESOURCE_DEFINITION", StringComparison.Ordinal) < 0) continue;

                int idx = 0;
                while (true)
                {
                    int p = text.IndexOf("RESOURCE_DEFINITION", idx, StringComparison.Ordinal);
                    if (p < 0) break;
                    int brace = text.IndexOf('{', p);
                    if (brace < 0) break;
                    int end = MatchBrace(text, brace);
                    if (end < 0) break;
                    string body = text.Substring(brace + 1, end - brace - 1);
                    idx = end + 1;

                    string name = Clean(nameRx.Match(body).Groups[1].Value);
                    if (string.IsNullOrEmpty(name)) continue;

                    var d = new ResDef();
                    d.Name = name;
                    d.Density = Num(Clean(densRx.Match(body).Groups[1].Value), 0);
                    d.UnitCost = Num(Clean(costRx.Match(body).Groups[1].Value), 0);
                    d.Source = path;
                    into[name] = d;
                }
            }
        }

        private static int MatchBrace(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string Clean(string v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            int c = v.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0) v = v.Substring(0, c);
            return v.Trim();
        }

        private static double Num(string v, double fallback)
        {
            double d;
            if (!string.IsNullOrEmpty(v) &&
                double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        private static string Fmt(string s, int width)
        {
            if (s == null) s = "";
            int pad = width - Wide(s);
            return s + new string(' ', Math.Max(1, pad));
        }

        private static int Wide(string s)
        {
            int n = 0;
            foreach (char c in s) n += c > 0x2E80 ? 2 : 1;
            return n;
        }
    }
}
