using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed class Control4_Live_Room_Devices
    {
        private readonly object _sync = new object();
        private ushort _gateway, _port;
        private IPAddress _address;
        private string _key;
        private bool _busy;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;

        private void Report(string text)
        {
            var handler = DiagnosticReceived;
            if (handler != null) handler(this, new GatewayMessageEventArgs { Message = text });
        }

        public void Initialize(ushort gateway, string address, ushort port, string key)
        {
            lock (_sync)
            {
                IPAddress ip;
                if (_busy) return;
                if (gateway == 0 || port < 1024 || !IPAddress.TryParse(address, out ip) ||
                    ip.AddressFamily != AddressFamily.InterNetwork || ip.Equals(IPAddress.Any) || !Hex(key, 64))
                {
                    _gateway = 0;
                    Report("Configure catalog IP, port and 64 lowercase hex key");
                    return;
                }
                _gateway = gateway;
                _address = ip;
                _port = port;
                _key = key;
            }
            Refresh();
        }

        public void Refresh()
        {
            ushort gateway, port;
            IPAddress address;
            string key;
            lock (_sync)
            {
                if (_busy || _gateway == 0) return;
                _busy = true;
                gateway = _gateway; address = _address; port = _port; key = _key;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Report("Reading authenticated Control4 room catalog");
                    string json = Fetch(address, port, key);
                    var rooms = Parse(json);
                    Control4_Room_Devices.ReplaceLive(gateway, rooms);
                    int devices = 0;
                    foreach (var room in rooms.Values) devices += room.Count;
                    Report("Discovered " + devices + " devices. Reselect a room on the panel.");
                }
                catch (Exception ex) { Report("Room catalog failed (" + ex.GetType().Name + ")"); }
                finally { lock (_sync) _busy = false; }
            });
        }

        private static bool Hex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        internal static string Sign(string key, string value)
        {
            using (var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key)))
                return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        private static bool Same(string expected, string actual)
        {
            if (expected.Length != actual.Length) return false;
            int different = 0;
            for (int i = 0; i < expected.Length; i++) different |= expected[i] ^ actual[i];
            return different == 0;
        }

        internal static string Fetch(IPAddress address, ushort port, string key)
        {
            var random = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(random);
            string nonce = BitConverter.ToString(random).Replace("-", "").ToLowerInvariant();
            var clock = Stopwatch.StartNew();
            using (var socket = new TcpClient())
            {
                var pending = socket.BeginConnect(address, port, null, null);
                using (pending.AsyncWaitHandle)
                {
                    if (!pending.AsyncWaitHandle.WaitOne(4000)) throw new TimeoutException();
                    socket.EndConnect(pending);
                }
                using (var stream = socket.GetStream())
                using (var buffer = new MemoryStream())
                {
                    var request = Encoding.ASCII.GetBytes(nonce + Sign(key, "request:" + nonce));
                    stream.WriteTimeout = 4000;
                    stream.Write(request, 0, request.Length);
                    var chunk = new byte[4096];
                    while (true)
                    {
                        int remaining = 10000 - (int)clock.ElapsedMilliseconds;
                        if (remaining <= 0) throw new TimeoutException();
                        stream.ReadTimeout = remaining;
                        int count = stream.Read(chunk, 0, chunk.Length);
                        if (count == 0) break;
                        if (buffer.Length + count > 262209) throw new InvalidDataException();
                        buffer.Write(chunk, 0, count);
                    }
                    var response = new UTF8Encoding(false, true).GetString(buffer.ToArray());
                    if (response.Length < 66 || response[64] != '\n') throw new InvalidDataException();
                    string signature = response.Substring(0, 64), body = response.Substring(65);
                    if (!Hex(signature, 64) || !Same(Sign(key, "response:" + nonce + ":" + body), signature))
                        throw new InvalidDataException();
                    return body;
                }
            }
        }

        internal static Dictionary<ushort, Dictionary<ushort, Control4_Room_Devices.Entry>> Parse(string json)
        {
            var root = JObject.Parse(json);
            var devices = root["devices"] as JArray;
            if (root["v"] == null || root["v"].Type != JTokenType.Integer || (int)root["v"] != 1 ||
                devices == null || devices.Count > 1024) throw new InvalidDataException();
            var result = new Dictionary<ushort, Dictionary<ushort, Control4_Room_Devices.Entry>>();
            var seen = new HashSet<ushort>();
            foreach (JToken row in devices)
            {
                ushort room, id;
                if (!(row is JObject) || !Id(row["room"], out room) || !Id(row["id"], out id) || !seen.Add(id) ||
                    row["shade"] == null || row["shade"].Type != JTokenType.Boolean ||
                    row["name"] == null || row["name"].Type != JTokenType.String) throw new InvalidDataException();
                string name = ((string)row["name"]).Trim();
                if (name.Length == 0 || name.Length > 80 || Array.Exists(name.ToCharArray(), Char.IsControl))
                    throw new InvalidDataException();
                Dictionary<ushort, Control4_Room_Devices.Entry> items;
                if (!result.TryGetValue(room, out items))
                    result.Add(room, items = new Dictionary<ushort, Control4_Room_Devices.Entry>());
                if (items.Count >= 128) throw new InvalidDataException();
                items.Add(id, new Control4_Room_Devices.Entry { Id = id, Name = name, Shade = (bool)row["shade"] });
            }
            return result;
        }

        private static bool Id(JToken token, out ushort id)
        {
            id = 0;
            return token != null && token.Type == JTokenType.Integer &&
                UInt16.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
        }
    }
}
