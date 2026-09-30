using System;
using System.Collections.Generic;
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
