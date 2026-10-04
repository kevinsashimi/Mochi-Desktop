using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace MochiDesktop {
    static class UpdateTests {
        static void Check(bool value, string message) { if (!value) throw new Exception("Updates: " + message); }
        static void Reject(Action action, string message) {
            try { action(); } catch { return; }
            throw new Exception("Updates accepted " + message);
        }
        static byte[] Manifest(Version version, string hash, long size) {
            return Encoding.UTF8.GetBytes(new XElement("MochiUpdate", new XElement("Version", version),
                new XElement("File", "Mochi.exe"), new XElement("Size", size), new XElement("Sha256", hash)).ToString());
        }
        sealed class FakeTransport : IUpdateTransport {
            public byte[] Metadata, Binary;
            public readonly List<string> Requests = new List<string>();
            public Exception Failure;
            public byte[] Get(string url, int max, CancellationToken cancellation) {
                cancellation.ThrowIfCancellationRequested(); Requests.Add(url);
                if (Failure != null) throw Failure;
                return url == UpdateService.CommitUrl ? Encoding.UTF8.GetBytes("{\"sha\":\"" + new string('a', 40) + "\"}") : Metadata;
            }
            public void Download(string url, string path, long size, CancellationToken cancellation) {
                cancellation.ThrowIfCancellationRequested(); Requests.Add(url);
                if (Failure != null) throw Failure;
                File.WriteAllBytes(path, Binary);
            }
        }
        static string Fixture(string directory, Version version, bool wait, bool marker) {
            Directory.CreateDirectory(directory);
            AssemblyName name = new AssemblyName("Mochi") { Version = version };
            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave, directory);
            ModuleBuilder module = assembly.DefineDynamicModule("Mochi", "Mochi.exe");
            TypeBuilder type = module.DefineType("Fixture", TypeAttributes.Public);
            MethodBuilder main = type.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            ILGenerator il = main.GetILGenerator();
            if (wait) { il.Emit(OpCodes.Ldc_I4, 1600); il.Emit(OpCodes.Call, typeof(Thread).GetMethod("Sleep", new[] { typeof(int) })); }
            if (marker) {
                il.Emit(OpCodes.Ldstr, Path.Combine(directory, "restarted.txt")); il.Emit(OpCodes.Ldstr, "started");
                il.Emit(OpCodes.Call, typeof(File).GetMethod("WriteAllText", new[] { typeof(string), typeof(string) }));
            }
            il.Emit(OpCodes.Ret); type.CreateType(); assembly.SetEntryPoint(main, PEFileKinds.ConsoleApplication); assembly.Save("Mochi.exe");
            return Path.Combine(directory, "Mochi.exe");
        }
        static UpdateFailure FailureAt(Action action, string stage) {
            try { action(); }
            catch (UpdateFailure error) {
                Check(error.Stage == stage, "wrong failure stage: " + error.Stage);
                return error;
            }
            throw new Exception("Updates: expected failure at " + stage);
        }
        static IEnumerable<MethodBase> Calls(MethodInfo method) {
            Dictionary<short, OpCode> codes = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.FieldType == typeof(OpCode)) { OpCode code = (OpCode)field.GetValue(null); codes[code.Value] = code; }
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            for (int offset = 0; offset < il.Length;) {
                short value = il[offset++];
                if (value == 0xfe) value = unchecked((short)(0xfe00 | il[offset++]));
                OpCode code = codes[value];
                switch (code.OperandType) {
                    case OperandType.InlineMethod:
                        yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, offset)); offset += 4; break;
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: offset++; break;
                    case OperandType.InlineVar: offset += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: offset += 8; break;
                    case OperandType.InlineSwitch: offset += 4 + 4 * BitConverter.ToInt32(il, offset); break;
                    default: offset += 4; break;
                }
            }
        }
        public static void VerifyFrameworkCompatibility(Assembly assembly) {
            MethodInfo check = assembly.GetType("MochiDesktop.UpdateInstaller").GetMethod("IsUpdateDirectory", BindingFlags.NonPublic | BindingFlags.Static);
            int trims = 0;
            // Executing under Mono alone cannot catch this Windows MissingMethodException.
            // Verify the emitted member reference, including when compiled with Mono libraries.
            foreach (MethodBase call in Calls(check)) if (call.DeclaringType == typeof(string) && call.Name == "TrimEnd") {
                ParameterInfo[] parameters = call.GetParameters();
                Check(parameters.Length == 1 && parameters[0].ParameterType == typeof(char[]), "TrimEnd must reference the .NET Framework char[] overload, not TrimEnd(char)");
                trims++;
            }
            Check(trims == 2, "update-directory validation lost its two separator trims");
            string directory = Path.Combine(Path.GetTempPath(), "Mochi-update-" + Guid.NewGuid().ToString("N"));
            Check((bool)check.Invoke(null, new object[] { directory }), "valid temporary update directory rejected");
            Check((bool)check.Invoke(null, new object[] { directory + Path.DirectorySeparatorChar }), "trailing separator rejected");
            Check(!(bool)check.Invoke(null, new object[] { directory + "-unexpected" }), "unexpected directory accepted");
            Check(!(bool)check.Invoke(null, new object[] { Path.Combine(directory, "Mochi-update-" + new string('a', 32)) }), "nested update directory accepted");
        }
        static void VerifyDiagnostics(string root, string self, UpdateInfo update) {
            FakeTransport network = new FakeTransport { Binary = File.ReadAllBytes(self), Failure = new IOException("transfer interrupted") };
            UpdateService service = new UpdateService(network);
            FailureAt(delegate { service.Download(update, CancellationToken.None); }, "Downloading the new app");
            network.Failure = null;
            string folder = Path.Combine(root, "diagnostic install");
            string target = Fixture(folder, new Version(0, 9, 0, 0), false, false);
            string oldHash = UpdateService.Hash(target);
            string preferences = Path.Combine(folder, "Project Files"); Directory.CreateDirectory(preferences);
            string settings = Path.Combine(preferences, "settings.xml"); File.WriteAllText(settings, "keep settings");
            string download = service.Download(update, CancellationToken.None);
            bool launched = false;
            try {
                using (FileStream locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None)) {
                    FailureAt(delegate {
                        UpdateInstaller.PrepareAndStart(download, update, target, delegate { launched = true; });
                    }, "Reading the running app's identity");
                }
                Check(!launched, "preparation failure started a helper");
                UpdateFailure failure = FailureAt(delegate {
                    UpdateInstaller.PrepareAndStart(download, update, target, delegate(ProcessStartInfo start) {
                        launched = true;
                        Check(!start.UseShellExecute && start.Arguments == "--apply-update " + UpdateInstaller.Quote(download), "helper launch parameters changed");
                        InstallRequest request = InstallRequest.Load(download);
                        Check(request.Target == target && request.OldHash == oldHash && File.Exists(start.FileName), "handoff not ready before launch");
                        throw new Win32Exception(5, "Access is denied");
                    });
                }, "Starting the updater");
                Check(launched && UpdateService.Hash(target) == oldHash && File.ReadAllText(settings) == "keep settings", "failed launch modified the installed app or preferences");
                string message = UpdateDiagnostics.Report(failure, "Preparing", target);
                string report = File.ReadAllText(Path.Combine(preferences, "update-error.txt"));
                Check(message.Contains("Starting the updater") && message.Contains("Windows error 5") && message.Contains("Win32Exception") && message.Contains("0x"), "failure dialog hid its stage or native code");
                Check(report.Contains("Step: Starting the updater") && report.Contains("Update target: " + target) && report.Contains("Access is denied"), "persistent report lost original failure details");
                string blocked = Path.Combine(root, "blocked-log-folder"); File.WriteAllText(blocked, "not a directory");
                string fallback = Path.Combine(root, "fallback-log");
                message = UpdateDiagnostics.Report(failure, "Preparing", target, new[] { blocked, fallback });
                Check(File.Exists(Path.Combine(fallback, "update-error.txt")) && message.Contains(fallback), "report did not fall back from an unwritable location");
                message = UpdateDiagnostics.Report(failure, "Preparing", target, new[] { blocked });
                Check(message.Contains("could not be saved") && message.Contains("Windows error 5"), "logging failure hid the update failure");
                FailureAt(delegate { UpdateDiagnostics.At("Outer stage", delegate { throw failure; }); }, "Starting the updater");
                OperationCanceledException canceled = new OperationCanceledException();
                try { UpdateDiagnostics.At("Canceled transfer", delegate { throw canceled; }); throw new Exception("cancellation swallowed"); }
                catch (OperationCanceledException error) { Check(Object.ReferenceEquals(error, canceled), "cancellation was wrapped"); }
                Check(UpdateService.Explain(new Win32Exception(32)).Contains("locked"), "sharing violation guidance missing");
                Check(UpdateService.Explain(new Win32Exception(225)).Contains("Protection history"), "security block guidance missing");
                Check(UpdateService.Explain(new Win32Exception(1260)).Contains("policy"), "application policy guidance missing");
                string invalid = Path.Combine(root, "invalid-image.exe"); File.WriteAllText(invalid, "not a managed assembly");
                FailureAt(delegate {
                    UpdateService.VerifyExecutable(invalid, new UpdateInfo { Size = new FileInfo(invalid).Length, Sha256 = UpdateService.Hash(invalid), Version = update.Version });
                }, "Reading the update file's assembly version");
            } finally { UpdateInstaller.Cleanup(download); }
        }
        public static int Run(string reportPath) {
            string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)), "update-fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            List<string> results = new List<string>();
            List<string> downloads = new List<string>();
            try {
                string self = Assembly.GetExecutingAssembly().Location;
                Version current = AppVersion.Current;
                string hash = UpdateService.Hash(self);
                long size = new FileInfo(self).Length;
                string commit = new string('a', 40);
                FakeTransport network = new FakeTransport { Metadata = Manifest(current, hash, size), Binary = File.ReadAllBytes(self) };
                UpdateService service = new UpdateService(network);
                Check(service.Check(current, CancellationToken.None) == null, "same version offered");
                Check(service.Check(new Version(current.Major + 1, 0, 0, 0), CancellationToken.None) == null, "downgrade offered");
                UpdateInfo update = service.Check(new Version(0, 9, 0, 0), CancellationToken.None);
                Check(update != null && update.Version == current, "newer version not found");
                Check(network.Requests.Count == 6 && network.Requests.TrueForAll(delegate(string url) { return !url.EndsWith(".exe"); }),
                    "checking downloaded an executable before consent");
                Check(network.Requests[1].Contains("/" + commit + "/Project%20Files/update.xml"), "manifest was not pinned to commit");
                Check(UpdateInfo.Parse(Manifest(new Version(1, 10, 0), hash, size), commit).Version > new Version(1, 9, 0, 0), "version comparison is not numeric");
                Reject(delegate { UpdateInfo.Parse(Encoding.UTF8.GetBytes("<bad>"), commit); }, "malformed manifest");
                Reject(delegate { UpdateInfo.Parse(Manifest(current, "invalid", size), commit); }, "invalid hash");
                Reject(delegate { UpdateInfo.Parse(Manifest(current, hash, UpdateInfo.MaximumSize + 1), commit); }, "oversize update");
                Reject(delegate { UpdateInfo.Parse(Manifest(current, hash, size), "../main"); }, "invalid commit");
                Reject(delegate { UpdateInfo.Parse(Encoding.UTF8.GetBytes("<!DOCTYPE MochiUpdate [<!ENTITY x SYSTEM 'file:///no'>]><MochiUpdate>&x;</MochiUpdate>"), commit); }, "XML entity");
                network.Failure = new IOException("offline");
                Reject(delegate { service.Check(current, CancellationToken.None); }, "network failure");
                network.Failure = null;
                using (CancellationTokenSource cancellation = new CancellationTokenSource()) {
                    cancellation.Cancel();
                    Reject(delegate { service.Check(current, cancellation.Token); }, "cancelled check");
                    Reject(delegate { service.Download(update, cancellation.Token); }, "cancelled download");
                }
                results.Add("PASS: newer/same/older versions; numeric ordering; malformed/oversize metadata; offline and cancellation; no executable download during checks.");

                string download = service.Download(update, CancellationToken.None); downloads.Add(download);
                Check(network.Requests[network.Requests.Count - 1].EndsWith("/" + commit + "/Mochi.exe"), "download is not only the commit-pinned Mochi.exe");
                Check(Directory.GetFiles(download).Length == 1, "download included extra files");
                network.Binary = Encoding.UTF8.GetBytes("broken");
                Reject(delegate { service.Download(update, CancellationToken.None); }, "corrupted download");
                network.Binary = File.ReadAllBytes(self);
                UpdateInfo wrongVersion = new UpdateInfo { Version = new Version(99, 0, 0, 0), Size = size, Sha256 = hash };
                Reject(delegate { UpdateService.VerifyExecutable(Path.Combine(download, "Mochi.download.exe"), wrongVersion); }, "mismatched binary version");
                results.Add("PASS: only Mochi.exe downloaded; SHA-256, length, and embedded version validation; bad downloads rejected.");

                string targetFolder = Path.Combine(root, "install");
                string target = Fixture(targetFolder, new Version(0, 9, 0, 0), false, false);
                byte[] original = File.ReadAllBytes(target);
                string originalHash = UpdateService.Hash(target);
                Directory.CreateDirectory(Path.Combine(targetFolder, "Project Files"));
                string settings = Path.Combine(targetFolder, "Project Files", "settings.xml");
                File.WriteAllText(settings, "<Mochi><Size>224</Size></Mochi>");
                string untouched = Path.Combine(targetFolder, "Project Files", "notes.txt");
                File.WriteAllText(untouched, "keep me");
                InstallRequest request = new InstallRequest { Target = target, OldHash = originalHash, Update = update };
                bool restarted = false;
                UpdateInstaller.ReplaceAndRestart(download, request, delegate(string app, string args) {
                    Check(app == target && args.StartsWith("--cleanup-update "), "bad restart");
                    restarted = true;
                });
                Check(restarted && UpdateService.Hash(target) == hash, "replacement failed");
                Check(File.ReadAllText(settings) == "<Mochi><Size>224</Size></Mochi>" && File.ReadAllText(untouched) == "keep me", "user files changed");
                Check(Directory.GetFiles(targetFolder).Length == 1, "temporary install files left behind");
                File.WriteAllBytes(target, original);
                Reject(delegate { UpdateInstaller.ReplaceAndRestart(download, request, delegate { throw new IOException("restart failed"); }); }, "restart failure");
                Check(UpdateService.Hash(target) == originalHash && Directory.GetFiles(targetFolder).Length == 1, "rollback failed");
                using (FileStream locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
                    Reject(delegate { UpdateInstaller.ReplaceAndRestart(download, request, delegate { }); }, "locked target");
                Check(UpdateService.Hash(target) == originalHash, "locked target changed");
                request.OldHash = new string('0', 64);
                Reject(delegate { UpdateInstaller.ReplaceAndRestart(download, request, delegate { }); }, "concurrently modified target");
                request.OldHash = originalHash;
                results.Add("PASS: atomic executable-only replacement, restart arguments, unchanged settings/support files, rollback on restart failure, locked and changed targets.");

                // Run the real helper against tiny test programs; never launch or close the user's pet.
                string helperDownload = Path.Combine(Path.GetTempPath(), "Mochi-update-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(helperDownload); downloads.Add(helperDownload);
                string integrationFolder = Path.Combine(root, "helper integration");
                string oldApp = Fixture(integrationFolder, new Version(0, 9, 0, 0), true, false);
                string futureFolder = Path.Combine(root, "future");
                string future = Fixture(futureFolder, new Version(current.Major + 1, 0, 0, 0), false, true);
                File.Copy(future, Path.Combine(helperDownload, "Mochi.download.exe"));
                File.Copy(self, Path.Combine(helperDownload, "Mochi.updater.exe"));
                using (Process parent = Process.Start(new ProcessStartInfo(oldApp) { UseShellExecute = false, CreateNoWindow = true })) {
                    InstallRequest integration = new InstallRequest {
                        Target = oldApp, OldHash = UpdateService.Hash(oldApp), ParentId = parent.Id,
                        ParentStarted = parent.StartTime.ToUniversalTime().Ticks,
                        Update = new UpdateInfo { Version = new Version(current.Major + 1, 0, 0, 0), Size = new FileInfo(future).Length, Sha256 = UpdateService.Hash(future) }
                    };
                    integration.Save(helperDownload);
                    using (Process helper = Process.Start(new ProcessStartInfo(Path.Combine(helperDownload, "Mochi.updater.exe"), "--apply-update " + UpdateInstaller.Quote(helperDownload)) {
                        UseShellExecute = false, CreateNoWindow = true
                    })) {
                        if (!helper.WaitForExit(20000)) { helper.Kill(); throw new Exception("Update helper did not finish."); }
                        Check(helper.ExitCode == 0, "separate updater helper failed");
                    }
                    Check(parent.HasExited && UpdateService.Hash(oldApp) == UpdateService.Hash(future), "helper did not wait and replace");
                }
                for (int i = 0; i < 40 && !File.Exists(Path.Combine(futureFolder, "restarted.txt")); i++) Thread.Sleep(50);
                Check(File.Exists(Path.Combine(futureFolder, "restarted.txt")), "helper did not restart the new executable");
                results.Add("PASS: real helper waits for old process, replaces Mochi.exe, and launches the new executable from paths containing spaces.");
                VerifyDiagnostics(root, self, update);
                results.Add("PASS: exact download, validation, preparation and launch failure stages; native error codes; persistent original-folder report and fallback; cancellation stays silent; blocked helper leaves app and settings intact.");
                VerifyFrameworkCompatibility(Assembly.GetExecutingAssembly());
                results.Add("PASS: emitted .NET Framework-compatible TrimEnd(char[]) references; valid temporary updater directories and trailing separators; malformed and nested directories rejected.");
                Companion.VerifyUpdateMenu();
                results.Add("PASS: shared pet/tray menu contains Check for updates and Info; displayed version comes from the running assembly.");
                File.WriteAllLines(reportPath, results.ToArray());
                return 0;
            } catch (Exception error) {
                results.Add("FAIL: " + error);
                File.WriteAllLines(reportPath, results.ToArray());
                return 1;
            } finally {
                foreach (string directory in downloads) UpdateInstaller.Cleanup(directory);
            }
        }
    }
}
