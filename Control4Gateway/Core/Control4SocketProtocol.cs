using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // Socket.IO v4 client / Engine.IO 3 wire format used by the original Python test.
    // Does not open sockets, hold credentials, or perform work at initialization.
    internal static class Control4SocketProtocol
    {
        internal static string Event(string name, string argument)
        {
            var packet = new JArray();
            packet.Add(name);
            if (argument != null) packet.Add(argument);
            return "42" + packet.ToString(Formatting.None);
        }

        internal static bool TryEvent(string packet, out string name, out JToken data)
        {
            string acknowledgement;
            return TryEventPacket(packet, out name, out data, out acknowledgement);
        }

        internal static bool TryEventPacket(string packet, out string name, out JToken data, out string acknowledgement)
        {
            name = null;
            data = null;
            acknowledgement = null;
            if (string.IsNullOrEmpty(packet) || packet.Length > 262144 || !packet.StartsWith("42")) return false;
            int position = 2;
            // Only the default namespace is used by the original Python integration.
            if (position < packet.Length && packet[position] == '/')
            {
                int comma = packet.IndexOf(',', position);
                if (comma != position + 1) return false;
                position = comma + 1;
            }
            int start = position;
            while (position < packet.Length && packet[position] >= '0' && packet[position] <= '9') position++;
            if (position - start > 10 || position >= packet.Length || packet[position] != '[') return false;
            if (position > start) acknowledgement = packet.Substring(start, position - start);
            try
            {
                var values = JToken.Parse(packet.Substring(position)) as JArray;
                if (values == null || values.Count < 1 || values[0].Type != JTokenType.String) return false;
                name = (string)values[0];
                data = values.Count > 1 ? values[1] : null;
                return true;
            }
            catch (JsonReaderException) { return false; }
        }

        internal static bool TryLight(JToken message, out ushort proxyId, out ushort level)
        {
            proxyId = 0;
            level = 0;
            if (message == null || message.Type != JTokenType.Object) return false;
            JToken eventName = message["evtName"];
            if (eventName == null || eventName.Type != JTokenType.String || (string)eventName != "OnDataToUI") return false;
            JToken id = message["iddevice"];
            JToken data = message["data"];
            if (id == null || (id.Type != JTokenType.Integer && id.Type != JTokenType.String) ||
                !UInt16.TryParse(id.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out proxyId) || proxyId == 0 ||
                data == null || data.Type != JTokenType.Object) return false;
            JToken value = data["light_level"] ?? data["LIGHT_LEVEL"];
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float && value.Type != JTokenType.String)) return false;
            decimal number;
            if (!Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                number < 0 || number > 100 || Decimal.Truncate(number) != number) return false;
            level = (ushort)number;
            return true;
        }
    }
}
