using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Runtime.InteropServices;
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
    sealed class UpdateFailure : Exception {
        public readonly string Stage;
        public UpdateFailure(string stage, Exception error) : base(stage + ": " + error.Message, error) { Stage = stage; }
    }

    static class UpdateDiagnostics {
        public static T At<T>(string stage, Func<T> action) {
            try { return action(); }
            catch (OperationCanceledException) { throw; }
            catch (UpdateFailure) { throw; }
            catch (Exception error) { throw new UpdateFailure(stage, error); }
        }
        public static void At(string stage, Action action) { At<object>(stage, delegate { action(); return null; }); }
        public static string Stage(Exception error, string fallback) {
            UpdateFailure failure = error as UpdateFailure;
            return failure == null ? fallback : failure.Stage;
        }
        public static string Details(Exception error) {
            Exception cause = error.GetBaseException();
            Win32Exception native = cause as Win32Exception;
            return cause.GetType().Name + " (0x" + cause.HResult.ToString("X8", CultureInfo.InvariantCulture) + ")" +
                (native == null ? "" : "; Windows error " + native.NativeErrorCode.ToString(CultureInfo.InvariantCulture)) +
                "\r\n" + cause.Message;
        }
        // Called for failures only. No report is uploaded or included in update requests.
        public static string Report(Exception error, string fallbackStage, string target = null, string[] reportFolders = null) {
            string stage = Stage(error, fallbackStage);
            string message = UpdateService.Explain(error) + "\r\n\r\nStep: " + stage + "\r\n" + Details(error);
            string report = "Mochi " + AppVersion.Current + "\r\n" + DateTimeOffset.Now.ToString("o") +
                "\r\nStep: " + stage + "\r\nRunning executable: " + Application.ExecutablePath +
                "\r\nUpdate target: " + (target ?? Application.ExecutablePath) +
                "\r\nRuntime: " + Environment.Version + "; process: " + (IntPtr.Size * 8) + "-bit\r\n\r\n" + error;
            try {
                if (reportFolders == null) {
                    reportFolders = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mochi") };
                }
                foreach (string folder in reportFolders) {
                    try {
                        Directory.CreateDirectory(folder);
                        string path = Path.Combine(folder, "update-error.txt");
                        File.WriteAllText(path, report);
                        return message + "\r\n\r\nDetails saved to:\r\n" + path;
                    } catch (Exception) { /* Preserve the update error if reporting is blocked too. */ }
                }
            } catch (Exception) { }
            return message + "\r\n\r\nThe error report could not be saved. Please copy these details.";
        }
    }

    static class AppVersion {
        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string Display(Version version) { return version.Revision > 0 ? version.ToString(4) : version.ToString(3); }
        public static string Text { get { return Display(Current); } }
        public static bool ManualDownloadOnly { get {
#if MOCHI_MANUAL_RELEASE
            return true;
#else
            return false;
#endif
        } }
    }

    sealed class UpdateInfo {
        const string PublicModulus = "xouWeDgE9/Lng9LSI/nnbajT+W7tWt0pt5K8rsyehyjkOzH6w8WNTiAkkkhVAjR6CL7DJdAxaAyucnMhp2ck8rsCuwydqpBE5JANY7rC/MHCgOE4yIxtEnn92yBp51ca7z1/GW/62MlhsUhjeQKtqnAf6Ote3pg2oCGPGsVHrjM9cVA96QA1/UkzmqrUmdHKwZBZAdAstm5tnwl+Sr4cZBR51vqa8uYXA/PbfPALa8ddj0KZrk1WxHrZPLwNf+abJOxU17p9QMfTx2D8raBmF5DmLOVXEea7yPuF4qDNUfi6reY+p+xrOs5dV0/pcSZOy8TcXCMnbEa+KDhm0cPHVTHzDTaob1i2+aod+QfYqziwxBL7CeqaW2Ceg2TR6L2CpQ3Z5ZrLm7HcFYhi+jd45UNgsyHU2vrSP+l1WDCVQEKhwmjgMZEdEskLW/eoJfiCONCTvQssm56pSMigb2KDBew4gSVxTh4fmwmkfhUJWS6YQck/tM50K0xLGXx7uzaO/5p3ZpA/P+u+aRbR7hg5JHoj1KZsCcWeRUyUawjfWbZMG8plrygXYLwP+1FmEtygsiGLRAt00N23Ju5kcwOVJqtu+OuwtM/3ciRi7MWw71qc9tJDA7orBl8LtLeyYXqkIjmKATof4wD3pcKp+TECRo56x92oBcejh63xJ2fo8Ns=";
        const string PublicExponent = "AQAB";
#if MOCHI_TESTS
        // Test builds accept a separate fixture key. Release builds contain only the
        // production public key above and can never accept fixture signatures.
        const string TestModulus = "y9Vdm+trR3h+PFk7keNrfC5TVkTW3RHFM7E+IZGD1NAZMiKh5Bcpj28rCWyo2nrpnNFrECAT7JRShtk0CtEPrwH/QDO5cZdLDgNpD7++nr+Uf2o8d/husHubz4pU3oAacjs5799qNVwya432FzbttVvApvmXQ9kjkcCyTt4S4TBhw0S5yZNfa7xgQhQlwHiECAoZ559mngQIyXBoOkTfs47oD1uNy+Aew4JBcfGNPZW70vKNhiUvGbkwdsKKAX/gIPo+141z4XzV0qDm8LwhPoa5wBBrp1qY3f/7zTLhNiqkj3wlWNIx//wIAZuAj0urgipjMyi5e6La3I2ZH+BjmQ==";
#endif
        public Version Version;
        public string Sha256;
        public string Signature;
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
            string signatureText = (string)xml.Element("Signature");
            if (xml.Name != "MochiUpdate" || (string)xml.Element("File") != "Mochi.exe" ||
                !System.Version.TryParse((string)xml.Element("Version"), out version) ||
                version.Build < 0 || version.Major < 1 ||
                !Int64.TryParse((string)xml.Element("Size"), NumberStyles.None, CultureInfo.InvariantCulture, out size) ||
                size <= 0 || size > MaximumSize || !IsHash(hash, 64) || !IsHash(commit, 40) ||
                String.IsNullOrEmpty(signatureText) || signatureText.Length > 2048)
                throw new InvalidDataException("The update information from GitHub is invalid.");
            version = new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
            hash = hash.ToLowerInvariant();
            byte[] signature;
            try { signature = Convert.FromBase64String(signatureText); }
            catch (FormatException) { throw new InvalidDataException("The update signature is invalid."); }
            byte[] payload = Canonical(version, hash, size);
            if (!VerifySignature(payload, signature))
                throw new InvalidDataException("The update signature is invalid. Mochi will not install an update whose publisher cannot be verified.");
            return new UpdateInfo { Version = version, Sha256 = hash, Signature = Convert.ToBase64String(signature), Size = size, Commit = commit };
        }
        public static byte[] Canonical(Version version, string hash, long size) {
            Version normalized = new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
            return Encoding.UTF8.GetBytes("MochiUpdate\nVersion=" + normalized.ToString(4) + "\nFile=Mochi.exe\nSize=" +
                size.ToString(CultureInfo.InvariantCulture) + "\nSha256=" + hash.ToLowerInvariant() + "\n");
        }
        static bool VerifyWithKey(byte[] payload, byte[] signature, string modulus) {
            try {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider()) {
                    rsa.PersistKeyInCsp = false;
                    rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromBase64String(modulus), Exponent = Convert.FromBase64String(PublicExponent) });
                    return rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), signature);
                }
            } catch (CryptographicException) { return false; }
        }
        static bool VerifySignature(byte[] payload, byte[] signature) {
            if (VerifyWithKey(payload, signature, PublicModulus)) return true;
#if MOCHI_TESTS
            return VerifyWithKey(payload, signature, TestModulus);
#else
            return false;
#endif
        }
        public static bool IsHash(string value, int length) {
            return value != null && Regex.IsMatch(value, "\\A[0-9a-fA-F]{" + length + "}\\z");
        }
    }

    interface IUpdateTransport {
        byte[] Get(string url, int maximumBytes, CancellationToken cancellation);
        void Download(string url, string path, long expectedSize, CancellationToken cancellation);
    }

    sealed class GitHubNotFoundException : IOException {
        public GitHubNotFoundException() : base("GitHub did not find the requested release or file.") { }
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
                    } catch (WebException error) {
                        cancellation.ThrowIfCancellationRequested();
                        if (deadline.IsCancellationRequested) throw new TimeoutException("The connection to GitHub timed out.");
                        HttpWebResponse response = error.Response as HttpWebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.NotFound) {
                            response.Dispose();
                            throw new GitHubNotFoundException();
                        }
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

    // Release announcements are informational only. They never authorize the
    // executable downloader or installer, which still require signed metadata.
    sealed class ManualReleaseInfo {
        public readonly Version Version;
        public readonly string PageUrl;
        ManualReleaseInfo(Version version, string tag) {
            Version = version;
            PageUrl = UpdateService.Repository + "/releases/tag/" + tag;
        }
        [DataContract] sealed class ReleaseResponse {
            [DataMember(Name = "tag_name", IsRequired = true)] public string Tag;
            [DataMember(Name = "draft", IsRequired = true)] public bool Draft;
            [DataMember(Name = "prerelease", IsRequired = true)] public bool Prerelease;
            [DataMember(Name = "published_at", IsRequired = true)] public string Published;
        }
        public static ManualReleaseInfo Parse(byte[] bytes) {
            if (bytes == null || bytes.Length > ManualReleaseService.MaximumBytes)
                throw new InvalidDataException("The GitHub release information is too large or missing.");
            ReleaseResponse release;
            using (MemoryStream stream = new MemoryStream(bytes))
                release = (ReleaseResponse)new DataContractJsonSerializer(typeof(ReleaseResponse)).ReadObject(stream);
            Version version;
            DateTimeOffset published;
            if (release == null || release.Draft || release.Prerelease || release.Tag == null || release.Tag.Length > 64 ||
                !Regex.IsMatch(release.Tag, @"\Av?[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z") ||
                !System.Version.TryParse(release.Tag.TrimStart('v'), out version) || version.Major < 1 ||
                !DateTimeOffset.TryParse(release.Published, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out published))
                throw new InvalidDataException("GitHub did not return a valid published Mochi release.");
            return new ManualReleaseInfo(new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision)), release.Tag);
        }
    }

    sealed class ManualReleaseService {
        public const string LatestUrl = "https://api.github.com/repos/kevinsashimi/Mochi-Desktop/releases/latest";
        public const int MaximumBytes = 256 * 1024;
        readonly IUpdateTransport transport;
        public ManualReleaseService(IUpdateTransport transport) { this.transport = transport; }
        public ManualReleaseInfo Check(CancellationToken cancellation) {
            return UpdateDiagnostics.At("Checking published GitHub releases", delegate {
                cancellation.ThrowIfCancellationRequested();
                byte[] bytes;
                try { bytes = transport.Get(LatestUrl, MaximumBytes, cancellation); }
                catch (GitHubNotFoundException) { cancellation.ThrowIfCancellationRequested(); return null; }
                cancellation.ThrowIfCancellationRequested();
                return ManualReleaseInfo.Parse(bytes);
            });
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
            byte[] commitBytes = UpdateDiagnostics.At("Reading the latest GitHub version", delegate {
                return transport.Get(CommitUrl, 512 * 1024, cancellation);
            });
            CommitResponse commit = UpdateDiagnostics.At("Reading the GitHub version reference", delegate {
                using (MemoryStream stream = new MemoryStream(commitBytes))
                    return (CommitResponse)new DataContractJsonSerializer(typeof(CommitResponse)).ReadObject(stream);
            });
            if (commit == null || !UpdateInfo.IsHash(commit.Sha, 40)) throw new InvalidDataException("GitHub returned an invalid version reference.");
            // Pin the small manifest and executable to the same commit.
            UpdateInfo latest = UpdateDiagnostics.At("Reading the update manifest", delegate {
                return UpdateInfo.Parse(transport.Get(RawBase + commit.Sha + "/Project%20Files/update.xml", 16384, cancellation), commit.Sha);
            });
            return latest.Version > installed ? latest : null;
        }
        public string Download(UpdateInfo update, CancellationToken cancellation) {
            string directory = UpdateDiagnostics.At("Creating the temporary update folder", delegate {
                string path = Path.Combine(Path.GetTempPath(), "Mochi-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path); return path;
            });
            string candidate = Path.Combine(directory, "Mochi.download.exe");
            try {
                UpdateDiagnostics.At("Downloading the new app", delegate {
                    transport.Download(RawBase + update.Commit + "/Mochi.exe", candidate, update.Size, cancellation);
                });
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
            UpdateDiagnostics.At("Verifying the update file's size and checksum", delegate {
                if (new FileInfo(path).Length != expected.Size || !String.Equals(Hash(path), expected.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded update failed its integrity check. Please try again.");
            });
            UpdateDiagnostics.At("Reading the update file's assembly version", delegate {
                AssemblyName assembly = AssemblyName.GetAssemblyName(path);
                if (assembly.Name != "Mochi" || assembly.Version != expected.Version)
                    throw new InvalidDataException("The downloaded program does not match the announced Mochi version.");
            });
        }
        public static string Explain(Exception error) {
            error = error.GetBaseException();
            Win32Exception native = error as Win32Exception;
            int code = native == null ? error.HResult & 0xffff : native.NativeErrorCode;
            if ((error is IOException || native != null) && (code == 32 || code == 33))
                return "A file needed by the updater is locked by another process. Close other Mochi copies and try again.";
            if (native != null && (code == 225 || code == 226))
                return "Windows security software blocked an update file. Check Windows Security Protection history for details.";
            if (native != null && code == 1260)
                return "A Windows application policy blocked the updater. Check with the administrator who manages this PC.";
            if (native != null && code == 5)
                return "Windows denied access while preparing or starting the updater.";
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
            return "The update stopped unexpectedly. The details below identify the failed step.";
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
                new XElement("Version", Update.Version), new XElement("Sha256", Update.Sha256), new XElement("Signature", Update.Signature),
                new XElement("Size", Update.Size)).Save(Path.Combine(directory, "install.xml"));
        }
        public static InstallRequest Load(string directory) {
            XElement xml;
            using (FileStream stream = File.OpenRead(Path.Combine(directory, "install.xml"))) xml = UpdateInfo.ReadXml(stream);
            UpdateInfo info = UpdateInfo.Parse(Encoding.UTF8.GetBytes(new XElement("MochiUpdate",
                new XElement("File", "Mochi.exe"), new XElement("Version", (string)xml.Element("Version")),
                new XElement("Size", (string)xml.Element("Size")), new XElement("Sha256", (string)xml.Element("Sha256")),
                new XElement("Signature", (string)xml.Element("Signature"))).ToString()), new string('0', 40));
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
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int informationClass,out int information,int length,out int returned);
        internal static bool IsElevated() {
#if MOCHI_TESTS
            string forced = Environment.GetEnvironmentVariable("MOCHI_TEST_ELEVATION");
            if (forced == "0") return false;
            if (forced == "1") return true;
#endif
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(),8,out token)) throw new Win32Exception(Marshal.GetLastWin32Error()); // TOKEN_QUERY
            try {
                int elevated,returned;
                if (!GetTokenInformation(token,20,out elevated,sizeof(int),out returned)) // TokenElevation
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return elevated != 0;
            } finally { CloseHandle(token); }
        }
        internal static void EnsureNotElevated() {
            if (IsElevated()) throw new InvalidDataException("Mochi cannot safely update while it is running as administrator. Close it, reopen it normally, and check for updates again.");
        }
        public static string Quote(string value) {
            if (value.IndexOf('"') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + value + "\"";
        }
        public static void PrepareAndStart(string directory, UpdateInfo update) {
            PrepareAndStart(directory, update, Application.ExecutablePath, delegate(ProcessStartInfo start) {
                using (Process process = Process.Start(start)) {
                    if (process == null) throw new IOException("The updater could not start.");
                }
            });
        }
        // The launch callback lets tests exercise the full preparation path without closing the test runner.
        internal static void PrepareAndStart(string directory, UpdateInfo update, string target, Action<ProcessStartInfo> launch) {
            UpdateDiagnostics.At("Checking updater privileges", EnsureNotElevated);
            UpdateService.VerifyExecutable(Path.Combine(directory, "Mochi.download.exe"), update);
            UpdateDiagnostics.At("Checking write access to Mochi's folder", delegate {
                string probe = Path.Combine(Path.GetDirectoryName(target), ".mochi-write-" + Guid.NewGuid().ToString("N"));
                using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {}
            });
            InstallRequest request = UpdateDiagnostics.At("Reading the running app's identity", delegate {
                using (Process current = Process.GetCurrentProcess()) {
                    return new InstallRequest { Target = target, OldHash = UpdateService.Hash(target), ParentId = current.Id,
                        ParentStarted = current.StartTime.ToUniversalTime().Ticks, Update = update };
                }
            });
            UpdateDiagnostics.At("Writing the update handoff", delegate { request.Save(directory); });
            string helper = Path.Combine(directory, "Mochi.updater.exe");
            UpdateDiagnostics.At("Copying the updater", delegate { File.Copy(target, helper, false); });
            UpdateDiagnostics.At("Starting the updater", delegate {
                launch(new ProcessStartInfo(helper, "--apply-update " + Quote(directory)) {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = directory
                });
            });
        }
        public static void ReplaceAndRestart(string directory, InstallRequest request, Action<string, string> restart) {
            string candidate = Path.Combine(directory, "Mochi.download.exe");
            UpdateService.VerifyExecutable(candidate, request.Update);
            UpdateDiagnostics.At("Checking the installed app before replacement", delegate {
                if (UpdateService.Hash(request.Target) != request.OldHash)
                    throw new IOException("Mochi changed while the update was downloading. Please check for updates again.");
                AssemblyName installed = AssemblyName.GetAssemblyName(request.Target);
                if (installed.Name != "Mochi" || request.Update.Version <= installed.Version)
                    throw new InvalidDataException("An update must be newer than the installed Mochi version.");
            });
            string suffix = Guid.NewGuid().ToString("N");
            string incoming = Path.Combine(Path.GetDirectoryName(request.Target), ".Mochi-incoming-" + suffix + ".tmp");
            string backup = Path.Combine(Path.GetDirectoryName(request.Target), ".Mochi-backup-" + suffix + ".tmp");
            bool restarted = false;
            try {
                UpdateDiagnostics.At("Copying the new app into Mochi's folder", delegate { File.Copy(candidate, incoming, false); });
                UpdateService.VerifyExecutable(incoming, request.Update);
                // Same-volume atomic replacement: interruption cannot leave a half-written executable.
                UpdateDiagnostics.At("Replacing the installed app", delegate { File.Replace(incoming, request.Target, backup); });
                try {
                    UpdateDiagnostics.At("Restarting Mochi", delegate { restart(request.Target, "--cleanup-update " + Quote(directory)); });
                    restarted = true;
                } catch {
                    UpdateDiagnostics.At("Restoring the previous app", delegate { File.Replace(backup, request.Target, null); });
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
            try { EnsureNotElevated(); }
            catch (Exception error) {
                MessageBox.Show(UpdateService.Explain(error),"Mochi update",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return 1;
            }
            return RunHelper(directory, delegate(Exception error,string target) {
                MessageBox.Show(UpdateDiagnostics.Report(error, "Installing the update", target),
                    "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        }
        internal static int RunHelper(string directory, Action<Exception,string> report) {
            InstallRequest request = null;
            bool parentExited = false;
            try {
                UpdateDiagnostics.At("Checking updater privileges", EnsureNotElevated);
                request = UpdateDiagnostics.At("Reading the updater handoff", delegate {
                    if (!IsUpdateDirectory(directory)) throw new InvalidDataException("Invalid update directory.");
                    return InstallRequest.Load(directory);
                });
                UpdateDiagnostics.At("Waiting for the running Mochi to close", delegate {
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
                });
                parentExited = true;
                // The old process has exited, releasing both the executable and the single-instance mutex.
                ReplaceAndRestart(directory, request, StartApp);
                return 0;
            } catch (Exception error) {
                report(error, request == null ? null : request.Target);
                if (parentExited && request != null && File.Exists(request.Target)) {
                    try { StartApp(request.Target, "--cleanup-update " + Quote(directory)); } catch {}
                }
                return 1;
            }
        }
        static bool IsUpdateDirectory(string directory) {
            // .NET Framework has TrimEnd(char[]), not Mono's newer TrimEnd(char).
            string full = Path.GetFullPath(directory).TrimEnd(new[] { Path.DirectorySeparatorChar });
            return String.Equals(Path.GetDirectoryName(full), Path.GetTempPath().TrimEnd(new[] { Path.DirectorySeparatorChar }), StringComparison.OrdinalIgnoreCase) &&
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
            if (simulation) return;
            RunUpdateCheck(manual, new GitHubTransport());
        }
        void RunUpdateCheck(bool manual, IUpdateTransport transport) {
            if (closing || IsDisposed || updateCancellation.IsCancellationRequested) return;
            if (checkingUpdates) { manualUpdateCheck |= manual; return; }
            checkingUpdates = true; manualUpdateCheck = manual; pendingUpdateNotice = null;
            updateItem.Enabled = false; updateItem.Text = "Checking for updates...";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
                if (AppVersion.ManualDownloadOnly)
                    e.Result = new ManualReleaseService(transport).Check(updateCancellation.Token);
                else e.Result = new UpdateService(transport).Check(AppVersion.Current, updateCancellation.Token);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                worker.Dispose();
                if (closing || IsDisposed) return;
                checkingUpdates = false; updateItem.Enabled = true; updateItem.Text = "Check for updates";
                if (updateCancellation.IsCancellationRequested) return;
                bool requested = manualUpdateCheck;
                if (e.Error != null) {
                    if (requested && !(e.Error is OperationCanceledException))
                        pendingUpdateNotice = delegate { ShowUpdateMessage(UpdateDiagnostics.Report(e.Error, "Checking for updates"), MessageBoxIcon.Information); };
                } else if (AppVersion.ManualDownloadOnly) {
                    QueueManualReleaseNotice((ManualReleaseInfo)e.Result, requested);
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
        void QueueManualReleaseNotice(ManualReleaseInfo release, bool requested) {
            if (release != null && release.Version > AppVersion.Current)
                pendingUpdateNotice = delegate { ShowManualDownloads(release); };
            else if (requested) pendingUpdateNotice = delegate {
                ShowUpdateMessage(release == null ?
                    "No public release is available on GitHub yet.\n\nYou're running Mochi Desktop " + AppVersion.Text + ". Please check again later." :
                    "You're up to date!\n\nMochi Desktop " + AppVersion.Text + "\nLatest public release: " + AppVersion.Display(release.Version),
                    MessageBoxIcon.Information);
            };
        }
        void PumpUpdateNotice() {
            if (pendingUpdateNotice == null || closing || modal || releaseWelcome != null || menu.Visible || down || choosingDestination || manualSwim != null || playful != null ||
                (!manualUpdateCheck && Now < 8)) return;
            Action notice = pendingUpdateNotice; pendingUpdateNotice = null; notice();
        }
        void BeginUpdateDialog() { CloseReleaseWelcome(); CancelInteraction(); swimming = false; modal = true; }
        void EndUpdateDialog() { modal = false; Schedule(); PauseAutomaticActivities(); edgeWatch.Reset(Now); }
        void ShowUpdateMessage(string message, MessageBoxIcon icon) {
            BeginUpdateDialog();
            try { MessageBox.Show(message, "Mochi updates", MessageBoxButtons.OK, icon); }
            finally { EndUpdateDialog(); }
        }
        void ShowManualDownloads(ManualReleaseInfo release) {
            BeginUpdateDialog();
            try {
                if (MessageBox.Show("Mochi " + AppVersion.Display(release.Version) + " is available!\nYou're running version " + AppVersion.Text + ".\n\n" +
                    "Download Mochi.exe from GitHub. Quit Mochi, then replace only Mochi.exe in the same folder to keep your settings.\n\n" +
                    "Open the download page?", "Mochi updates", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                try { using (Process browser = Process.Start(new ProcessStartInfo(release.PageUrl) { UseShellExecute = true })) { } }
                catch (Exception error) {
                    if (!(error is Win32Exception) && !(error is InvalidOperationException)) throw;
                    MessageBox.Show("Couldn't open your browser. Visit:\n\n" + release.PageUrl,
                        "Mochi updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            } finally { EndUpdateDialog(); }
        }
        void ShowInfo() {
            BeginUpdateDialog();
            try {
                MessageBox.Show("Mochi Desktop\nVersion " + AppVersion.Text +
                    "\n\nYour little whale shark companion.\n\n" + UpdateService.Repository +
                    "\n\nRunning from:\n" + Application.ExecutablePath +
                    (AppVersion.ManualDownloadOnly ? "\n\nChecks for updates at startup. Use Check for updates anytime; download and install new versions manually." :
                    "\n\nUse Check for updates to look for a newer version."),
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
                        if (dialog.Error != null) MessageBox.Show(UpdateDiagnostics.Report(dialog.Error, "Downloading the update"), "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try {
                        UpdateInstaller.PrepareAndStart(dialog.DownloadDirectory, update);
                        restarting = true;
                    } catch (Exception error) {
                        UpdateInstaller.Cleanup(dialog.DownloadDirectory);
                        MessageBox.Show(UpdateDiagnostics.Report(error, "Preparing the updater"), "Mochi update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            } finally { EndUpdateDialog(); }
            if (restarting) Close();
        }
        void StopUpdateChecks() { updateCancellation.Cancel(); pendingUpdateNotice = null; }
#if MOCHI_TESTS
        internal static void VerifyManualReleaseCheck(IUpdateTransport transport, bool requested, bool notice, bool cancel) {
            using (Companion pet = new Companion(true)) {
                pet.RunUpdateCheck(requested, transport);
                if (!pet.checkingUpdates || pet.updateItem.Enabled) throw new Exception("Release check did not enter its busy state.");
                pet.RunUpdateCheck(requested, transport); // A repeated click must share the active request.
                if (cancel) pet.StopUpdateChecks();
                Stopwatch timeout = Stopwatch.StartNew();
                while (pet.checkingUpdates && timeout.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(10); }
                if (pet.checkingUpdates || !pet.updateItem.Enabled || pet.updateItem.Text != "Check for updates")
                    throw new Exception("Release check did not restore its menu state.");
                if ((pet.pendingUpdateNotice != null) != notice) throw new Exception("Incorrect release notification state.");
            }
        }
#endif
        public static void VerifyUpdateMenu() {
            using (Companion pet = new Companion(true)) {
                int checks = 0, info = 0;
                foreach (ToolStripItem item in pet.menu.Items) {
                    if (item.Text == "Check for updates") checks++;
                    if (item.Text == "Info") info++;
                }
                if (checks != 1 || info != 1) throw new Exception("Missing or duplicate updater menu entries.");
                if (AppVersion.ManualDownloadOnly) {
                    pet.StartUpdateCheck(false);
                    if (pet.pendingUpdateNotice != null || pet.checkingUpdates || !pet.updateItem.Enabled)
                        throw new Exception("Simulation launched a real network request.");
                    pet.QueueManualReleaseNotice(null, false);
                    if (pet.pendingUpdateNotice != null) throw new Exception("Startup announced a missing public release.");
                    pet.QueueManualReleaseNotice(null, true);
                    if (pet.pendingUpdateNotice == null) throw new Exception("Explicit check did not report a missing public release.");
                    pet.StopUpdateChecks();
                    if (pet.pendingUpdateNotice != null) throw new Exception("Manual-download notice survived cancellation.");
                }
            }
        }
    }
}
