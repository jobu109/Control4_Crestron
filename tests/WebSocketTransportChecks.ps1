$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '../Control4Gateway/Core/Control4WebSocketTransport.cs'
$checks = @'
namespace Control4Crestron
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Net.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;

    public static class TransportChecks
    {
        public static void Run(X509Certificate2 certificate)
        {
            Scenario(certificate, false);
            Scenario(certificate, true);
        }
        private static byte[] ReadFrame(Stream stream, int expectedOpcode)
        {
            int first = stream.ReadByte(), second = stream.ReadByte();
            if ((first & 15) != expectedOpcode || (second & 128) == 0) throw new Exception("Client frame must be masked");
            int length = second & 127;
            if (length == 126) length = (stream.ReadByte() << 8) | stream.ReadByte();
            if (length == 127) throw new Exception("Unexpected large fixture frame");
            byte[] mask = new byte[4], bytes = new byte[length];
            for (int i = 0; i < 4; i++) mask[i] = (byte)stream.ReadByte();
            for (int i = 0; i < length; i++) bytes[i] = (byte)(stream.ReadByte() ^ mask[i % 4]);
            return bytes;
        }
        private static void Scenario(X509Certificate2 certificate, bool badAccept)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Exception serverError = null;
            var server = new Thread(delegate()
            {
                try
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var ssl = new SslStream(client.GetStream(), false))
                    {
                        ssl.ReadTimeout = 4000; ssl.WriteTimeout = 4000;
                        ssl.AuthenticateAsServer(certificate, false, System.Security.Authentication.SslProtocols.Tls12, false);
                        var header = new StringBuilder();
                        while (!header.ToString().EndsWith("\r\n\r\n"))
                        {
                            int b = ssl.ReadByte();
                            if (b < 0 || header.Length > 16384) throw new Exception("Missing request");
                            header.Append((char)b);
                        }
                        string key = null;
                        foreach (string line in header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                            if (line.StartsWith("Sec-WebSocket-Key: ")) key = line.Substring(19);
                        if (!header.ToString().Contains("EIO=3&transport=websocket") ||
                            !header.ToString().Contains("JWT: fixture-token\r\n")) throw new Exception("Wrong handshake");
                        string accept;
                        using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                        byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + (badAccept ? "wrong" : accept) + "\r\n\r\n");
                        ssl.Write(response, 0, response.Length);
                        if (badAccept) return;
                        // Fragmented text with an interleaved WebSocket ping.
                        byte[] frames = { 1, 2, 104, 101, 137, 1, 120, 128, 3, 108, 108, 111 };
                        ssl.Write(frames, 0, frames.Length);
                        if (Encoding.UTF8.GetString(ReadFrame(ssl, 10)) != "x") throw new Exception("Wrong pong");
                        string text = Encoding.UTF8.GetString(ReadFrame(ssl, 1));
                        if (text != new string('a', 200)) throw new Exception("Extended-length send failed");
                        byte[] invalid = { 129, 128 }; // Server masking is forbidden.
                        ssl.Write(invalid, 0, invalid.Length);
                    }
                }
                catch (Exception ex) { serverError = ex; }
            });
            server.IsBackground = true;
            server.Start();
            try
            {
                using (var transport = new Control4WebSocketTransport())
                {
                    bool rejected = false;
                    try { transport.Connect("127.0.0.1", "fixture-token", delegate { return true; }, port); }
                    catch (InvalidDataException) { rejected = true; }
                    if (rejected != badAccept) throw new Exception("Upgrade validation failed");
                    if (!badAccept)
                    {
                        if (transport.Receive() != "hello") throw new Exception("Fragment assembly failed");
                        transport.Send(new string('a', 200));
                        bool invalidRejected = false;
                        try { transport.Receive(); } catch (InvalidDataException) { invalidRejected = true; }
                        if (!invalidRejected) throw new Exception("Masked server frame accepted");
                    }
                }
            }
            finally
            {
                if (!server.Join(5000)) throw new Exception("Mock server did not terminate");
                listener.Stop();
            }
            if (serverError != null) throw new Exception("Mock server failed", serverError);
        }
    }
}
'@
Add-Type -TypeDefinition ([IO.File]::ReadAllText($sourcePath) + $checks)
$key = [Security.Cryptography.RSA]::Create(2048)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $key, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $generated = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddHours(1))
    try {
        # Schannel requires a key container for the mock TLS server; do not install a certificate in a trust store.
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($generated.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, 'fixture'), 'fixture', [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    } finally { $generated.Dispose() }
    try { [Control4Crestron.TransportChecks]::Run($certificate) } finally { $certificate.Dispose() }
} finally { $key.Dispose() }
Write-Output 'Transport checks passed: TLS upgrade, accept validation, fragmentation, ping/pong, masked client send, extended length, invalid server masking.'
