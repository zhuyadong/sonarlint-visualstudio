﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using Grpc.Core;
using Sonarlint;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Linq;

namespace SonarLint.VisualStudio.Integration.Vsix
{
    [Export(typeof(ISonarLintDaemon))]
    class SonarLintDaemon : ISonarLintDaemon
    {
        private static readonly string DAEMON_HOST = "localhost";
        private static readonly int DEFAULT_DAEMON_PORT = 8050;

        public const string daemonVersion = "2.17.0.831";
        private const string uriFormat = "https://repox.sonarsource.com/sonarsource-dev/org/sonarsource/sonarlint/core/sonarlint-daemon/{0}/sonarlint-daemon-{0}-windows.zip";
        private readonly string version;
        private readonly string tmpPath;
        private readonly string storagePath;
        private int port;

        private Process process;

        private readonly string workingDirectory;

        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public event AsyncCompletedEventHandler DownloadCompleted;

        private Channel channel;
        private StandaloneSonarLint.StandaloneSonarLintClient daemonClient;

        public SonarLintDaemon() : this(daemonVersion, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.GetTempPath())
        {
        }

        public SonarLintDaemon(string version, string storagePath, string tmpPath)
        {
            this.version = version;
            this.tmpPath = tmpPath;
            this.storagePath = storagePath;
            this.workingDirectory = CreateTempDirectory();
        }

        public void Dispose()
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, true);
            }

            if (IsRunning)
            {
                Stop();
            }
        }

        public void Install()
        {
            Download();
        }

        public bool IsRunning => process != null && !process.HasExited;

        public void Start()
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Process already running");
            }
            if (!IsInstalled)
            {
                throw new InvalidOperationException("Daemon is not installed");
            }

            port = TcpUtil.FindFreePort(DEFAULT_DAEMON_PORT);
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = ExePath,
                    Arguments = GetCmdArgs(port),
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.OutputDataReceived += (sender, args) =>
            {
                string data = args.Data;
                if (data != null)
                {
                    if (data.Contains("Server started"))
                    {
                        CreateChannelAndStremLogs();
                    }
                    WritelnToPane(data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                string data = args.Data;
                if (data != null)
                {
                    WritelnToPane(data);
                }
            };
            WritelnToPane($"Running {ExePath}");
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to start SonarLint daemon: {0}", e);
                WritelnToPane($"Unable to start SonarLint daemon {e.Message}");
            }
        }

        private void CreateChannelAndStremLogs()
        {
            channel = new Channel($"{DAEMON_HOST}:{port}", ChannelCredentials.Insecure);
            daemonClient = new StandaloneSonarLint.StandaloneSonarLintClient(channel);
            ListenForLogs();
        }

        private async System.Threading.Tasks.Task ListenForLogs()
        {
            ISonarLintSettings settings = ServiceProvider.GlobalProvider.GetMefService<ISonarLintSettings>();
            try
            {
                using (var streamLogs = daemonClient.StreamLogs(new Sonarlint.Void(), new CallOptions(null, null, channel.ShutdownToken).WithWaitForReady(true)))
                {
                    while (await streamLogs.ResponseStream.MoveNext())
                    {
                        var log = streamLogs.ResponseStream.Current;
                        if (ShouldLog(settings, log))
                        {
                            WritelnToPane($"{log.Level} {log.Log}");
                        }
                    }
                }
            }
            catch (RpcException e)
            {
                Debug.WriteLine("RPC failed: {0}", e);
                WritelnToPane("RPC failed " + e);
                throw;
            }
        }

        private static bool ShouldLog(ISonarLintSettings settings, LogEvent log)
        {
            return "Still alive" != log.Log
                && (settings.DaemonLogLevel == DaemonLogLevel.ERROR && log.Level == "ERROR"
                || settings.DaemonLogLevel == DaemonLogLevel.INFO && new[] { "ERROR", "WARN", "INFO" }.Contains(log.Level)
                || settings.DaemonLogLevel == DaemonLogLevel.VERBOSE);
        }

        private void WritelnToPane(string msg)
        {
            VsShellUtils.WriteToSonarLintOutputPane(ServiceProvider.GlobalProvider, msg);
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
                // throw exception?
            }
            channel.ShutdownAsync().Wait();
            process.Kill();
            process.WaitForExit();
        }

        public int Port => port;

        public bool IsInstalled => Directory.Exists(InstallationPath) && File.Exists(ExePath);

        private void Download()
        {
            Uri uri = new Uri(string.Format(uriFormat, version));
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (sender, args) => DownloadProgressChanged?.Invoke(sender, args);
                client.DownloadFileCompleted += Unzip;
                client.DownloadFileCompleted += (sender, args) => DownloadCompleted?.Invoke(sender, args);
                client.DownloadFileAsync(uri, ZipFilePath);
            }
        }

        private void Unzip(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                return;
            }

            if (Directory.Exists(InstallationPath))
            {
                Directory.Delete(InstallationPath, true);
            }
            ZipFile.ExtractToDirectory(ZipFilePath, storagePath);
        }

        private string InstallationPath => Path.Combine(storagePath, $"sonarlint-daemon-{version}-windows");

        private string ZipFilePath => Path.Combine(tmpPath, $"sonarlint-daemon-{version}-windows.zip");

        private string ExePath => Path.Combine(InstallationPath, "jre", "bin", "java.exe");

        private string GetCmdArgs(int port)
        {
            string jarPath = Path.Combine(InstallationPath, "lib", $"sonarlint-daemon-{version}.jar");
            string logPath = Path.Combine(InstallationPath, "conf", "logback.xml");
            string className = "org.sonarlint.daemon.Daemon";

            return string.Format("-Djava.awt.headless=true" +
                " -cp \"{0}\"" +
                " \"-Dlogback.configurationFile={1}\"" +
                " \"-Dsonarlint.home={2}\"" +
                " {3}" +
                " \"--port\" \"{4}\"",
                jarPath, logPath, InstallationPath, className, port);
        }

        private string CreateTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "SonarLintDaemon", Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public void RequestAnalysis(string path, string charset, IIssueConsumer consumer)
        {
            WritelnToPane($"Analysing {path}");
            if (daemonClient != null)
            {
                Analyze(path, charset, consumer);
            }
        }

        private async void Analyze(string path, string charset, IIssueConsumer consumer)
        {
            var request = new AnalysisReq
            {
                BaseDir = path,
                WorkDir = workingDirectory,
            };
            request.File.Add(new InputFile
            {
                Path = path,
                Charset = charset
            });

            using (var call = daemonClient.Analyze(request))
            {
                try
                {
                    await ProcessIssues(call, path, consumer);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Call to client.Analyze failed: {0}", e);
                }
            }
        }

        private async System.Threading.Tasks.Task ProcessIssues(AsyncServerStreamingCall<Issue> call, string path, IIssueConsumer consumer)
        {
            var issues = new List<Issue>();
            int issueCount = 0;
            while (await call.ResponseStream.MoveNext())
            {
                var issue = call.ResponseStream.Current;
                issues.Add(issue);
                issueCount++;
            }
            WritelnToPane($"Found {issueCount} issue(s)");

            consumer.Accept(path, issues);
        }
    }
}
