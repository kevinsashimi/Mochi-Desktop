using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace MochiDesktop {
    static class AppVersion {
        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string Display(Version version) { return version.Revision > 0 ? version.ToString(4) : version.ToString(3); }
        public static string Text { get { return Display(Current); } }
    }

    sealed class UpdateInfo {
        public Version Version;
        public string Sha256;
        public long Size;
        public string Commit;
        public const long MaximumSize = 50 * 1024 * 1024;

        public static XElement ReadXml(Stream stream) {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16384 };
            using (XmlReader reader = XmlReader.Create(stream, settings)) return XElement.Load(reader);
        }
        public static UpdateInfo Parse(byte[] bytes, string commit) {
            XElement xml;
            using (MemoryStream stream = new MemoryStream(bytes)) xml = ReadXml(stream);
            Version version;
            long size;
            string hash = (string)xml.Element("Sha256");
            if (xml.Name != "MochiUpdate" || (string)xml.Element("File") != "Mochi.exe" ||
                !System.Version.TryParse((string)xml.Element("Version"), out version) ||
                version.Build < 0 || version.Major < 1 ||
                !Int64.TryParse((string)xml.Element("Size"), NumberStyles.None, CultureInfo.InvariantCulture, out size) ||
                size <= 0 || size > MaximumSize || !IsHash(hash, 64) || !IsHash(commit, 40))
                throw new InvalidDataException("The update information from GitHub is invalid.");
            version = new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
            return new UpdateInfo { Version = version, Sha256 = hash.ToLowerInvariant(), Size = size, Commit = commit };
        }
        public static bool IsHash(string value, int length) {
            return value != null && Regex.IsMatch(value, "\\A[0-9a-fA-F]{" + length + "}\\z");
        }
    }

    interface IUpdateTransport {
        byte[] Get(string url, int maximumBytes, CancellationToken cancellation);
        void Download(string url, string path, long expectedSize, CancellationToken cancellation);
    }

    sealed class GitHubTransport : IUpdateTransport {
        static HttpWebRequest CreateRequest(string url, int timeout) {
            Uri uri = new Uri(url);
            if (uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "api.github.com" && uri.Host != "raw.githubusercontent.com"))
                throw new InvalidDataException("Updates must come from GitHub over HTTPS.");
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 on .NET Framework.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "Mochi-Desktop/" + AppVersion.Text;
            request.Accept = "application/vnd.github+json";
            request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            request.AllowAutoRedirect = false;
            return request;
        }
        static void Read(string url, Stream output, long limit, int timeout, CancellationToken cancellation) {
            HttpWebRequest request = CreateRequest(url, timeout);
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                using (System.Threading.Timer timer = new System.Threading.Timer(delegate { deadline.Cancel(); }, null, timeout, Timeout.Infinite))
                using (deadline.Token.Register(request.Abort)) {
                    try {
                        deadline.Token.ThrowIfCancellationRequested();
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
                            if (response.StatusCode != HttpStatusCode.OK) throw new IOException("GitHub did not return an update file.");
                            if (response.ContentLength > limit) throw new InvalidDataException("The update file is larger than expected.");
                            using (Stream input = response.GetResponseStream()) {
                                byte[] buffer = new byte[32768];
                                long total = 0;
                                int count;
                                while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                                    deadline.Token.ThrowIfCancellationRequested();
                                    total += count;
                                    if (total > limit) throw new InvalidDataException("The update file is larger than expected.");
                                    output.Write(buffer, 0, count);
                                }
                            }
                        }
                    } catch (WebException) {
                        cancellation.ThrowIfCancellationRequested();
                        if (deadline.IsCancellationRequested) throw new TimeoutException("The connection to GitHub timed out.");
                        throw;
                    } catch (OperationCanceledException) {
                        cancellation.ThrowIfCancellationRequested();
                        throw new TimeoutException("The connection to GitHub timed out.");
                    }
                }
            }
        }
        public byte[] Get(string url, int maximumBytes, CancellationToken cancellation) {
            using (MemoryStream stream = new MemoryStream()) {
                Read(url, stream, maximumBytes, 15000, cancellation);
                return stream.ToArray();
            }
        }
        public void Download(string url, string path, long expectedSize, CancellationToken cancellation) {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                Read(url, stream, expectedSize, 120000, cancellation);
        }
    }

    sealed class UpdateService {
        public const string Repository = "https://github.com/kevinsashimi/Mochi-Desktop";
        public const string CommitUrl = "https://api.github.com/repos/kevinsashimi/Mochi-Desktop/commits/main";
        const string RawBase = "https://raw.githubusercontent.com/kevinsashimi/Mochi-Desktop/";
        readonly IUpdateTransport transport;
        public UpdateService(IUpdateTransport transport) { this.transport = transport; }
        [DataContract] sealed class CommitResponse { [DataMember(Name = "sha")] public string Sha { get; set; } }
        public UpdateInfo Check(Version installed, CancellationToken cancellation) {
            byte[] commitBytes = transport.Get(CommitUrl, 512 * 1024, cancellation);
            CommitResponse commit;
            using (MemoryStream stream = new MemoryStream(commitBytes))
                commit = (CommitResponse)new DataContractJsonSerializer(typeof(CommitResponse)).ReadObject(stream);
            if (commit == null || !UpdateInfo.IsHash(commit.Sha, 40)) throw new InvalidDataException("GitHub returned an invalid version reference.");
            // Pin the small manifest and the executable to the same commit, even if main changes during download.
            UpdateInfo latest = UpdateInfo.Parse(transport.Get(RawBase + commit.Sha + "/Project%20Files/update.xml", 16384, cancellation), commit.Sha);
            return latest.Version > installed ? latest : null;
        }
        public string Download(UpdateInfo update, CancellationToken cancellation) {
            string directory = Path.Combine(Path.GetTempPath(), "Mochi-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string candidate = Path.Combine(directory, "Mochi.download.exe");
            try {
                transport.Download(RawBase + update.Commit + "/Mochi.exe", candidate, update.Size, cancellation);
                cancellation.ThrowIfCancellationRequested();
                VerifyExecutable(candidate, update);
                return directory;
            } catch {
                UpdateInstaller.Cleanup(directory);
                throw;
            }
        }
        public static string Hash(string path) {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static void VerifyExecutable(string path, UpdateInfo expected) {
            if (new FileInfo(path).Length != expected.Size || !String.Equals(Hash(path), expected.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded update failed its integrity check. Please try again.");
            AssemblyName assembly = AssemblyName.GetAssemblyName(path);
            if (assembly.Name != "Mochi" || assembly.Version != expected.Version)
                throw new InvalidDataException("The downloaded program does not match the announced Mochi version.");
        }
        public static string Explain(Exception error) {
            WebException web = error as WebException;
            HttpWebResponse response = web == null ? null : web.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                return "Update information is not available on GitHub yet. Please try again later.";
            if (response != null && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429))
                return "GitHub is temporarily limiting update checks. Please try again later.";
            if (error is WebException || error is TimeoutException)
                return "Could not reach GitHub. Check your internet connection and try again.";
            if (error is UnauthorizedAccessException)
                return "Mochi cannot write to its folder. Move it to a folder you can write to, then try again.";
            if (error is InvalidDataException) return error.Message;
            return "The update could not be completed. Your current app is still available. Please try again.";
        }
    }

    sealed class InstallRequest {
        public string Target, OldHash;
        public int ParentId;
        public long ParentStarted;
        public UpdateInfo Update;
        public void Save(string directory) {
            new XElement("MochiInstall", new XElement("Target", Target), new XElement("OldHash", OldHash),
                new XElement("ParentId", ParentId), new XElement("ParentStarted", ParentStarted),
                new XElement("Version", Update.Version), new XElement("Sha256", Update.Sha256),
                new XElement("Size", Update.Size)).Save(Path.Combine(directory, "install.xml"));
        }
        public static InstallRequest Load(string directory) {
            XElement xml;
            using (FileStream stream = File.OpenRead(Path.Combine(directory, "install.xml"))) xml = UpdateInfo.ReadXml(stream);
            UpdateInfo info = UpdateInfo.Parse(Encoding.UTF8.GetBytes(new XElement("MochiUpdate",
                new XElement("File", "Mochi.exe"), new XElement("Version", (string)xml.Element("Version")),
                new XElement("Size", (string)xml.Element("Size")), new XElement("Sha256", (string)xml.Element("Sha256"))).ToString()), new string('0', 40));
            string target = (string)xml.Element("Target");
            if (String.IsNullOrEmpty(target) || !Path.IsPathRooted(target) ||
                !String.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase) ||
                !UpdateInfo.IsHash((string)xml.Element("OldHash"), 64))
                throw new InvalidDataException("Invalid installation request.");
            return new InstallRequest { Target = Path.GetFullPath(target), OldHash = (string)xml.Element("OldHash"),
                ParentId = (int)xml.Element("ParentId"), ParentStarted = (long)xml.Element("ParentStarted"), Update = info };
        }
    }

    static class UpdateInstaller {
        public static string Quote(string value) {
            if (value.IndexOf('"') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + value + "\"";
        }
        public static void PrepareAndStart(string directory, UpdateInfo update) {
            string target = Application.ExecutablePath;
            UpdateService.VerifyExecutable(Path.Combine(directory, "Mochi.download.exe"), update);
            // Check folder access before asking the running companion to exit.
            string probe = Path.Combine(Path.GetDirectoryName(target), ".mochi-write-" + Guid.NewGuid().ToString("N"));
            using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {}
            using (Process current = Process.GetCurrentProcess()) {
                new InstallRequest { Target = target, OldHash = UpdateService.Hash(target), ParentId = current.Id,
                    ParentStarted = current.StartTime.ToUniversalTime().Ticks, Update = update }.Save(directory);
            }
            string helper = Path.Combine(directory, "Mochi.updater.exe");
            File.Copy(target, helper, false);
            using (Process process = Process.Start(new ProcessStartInfo(helper, "--apply-update " + Quote(directory)) {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = directory
            })) { if (process == null) throw new IOException("The updater could not start."); }
        }
        public static void ReplaceAndRestart(string directory, InstallRequest request, Action<string, string> restart) {
            string candidate = Path.Combine(directory, "Mochi.download.exe");
            UpdateService.VerifyExecutable(candidate, request.Update);
            if (UpdateService.Hash(request.Target) != request.OldHash)
                throw new IOException("Mochi changed while the update was downloading. Please check for updates again.");
            AssemblyName installed = AssemblyName.GetAssemblyName(request.Target);
            if (installed.Name != "Mochi" || request.Update.Version <= installed.Version)
                throw new InvalidDataException("An update must be newer than the installed Mochi version.");
            string suffix = Guid.NewGuid().ToString("N");
            string incoming = Path.Combine(Path.GetDirectoryName(request.Target), ".Mochi-incoming-" + suffix + ".tmp");
            string backup = Path.Combine(Path.GetDirectoryName(request.Target), ".Mochi-backup-" + suffix + ".tmp");
            bool restarted = false;
            try {
                File.Copy(candidate, incoming, false);
                UpdateService.VerifyExecutable(incoming, request.Update);
                // Same-volume atomic replacement: interruption cannot leave a half-written executable.
                File.Replace(incoming, request.Target, backup);
                try { restart(request.Target, "--cleanup-update " + Quote(directory)); restarted = true; }
                catch {
                    File.Replace(backup, request.Target, null);
                    throw;
                }
            } finally {
                TryDelete(incoming);
                // Keep the backup if rollback itself failed.
                if (restarted) TryDelete(backup);
            }
        }
        public static void StartApp(string target, string arguments) {
            using (Process process = Process.Start(new ProcessStartInfo(target, arguments) {
                UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)
            })) { if (process == null) throw new IOException("Mochi could not restart."); }
        }
        public static int RunHelper(string directory) {
            InstallRequest request = null;
            bool parentExited = false;
            try {
                if (!IsUpdateDirectory(directory)) throw new InvalidDataException("Invalid update directory.");
                request = InstallRequest.Load(directory);
                Process parent = null;
                try { parent = Process.GetProcessById(request.ParentId); } catch (ArgumentException) {}
                if (parent != null) using (parent) {
                    if (!parent.HasExited) {
                        try {
                            if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStarted ||
                                !String.Equals(parent.MainModule.FileName, request.Target, StringComparison.OrdinalIgnoreCase))
                                throw new IOException("The running app changed. Please try updating again.");
                        } catch (InvalidOperationException) { if (!parent.HasExited) throw; }
                        if (!parent.WaitForExit(30000)) throw new IOException("Mochi did not close in time. Please try again.");
                    }
                }
                parentExited = true;
                // The old process has exited, releasing both the executable and the single-instance mutex.
                ReplaceAndRestart(directory, request, StartApp);
                return 0;
            } catch (Exception error) {
                MessageBox.Show(UpdateService.Explain(error), "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (parentExited && request != null && File.Exists(request.Target)) {
                    try { StartApp(request.Target, "--cleanup-update " + Quote(directory)); } catch {}
                }
                return 1;
            }
        }
        static bool IsUpdateDirectory(string directory) {
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            return String.Equals(Path.GetDirectoryName(full), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(Path.GetFileName(full), "\\AMochi-update-[0-9a-f]{32}\\z") &&
                (!Directory.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0);
        }
        public static void Cleanup(string directory) {
            try {
                if (!IsUpdateDirectory(directory)) return;
                foreach (string file in new[] { "Mochi.download.exe", "Mochi.updater.exe", "install.xml" }) TryDelete(Path.Combine(directory, file));
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
            } catch {}
        }
        public static void CleanupAfterRestart(string directory) {
            ThreadPool.QueueUserWorkItem(delegate {
                for (int i = 0; i < 30; i++) {
                    Cleanup(directory);
                    if (!Directory.Exists(directory)) return;
                    Thread.Sleep(500);
                }
            });
        }
        static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch {} }
    }

    sealed class UpdateDownloadDialog : Form {
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly BackgroundWorker worker = new BackgroundWorker();
        readonly Button cancel;
        bool finished;
        public string DownloadDirectory;
        public Exception Error;
        public UpdateDownloadDialog(UpdateService service, UpdateInfo update) {
            Text = "Updating Mochi"; ClientSize = new Size(420, 155); Font = new Font("Segoe UI", 10);
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Downloading Mochi " + AppVersion.Display(update.Version) + "...",
                AutoSize = true, Location = new Point(22, 22) });
            Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Location = new Point(22, 59), Size = new Size(376, 22) });
            cancel = new Button { Text = "Cancel", Location = new Point(298, 105), Size = new Size(100, 30) };
            cancel.Click += delegate { Close(); }; Controls.Add(cancel); CancelButton = cancel;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = service.Download(update, cancellation.Token); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                finished = true;
                if (e.Error != null) {
                    if (!(e.Error is OperationCanceledException) && !cancellation.IsCancellationRequested) Error = e.Error;
                    DialogResult = DialogResult.Cancel;
                } else if (cancellation.IsCancellationRequested) {
                    UpdateInstaller.Cleanup((string)e.Result); DialogResult = DialogResult.Cancel;
                } else { DownloadDirectory = (string)e.Result; DialogResult = DialogResult.OK; }
                Close();
            };
            Shown += delegate { worker.RunWorkerAsync(); };
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if (!finished) { cancellation.Cancel(); cancel.Enabled = false; cancel.Text = "Cancelling..."; e.Cancel = true; }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) { cancellation.Dispose(); worker.Dispose(); }
            base.Dispose(disposing);
        }
    }

    sealed partial class Companion {
        ToolStripMenuItem updateItem;
        readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        bool checkingUpdates;
        bool manualUpdateCheck;
        Action pendingUpdateNotice;
        void AddUpdateMenu() {
            updateItem = new ToolStripMenuItem("Check for updates");
            updateItem.Click += delegate { StartUpdateCheck(true); };
            menu.Items.Add(updateItem);
            menu.Items.Add("Info", null, delegate { ShowInfo(); });
        }
        void StartUpdateCheck(bool manual) {
            if (simulation || closing || IsDisposed) return;
            if (checkingUpdates) { manualUpdateCheck |= manual; return; }
            checkingUpdates = true; manualUpdateCheck = manual; pendingUpdateNotice = null;
            updateItem.Enabled = false; updateItem.Text = "Checking for updates...";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
                e.Result = new UpdateService(new GitHubTransport()).Check(AppVersion.Current, updateCancellation.Token);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                worker.Dispose();
                if (closing || IsDisposed) return;
                checkingUpdates = false; updateItem.Enabled = true; updateItem.Text = "Check for updates";
                bool requested = manualUpdateCheck;
                if (e.Error != null) {
                    if (requested && !(e.Error is OperationCanceledException))
                        pendingUpdateNotice = delegate { ShowUpdateMessage(UpdateService.Explain(e.Error), MessageBoxIcon.Information); };
                } else {
                    UpdateInfo update = (UpdateInfo)e.Result;
                    if (update != null) pendingUpdateNotice = delegate { OfferUpdate(update); };
                    else if (requested) pendingUpdateNotice = delegate {
                        ShowUpdateMessage("You're up to date!\n\nMochi Desktop " + AppVersion.Text, MessageBoxIcon.Information);
                    };
                }
            };
            worker.RunWorkerAsync();
        }
        void PumpUpdateNotice() {
            if (pendingUpdateNotice == null || closing || modal || menu.Visible || down || choosingDestination ||
                (!manualUpdateCheck && Now < 8)) return;
            Action notice = pendingUpdateNotice; pendingUpdateNotice = null; notice();
        }
        void BeginUpdateDialog() { CancelInteraction(); swimming = false; modal = true; }
        void EndUpdateDialog() { modal = false; Schedule(); ScheduleIdleActivity(); edgeWatch.Reset(Now); }
        void ShowUpdateMessage(string message, MessageBoxIcon icon) {
            BeginUpdateDialog();
            try { MessageBox.Show(message, "Mochi updates", MessageBoxButtons.OK, icon); }
            finally { EndUpdateDialog(); }
        }
        void ShowInfo() {
            BeginUpdateDialog();
            try {
                MessageBox.Show("Mochi Desktop\nVersion " + AppVersion.Text +
                    "\n\nYour little whale shark companion.\n\n" + UpdateService.Repository +
                    "\n\nUse Check for updates to look for a newer version.",
                    "About Mochi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } finally { EndUpdateDialog(); }
        }
        void OfferUpdate(UpdateInfo update) {
            bool restarting = false;
            BeginUpdateDialog();
            try {
                if (MessageBox.Show("Mochi " + AppVersion.Display(update.Version) + " is available!\nYou're running version " +
                    AppVersion.Text + ".\n\nDownload and install it now? Mochi will restart when it's ready.\nYour settings will be kept.",
                    "Mochi update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                using (UpdateDownloadDialog dialog = new UpdateDownloadDialog(new UpdateService(new GitHubTransport()), update)) {
                    if (dialog.ShowDialog() != DialogResult.OK) {
                        if (dialog.Error != null) MessageBox.Show(UpdateService.Explain(dialog.Error), "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try {
                        UpdateInstaller.PrepareAndStart(dialog.DownloadDirectory, update);
                        restarting = true;
                    } catch (Exception error) {
                        UpdateInstaller.Cleanup(dialog.DownloadDirectory);
                        MessageBox.Show(UpdateService.Explain(error), "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            } finally { EndUpdateDialog(); }
            if (restarting) Close();
        }
        void StopUpdateChecks() { updateCancellation.Cancel(); pendingUpdateNotice = null; }
        public static void VerifyUpdateMenu() {
            using (Companion pet = new Companion(true)) {
                int checks = 0, info = 0;
                foreach (ToolStripItem item in pet.menu.Items) {
                    if (item.Text == "Check for updates") checks++;
                    if (item.Text == "Info") info++;
                }
                if (checks != 1 || info != 1) throw new Exception("Missing or duplicate updater menu entries.");
            }
        }
    }
}
