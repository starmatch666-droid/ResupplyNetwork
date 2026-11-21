using System;

namespace ResupplyNetwork
{
    /// <summary>
    /// Runtime helpers for RealSolarSystem. RSS keeps the internal home-body name
    /// "Kerbin" for compatibility, while its display name is Earth.
    /// </summary>
    public static class BodyIdentity
    {
        public static CelestialBody Home
        {
            get
            {
                return Planetarium.fetch != null
                    ? Planetarium.fetch.Home
                    : FlightGlobals.GetHomeBody();
            }
        }

        public static bool IsRss
        {
            get
            {
                CelestialBody home = Home;
                if (home == null) return false;
                if (string.Equals(home.bodyName, "Earth", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (home.Radius >= 1000000.0) return true;

                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    CelestialBody body = FlightGlobals.Bodies[i];
                    if (body == null || body.orbit == null) continue;
                    if (!string.Equals(body.bodyName, "Moon", StringComparison.OrdinalIgnoreCase)) continue;
                    if (body.orbit.referenceBody != home) continue;

                    if (string.Equals(home.bodyName, "Kerbin", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        public static string DisplayName(CelestialBody body)
        {
            if (body == null) return "?";
            string name = body.displayName;
            if (string.IsNullOrEmpty(name)) name = body.bodyName;

            int caret = name.IndexOf('^');
            if (caret >= 0) name = name.Substring(0, caret);
            name = name.Trim();
            return name.Length > 0 ? name : body.bodyName;
        }

        public static string HomeDisplayName
        {
            get
            {
                CelestialBody home = Home;
                if (IsRss && home != null)
                {
                    string display = DisplayName(home);
                    if (!string.IsNullOrEmpty(display) &&
                        !string.Equals(display, "Kerbin", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(display, "坎巴拉", StringComparison.Ordinal))
                        return display;

                    string rssName = Loc.T("#RSS_Earth_name");
                    if (!string.IsNullOrEmpty(rssName) &&
                        !rssName.StartsWith("#", StringComparison.Ordinal))
                        return rssName;
                    return "Earth";
                }
                return Loc.T("#LOC_RN_Origin_Home");
            }
        }
    }
}
