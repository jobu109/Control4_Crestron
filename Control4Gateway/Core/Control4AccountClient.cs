using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // Account protocol from pyControl4 2.0.2. Calls are made only by GatewayCore workers.
    internal static class Control4AccountClient
    {
        private const string AuthenticationUrl = "https://apis.control4.com/authentication/v1/rest";
        private const string AccountsUrl = "https://apis.control4.com/account/v3/rest/accounts";
        private const string AuthorizationUrl = "https://apis.control4.com/authentication/v1/rest/authorization";
        // Public pyControl4 application key used by the previously successful account test.
        // It is not a customer credential. A dedicated application key is preferable long term.
        private const string ApplicationKey = "78f6791373d61bea49fdb9fb8897f1f3af193f11";

        internal sealed class DirectorAuthorization
        {
            internal string Token;
            internal int ValidSeconds;
        }

        internal static DirectorAuthorization AcquireDirectorToken(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Account credentials missing");

            var loginBody = new JObject(
                new JProperty("clientInfo", new JObject(
                    new JProperty("device", new JObject(
                        new JProperty("deviceName", "pyControl4"),
                        new JProperty("deviceUUID", "0000000000000000"),
                        new JProperty("make", "pyControl4"),
                        new JProperty("model", "pyControl4"),
                        new JProperty("os", "Android"),
                        new JProperty("osVersion", "10"))),
                    new JProperty("userInfo", new JObject(
                        new JProperty("applicationKey", ApplicationKey),
                        new JProperty("password", password),
                        new JProperty("userName", username))))));

            JObject login = Send("POST", AuthenticationUrl, null, loginBody);
            string accountToken = RequiredString(login, "authToken", "token");
            JObject accounts = Send("GET", AccountsUrl, accountToken, null);
            string commonName = RequiredString(accounts, "account", "controllerCommonName");
            var authorizationBody = new JObject(
                new JProperty("serviceInfo", new JObject(
                    new JProperty("commonName", commonName),
                    new JProperty("services", "director"))));
            JObject authorization = Send("POST", AuthorizationUrl, accountToken, authorizationBody);
            string directorToken = RequiredString(authorization, "authToken", "token");
            int validSeconds = RequiredPositiveInt(authorization, "authToken", "validSeconds");
            return new DirectorAuthorization { Token = directorToken, ValidSeconds = validSeconds };
        }

        private static JObject Send(string method, string url, string bearerToken, JObject body)
        {
            // Cloud HTTPS uses normal certificate validation. No process-wide TLS override.
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.KeepAlive = false;
            request.Accept = "application/json";
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearerToken;
            if (body != null)
            {
                request.ContentType = "application/json";
                byte[] bytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                if (json.Length > 131072) throw new InvalidDataException("Account response too large");
                return JObject.Parse(json);
            }
        }

        private static string RequiredString(JObject root, params string[] path)
        {
            JToken value = Lookup(root, path);
            if (value == null || value.Type != JTokenType.String || string.IsNullOrEmpty((string)value))
                throw new InvalidDataException("Control4 account response missing a required field");
            return (string)value;
        }

        private static int RequiredPositiveInt(JObject root, params string[] path)
        {
            JToken value = Lookup(root, path);
            int seconds;
            if (value == null || !Int32.TryParse(value.ToString(), out seconds) || seconds <= 0)
                throw new InvalidDataException("Control4 account response has invalid token lifetime");
            return seconds;
        }

        private static JToken Lookup(JToken root, string[] path)
        {
            JToken value = root;
            foreach (string segment in path)
            {
                if (value == null || value.Type != JTokenType.Object) return null;
                value = value[segment];
            }
            return value;
        }
    }
}
