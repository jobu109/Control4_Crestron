using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // House location and current weather for the panel Home page. Everything is
    // discovered at runtime through the authenticated Director connection:
    //  - coordinates from the project root item's Latitude/Longitude variables
    //  - weather from an agent whose name contains "weather" and that exposes a
    //    CURRENT_TEMPERATURE variable (for example the OpenWeather driver).
    //  - the temperature scale from the driver's own readings: temperature, humidity and
    //    dew point only agree under one scale (Director does not expose Composer's setting).
    // Values that are absent or not being reported stay out of the document.
    public sealed partial class GatewayCore
    {
        // Last scale the readings identified; kept for readings that cannot tell (high humidity).
        private string _siteTemperatureUnit;

        internal void ReadPanelSite(Action<int, JObject> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                JObject result = new JObject { ["room_id"] = 0, ["weather_status"] = "unavailable" };
                try
                {
                    string host, token;
                    if (GetConnection(out host, out token) && CurrentGeneration(generation))
                        result = FetchPanelSite(host, token, generation);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Site read failed: " + FailureKind(ex));
                }
                if (CurrentGeneration(generation)) completed(generation, result);
            });
        }

        private JObject FetchPanelSite(string host, string token, int generation)
        {
            JArray items = InventoryItems(ReadWithTimeoutRetry(host, token, "/api/v1/items", generation));
            ushort root = 0;
            var weatherCandidates = new List<ushort>();
            ushort customVariablesAgent = 0;
            foreach (JToken row in items)
            {
                ushort id;
                if (!(row is JObject) || !UInt16.TryParse(ScalarText(row["item_id"] ?? row["id"]), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0) continue;
                string type = ScalarText(row["type"]);
                string name = ScalarText(row["name"]) ?? "";
                if (type == "1") root = root == 0 ? id : root;
                // Composer Programming custom variables live on Director's "variables" agent.
                else if (type == "9" && String.Equals(name, "variables", StringComparison.OrdinalIgnoreCase)) customVariablesAgent = id;
                else if (type == "9" && name.IndexOf("weather", StringComparison.OrdinalIgnoreCase) >= 0 && weatherCandidates.Count < 4)
                    weatherCandidates.Add(id);
            }

            JToken rootVariables = null;
            if (root != 0 && CurrentGeneration(generation))
                rootVariables = ReadSiteVariables(host, token, root, generation);

            JToken weatherVariables = null;
            foreach (ushort id in weatherCandidates)
            {
                if (!CurrentGeneration(generation)) break;
                JToken variables = ReadSiteVariables(host, token, id, generation);
                if (SiteVariable(variables, "CURRENT_TEMPERATURE") != null) { weatherVariables = variables; break; }
            }
            JToken customVariables = null;
            if (customVariablesAgent != 0 && SitePlaceName(weatherVariables) == null && CurrentGeneration(generation))
                customVariables = ReadSiteVariables(host, token, customVariablesAgent, generation);

            string knownUnit;
            lock (_sync) knownUnit = _siteTemperatureUnit;
            JObject site = BuildPanelSite(rootVariables, weatherVariables, knownUnit, customVariables);
            string unit = ScalarText(site["temperature_unit"]);
            if (unit != null) lock (_sync) _siteTemperatureUnit = unit;
            return site;
        }

        private JToken ReadSiteVariables(string host, string token, ushort id, int generation)
        {
            string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables";
            try
            {
                string json = ReadWithTimeoutRetry(host, token, path, generation);
                if (String.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid variable list size");
                return JToken.Parse(json);
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                lock (_sync) if (!_online) throw;
                return null;
            }
        }

        /// <summary>Builds the panel site document from Director variable lists. Pure, for offline tests.</summary>
        internal static JObject BuildPanelSite(JToken rootVariables, JToken weatherVariables, string knownUnit, JToken customVariables)
        {
            var site = new JObject { ["room_id"] = 0 };
            decimal latitude, longitude;
            if (SiteNumber(SiteVariable(rootVariables, "Latitude"), out latitude) &&
                SiteNumber(SiteVariable(rootVariables, "Longitude"), out longitude) &&
                latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180 && !(latitude == 0 && longitude == 0))
            {
                site["latitude"] = latitude;
                site["longitude"] = longitude;
            }

            // Director does not expose Composer's location name (root /setup 404, root /properties 400,
            // and no location among OpenWeather's 51 properties), so the weather driver may publish it.
            // Order: the weather driver, a Composer custom variable, then the project root item.
            string place = SitePlaceName(weatherVariables) ?? SitePlaceName(customVariables) ?? SitePlaceName(rootVariables);
            if (place != null) site["name"] = place;

            if (weatherVariables == null || SiteVariable(weatherVariables, "CURRENT_TEMPERATURE") == null)
            {
                site["weather_status"] = "unavailable";
                return site;
            }
            decimal humidity, pressure, temperature;
            bool humidityReported = SiteNumber(SiteVariable(weatherVariables, "CURRENT_HUMIDITY"), out humidity) && humidity > 0;
            bool pressureReported = SiteNumber(SiteVariable(weatherVariables, "CURRENT_PRESSURE"), out pressure) && pressure > 0;
            // A driver that is installed but not updating reports 0 for everything.
            if (!humidityReported && !pressureReported ||
                !SiteNumber(SiteVariable(weatherVariables, "CURRENT_TEMPERATURE"), out temperature) || temperature < -100 || temperature > 150)
            {
                site["weather_status"] = "not_reporting";
                return site;
            }
            var weather = new JObject { ["temperature"] = temperature };
            decimal low, high;
            if (SiteNumber(SiteVariable(weatherVariables, "TODAY_TEMPERATURE_MIN"), out low) &&
                SiteNumber(SiteVariable(weatherVariables, "TODAY_TEMPERATURE_MAX"), out high) && low <= high && !(low == 0 && high == 0))
            {
                weather["low"] = low;
                weather["high"] = high;
            }
            if (humidityReported) weather["humidity"] = humidity;
            // Conditions for the Home page overview. OpenWeather reports wind in mph (imperial) or m/s
            // (metric), cloud cover in percent, and rain/snow as the last hour's millimetres.
            decimal wind, clouds, rain, snow;
            if (SiteNumber(SiteVariable(weatherVariables, "CURRENT_WIND_SPEED"), out wind) && wind >= 0 && wind < 300) weather["wind_speed"] = wind;
            if (SiteNumber(SiteVariable(weatherVariables, "CURRENT_CLOUDS"), out clouds) && clouds >= 0 && clouds <= 100) weather["clouds"] = clouds;
            if (SiteNumber(SiteVariable(weatherVariables, "CURRENT_RAIN"), out rain) && rain >= 0 && rain < 1000) weather["rain"] = rain;
            if (SiteNumber(SiteVariable(weatherVariables, "CURRENT_SNOW"), out snow) && snow >= 0 && snow < 1000) weather["snow"] = snow;
            decimal dewPoint;
            string unit = humidityReported && SiteNumber(SiteVariable(weatherVariables, "CURRENT_DEW_POINT"), out dewPoint)
                ? InferTemperatureUnit(temperature, humidity, dewPoint) : null;
            unit = unit ?? (knownUnit == "F" || knownUnit == "C" ? knownUnit : null);
            if (unit != null) site["temperature_unit"] = unit;
            site["weather"] = weather;
            site["weather_status"] = "reporting";
            return site;
        }

        // Variable names a weather driver may use for its place name, in order of preference.
        private static readonly string[] SitePlaceVariables = { "LOCATION_NAME", "CURRENT_LOCATION", "LOCATION", "CITY_NAME", "CITY" };

        /// <summary>First non-empty, display-safe place name variable, or null.</summary>
        internal static string SitePlaceName(JToken weatherVariables)
        {
            foreach (string varName in SitePlaceVariables)
            {
                JToken value = SiteVariable(weatherVariables, varName);
                if (value == null || value.Type != JTokenType.String) continue;
                string text = ((string)value).Trim();
                if (text.Length == 0 || text.Length > 64 || text.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                    text.IndexOfAny(new[] { '<', '>', '{', '}' }) >= 0) continue;
                foreach (char c in text) if (Char.IsControl(c)) { text = null; break; }
                if (text != null) return text;
            }
            return null;
        }

        /// <summary>
        /// "F" or "C" when the reported dew point matches the one calculated from temperature and
        /// humidity (Magnus formula) under exactly one scale; null when the readings cannot tell.
        /// </summary>
        internal static string InferTemperatureUnit(decimal temperature, decimal humidity, decimal dewPoint)
        {
            if (humidity <= 0 || humidity > 100) return null;
            double t = (double)temperature, rh = (double)humidity, dew = (double)dewPoint;
            double asFahrenheit = DewPointCelsius((t - 32) * 5 / 9, rh) * 9 / 5 + 32;
            double asCelsius = DewPointCelsius(t, rh);
            double errorF = Math.Abs(asFahrenheit - dew), errorC = Math.Abs(asCelsius - dew);
            // Integer humidity and rounded readings leave about a degree of slack; require the other
            // scale to be clearly worse so near-saturated air (both close) stays undecided.
            if (errorF <= 1.5 && errorC >= 2.5 && errorC >= errorF * 3) return "F";
            if (errorC <= 1.0 && errorF >= 2.5 && errorF >= errorC * 3) return "C";
            return null;
        }

        private static double DewPointCelsius(double celsius, double humidity)
        {
            const double a = 17.62, b = 243.12;
            double gamma = Math.Log(humidity / 100) + a * celsius / (b + celsius);
            return b * gamma / (a - gamma);
        }

        /// <summary>Value of a variable by its Director varName (not the owning item's name).</summary>
        private static JToken SiteVariable(JToken variables, string varName)
        {
            if (!(variables is JArray)) return null;
            foreach (JToken entry in (JArray)variables)
                if (entry is JObject && String.Equals(ScalarText(entry["varName"]), varName, StringComparison.OrdinalIgnoreCase))
                    return entry["value"] ?? new JValue((object)null);
            return null;
        }

        private static bool SiteNumber(JToken value, out decimal number)
        {
            number = 0;
            return value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float || value.Type == JTokenType.String) &&
                Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }
    }
}
