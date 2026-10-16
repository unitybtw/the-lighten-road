using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace TheLightenRoad.Editor
{
    [InitializeOnLoad]
    public static class McpBridgeUtility
    {
        private const string KeyAutoStartOnLoad = "MCPForUnity.AutoStartOnLoad";
        private const string KeyUseHttpTransport = "MCPForUnity.UseHttpTransport";

        static McpBridgeUtility()
        {
            EditorPrefs.SetBool(KeyAutoStartOnLoad, true);
            EditorPrefs.SetBool(KeyUseHttpTransport, true);
            EditorApplication.delayCall += EnsureConnected;
        }

        private static void EnsureConnected()
        {
            try
            {
                if (!MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
                {
                    _ = ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[McpBridgeUtility] Auto-connect attempt: {ex.Message}");
            }
        }

        [MenuItem("Tools/MCP/Connect Bridge")]
        public static void ConnectMenu()
        {
            _ = ConnectAsync();
        }

        public static async Task<bool> ConnectAsync()
        {
            try
            {
                bool started = await MCPServiceLocator.Bridge.StartAsync();
                if (started)
                {
                    Debug.Log("[McpBridgeUtility] Unity MCP Bridge connected successfully.");
                }
                return started;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[McpBridgeUtility] Failed to start MCP bridge: {ex.Message}");
                return false;
            }
        }
    }
}
