using System;
using UnityEngine;

namespace ResupplyNetwork
{
    public sealed class SupplyColony
    {
        public Vessel Vessel;
        public CelestialBody Body;
        public int Crew;
    }

    /// <summary>
    /// 找出目标所在天体上"功能完善"的殖民地，把补给来源从坎巴拉改到当地。
    ///
    /// 判定 deliberately 不依赖某个殖民地 mod 的专属模块（这台机器上就没装 MKS），
    /// 而是看通用特征：停在表面 + 有足够乘员 + 至少有一个转换器/采掘器/后勤模块。
    /// 这些名字来自实际安装的 USI / PlanetaryBaseInc 部件。
    /// </summary>
    public static class ColonyOrigin
    {
        /// <summary>殖民地发货按"一次本地发射"计时。</summary>
        public const double LocalTransferTime = 21600.0;   // 6 小时

        private static CelestialBody _cacheBody;
        private static SupplyColony _cache;
        private static float _cacheUntil;
        private const float CacheSeconds = 2f;   // 窗口每帧都要报价，扫全舰队要节流

        public static SupplyColony FindFor(Vessel target)
        {
            ResupplySettings s = ResupplySettings.Instance;
            if (!s.ColonyEnabled || target == null) return null;

            CelestialBody body = target.mainBody;
            if (body == null) return null;

            float now = Time.realtimeSinceStartup;
            if (_cacheBody == body && now < _cacheUntil) return _cache;

            SupplyColony best = null;
            var all = FlightGlobals.Vessels;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    Vessel v = all[i];
                    if (v == null || v == target) continue;
                    if (v.mainBody != body) continue;
                    if (v.situation != Vessel.Situations.LANDED &&
                        v.situation != Vessel.Situations.SPLASHED) continue;
                    if (v.vesselType == VesselType.Debris || v.vesselType == VesselType.Flag ||
                        v.vesselType == VesselType.EVA || v.vesselType == VesselType.Unknown ||
                        v.vesselType == VesselType.SpaceObject) continue;

                    int crew = CrewOf(v);
                    if (crew < s.ColonyMinCrew) continue;
                    if (!HasProduction(v)) continue;

                    if (best == null || crew > best.Crew)
                    {
                        var c = new SupplyColony();
                        c.Vessel = v;
                        c.Body = body;
                        c.Crew = crew;
                        best = c;
                    }
                }
            }

            _cacheBody = body;
            _cache = best;
            _cacheUntil = now + CacheSeconds;
            return best;
        }

        public static int CrewOf(Vessel v)
        {
            if (v == null) return 0;
            if (v.loaded)
            {
                try { return v.GetCrewCount(); }
                catch { return 0; }
            }
            var pv = v.protoVessel;
            if (pv == null) return 0;
            try
            {
                var crew = pv.GetVesselCrew();
                return crew == null ? 0 : crew.Count;
            }
            catch { return 0; }
        }

        /// <summary>天体显示名。KSP 的 displayName 带 ^N 之类的转义后缀，要剥掉。</summary>
        public static string BodyName(CelestialBody b)
        {
            if (b == null) return "?";
            string name = b.displayName;
            if (string.IsNullOrEmpty(name)) name = b.bodyName;
            int caret = name.IndexOf('^');
            if (caret >= 0) name = name.Substring(0, caret);
            name = name.Trim();
            return name.Length > 0 ? name : b.bodyName;
        }

        /// <summary>有转换器 / 采掘器 / 后勤模块，才算是"能生产"的基地，而不是一顶帐篷。</summary>
        private static bool HasProduction(Vessel v)
        {
            if (v.loaded)
            {
                var parts = v.parts;
                if (parts == null) return false;
                for (int i = 0; i < parts.Count; i++)
                {
                    Part part = parts[i];
                    if (part == null || part.Modules == null) continue;
                    for (int j = 0; j < part.Modules.Count; j++)
                    {
                        PartModule m = part.Modules[j];
                        if (m != null && IsProductionModule(m.moduleName)) return true;
                    }
                }
                return false;
            }

            var pv2 = v.protoVessel;
            if (pv2 == null || pv2.protoPartSnapshots == null) return false;
            for (int i = 0; i < pv2.protoPartSnapshots.Count; i++)
            {
                var pps = pv2.protoPartSnapshots[i];
                if (pps == null || pps.modules == null) continue;
                for (int j = 0; j < pps.modules.Count; j++)
                {
                    var pm = pps.modules[j];
                    if (pm != null && IsProductionModule(pm.moduleName)) return true;
                }
            }
            return false;
        }

        private static bool IsProductionModule(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Converter", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Harvester", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("LogisticsConsumer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>从地表到轨道的粗略 Δv：轨道速度 × 经验倍率（有大气更高）。</summary>
        public static double AscentDv(CelestialBody b)
        {
            if (b == null || b.Radius <= 0.0 || b.gravParameter <= 0.0) return 0.0;
            double vOrb = Math.Sqrt(b.gravParameter / b.Radius);
            bool atmo = b.atmosphere && b.atmosphereDepth > 0.0;
            return vOrb * (atmo ? 1.6 : 1.3);
        }

        /// <summary>
        /// 殖民地的本地运费：按"当地入轨难度 / 坎巴拉入轨难度"等比缩放 K_mass。
        /// 例如 Duna 的入轨 Δv 约为 Kerbin 的 40%，运费也大致是四成。
        /// </summary>
        public static double FreightRate(CelestialBody body, ResupplySettings s)
        {
            CelestialBody home = Planetarium.fetch != null ? Planetarium.fetch.Home : null;
            double reference = AscentDv(home);
            if (reference <= 0.0) reference = 3400.0;

            double ratio = AscentDv(body) / reference;
            if (ratio < 0.02) ratio = 0.02;   // 下限，别让低重力卫星变成免费

            return s.KMass * ratio * s.KPremium * s.ColonyFreightFactor;
        }
    }
}
