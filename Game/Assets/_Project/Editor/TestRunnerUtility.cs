using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TheLightenRoad.Editor
{
    public static class TestRunnerUtility
    {
        private class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("[TestRunner] Starting PlayMode tests...");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[TestRunner] FINISHED: {result.PassCount} passed, {result.FailCount} failed, {result.InconclusiveCount} inconclusive, {result.SkipCount} skipped in {result.Duration:F2}s");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[TestRunner] FAILED: {result.FullName}\n{result.Message}\n{result.StackTrace}");
                }
                else if (result.TestStatus == TestStatus.Passed && !result.HasChildren)
                {
                    Debug.Log($"[TestRunner] PASSED: {result.Name}");
                }
            }
        }

        [MenuItem("Tools/The Lighten Road/Run PlayMode Tests")]
        public static void RunPlayModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.PlayMode
            };
            api.RegisterCallbacks(new TestCallbacks());
            api.Execute(new ExecutionSettings(filter));
        }
    }
}
