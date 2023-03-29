using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
//using Mono.Debugging.Client;
//using Mono.Debugging.Soft;
using NUnit.Framework;
//using Xamarin.ProjectTools;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Meadow.CLI.Test;

namespace Meadow.CLI.Test
{
    [TestFixture]
    [Category("UsesDevice")]
    public class DebuggingTest : BaseTest
    {
        [TearDown]
        public void ClearDebugProperties()
        {
            //ClearDebugProperty();
        }

        int FindTextInFile(string file, string text)
        {
            int lineNumber = 1;
            foreach (var line in File.ReadAllLines(file))
            {
                if (line.Contains(text))
                {
                    return lineNumber;
                }
                lineNumber++;
            }
            Console.WriteLine($"Could not find '{text}' in '{file}'");
            return -1;
        }

#pragma warning disable 414
        static object[] DebuggerCustomAppTestCases = new object[] {
            new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
            },
            new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     true,
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     false,
            },
        };
#pragma warning restore 414

        [Test, Category("Debugger")]
        [TestCaseSource(nameof(DebuggerCustomAppTestCases))]
        [Retry(5)]
        public void CustomApplicationRunsWithDebuggerAndBreaks(bool embedAssemblies, string fastDevType, bool activityStarts)
        {
            int breakcountHitCount = 0;
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            var sw = new Stopwatch();
            // setup the debugger
            /* var session = new SoftDebuggerSession();
            try
            {
                session.Breakpoints = new BreakpointStore();
                string file = Path.Combine(Root, b.ProjectDirectory, "MainActivity.cs");
                int line = FindTextInFile(file, "base.OnCreate (bundle);");
                session.Breakpoints.Add(file, line);
                file = Path.Combine(Root, b.ProjectDirectory, "MyApplication.cs");
                line = FindTextInFile(file, "base.OnCreate ();");
                session.Breakpoints.Add(file, line);
                session.TargetHitBreakpoint += (sender, e) => {
                    TestContext.WriteLine($"BREAK {e.Type}, {e.Backtrace.GetFrame(0)}");
                    breakcountHitCount++;
                    session.Continue();
                };
                var rnd = new Random();
                int port = rnd.Next(10000, 20000);
                TestContext.Out.WriteLine($"{port}");
                var args = new SoftDebuggerConnectArgs("", IPAddress.Loopback, port)
                {
                    MaxConnectionAttempts = 2000, // we need a long delay here to get a reliable connection
                };
                var startInfo = new SoftDebuggerStartInfo(args)
                {
                    WorkingDirectory = Path.Combine(b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
                };
                var options = new DebuggerSessionOptions()
                {
                    EvaluationOptions = EvaluationOptions.DefaultOptions,
                };
                options.EvaluationOptions.UseExternalTypeResolver = true;
                RunProjectAndAssert(proj, b, doNotCleanupOnUpdate: true, parameters: new string[] {
                    $"AndroidSdbTargetPort={port}",
                    $"AndroidSdbHostPort={port}",
                    "AndroidAttachDebugger=True",
                });

                session.LogWriter += (isStderr, text) => { Console.WriteLine(text); };
                session.OutputWriter += (isStderr, text) => { Console.WriteLine(text); };
                session.DebugWriter += (level, category, message) => { Console.WriteLine(message); };
                // do we expect the app to start?
                Assert.AreEqual(activityStarts, WaitForDebuggerToStart(Path.Combine(Root, b.ProjectDirectory, "logcat.log")), "Debugger should have started");
                if (!activityStarts)
                    return;
                Assert.False(session.HasExited, "Target should not have exited.");
                session.Run(startInfo, options);
                var expectedTime = TimeSpan.FromSeconds(1);
                var actualTime = ProfileFor(() => session.IsConnected);
                Assert.True(session.IsConnected, "Debugger should have connected but it did not.");
                TestContext.Out.WriteLine($"Debugger connected in {actualTime}");
                Assert.LessOrEqual(actualTime, expectedTime, $"Debugger should have connected within {expectedTime} but it took {actualTime}.");
                // we need to wait here for a while to allow the breakpoints to hit
                // but we need to timeout
                TimeSpan timeout = TimeSpan.FromSeconds(60);
                while (session.IsConnected && breakcountHitCount < 2 && timeout >= TimeSpan.Zero)
                {
                    Thread.Sleep(10);
                    timeout = timeout.Subtract(TimeSpan.FromMilliseconds(10));
                }
                WaitFor(2000);
                int expected = 2;
                Assert.AreEqual(expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
                b.BuildLogFile = "uninstall.log";
                Assert.True(b.Uninstall(proj), "Project should have uninstalled.");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception occurred {ex}");
            }
            finally
            {
                session.Exit();
            }*/
        }

#pragma warning disable 414
        static object[] DebuggerTestCases = new object[] {
            new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "full",
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  true,
				/* user */		 null,
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* allowDeltaInstall */  true,
				/* user */		 null,
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 "guest1",
				/* debugType */          "",
            },
            new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 "guest1",
				/* debugType */          "",
            },
        };
#pragma warning restore 414

        [Test, Category("Debugger")]
        [TestCaseSource(nameof(DebuggerTestCases))]
        [Retry(5)]
        public void ApplicationRunsWithDebuggerAndBreaks(bool embedAssemblies, string fastDevType, bool allowDeltaInstall, string username, string debugType)
        {
            int breakcountHitCount = 0;
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            var sw = new Stopwatch();
            // setup the debugger
            /*var session = new SoftDebuggerSession();
            try
            {
                session.Breakpoints = new BreakpointStore();
                string file = Path.Combine(Root, appBuilder.ProjectDirectory, "MainActivity.cs");
                int line = FindTextInFile(file, "base.OnCreate (savedInstanceState);");
                session.Breakpoints.Add(file, line);

                file = Path.Combine(Root, appBuilder.ProjectDirectory, "MainPage.xaml.cs");
                line = FindTextInFile(file, "InitializeComponent ();");
                session.Breakpoints.Add(file, line);

                file = Path.Combine(Root, appBuilder.ProjectDirectory, "MainPage.xaml.cs");
                line = FindTextInFile(file, "Console.WriteLine (");
                session.Breakpoints.Add(file, line);

                file = Path.Combine(Root, appBuilder.ProjectDirectory, "App.xaml.cs");
                line = FindTextInFile(file, "InitializeComponent ();");
                session.Breakpoints.Add(file, line);

                file = Path.Combine(Root, libBuilder.ProjectDirectory, "Foo.cs");
                line = FindTextInFile(file, "public Foo ()");
                // Add one to the line so we get the '{' under the constructor
                session.Breakpoints.Add(file, line++);

                session.TargetHitBreakpoint += (sender, e) => {
                    TestContext.WriteLine($"BREAK {e.Type}, {e.Backtrace.GetFrame(0)}");
                    breakcountHitCount++;
                    session.Continue();
                };
                var rnd = new Random();
                int port = rnd.Next(10000, 20000);
                TestContext.Out.WriteLine($"{port}");
                var args = new SoftDebuggerConnectArgs("", IPAddress.Loopback, port)
                {
                    MaxConnectionAttempts = 2000,
                };
                var startInfo = new SoftDebuggerStartInfo(args)
                {
                    WorkingDirectory = Path.Combine(appBuilder.ProjectDirectory, app.IntermediateOutputPath, "android", "assets"),
                };
                var options = new DebuggerSessionOptions()
                {
                    EvaluationOptions = EvaluationOptions.DefaultOptions,
                };
                options.EvaluationOptions.UseExternalTypeResolver = true;

                parameters.Add($"AndroidSdbTargetPort={port}");
                parameters.Add($"AndroidSdbHostPort={port}");
                parameters.Add("AndroidAttachDebugger=True");

                RunProjectAndAssert(app, appBuilder, doNotCleanupOnUpdate: true, parameters: parameters.ToArray());

                session.LogWriter += (isStderr, text) => {
                    TestContext.Out.WriteLine(text);
                };
                session.OutputWriter += (isStderr, text) => {
                    TestContext.Out.WriteLine(text);
                };
                session.DebugWriter += (level, category, message) => {
                    TestContext.Out.WriteLine(message);
                };
                Assert.IsTrue(WaitForDebuggerToStart(Path.Combine(Root, appBuilder.ProjectDirectory, "logcat.log")), "Debugger should have started");
                session.Run(startInfo, options);
                TestContext.Out.WriteLine($"Detected debugger startup in log");
                Assert.False(session.HasExited, "Target should not have exited.");
                WaitFor(TimeSpan.FromSeconds(30), () => session.IsConnected);
                Assert.True(session.IsConnected, "Debugger should have connected but it did not.");
                // we need to wait here for a while to allow the breakpoints to hit
                // but we need to timeout
                TestContext.Out.WriteLine($"Debugger connected.");
                TimeSpan timeout = TimeSpan.FromSeconds(60);
                int expected = 4;
                while (session.IsConnected && breakcountHitCount < 3 && timeout >= TimeSpan.Zero)
                {
                    Thread.Sleep(10);
                    timeout = timeout.Subtract(TimeSpan.FromMilliseconds(10));
                }
                WaitFor(2000);
                Assert.AreEqual(expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
                breakcountHitCount = 0;
                ClearAdbLogcat();
                ClearBlockingDialogs();
                Assert.True(ClickButton(app.PackageName, "myXFButton", "CLICK ME"), "Button should have been clicked!");
                while (session.IsConnected && breakcountHitCount < 1 && timeout >= TimeSpan.Zero)
                {
                    Thread.Sleep(10);
                    timeout = timeout.Subtract(TimeSpan.FromMilliseconds(10));
                }
                expected = 1;
                Assert.AreEqual(expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
                appBuilder.BuildLogFile = "uninstall.log";
                Assert.True(appBuilder.Uninstall(app), "Project should have uninstalled.");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception occurred {ex}");
            }
            finally
            {
                session.Exit();
            }*/
        }
    }
}