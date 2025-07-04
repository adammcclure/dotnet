// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class NoRestoreTests
    {
        private const string InteractiveFlag = "--interactive";

        private static DotNetWatchContext CreateContext(string[] args = null, EnvironmentOptions environmentOptions = null)
        {
            environmentOptions ??= TestOptions.GetEnvironmentOptions();

            return new()
            {
                Reporter = NullReporter.Singleton,
                ProcessRunner = new ProcessRunner(environmentOptions.ProcessCleanupTimeout, CancellationToken.None),
                Options = new(),
                RootProjectOptions = TestOptions.GetProjectOptions(args),
                EnvironmentOptions = environmentOptions,
            };
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedOnFirstRun()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));

            evaluator.RequiresRevaluation = true;

            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var context = CreateContext([], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent()
        {
            var context = CreateContext(["--no-restore"], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "--no-restore", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash1()
        {
            var context = CreateContext(["--", "--no-restore"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag, "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", InteractiveFlag, "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash2()
        {
            var context = CreateContext(["--", "--", "--no-restore"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag, "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", InteractiveFlag, "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void AddsNoRestoreSwitch()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            var context = CreateContext(["run", "-f", ToolsetInfo.CurrentTargetFramework]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "-f", ToolsetInfo.CurrentTargetFramework, InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void AddsNoRestoreSwitch_ForTestCommand()
        {
            var context = CreateContext(["test", "--filter SomeFilter"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["test", InteractiveFlag, "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["test", "--no-restore", InteractiveFlag, "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 1));
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public void DoesNotModifyArgumentsForUnknownCommands()
        {
            var context = CreateContext(["pack"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["pack", InteractiveFlag], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["pack", InteractiveFlag], evaluator.GetProcessArguments(iteration: 1));
        }
    }
}
