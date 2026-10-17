using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The real transport against a one-reply server on this PC; nothing leaves the machine.
    /// </summary>
    public class TransportTests
    {
        /// <summary>
        /// Answers one request with the given status and body, and records what it received.
        /// </summary>
        private sealed class OneReplyServer : IDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            public readonly Task<string> Received;
            public string Url => "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;

            public OneReplyServer(int status, string body)
            {
                listener.Start();
                Received = Task.Run(() =>
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();
                    string request = ReadRequest(stream);
                    byte[] content = Encoding.UTF8.GetBytes(body);
                    string head = "HTTP/1.1 " + status + " X\r\nContent-Type: application/json\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n";
                    byte[] headBytes = Encoding.ASCII.GetBytes(head);
                    stream.Write(headBytes, 0, headBytes.Length);
                    stream.Write(content, 0, content.Length);
                    return request;
                });
            }

            private static string ReadRequest(Stream stream)
            {
                MemoryStream buffer = new();
                byte[] chunk = new byte[4096];
                while (true)
                {
                    int read = stream.Read(chunk, 0, chunk.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    buffer.Write(chunk, 0, read);
                    string text = Encoding.UTF8.GetString(buffer.ToArray());
                    int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (end < 0)
                    {
                        continue;
                    }
                    int length = 0;
                    foreach (string line in text.Substring(0, end).Split(new[] { "\r\n" }, StringSplitOptions.None))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            length = int.Parse(line.Substring(15).Trim());
                        }
                    }
                    if (buffer.Length >= Encoding.UTF8.GetByteCount(text.Substring(0, end + 4)) + length)
                    {
                        return text;
                    }
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            public void Dispose() => listener.Stop();
        }

        private static HttpRequestSpec Request(string url, string body = "{\"hello\":\"ナナ\"}")
        {
            HttpRequestSpec spec = new() { Url = url + "/v1/messages", Body = body };
            spec.Headers.Add(new System.Collections.Generic.KeyValuePair<string, string>("Content-Type", "application/json"));
            spec.Headers.Add(new System.Collections.Generic.KeyValuePair<string, string>("x-api-key", "k-123"));
            spec.Headers.Add(new System.Collections.Generic.KeyValuePair<string, string>("anthropic-version", "2023-06-01"));
            return spec;
        }

        [Fact]
        public void SendsHeadersAndBody_ReadsTheReply()
        {
            using OneReplyServer server = new(200, "{\"ok\":\"日本\"}");
            HttpResult result = NetTransport.Exchange(Request(server.Url));
            string received = server.Received.Result;

            Assert.Equal(200, result.Status);
            Assert.Equal("{\"ok\":\"日本\"}", result.Body);
            Assert.Null(result.NetworkError);
            Assert.StartsWith("POST /v1/messages HTTP/1.1", received);
            Assert.Contains("Content-Type: application/json", received);
            Assert.Contains("x-api-key: k-123", received);
            Assert.Contains("anthropic-version: 2023-06-01", received);
            Assert.EndsWith("{\"hello\":\"ナナ\"}", received);
        }

        [Fact]
        public void HttpError_KeepsTheServicesMessage()
        {
            using OneReplyServer server = new(401, "{\"error\":{\"message\":\"invalid x-api-key\"}}");
            HttpResult result = NetTransport.Exchange(Request(server.Url));
            Assert.Equal(401, result.Status);
            Assert.Contains("invalid x-api-key", result.Body);
        }

        [Fact]
        public void NothingListening_IsANetworkError()
        {
            TcpListener free = new(IPAddress.Loopback, 0);
            free.Start();
            int port = ((IPEndPoint)free.LocalEndpoint).Port;
            free.Stop();

            HttpResult result = NetTransport.Exchange(Request("http://127.0.0.1:" + port));
            Assert.Equal(0, result.Status);
            Assert.False(string.IsNullOrEmpty(result.NetworkError));
        }
    }
}
