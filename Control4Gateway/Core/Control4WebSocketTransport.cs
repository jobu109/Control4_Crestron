using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Control4Crestron
{
    // Per-connection TLS callback keeps local controller trust separate from cloud HTTPS.
    // A single session thread reads; heartbeat and protocol replies share the send lock.
    internal sealed class Control4WebSocketTransport : IDisposable
    {
        private readonly object _sendSync = new object();
        private readonly TcpClient _tcp = new TcpClient();
        private SslStream _stream;
        private readonly MemoryStream _message = new MemoryStream();
        private bool _fragmented;
        private const int MaxMessage = 262144;
        internal string Stage { get; set; }

        internal void Connect(string host, string token, RemoteCertificateValidationCallback validate, int port = 443)
        {
            Stage = "TCP connect";
            var pending = _tcp.BeginConnect(host, port, null, null);
            using (pending.AsyncWaitHandle)
            {
                if (!pending.AsyncWaitHandle.WaitOne(6000)) throw new IOException("Connection timeout");
                _tcp.EndConnect(pending);
            }
            Stage = "TLS setup";
            _stream = new SslStream(_tcp.GetStream(), false, validate);
            _stream.ReadTimeout = 10000;
            _stream.WriteTimeout = 6000;
            Stage = "TLS handshake";
            _stream.AuthenticateAsClient(host, null, System.Security.Authentication.SslProtocols.Tls12, false);
            Stage = "WebSocket upgrade send";
            byte[] random = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(random);
            string key = Convert.ToBase64String(random);
            // Reject control characters before constructing HTTP headers.
            if (token.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new InvalidDataException("Invalid authorization");
            string request = "GET /socket.io/?EIO=3&transport=websocket HTTP/1.1\r\nHost: " + host +
                "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: " + key +
                "\r\nJWT: " + token + "\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            _stream.Write(bytes, 0, bytes.Length);
            Stage = "WebSocket upgrade response";
            // Read exactly the HTTP header so the first WebSocket frame is not consumed.
            var header = new StringBuilder();
            while (header.Length < 16384)
            {
                int value = _stream.ReadByte();
                if (value < 0) throw new EndOfStreamException();
                header.Append((char)value);
                if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n") break;
            }
            string[] lines = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (!header.ToString().EndsWith("\r\n\r\n") || !lines[0].StartsWith("HTTP/1.1 101 "))
                throw new InvalidDataException("WebSocket upgrade rejected");
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) headers[lines[i].Substring(0, colon)] = lines[i].Substring(colon + 1).Trim();
            }
            string accept;
            using (var sha = SHA1.Create())
                accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            string actual, upgrade, connection;
            if (!headers.TryGetValue("Sec-WebSocket-Accept", out actual) || actual != accept ||
                !headers.TryGetValue("Upgrade", out upgrade) || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase) ||
                !headers.TryGetValue("Connection", out connection) || connection.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidDataException("Invalid WebSocket upgrade");
            _stream.ReadTimeout = 60000;
            Stage = "Engine.IO opening";
        }

        internal void Send(string text) { SendFrame(1, Encoding.UTF8.GetBytes(text)); }

        private void SendFrame(int opcode, byte[] payload)
        {
            if (payload.Length > MaxMessage) throw new InvalidDataException("Frame too large");
            lock (_sendSync)
            {
                byte[] mask = new byte[4];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(mask);
                using (var frame = new MemoryStream())
                {
                    frame.WriteByte((byte)(128 | opcode));
                    if (payload.Length < 126) frame.WriteByte((byte)(128 | payload.Length));
                    else if (payload.Length <= 65535)
                    {
                        frame.WriteByte(254);
                        frame.WriteByte((byte)(payload.Length >> 8)); frame.WriteByte((byte)payload.Length);
                    }
                    else
                    {
                        frame.WriteByte(255);
                        for (int shift = 56; shift >= 0; shift -= 8) frame.WriteByte((byte)((ulong)payload.Length >> shift));
                    }
                    frame.Write(mask, 0, 4);
                    for (int i = 0; i < payload.Length; i++) frame.WriteByte((byte)(payload[i] ^ mask[i % 4]));
                    byte[] bytes = frame.ToArray();
                    _stream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        internal string Receive()
        {
            while (true)
            {
                int first = ReadByte(), second = ReadByte();
                int opcode = first & 15;
                bool final = (first & 128) != 0;
                if ((first & 112) != 0 || (second & 128) != 0) throw new InvalidDataException("Invalid frame flags");
                ulong length = (ulong)(second & 127);
                if (length == 126) length = (ulong)((ReadByte() << 8) | ReadByte());
                else if (length == 127)
                {
                    length = 0;
                    for (int i = 0; i < 8; i++) length = (length << 8) | (uint)ReadByte();
                }
                if (length > MaxMessage || (opcode >= 8 && (!final || length > 125)))
                    throw new InvalidDataException("Invalid frame length");
                byte[] payload = new byte[(int)length];
                for (int offset = 0; offset < payload.Length;)
                {
                    int count = _stream.Read(payload, offset, payload.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                if (opcode == 8) { SendFrame(8, payload); throw new EndOfStreamException(); }
                if (opcode == 9) { SendFrame(10, payload); continue; }
                if (opcode == 10) continue;
                if ((opcode != 0 && opcode != 1) || (opcode == 0 && !_fragmented) || (opcode == 1 && _fragmented))
                    throw new InvalidDataException("Unexpected frame opcode");
                if (_message.Length + payload.Length > MaxMessage) throw new InvalidDataException("Message too large");
                _message.Write(payload, 0, payload.Length);
                _fragmented = !final;
                if (!final) continue;
                string result = new UTF8Encoding(false, true).GetString(_message.ToArray());
                _message.SetLength(0);
                return result;
            }
        }
        private int ReadByte()
        {
            int value = _stream.ReadByte();
            if (value < 0) throw new EndOfStreamException();
            return value;
        }
        public void Dispose()
        {
            // Closing TCP first interrupts a blocked TLS receive on Disconnect.
            _tcp.Close();
            if (_stream != null) _stream.Dispose();
        }
    }
}
