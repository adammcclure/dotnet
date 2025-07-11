﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting.Test;
using Microsoft.CodeAnalysis.Scripting.TestUtilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.Scripting.UnitTests
{
    public class ScriptTests : CSharpScriptTestBase
    {
        public class Globals
        {
            public int X;
            public int Y;
        }

        [Fact]
        public void TestCreateScript()
        {
            var script = CSharpScript.Create("1 + 2", ScriptOptions);
            Assert.Equal("1 + 2", script.Code);
        }

        [Fact]
        public void TestCreateScript_CodeIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => CSharpScript.Create((string)null));
        }

        [Fact]
        public void TestCreateFromStreamScript()
        {
            var script = CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("1 + 2")), ScriptOptions);
            Assert.Equal("1 + 2", script.Code);
        }

        [Fact]
        public void TestCreateFromStreamScript_StreamIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => CSharpScript.Create((Stream)null, ScriptOptions));
        }

        [Fact]
        public async Task TestGetCompilation()
        {
            var state = await CSharpScript.RunAsync("1 + 2", options: ScriptOptions, globals: new ScriptTests());
            var compilation = state.Script.GetCompilation();
            Assert.Equal(state.Script.Code, compilation.SyntaxTrees.First().GetText().ToString());
        }

        [Fact]
        public async Task TestGetCompilationSourceText()
        {
            var state = await CSharpScript.RunAsync("1 + 2", options: ScriptOptions, globals: new ScriptTests());
            var compilation = state.Script.GetCompilation();
            Assert.Equal(state.Script.SourceText, compilation.SyntaxTrees.First().GetText());
        }

        [Fact]
        public void TestEmit_PortablePdb() => TestEmit(DebugInformationFormat.PortablePdb);

        [ConditionalFact(typeof(WindowsOnly))]
        public void TestEmit_WindowsPdb() => TestEmit(DebugInformationFormat.Pdb);

        private void TestEmit(DebugInformationFormat format)
        {
            var script = CSharpScript.Create("1 + 2", options: ScriptOptions.WithEmitDebugInformation(true));
            var compilation = script.GetCompilation();
            var emitOptions = ScriptBuilder.GetEmitOptions(emitDebugInformation: true).WithDebugInformationFormat(format);

            var peStream = new MemoryStream();
            var pdbStream = new MemoryStream();
            var emitResult = ScriptBuilder.Emit(peStream, pdbStream, compilation, emitOptions, cancellationToken: default);

            peStream.Position = 0;
            pdbStream.Position = 0;

            PdbValidation.ValidateDebugDirectory(
                peStream,
                portablePdbStreamOpt: (format == DebugInformationFormat.PortablePdb) ? pdbStream : null,
                pdbPath: compilation.AssemblyName + ".pdb",
                hashAlgorithm: default,
                hasEmbeddedPdb: false,
                isDeterministic: false);
        }

        [Fact]
        public async Task TestCreateScriptDelegate()
        {
            // create a delegate for the entire script
            var script = CSharpScript.Create("1 + 2", ScriptOptions);
            var fn = script.CreateDelegate();

            Assert.Equal(3, fn().Result);
            await Assert.ThrowsAsync<ArgumentException>("globals", () => fn(new object()));
        }

        [Fact]
        public async Task TestCreateScriptDelegateWithGlobals()
        {
            // create a delegate for the entire script
            var script = CSharpScript.Create<int>("X + Y", options: ScriptOptions, globalsType: typeof(Globals));
            var fn = script.CreateDelegate();

            await Assert.ThrowsAsync<ArgumentException>("globals", () => fn());
            await Assert.ThrowsAsync<ArgumentException>("globals", () => fn(new object()));
            Assert.Equal(4, fn(new Globals { X = 1, Y = 3 }).Result);
        }

        [Fact]
        public async Task TestRunScript()
        {
            var state = await CSharpScript.RunAsync("1 + 2", ScriptOptions);
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestCreateAndRunScript()
        {
            var script = CSharpScript.Create("1 + 2", ScriptOptions);
            var state = await script.RunAsync();
            Assert.Same(script, state.Script);
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestCreateFromStreamAndRunScript()
        {
            var script = CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("1 + 2")), ScriptOptions);
            var state = await script.RunAsync();
            Assert.Same(script, state.Script);
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestEvalScript()
        {
            var value = await CSharpScript.EvaluateAsync("1 + 2", ScriptOptions);
            Assert.Equal(3, value);
        }

        [Fact]
        public async Task TestRunScriptWithSpecifiedReturnType()
        {
            var state = await CSharpScript.RunAsync("1 + 2", ScriptOptions);
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public void TestRunVoidScript()
        {
            var state = ScriptingTestHelpers.RunScriptWithOutput(
                CSharpScript.Create("System.Console.WriteLine(0);", ScriptOptions),
                "0");
            Assert.Null(state.ReturnValue);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/5279")]
        public async Task TestRunExpressionStatement()
        {
            var state = await CSharpScript.RunAsync(
@"int F() { return 1; }
F();", ScriptOptions);
            Assert.Null(state.ReturnValue);
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/170")]
        public async Task TestRunDynamicVoidScriptWithTerminatingSemicolon()
        {
            await CSharpScript.RunAsync(@"
class SomeClass
{
    public void Do()
    {
    }
}
dynamic d = new SomeClass();
d.Do();"
, ScriptOptions.WithReferences(MscorlibRef, SystemRef, SystemCoreRef, CSharpRef));
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/170")]
        public async Task TestRunDynamicVoidScriptWithoutTerminatingSemicolon()
        {
            await CSharpScript.RunAsync(@"
class SomeClass
{
    public void Do()
    {
    }
}
dynamic d = new SomeClass();
d.Do()"
, ScriptOptions.WithReferences(MscorlibRef, SystemRef, SystemCoreRef, CSharpRef));
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6676")]
        public void TestRunEmbeddedStatementNotFollowedBySemicolon()
        {
            var exceptionThrown = false;

            try
            {
                var state = CSharpScript.RunAsync(@"if (true)
 System.Console.WriteLine(true)", options: ScriptOptions, globals: new ScriptTests());
            }
            catch (CompilationErrorException ex)
            {
                exceptionThrown = true;
                ex.Diagnostics.Verify(
                // (2,32): error CS1002: ; expected
                //  System.Console.WriteLine(true)
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(2, 32));
            }

            Assert.True(exceptionThrown);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6676")]
        public void TestRunEmbeddedStatementFollowedBySemicolon()
        {
            var state = CSharpScript.RunAsync(@"if (true)
System.Console.WriteLine(true);", options: ScriptOptions, globals: new ScriptTests());
            Assert.Null(state.Exception);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6676")]
        public void TestRunStatementFollowedBySpace()
        {
            var state = CSharpScript.RunAsync(@"System.Console.WriteLine(true) ", options: ScriptOptions, globals: new ScriptTests());
            Assert.Null(state.Exception);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6676")]
        public void TestRunStatementFollowedByNewLineNoSemicolon()
        {
            var state = CSharpScript.RunAsync(@"
System.Console.WriteLine(true)

", options: ScriptOptions, globals: new ScriptTests());
            Assert.Null(state.Exception);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6676")]
        public void TestRunEmbeddedNoSemicolonFollowedByAnotherStatement()
        {
            var exceptionThrown = false;

            try
            {
                var state = CSharpScript.RunAsync(@"if (e) a = b 
throw e;", options: ScriptOptions, globals: new ScriptTests());
            }
            catch (CompilationErrorException ex)
            {
                exceptionThrown = true;
                // Verify that it produces a single ExpectedSemicolon error. 
                // No duplicates for the same error.
                ex.Diagnostics.Verify(
                // (1,13): error CS1002: ; expected
                // if (e) a = b 
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(1, 13));
            }

            Assert.True(exceptionThrown);
        }

        [Fact]
        public async Task TestRunScriptWithGlobals()
        {
            var state = await CSharpScript.RunAsync("X + Y", options: ScriptOptions, globals: new Globals { X = 1, Y = 2 });
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestRunCreatedScriptWithExpectedGlobals()
        {
            var script = CSharpScript.Create("X + Y", options: ScriptOptions, globalsType: typeof(Globals));
            var state = await script.RunAsync(new Globals { X = 1, Y = 2 });
            Assert.Equal(3, state.ReturnValue);
            Assert.Same(script, state.Script);
        }

        [Fact]
        public async Task TestRunCreatedScriptWithUnexpectedGlobals()
        {
            var script = CSharpScript.Create("X + Y", ScriptOptions);

            // Global variables passed to a script without a global type
            await Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync(new Globals { X = 1, Y = 2 }));
        }

        [Fact]
        public async Task TestRunCreatedScriptWithoutGlobals()
        {
            var script = CSharpScript.Create("X + Y", options: ScriptOptions, globalsType: typeof(Globals));

            //  The script requires access to global variables but none were given
            await Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync());
        }

        [Fact]
        public async Task TestRunCreatedScriptWithMismatchedGlobals()
        {
            var script = CSharpScript.Create("X + Y", options: ScriptOptions, globalsType: typeof(Globals));

            //  The globals of type 'System.Object' is not assignable to 'Microsoft.CodeAnalysis.CSharp.Scripting.Test.ScriptTests+Globals'
            await Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync(new object()));
        }

        [Fact]
        public async Task ContinueAsync_Error1()
        {
            var state = await CSharpScript.RunAsync("X + Y", options: ScriptOptions, globals: new Globals());

            await Assert.ThrowsAsync<ArgumentNullException>("previousState", () => state.Script.RunFromAsync(null));
        }

        [Fact]
        public async Task ContinueAsync_Error2()
        {
            var state1 = await CSharpScript.RunAsync("X + Y + 1", options: ScriptOptions, globals: new Globals());
            var state2 = await CSharpScript.RunAsync("X + Y + 2", options: ScriptOptions, globals: new Globals());

            await Assert.ThrowsAsync<ArgumentException>("previousState", () => state1.Script.RunFromAsync(state2));
        }

        [Fact]
        public async Task TestRunScriptWithScriptState()
        {
            // run a script using another scripts end state as the starting state (globals)
            var state = await CSharpScript.RunAsync("int X = 100;", ScriptOptions).ContinueWith("X + X");
            Assert.Equal(200, state.ReturnValue);
        }

        [Fact]
        public async Task TestRepl()
        {
            var submissions = new[]
            {
                "int x = 100;",
                "int y = x * x;",
                "x + y"
            };

            var state = await CSharpScript.RunAsync("", ScriptOptions);
            foreach (var submission in submissions)
            {
                state = await state.ContinueWithAsync(submission);
            }

            Assert.Equal(10100, state.ReturnValue);
        }

#if TODO // https://github.com/dotnet/roslyn/issues/3720
        [Fact]
        public void TestCreateMethodDelegate()
        {
            // create a delegate to a method declared in the script
            var state = CSharpScript.Run("int Times(int x) { return x * x; }");
            var fn = state.CreateDelegate<Func<int, int>>("Times");
            var result = fn(5);
            Assert.Equal(25, result);
        }
#endif

        [Fact]
        public async Task ScriptVariables_Chain()
        {
            var globals = new Globals { X = 10, Y = 20 };

            var script =
                CSharpScript.Create(
                    "var a = '1';",
                    options: ScriptOptions,
                    globalsType: globals.GetType()).
                ContinueWith("var b = 2u;").
                ContinueWith("var a = 3m;").
                ContinueWith("var x = a + b;").
                ContinueWith("var X = Y;");

            var state = await script.RunAsync(globals);

            AssertEx.Equal(new[] { "a", "b", "a", "x", "X" }, state.Variables.Select(v => v.Name));
            AssertEx.Equal(new object[] { '1', 2u, 3m, 5m, 20 }, state.Variables.Select(v => v.Value));
            AssertEx.Equal(new Type[] { typeof(char), typeof(uint), typeof(decimal), typeof(decimal), typeof(int) }, state.Variables.Select(v => v.Type));

            Assert.Equal(3m, state.GetVariable("a").Value);
            Assert.Equal(2u, state.GetVariable("b").Value);
            Assert.Equal(5m, state.GetVariable("x").Value);
            Assert.Equal(20, state.GetVariable("X").Value);

            Assert.Null(state.GetVariable("A"));
            Assert.Same(state.GetVariable("X"), state.GetVariable("X"));
        }

        [Fact]
        public async Task ScriptVariable_SetValue()
        {
            var script = CSharpScript.Create("var x = 1;", ScriptOptions);

            var s1 = await script.RunAsync();
            s1.GetVariable("x").Value = 2;
            Assert.Equal(2, s1.GetVariable("x").Value);

            // rerunning the script from the beginning rebuilds the state:
            var s2 = await s1.Script.RunAsync();
            Assert.Equal(1, s2.GetVariable("x").Value);

            // continuing preserves the state:
            var s3 = await s1.ContinueWithAsync("x");
            Assert.Equal(2, s3.GetVariable("x").Value);
            Assert.Equal(2, s3.ReturnValue);
        }

        [Fact]
        public async Task ScriptVariable_SetValue_Errors()
        {
            var state = await CSharpScript.RunAsync(@"
var x = 1;
readonly var y = 2;
const int z = 3;
", ScriptOptions);

            Assert.False(state.GetVariable("x").IsReadOnly);
            Assert.True(state.GetVariable("y").IsReadOnly);
            Assert.True(state.GetVariable("z").IsReadOnly);

            Assert.Throws<ArgumentException>(() => state.GetVariable("x").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("y").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("z").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("y").Value = 0);
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("z").Value = 0);
        }

        [Fact]
        public async Task TestBranchingSubscripts()
        {
            // run script to create declaration of M
            var state1 = await CSharpScript.RunAsync("int M(int x) { return x + x; }", ScriptOptions);

            // run second script starting from first script's end state
            // this script's new declaration should hide the old declaration
            var state2 = await state1.ContinueWithAsync("int M(int x) { return x * x; } M(5)");
            Assert.Equal(25, state2.ReturnValue);

            // run third script also starting from first script's end state
            // it should not see any declarations made by the second script.
            var state3 = await state1.ContinueWithAsync("M(5)");
            Assert.Equal(10, state3.ReturnValue);
        }

        [Fact]
        public async Task StaticDelegate0()
        {
            var state0 = await CSharpScript.RunAsync("static int Add(int x, int y) => x + y;", options: ScriptOptions.WithLanguageVersion(LanguageVersion.Preview));
            var state1 = await state0.ContinueWithAsync("System.Func<int, int, int> adder = Add;");
            var state2 = await state1.ContinueWithAsync("adder(1, 1)");
            Assert.Equal(2, state2.ReturnValue);
        }

        [Fact]
        public async Task StaticDelegate1()
        {
            var state0 = await CSharpScript.RunAsync("class Id<T> { static T Core(T t) => t; public static System.Func<T, T> Get => Core; }", ScriptOptions);
            var state1 = await state0.ContinueWithAsync("Id<int>.Get(1)");
            Assert.Equal(1, state1.ReturnValue);
        }

        [Fact]
        public async Task StaticDelegate2()
        {
            var state0 = await CSharpScript.RunAsync("class Id { static T Core<T>(T t) => t; public static System.Func<T, T> Get<T>() => Core; }", ScriptOptions);
            var state1 = await state0.ContinueWithAsync("Id.Get<int>()(1)");
            Assert.Equal(1, state1.ReturnValue);
        }

        [Fact]
        public async Task ReturnIntAsObject()
        {
            var expected = 42;
            var script = CSharpScript.Create<object>($"return {expected};", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void NoReturn()
        {
            Assert.Null(ScriptingTestHelpers.EvaluateScriptWithOutput(
                CSharpScript.Create("System.Console.WriteLine();", ScriptOptions), ""));
        }

        [Fact]
        public async Task ReturnAwait()
        {
            var script = CSharpScript.Create<int>("return await System.Threading.Tasks.Task.FromResult(42);", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInNestedScopeNoTrailingExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnInNestedScopeWithTrailingVoidExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine();", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
bool condition = true;
if (condition)
{
    return 1;
}
System.Console.WriteLine();", ScriptOptions);

            Assert.Equal(1, ScriptingTestHelpers.EvaluateScriptWithOutput(script, ""));
        }

        [Fact]
        public async Task ReturnInNestedScopeWithTrailingVoidExpressionAsInt()
        {
            var script = CSharpScript.Create<int>(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine();", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Equal(0, result);

            script = CSharpScript.Create<int>(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine()", ScriptOptions);

            Assert.Equal(0, ScriptingTestHelpers.EvaluateScriptWithOutput(script, ""));
        }

        [Fact]
        public async Task ReturnIntWithTrailingDoubleExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}
1.1", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Equal(1.1, result);

            script = CSharpScript.Create(@"
bool condition = true;
if (condition)
{
    return 1;
}
1.1", ScriptOptions);
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task ReturnGenericAsInterface()
        {
            var script = CSharpScript.Create<IEnumerable<int>>(@"
if (false)
{
    return new System.Collections.Generic.List<int> { 1, 2, 3 };
}", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create<IEnumerable<int>>(@"
if (true)
{
    return new System.Collections.Generic.List<int> { 1, 2, 3 };
}", ScriptOptions);
            result = await script.EvaluateAsync();
            Assert.Equal(new List<int> { 1, 2, 3 }, result);
        }

        [Fact]
        public async Task ReturnNullable()
        {
            var script = CSharpScript.Create<int?>(@"
if (false)
{
    return 42;
}", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.False(result.HasValue);

            script = CSharpScript.Create<int?>(@"
if (true)
{
    return 42;
}", ScriptOptions);
            result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInLoadedFile()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 42;"));
            var options = ScriptOptions.WithSourceResolver(resolver);

            var script = CSharpScript.Create("#load \"a.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(42, result);

            script = CSharpScript.Create(@"
#load ""a.csx""
-1", options);
            result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInLoadedFileTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
if (false)
{
    return 42;
}
1"));
            var options = ScriptOptions.WithSourceResolver(resolver);

            var script = CSharpScript.Create("#load \"a.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
#load ""a.csx""
2", options);
            result = await script.EvaluateAsync();
            Assert.Equal(2, result);
        }

        [Fact]
        public void ReturnInLoadedFileTrailingVoidExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
if (false)
{
    return 1;
}
System.Console.WriteLine(42)"));
            var options = ScriptOptions.WithSourceResolver(resolver);

            var script = CSharpScript.Create("#load \"a.csx\"", options);
            var result = ScriptingTestHelpers.EvaluateScriptWithOutput(script, "42");
            Assert.Null(result);

            script = CSharpScript.Create(@"
#load ""a.csx""
2", options);
            result = ScriptingTestHelpers.EvaluateScriptWithOutput(script, "42");
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task MultipleLoadedFilesWithTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", @"
#load ""a.csx""
2"));
            var options = ScriptOptions.WithSourceResolver(resolver);
            var script = CSharpScript.Create("#load \"b.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""", options);
            result = await script.EvaluateAsync();
            Assert.Null(result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""
3", options);
            result = await script.EvaluateAsync();
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task MultipleLoadedFilesWithReturnAndTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", @"
#load ""a.csx""
2"));
            var options = ScriptOptions.WithSourceResolver(resolver);
            var script = CSharpScript.Create("#load \"b.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(1, result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""", options);
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""
return 3;", options);
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task LoadedFileWithReturnAndGoto()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
goto EOF;
NEXT:
return 1;
EOF:;
2"));
            var options = ScriptOptions.WithSourceResolver(resolver);

            var script = CSharpScript.Create(@"
#load ""a.csx""
goto NEXT;
return 3;
NEXT:;", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
#load ""a.csx""
L1: goto EOF;
L2: return 3;
EOF:
EOF2: ;
4", options);
            result = await script.EvaluateAsync();
            Assert.Equal(4, result);
        }

        [Fact]
        public async Task VoidReturn()
        {
            var script = CSharpScript.Create("return;", ScriptOptions);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
var b = true;
if (b)
{
    return;
}
b", ScriptOptions);
            result = await script.EvaluateAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task LoadedFileWithVoidReturn()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
var i = 42;
return;
i = -1;"));
            var options = ScriptOptions.WithSourceResolver(resolver);
            var script = CSharpScript.Create<int>(@"
#load ""a.csx""
i", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Pdb_CreateFromString_CodeFromFile_WithEmitDebugInformation_WithoutFileEncoding_CompilationErrorException()
        {
            var code = "throw new System.Exception();";
            try
            {
                var opts = ScriptOptions.WithEmitDebugInformation(true).WithFilePath("debug.csx").WithFileEncoding(null);
                var script = await CSharpScript.RunAsync(code, opts);
            }
            catch (CompilationErrorException ex)
            {
                //  CS8055: Cannot emit debug information for a source text without encoding.
                ex.Diagnostics.Verify(Diagnostic(ErrorCode.ERR_EncodinglessSyntaxTree, code).WithLocation(1, 1));
            }
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        [WorkItem("https://github.com/dotnet/roslyn/issues/19027")]
        public Task Pdb_CreateFromString_CodeFromFile_WithEmitDebugInformation_WithFileEncoding_ResultInPdbEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(true).WithFilePath("debug.csx").WithFileEncoding(Encoding.UTF8);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts), line: 1, column: 1, filename: "debug.csx");
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        public Task Pdb_CreateFromString_CodeFromFile_WithoutEmitDebugInformation_WithoutFileEncoding_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false).WithFilePath(null).WithFileEncoding(null);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts));
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        public Task Pdb_CreateFromString_CodeFromFile_WithoutEmitDebugInformation_WithFileEncoding_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false).WithFilePath("debug.csx").WithFileEncoding(Encoding.UTF8);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts));
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        [WorkItem("https://github.com/dotnet/roslyn/issues/19027")]
        public Task Pdb_CreateFromStream_CodeFromFile_WithEmitDebugInformation_ResultInPdbEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(true).WithFilePath("debug.csx");
            return VerifyStackTraceAsync(() => CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("throw new System.Exception();")), opts), line: 1, column: 1, filename: "debug.csx");
        }

        [Fact]
        public Task Pdb_CreateFromStream_CodeFromFile_WithoutEmitDebugInformation_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false).WithFilePath("debug.csx");
            return VerifyStackTraceAsync(() => CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("throw new System.Exception();")), opts));
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        [WorkItem("https://github.com/dotnet/roslyn/issues/19027")]
        public Task Pdb_CreateFromString_InlineCode_WithEmitDebugInformation_WithoutFileEncoding_ResultInPdbEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(true).WithFileEncoding(null);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts), line: 1, column: 1, filename: "");
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        [WorkItem("https://github.com/dotnet/roslyn/issues/19027")]
        public Task Pdb_CreateFromString_InlineCode_WithEmitDebugInformation_WithFileEncoding_ResultInPdbEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(true).WithFileEncoding(Encoding.UTF8);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts), line: 1, column: 1, filename: "");
        }

        [Fact]
        public Task Pdb_CreateFromString_InlineCode_WithoutEmitDebugInformation_WithoutFileEncoding_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false).WithFileEncoding(null);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts));
        }

        [Fact]
        public Task Pdb_CreateFromString_InlineCode_WithoutEmitDebugInformation_WithFileEncoding_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false).WithFileEncoding(Encoding.UTF8);
            return VerifyStackTraceAsync(() => CSharpScript.Create("throw new System.Exception();", opts));
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = "https://github.com/dotnet/roslyn/issues/30169")]
        [WorkItem("https://github.com/dotnet/roslyn/issues/19027")]
        public Task Pdb_CreateFromStream_InlineCode_WithEmitDebugInformation_ResultInPdbEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(true);
            return VerifyStackTraceAsync(() => CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("throw new System.Exception();")), opts), line: 1, column: 1, filename: "");
        }

        [Fact]
        public Task Pdb_CreateFromStream_InlineCode_WithoutEmitDebugInformation_ResultInPdbNotEmitted()
        {
            var opts = ScriptOptions.WithEmitDebugInformation(false);
            return VerifyStackTraceAsync(() => CSharpScript.Create(new MemoryStream(Encoding.UTF8.GetBytes("throw new System.Exception();")), opts));
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/12348")]
        public void StreamWithOffset()
        {
            var resolver = new StreamOffsetResolver();
            var options = ScriptOptions.WithSourceResolver(resolver);
            var script = CSharpScript.Create(@"#load ""a.csx""", options);
            ScriptingTestHelpers.EvaluateScriptWithOutput(script, "Hello World!");
        }

        [Fact]
        public void CreateScriptWithFeatureThatIsNotSupportedInTheSelectedLanguageVersion()
        {
            var script = CSharpScript.Create(@"string x = default;", ScriptOptions.WithLanguageVersion(LanguageVersion.CSharp7));
            var compilation = script.GetCompilation();

            compilation.VerifyDiagnostics(
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "default").
                    WithArguments("default literal", "7.1").
                    WithLocation(1, 12)
            );
        }

        [Fact]
        public void CreateScriptWithNullableContextWithCSharp8()
        {
            var script = CSharpScript.Create(@"#nullable enable
                string x = null;", ScriptOptions.WithLanguageVersion(LanguageVersion.CSharp8));
            var compilation = script.GetCompilation();

            compilation.VerifyDiagnostics(
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(2, 28)
            );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/49529")]
        public async Task SwitchPatternWithVar_WhenValidCode_ShouldReturnValidResult()
        {
            var code = @"
using System;
var data = ""data"";
var reply = data switch {
    null => ""data are null"",
    """" => ""data are empty"",
    _ => data
};

return reply;
";
            var script = CSharpScript.Create(code, ScriptOptions.WithLanguageVersion(LanguageVersion.CSharp8));
            var compilation = script.GetCompilation();
            compilation.VerifyDiagnostics();

            var result = await script.EvaluateAsync();

            Assert.Equal("data", result);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/49529")]
        public async Task SwitchPatternWithVar_WhenNonExistentVariable_ShouldReturnNameNotInContextCompilationError()
        {
            var exceptionThrown = false;

            try
            {
                await CSharpScript.RunAsync(@"var data = notExistentVariable switch { _ => null };", options: ScriptOptions, globals: new ScriptTests());
            }
            catch (CompilationErrorException ex)
            {
                exceptionThrown = true;
                // Verify that it produces a single NameNotInContext error. 
                ex.Diagnostics.Verify(
                    // (1,12): error CS0103: The name 'notExistentVariable' does not exist in the current context
                    // var data = notExistentVariable switch { _ => null };
                    Diagnostic(ErrorCode.ERR_NameNotInContext, "notExistentVariable").WithArguments("notExistentVariable").WithLocation(1, 12));
            }

            Assert.True(exceptionThrown);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/49529")]
        public async Task SwitchPatternWithVar_WhenInvalidPattern_ShouldReturnUnsupportedTypeForRelationalPatternAndNoImplicitConvErrors()
        {
            var exceptionThrown = false;

            try
            {
                await CSharpScript.RunAsync(@"var data = ""data"" switch { < 5 => null };", options: ScriptOptions, globals: new ScriptTests());
            }
            catch (CompilationErrorException ex)
            {
                exceptionThrown = true;
                // Verify that it produces a single NameNotInContext error. 
                ex.Diagnostics.Verify(
                    // (1,28): error CS8781: Relational patterns may not be used for a value of type 'string'.
                    // var data = "data" switch { < 5 => null };
                    Diagnostic(ErrorCode.ERR_UnsupportedTypeForRelationalPattern, "< 5").WithArguments("string").WithLocation(1, 28),
                    // (1,30): error CS0029: Cannot implicitly convert type 'int' to 'string'
                    // var data = "data" switch { < 5 => null };
                    Diagnostic(ErrorCode.ERR_NoImplicitConv, "5").WithArguments("int", "string").WithLocation(1, 30));
            }

            Assert.True(exceptionThrown);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/49529")]
        public async Task SwitchPatternWithVar_WhenInvalidArm_ShouldReturnTheNameNotInContextError()
        {
            var exceptionThrown = false;

            try
            {
                await CSharpScript.RunAsync(@"var data = ""test"" switch { _ => armError };", options: ScriptOptions, globals: new ScriptTests());
            }
            catch (CompilationErrorException ex)
            {
                exceptionThrown = true;
                // Verify that it produces a single NameNotInContext error. 
                ex.Diagnostics.Verify(
                    // (1,33): error CS0103: The name 'armError' does not exist in the current context
                    // var data = "test" switch { _ => armError };
                    Diagnostic(ErrorCode.ERR_NameNotInContext, "armError").WithArguments("armError")
                        .WithLocation(1, 33));
            }

            Assert.True(exceptionThrown);
        }

        [Fact]
        public void Function_ReturningPartialType()
        {
            var script = CSharpScript.Create("class partial;", ScriptOptions.WithLanguageVersion(LanguageVersion.Preview))
                .ContinueWith("partial M() => new();");
            script.GetCompilation().VerifyDiagnostics(
                // (1,9): error CS1520: Method must have a return type
                // partial M() => new();
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(1, 9),
                // (1,9): error CS0759: No defining declaration found for implementing declaration of partial method 'M()'
                // partial M() => new();
                Diagnostic(ErrorCode.ERR_PartialMethodMustHaveLatent, "M").WithArguments("M()").WithLocation(1, 9),
                // (1,9): error CS0751: A partial member must be declared within a partial type
                // partial M() => new();
                Diagnostic(ErrorCode.ERR_PartialMemberOnlyInPartialClass, "M").WithLocation(1, 9),
                // (1,16): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                // partial M() => new();
                Diagnostic(ErrorCode.ERR_IllegalStatement, "new()").WithLocation(1, 16));
        }

        [Fact]
        public async Task Function_ReturningPartialType_CSharp13()
        {
            var script = CSharpScript.Create("class partial;", ScriptOptions.WithLanguageVersion(LanguageVersion.CSharp13))
                .ContinueWith("partial M() => new();")
                .ContinueWith("M()");
            script.GetCompilation().VerifyDiagnostics();

            var result = await script.EvaluateAsync();
            Assert.Equal("partial", result.GetType().Name);
        }

        private class StreamOffsetResolver : SourceReferenceResolver
        {
            public override bool Equals(object other) => ReferenceEquals(this, other);
            public override int GetHashCode() => 42;
            public override string ResolveReference(string path, string baseFilePath) => path;
            public override string NormalizePath(string path, string baseFilePath) => path;

            public override Stream OpenRead(string resolvedPath)
            {
                // Make an ASCII text buffer with Hello World script preceded by padding Qs
                const int padding = 42;
                string text = @"System.Console.WriteLine(""Hello World!"");";
                byte[] bytes = Enumerable.Repeat((byte)'Q', text.Length + padding).ToArray();
                System.Text.Encoding.ASCII.GetBytes(text, 0, text.Length, bytes, padding);

                // Make a stream over the program portion, skipping the Qs.
                var stream = new MemoryStream(
                    bytes,
                    padding,
                    text.Length,
                    writable: false,
                    publiclyVisible: true);

                // sanity check that reading entire stream gives us back our text.
                using (var streamReader = new StreamReader(
                    stream,
                    System.Text.Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: bytes.Length,
                    leaveOpen: true))
                {
                    var textFromStream = streamReader.ReadToEnd();
                    Assert.Equal(textFromStream, text);
                }

                stream.Position = 0;
                return stream;
            }
        }

        private async Task VerifyStackTraceAsync(Func<Script<object>> scriptProvider, int line = 0, int column = 0, string filename = null)
        {
            try
            {
                var script = scriptProvider();
                await script.RunAsync();
            }
            catch (Exception ex)
            {
                // line information is only available when PDBs have been emitted
                var needFileInfo = true;
                var stackTrace = new StackTrace(ex, needFileInfo);
                var firstFrame = stackTrace.GetFrames()[0];
                Assert.Equal(filename, firstFrame.GetFileName());
                Assert.Equal(line, firstFrame.GetFileLineNumber());
                Assert.Equal(column, firstFrame.GetFileColumnNumber());
            }
        }
    }
}
