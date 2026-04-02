using UnityEngine;
using UnityEditor;
using VRTraining.Middleware;

/// <summary>
/// Unity Editor tool to start/stop the local middleware server for testing.
/// Access via menu: Tools > VR Training > Middleware Server
/// Automatically restarts across Play mode transitions.
/// </summary>
[InitializeOnLoad]
public class MiddlewareTestServer : EditorWindow
{
    private const string SessionKey = "MiddlewareTestServer.ShouldRun";

    private static SessionApiServer _server;

    static MiddlewareTestServer()
    {
        // Called after every domain reload (entering/exiting play mode, recompile)
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += AutoRestartIfNeeded;
    }

    [MenuItem("Tools/VR Training/Middleware Server")]
    public static void ShowWindow()
    {
        GetWindow<MiddlewareTestServer>("Middleware Server");
    }

    private void OnGUI()
    {
        bool running = _server != null;

        GUILayout.Label("Local Middleware Server", EditorStyles.boldLabel);
        GUILayout.Label("URL: http://localhost:5080/api");
        GUILayout.Space(10);

        if (!running)
        {
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
            {
                StartServer();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Server is RUNNING", MessageType.Info);
            if (GUILayout.Button("Stop Server", GUILayout.Height(40)))
            {
                StopServer();
            }
        }
    }

    private static void StartServer()
    {
        if (_server != null) return;

        _server = new SessionApiServer(5080);
        _server.Start();
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[Middleware] Server started on http://localhost:5080/api");
    }

    private static void StopServer()
    {
        _server?.Dispose();
        _server = null;
        SessionState.SetBool(SessionKey, false);
        Debug.Log("[Middleware] Server stopped.");
    }

    /// <summary>
    /// After domain reload, restart the server if it was previously running.
    /// </summary>
    private static void AutoRestartIfNeeded()
    {
        if (SessionState.GetBool(SessionKey, false) && _server == null)
        {
            StartServer();
            Debug.Log("[Middleware] Server auto-restarted after domain reload.");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Server is lost during domain reload; restart when new domain is ready
        if (state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            AutoRestartIfNeeded();
        }
    }

    private void OnDestroy()
    {
        // Only stop if the window is explicitly closed, not during domain reload
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            StopServer();
        }
    }
}