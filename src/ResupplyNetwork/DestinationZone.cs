using System;
using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>某个目的地的运费率与交付时长。</summary>
    public struct DestinationZone
    {
        public string Name;
        public double DvPark;         // m/s，从 LKO 到该处停泊
        public double TransferTime;   // 秒
        public double FreightRate;    // 资金/吨
        public bool IsOverride;
    }

    public static class ZoneCalculator
    {
        /// <summary>优先用 cfg 覆盖；否则按轨道数据估算（兼容任意行星包）。</summary>
        public static DestinationZone Resolve(Vessel vessel)
        {
            var s = ResupplySettings.Instance;
            CelestialBody body = vessel != null ? vessel.mainBody : null;
            string bodyName = body != null ? body.bodyName : string.Empty;
            string sit = vessel != null ? vessel.situation.ToString() : string.Empty;

            foreach (var z in s.Zones)
            {
                if (!string.Equals(z.Body, bodyName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(z.Situation) &&
                    !string.Equals(z.Situation, sit, StringComparison.OrdinalIgnoreCase)) continue;

                var dz = new DestinationZone();
                dz.Name = BodyIdentity.DisplayName(body) + "/" + sit;
                dz.DvPark = z.Dv;
                dz.TransferTime = z.TransferTime;
                // 写死 rate 的优先；否则按公式算，再乘上该目的地的倍率（默认 1.0）。
                // 这样难度设置页拖动系数时，未写死 rate 的目的地会实时跟着变。
                dz.FreightRate = z.HasRate
                    ? z.Rate
                    : RateFrom(z.Dv, z.TransferTime) * z.RateMultiplier;
                dz.IsOverride = true;
                return dz;
            }

            return Compute(body, vessel != null ? vessel.situation : Vessel.Situations.ORBITING);
        }

        public static double RateFrom(double dvPark, double transferTime)
        {
            var s = ResupplySettings.Instance;
            double rate = CostCore.FreightRate(s.KMass, dvPark, s.DvRef, s.KTime, transferTime, s.KPremium);
            if (BodyIdentity.IsRss) rate *= s.RssFreightMultiplier;
            return rate;
        }

        /// <summary>霍曼转移近似。对任何行星包都成立，不需要手工维护表。</summary>
        private static DestinationZone Compute(CelestialBody body, Vessel.Situations situation)
        {
            var s = ResupplySettings.Instance;
            var dz = new DestinationZone();

            if (body == null)
            {
                dz.Name = "未知";
                dz.TransferTime = 6.0 * 3600.0;
                dz.FreightRate = RateFrom(0.0, dz.TransferTime);
                return dz;
            }

            CelestialBody home = Planetarium.fetch != null ? Planetarium.fetch.Home : FlightGlobals.GetHomeBody();
            if (body == home)
            {
                dz.Name = BodyIdentity.DisplayName(body);
                dz.TransferTime = 6.0 * 3600.0;
                dz.DvPark = 0.0;
                dz.FreightRate = RateFrom(0.0, dz.TransferTime);
                return dz;
            }

            CelestialBody parent = body.orbit != null ? body.orbit.referenceBody : null;
            CelestialBody sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            bool isMoon = parent != null && parent != sun;

            double dv;
            double time;

            if (isMoon)
            {
                DestinationZone parentZone = Compute(parent, Vessel.Situations.ORBITING);
                double moonDv;
                double moonTime;
                MoonTransfer(parent, body, out moonDv, out moonTime);
                dv = parentZone.DvPark + moonDv;
                time = parentZone.TransferTime + moonTime;
                dz.Name = BodyIdentity.DisplayName(body) + " (" +
                          BodyIdentity.DisplayName(parent) + " 卫星)";
            }
            else
            {
                HohmannFromHome(body, home, out dv, out time);
                dz.Name = BodyIdentity.DisplayName(body);
            }

            if (situation == Vessel.Situations.LANDED || situation == Vessel.Situations.SPLASHED)
            {
                dv += ColonyOrigin.AscentDv(body);
                time += CostCore.KerbinDay;
            }

            dz.DvPark = dv;
            dz.TransferTime = time;
            dz.FreightRate = RateFrom(dv, time);
            return dz;
        }

        /// <summary>
        /// Approximate a transfer from a low orbit around the moon's parent body
        /// to a low orbit around the moon. The capture term is deliberately
        /// conservative; planet packs vary too much for a single exact value.
        /// </summary>
        private static void MoonTransfer(CelestialBody parent, CelestialBody moon,
                                         out double dv, out double time)
        {
            dv = 900.0;
            time = 2.0 * CostCore.KerbinDay;

            if (parent == null || moon == null || moon.orbit == null) return;
            if (parent.gravParameter <= 0.0 || moon.gravParameter <= 0.0) return;

            double parkingAltitude = Math.Max(60000.0, parent.Radius * 0.03);
            double moonAltitude = Math.Max(25000.0, moon.Radius * 0.02);
            double rPark = parent.Radius + parkingAltitude;
            double rMoon = moon.orbit.semiMajorAxis;
            double rMoonPark = moon.Radius + moonAltitude;
            if (rPark <= 0.0 || rMoon <= rPark || rMoonPark <= 0.0) return;

            double a = 0.5 * (rPark + rMoon);
            double vPark = Math.Sqrt(parent.gravParameter / rPark);
            double vTransfer = Math.Sqrt(parent.gravParameter * (2.0 / rPark - 1.0 / a));
            double vApo = Math.Sqrt(parent.gravParameter * (2.0 / rMoon - 1.0 / a));
            double vMoon = Math.Sqrt(parent.gravParameter / rMoon);
            double relativeArrival = Math.Abs(vMoon - vApo);
            double vLowMoon = Math.Sqrt(moon.gravParameter / rMoonPark);
            double capture = Math.Max(vLowMoon - relativeArrival, vLowMoon * 0.45);

            dv = Math.Max(0.0, vTransfer - vPark) + capture;
            time = Math.PI * Math.Sqrt(a * a * a / parent.gravParameter);
        }

        /// <summary>从母星轨道到目标行星轨道的霍曼近似（绕恒星）。</summary>
        private static void HohmannFromHome(CelestialBody body, CelestialBody home, out double dv, out double time)
        {
            dv = 0.0;
            time = 120.0 * CostCore.KerbinDay;

            if (body.orbit == null || home == null || home.orbit == null) return;
            CelestialBody sun = home.orbit.referenceBody;
            if (sun == null || sun.gravParameter <= 0.0) return;
            if (body.orbit.referenceBody != sun) return;

            double r1 = home.orbit.semiMajorAxis;
            double r2 = body.orbit.semiMajorAxis;
            if (r1 <= 0.0 || r2 <= 0.0 || Math.Abs(r2 - r1) < 1.0) return;

            double mu = sun.gravParameter;
            double a = 0.5 * (r1 + r2);
            double v1 = Math.Sqrt(mu / r1);
            double vTransfer1 = Math.Sqrt(mu * (2.0 / r1 - 1.0 / a));
            double vTransfer2 = Math.Sqrt(mu * (2.0 / r2 - 1.0 / a));
            double v2 = Math.Sqrt(mu / r2);

            dv = Math.Abs(vTransfer1 - v1) + Math.Abs(v2 - vTransfer2);
            time = Math.PI * Math.Sqrt(a * a * a / mu);   // 半个转移椭圆周期
        }
    }
}
