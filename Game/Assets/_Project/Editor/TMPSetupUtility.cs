using System.IO;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace TheLightenRoad.Editor
{
    [InitializeOnLoad]
    public static class TMPSetupUtility
    {
        static TMPSetupUtility()
        {
            EditorApplication.delayCall += EnsureTMPEssentials;
        }

        [MenuItem("Tools/The Lighten Road/Import TMP Essential Resources")]
        public static void EnsureTMPEssentials()
        {
            // If TextMesh Pro folder or TMP Settings already exist, no need to re-import
            if (Directory.Exists("Assets/TextMesh Pro") || AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
                return;

            string packageFullPath = TMP_EditorUtility.packageFullPath;
            string packagePath = packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";

            if (File.Exists(packagePath))
            {
                Debug.Log($"[TMPSetupUtility] Auto-importing TMP Essential Resources from '{packagePath}' to initialize TextCore font assets...");
                AssetDatabase.ImportPackage(packagePath, false);
            }
            else
            {
                Debug.LogWarning($"[TMPSetupUtility] Could not locate TMP Essential Resources at '{packagePath}'");
            }
        }
    }
}
