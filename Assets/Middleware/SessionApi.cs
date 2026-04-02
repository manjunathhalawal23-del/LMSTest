using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRTraining.Middleware
{
    /// <summary>
    /// Represents a training session bridging the SCORM LMS and the Quest VR app.
    /// </summary>
    public class TrainingSession
    {
        public string Code { get; set; }
        public string LearnerName { get; set; }
        public string LearnerID { get; set; }
        public string CourseTitle { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public bool Activated { get; set; }
        public bool Completed { get; set; }
        public int ScoreRaw { get; set; }
        public int ScoreMin { get; set; }
        public int ScoreMax { get; set; }
        public int PassingScore { get; set; }
        public string Duration { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// Lightweight self-hosted HTTP API for managing VR training sessions.
    /// Compatible with .NET Framework 4.7.1 using HttpListener.
    /// For POC/local testing only. For production, deploy as an Azure Function (.NET 8).
    /// </summary>
    public class SessionApiServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<string, TrainingSession> _sessions
            = new ConcurrentDictionary<string, TrainingSession>();
        private readonly Random _rng = new Random();
        private readonly string _baseUrl;

        /// <summary>
        /// Creates a new API server instance.
        /// </summary>
        /// <param name="port">Port to listen on. Default is 5080.</param>
        public SessionApiServer(int port = 5080)
        {
            //_baseUrl = $"http://+:{port}/api/";
            _baseUrl = $"http://localhost:{port}/api/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(_baseUrl);
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the HTTP API server on a background thread.
        /// </summary>
        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[SessionApi] Listening on {_baseUrl}");
            Task.Run(() => ListenLoop(_cts.Token));
        }

        /// <summary>
        /// Stops the HTTP API server.
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _listener.Close();
            _cts.Dispose();
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SessionApi] Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // Add CORS headers for LMS cross-origin requests
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            try
            {
                // Route: POST /api/session/create
                // Route: GET  /api/session/{code}
                // Route: POST /api/session/complete
                // Route: GET  /api/session/{code}/result

                string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

                if (req.HttpMethod == "POST" && path == "/api/session/create")
                {
                    await HandleCreateSession(req, res);
                }
                else if (req.HttpMethod == "POST" && path == "/api/session/complete")
                {
                    await HandleCompleteSession(req, res);
                }
                else if (req.HttpMethod == "GET" && path.StartsWith("/api/session/") && path.EndsWith("/result"))
                {
                    string code = ExtractCodeFromPath(path, "/result");
                    HandleGetSessionResult(code, res);
                }
                else if (req.HttpMethod == "GET" && path.StartsWith("/api/session/"))
                {
                    string code = path.Substring("/api/session/".Length);
                    HandleGetSession(code, res);
                }
                else
                {
                    WriteJson(res, 404, new { error = "Not found" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SessionApi] Error: {ex.Message}");
                WriteJson(res, 500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Creates a new training session and returns a 6-digit launch code.
        /// Called by the SCORM page when it loads in the LMS.
        /// </summary>
        private async Task HandleCreateSession(HttpListenerRequest req, HttpListenerResponse res)
        {
            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            JObject input = JObject.Parse(body);

            string code;
            do
            {
                code = _rng.Next(100000, 999999).ToString();
            } while (_sessions.ContainsKey(code));

            int expiryMinutes = (int)(input["expiryMinutes"] ?? 60);

            var session = new TrainingSession
            {
                Code = code,
                LearnerName = (string)input["learnerName"] ?? "Unknown",
                LearnerID = (string)input["learnerID"] ?? "unknown",
                CourseTitle = (string)input["courseTitle"] ?? "VR Training",
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Activated = false,
                Completed = false,
                ScoreMax = 100,
                PassingScore = 70
            };

            _sessions[code] = session;

            WriteJson(res, 200, new { code = session.Code, expiresUtc = session.ExpiresUtc });
        }

        /// <summary>
        /// Validates a launch code and returns session info.
        /// Called by the Quest VR app when the learner enters the code.
        /// </summary>
        private void HandleGetSession(string code, HttpListenerResponse res)
        {
            if (!_sessions.TryGetValue(code, out var session))
            {
                WriteJson(res, 404, new { error = "Invalid code" });
                return;
            }

            if (DateTime.UtcNow > session.ExpiresUtc)
            {
                WriteJson(res, 400, new { error = "Session expired" });
                return;
            }

            session.Activated = true;

            WriteJson(res, 200, new
            {
                learnerName = session.LearnerName,
                learnerID = session.LearnerID,
                courseTitle = session.CourseTitle,
                expiresUtc = session.ExpiresUtc
            });
        }

        /// <summary>
        /// Receives training results from the Quest VR app.
        /// </summary>
        private async Task HandleCompleteSession(HttpListenerRequest req, HttpListenerResponse res)
        {
            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            JObject input = JObject.Parse(body);

            string code = (string)input["code"];
            if (string.IsNullOrEmpty(code) || !_sessions.TryGetValue(code, out var session))
            {
                WriteJson(res, 404, new { error = "Invalid code" });
                return;
            }

            session.Completed = true;
            session.ScoreRaw = (int)(input["scoreRaw"] ?? 0);
            session.ScoreMin = (int)(input["scoreMin"] ?? 0);
            session.ScoreMax = (int)(input["scoreMax"] ?? 100);
            session.PassingScore = (int)(input["passingScore"] ?? 70);
            session.Duration = (string)input["duration"] ?? "N/A";
            session.Details = (string)input["details"] ?? "";

            WriteJson(res, 200, new { success = true });
        }

        /// <summary>
        /// Returns training results for a given session code.
        /// Called by the SCORM page via polling to detect completion.
        /// </summary>
        private void HandleGetSessionResult(string code, HttpListenerResponse res)
        {
            if (!_sessions.TryGetValue(code, out var session))
            {
                WriteJson(res, 404, new { error = "Invalid code" });
                return;
            }

            WriteJson(res, 200, new
            {
                completed = session.Completed,
                scoreRaw = session.ScoreRaw,
                scoreMin = session.ScoreMin,
                scoreMax = session.ScoreMax,
                passingScore = session.PassingScore,
                duration = session.Duration,
                details = session.Details
            });
        }

        private static string ExtractCodeFromPath(string path, string suffix)
        {
            string trimmed = path.Substring(0, path.Length - suffix.Length);
            return trimmed.Substring(trimmed.LastIndexOf('/') + 1);
        }

        private static void WriteJson(HttpListenerResponse res, int statusCode, object data)
        {
            res.StatusCode = statusCode;
            res.ContentType = "application/json";
            string json = JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
            res.Close();
        }
    }
}