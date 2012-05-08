﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Debugger;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace AnalysisTest.Django {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\visualstudio_py_debugger.py")]
    [DeploymentItem(@"..\\PythonTools\\visualstudio_py_launcher.py")]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    [DeploymentItem("Binaries\\Win32\\Debug\\PyDebugAttach.dll")]
    [DeploymentItem("Binaries\\Win32\\Debug\\x64\\PyDebugAttach.dll", "x64")]
    public class DjangoDebuggerTests : BaseDebuggerTests {
        private static DbState _dbstate;

        enum DbState {
            Unknown,
            BarApp
        }

        /// <summary>
        /// Ensures the app is initialized with the appropriate set of data.  If we're
        /// already initialized that way we don't re-initialize.
        /// </summary>
        private void Init(DbState requiredState) {
            if (_dbstate != requiredState) {
                switch (requiredState) {
                    case DbState.BarApp:
                        var psi = new ProcessStartInfo();
                        psi.Arguments = "manage.py syncdb --noinput";
                        psi.WorkingDirectory = Path.Combine(Environment.CurrentDirectory, DebuggerTestPath);
                        psi.FileName = Version.Path;
                        var proc = Process.Start(psi);
                        proc.WaitForExit();
                        Assert.AreEqual(0, proc.ExitCode);

                        psi = new ProcessStartInfo();
                        psi.Arguments = "manage.py loaddata data.yaml";
                        psi.WorkingDirectory = Path.Combine(Environment.CurrentDirectory, DebuggerTestPath);
                        psi.FileName = Version.Path;
                        proc = Process.Start(psi);
                        proc.WaitForExit();
                        Assert.AreEqual(0, proc.ExitCode);
                        break;
                }
                _dbstate = requiredState;
            }
        }

        [TestMethod]
        public void BreakInTemplate() {
            Init(DbState.BarApp);

            string cwd = Path.Combine(Environment.CurrentDirectory, DebuggerTestPath);
            
            BreakpointTest(
                "manage.py",
                new[] { 1, 3, 4 },
                new[] { 1, 3, 4 },
                breakFilename: Path.Combine(cwd, "Templates", "polls", "index.html"),
                arguments: "runserver --noreload",
                checkBound: false,
                checkThread: false,
                processLoaded: new WebPageRequester().DoRequest,
                debugOptions: PythonDebugOptions.DjangoDebugging,
                waitForExit: false
            );
        }

        [TestMethod]
        public void TemplateLocals() {
            Init(DbState.BarApp);

            LocalsTest("polls\\index.html", 3, new[] { "latest_poll_list" });
            LocalsTest("polls\\index.html", 4, new[] { "forloop", "latest_poll_list", "poll" });
        }

        private void LocalsTest(string filename, int breakLine, string[] expectedLocals) {
            string cwd = Path.Combine(Environment.CurrentDirectory, DebuggerTestPath);
            LocalsTest("manage.py",
                breakLine,
                new string[0],
                expectedLocals,
                breakFilename: Path.Combine(cwd, "Templates", filename),
                arguments: "runserver --noreload",
                processLoaded: new WebPageRequester().DoRequest,
                debugOptions: PythonDebugOptions.DjangoDebugging,
                waitForExit: false
            );
        }

        class WebPageRequester {
            private readonly string _url;

            public WebPageRequester(string url = "http://127.0.0.1:8000/Bar/") {
                _url = url;
            }

            public void DoRequest() {
                ThreadPool.QueueUserWorkItem(DoRequestWorker, null);
            }

            public void DoRequestWorker(object data) {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Blocking = true;
                for (int i = 0; i < 200; i++) {
                    try {
                        socket.Connect(IPAddress.Loopback, 8000);
                        break;
                    } catch {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                socket.Close();

                HttpWebRequest myReq = (HttpWebRequest)WebRequest.Create(_url);
                try {
                    myReq.GetResponse();
                } catch (WebException) {
                    // the process can be killed and the connection with it
                }
            }
        }

        internal override string DebuggerTestPath {
            get {
                return @"Python.VS.TestData\DjangoProject\";
            }
        }

        internal override PythonVersion Version {
            get {
                return PythonPaths.Python27;
            }
        }

    }
}
