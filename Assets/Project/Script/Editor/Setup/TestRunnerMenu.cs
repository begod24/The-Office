using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Office.Editor
{
    public static class TestRunnerMenu
    {
        [MenuItem("Office/Tests/Run EditMode Tests", priority = 100)]
        public static void RunEditModeTests() => Run(TestMode.EditMode);

        [MenuItem("Office/Tests/Run PlayMode Tests", priority = 101)]
        public static void RunPlayModeTests() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new SummaryCallbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private sealed class SummaryCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new StringBuilder();

                report.Append("[Tests] ")
                    .Append(result.PassCount).Append(" passed, ")
                    .Append(result.FailCount).Append(" failed, ")
                    .Append(result.SkipCount).Append(" skipped in ")
                    .Append(result.Duration.ToString("F2")).Append("s.");

                if (result.FailCount > 0) AppendFailures(result, report);

                if (result.FailCount > 0) Debug.LogError(report.ToString());
                else Debug.Log(report.ToString());
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            private static void AppendFailures(ITestResultAdaptor node, StringBuilder report)
            {
                if (!node.HasChildren)
                {
                    if (node.TestStatus != TestStatus.Failed) return;

                    report.AppendLine().Append("  FAIL ").Append(node.FullName)
                        .AppendLine().Append("       ").Append(node.Message);
                    return;
                }

                foreach (var child in node.Children) AppendFailures(child, report);
            }
        }
    }
}
