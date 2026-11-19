using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // ASCII-only serial envelopes: safe on the observed 255-character join path.
    // Requests use the existing 1-based page input, not a periodic network poll.
    internal sealed class UiSerialFrames
    {
        internal const int MaxDocumentBytes = 262144;
        private string _document;
        private readonly List<string> _frames = new List<string>();
        internal int Count { get { return _frames.Count; } }
        internal bool Set(ushort room, string kind, string document)
        {
            if (document == _document && _frames.Count > 0) return false;
            _document = document;
            byte[] bytes = Encoding.UTF8.GetBytes(document);
            if (bytes.Length > MaxDocumentBytes)
                bytes = Encoding.UTF8.GetBytes(new JObject(new JProperty("room_id", room), new JProperty("wire_error", "document_too_large")).ToString(Formatting.None));
            string encoded = Convert.ToBase64String(bytes);
            string epoch = Guid.NewGuid().ToString("N").Substring(0, 16);
            int count = Math.Max(1, (encoded.Length + 159) / 160);
            _frames.Clear();
            for (int i = 0; i < count; i++)
            {
                string part = encoded.Substring(i * 160, Math.Min(160, encoded.Length - i * 160));
                string frame = new JObject(new JProperty("w", 1), new JProperty("r", room), new JProperty("k", kind),
                    new JProperty("e", epoch), new JProperty("p", i + 1), new JProperty("n", count), new JProperty("d", part)).ToString(Formatting.None);
                if (frame.Length > 255) throw new InvalidOperationException("UI serial frame exceeds 255 characters");
                _frames.Add(frame);
            }
            return true;
        }
        internal string Read(ref ushort page)
        {
            page = (ushort)Math.Max(1, Math.Min((int)page, _frames.Count));
            return _frames[page - 1];
        }
        internal static string CompactKeys(string json, ushort room)
        {
            var original = JObject.Parse(json);
            var states = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JObject key in (JArray)original["keys"]) states[(string)key["key"]] = (string)key["state"];
            var codes = new StringBuilder();
            foreach (string key in GatewayCore.Sr260Keys)
            {
                string state; states.TryGetValue(key, out state);
                codes.Append(state == "advertised" ? 'a' : state == "local_ui" ? 'l' : 'u');
            }
            return new JObject(new JProperty("wire", "keys1"), new JProperty("room_id", room), new JProperty("states", codes.ToString())).ToString(Formatting.None);
        }
    }
}
