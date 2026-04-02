using UnityEngine;
using UnityEditor;
using VRTraining.Middleware;

/// <summary>
/// Unity Editor tool to start/stop the local middleware server for testing.
/// Access via menu: Tools > VR Training > Start Middleware Server
/// </summary>
public class MiddlewareTestServer : EditorWindow
{
    private static SessionApiServer _server;
    private static bool _running;

    [MenuItem("Tools/VR Training/Middleware Server")]
    public static void ShowWindow()
    {
        GetWindow<MiddlewareTestServer>("Middleware Server");
    }

    private void OnGUI()
    {
        GUILayout.Label("Local Middleware Server", EditorStyles.boldLabel);
        GUILayout.Label("URL: http://localhost:5080/api");
        GUILayout.Space(10);

        if (!_running)
        {
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
            {
                _server = new SessionApiServer(5080);
                _server.Start();
                _running = true;
                Debug.Log("[Middleware] Server started on http://localhost:5080/api");
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Server is RUNNING", MessageType.Info);
            if (GUILayout.Button("Stop Server", GUILayout.Height(40)))
            {
                _server?.Dispose();
                _server = null;
                _running = false;
                Debug.Log("[Middleware] Server stopped.");
            }
        }
    }

    private void OnDestroy()
    {
        _server?.Dispose();
        _server = null;
        _running = false;
    }
}