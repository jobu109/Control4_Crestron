using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Renci.SshNet;

namespace Control4GatewayTools
{
    // PC-side collector only. This assembly is never included in the RMC4 CLZ.
    public sealed class ReportSftpConnection : IDisposable
    {
        private readonly SftpClient client;
        private readonly string pinFile;
        public string Fingerprint { get; private set; }
        public bool HostKeyChanged { get; private set; }
        public bool IsConnected { get { return client.IsConnected; } }

        public ReportSftpConnection(string address, string username, string password, string pinFile)
        {
            this.pinFile = pinFile;
            client = new SftpClient(address, 22, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
            client.OperationTimeout = TimeSpan.FromSeconds(10);
            client.KeepAliveInterval = TimeSpan.FromSeconds(15);
            // SSH.NET invokes this on its worker thread, outside PowerShell's runspace.
            client.HostKeyReceived += delegate(object sender, Renci.SshNet.Common.HostKeyEventArgs args)
            {
                using (var sha = SHA256.Create())
                    Fingerprint = "SHA256:" + Convert.ToBase64String(sha.ComputeHash(args.HostKey)).TrimEnd('=');
                HostKeyChanged = File.Exists(pinFile) && File.ReadAllText(pinFile).Trim() != Fingerprint;
                args.CanTrust = !HostKeyChanged;
            };
        }

        public void Connect()
        {
            HostKeyChanged = false;
            client.Connect();
            // Trust on first successful connection; later key changes are rejected.
            if (!File.Exists(pinFile)) File.WriteAllText(pinFile, Fingerprint + Environment.NewLine);
        }

        public string Stamp(string remotePath)
        {
            if (!client.Exists(remotePath)) return null;
            var attributes = client.GetAttributes(remotePath);
            if (!attributes.IsRegularFile || attributes.Size > 8388608)
                throw new InvalidDataException("Report is not a bounded regular file");
            return attributes.Size.ToString(CultureInfo.InvariantCulture) + ":" + attributes.LastWriteTimeUtc.Ticks;
        }

        public void Download(string remotePath, string destination)
        {
            using (var stream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                client.DownloadFile(remotePath, stream);
        }

        public void Dispose() { client.Dispose(); }
    }
}
