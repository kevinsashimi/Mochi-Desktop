using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
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
        static byte[] ProductionSignatureVector(){
            const string hash="abababababababababababababababababababababababababababababababab";
            const string signature="U/ok3dIYyFKayWV9plP7GhOmithClPtC1QFv1bMqlzJXX2Li783WUf/U1YPlg2M51zk8IpNfkEyynoucIbHFvXtB3R/9mULKTTLefktxj9S3HhZZOuijkh4+MpnyAFjOXoVDfMqr/sVY9GgQ/Xlcw1U2nq9eeaIPTpghSGD3Z8nkrPWYmBK3ahqZhnEZqOOnRTEI1lyBk/uSaILGk09M0rRCewL+ztwFpXVPLMwkQ1olMWibJe9q7/Z6HZGRXbVOSfQ/et2F2SR6MPXP/mPgF8+LQczmiiAk9MU2uUHrrR6emg5i+6CusxcUaXv9LkJUY+NkzzQCcVyWqRJ36ejQtnww1yHfyw8HXrbQClyk9mPonnPynOUmSvUZFdZsseSPYnoiDYSrwxTmmbbKIbXb9segVz0633VuekgcfqOSKeTRPOyZRG7InJIYhGxyNMPqeX6FcSEnkVw9BnVpP8Ouvjs4QYtEFIL0orZ4wjGZmqv8vQ2oH+b33hWrSqOViSjnbYkVK/5IsArrH3gWmxbKXXHa/yxP24fhhLU9oPtLelMq4Ri4qUbDXLA0AeqFF/rYnCSTAXAzHv8K8YIwjZ5MJojwRciwW8uA1ZbJnpO46A1NddR96zrZFdK82rTLJxwXbYT6H5N4Ko+p/WEjR79Dg7yneHMYp6hmgOXyWs5aHW0=";
            return Encoding.UTF8.GetBytes(new XElement("MochiUpdate",new XElement("Version","9.8.7.6"),
                new XElement("File","Mochi.exe"),new XElement("Size","123456"),new XElement("Sha256",hash),new XElement("Signature",signature)).ToString());
        }
        static byte[] Manifest(Version version, string hash, long size) {
#if MOCHI_TESTS
            RSAParameters key = new RSAParameters {
                Modulus = Convert.FromBase64String("y9Vdm+trR3h+PFk7keNrfC5TVkTW3RHFM7E+IZGD1NAZMiKh5Bcpj28rCWyo2nrpnNFrECAT7JRShtk0CtEPrwH/QDO5cZdLDgNpD7++nr+Uf2o8d/husHubz4pU3oAacjs5799qNVwya432FzbttVvApvmXQ9kjkcCyTt4S4TBhw0S5yZNfa7xgQhQlwHiECAoZ559mngQIyXBoOkTfs47oD1uNy+Aew4JBcfGNPZW70vKNhiUvGbkwdsKKAX/gIPo+141z4XzV0qDm8LwhPoa5wBBrp1qY3f/7zTLhNiqkj3wlWNIx//wIAZuAj0urgipjMyi5e6La3I2ZH+BjmQ=="),
                Exponent = Convert.FromBase64String("AQAB"),
                P = Convert.FromBase64String("9ey+GAssZ5Q3aJR3ulwMmlleMn56P8xM0fjClspeteF67tJV/dXM1tEiaKqJuANzXEz/0RX91hSV3+K0RcptWYTa1WmvJf+fGyrDK9QnMqkfQ7PYTwz/1c9KRbY/BHDEEJoBa/Rgk2KNYA+28CDAdHAeo4r/7ZTy+gXF13a+rsE="),
                Q = Convert.FromBase64String("1C8rbrMlrxdE3dYAcp+hxQ1x7oX8zNJNREx6kIbcxhzwpWCSmDQCQa64IC8AvAIghpR30FOlyh6kn5D0+AYxa9FGRJm2t8WBL6st19rE8LR9tq5TUpkjMtKrL3pu4BbiH/AXsQqtTuL312vhW0/4X14UU7PDsMo/3C0KZLvQwtk="),
                DP = Convert.FromBase64String("xdJwf4rNZ2yUva1gOiq9gJOO8HNJB3Th/q1qaj3Tvd7pMp9DyPJo2gxU73jvW3R7Ia5Wwew8yi64+OW2OcXB/JagKbffymM7q3DUmrcH6PqeSamrwLsmOOmMR1N4kavcK7aWmi50WVqXesRpVfQinAdsvgWcJZMBeRmXseC1NoE="),
                DQ = Convert.FromBase64String("l630jVjxSl4G89HkVgw5N0UqOcl8cuqvI3DMeP5/WfwcJwyHHtim+E8u/oq7KqsWlz4Q/FKfhIItSof0Jk0/DQT4B1ge3vYIKIxk5BZWjH9qEqpBlB02pUEl8HXERtr04MHgHcY1J9xmMY/D9GOReJMnUsKB1KJvzjRAZsxAKtk="),
                InverseQ = Convert.FromBase64String("FsCg53vl0NX+Afw+2eGSbV3cva07GKlXrLB2SAL9rwHwIFOvDktM5ALHvsSuQyM0IIe+RTkkEwA3LlJPnYfSLD2PciY7Pih5aQ2QXhybB3EQpEc1iq+WS0GLuKo50mhM7ds/FSgBjT/isPUzZIrTXaoVxLJl1SSTTNb0yRvCubQ="),
                D = Convert.FromBase64String("ApxSGXsBid3s1PpKGWddoy6nNdfGHjj56vxySKUH2JgNOI+JvgV/bGU+un1MKNcx6oaincdhZfJs36oEwP42OpY2lhSLg51N0mlABVn996PJw7uBOIKjrUo5/8Wcamie3hds2aoFftc2VJsR+7BiCUR6clEIS0/KxTU/+2Nb/8sUWWoBPHvKsh6oDhNC84dIwpFe4w7rXyp01xVed5Wdk653Bp/o1TEZYNCzPC123NkmNYqO31sX8NMO0pS2fXpGmPJAxuBWa3jSdWPNz/QZYvHvd+UEItGavPAgEv1fjJ8o6sA+1UJ+yGs45ryAB8sW7OjGds/8McgsAR4KJIZ4wQ==")
            };
            byte[] signature;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider()) {
                rsa.PersistKeyInCsp = false; rsa.ImportParameters(key);
                signature = rsa.SignData(UpdateInfo.Canonical(version, hash, size), CryptoConfig.MapNameToOID("SHA256"));
            }
            return Encoding.UTF8.GetBytes(new XElement("MochiUpdate", new XElement("Version", version),
                new XElement("File", "Mochi.exe"), new XElement("Size", size), new XElement("Sha256", hash),
                new XElement("Signature", Convert.ToBase64String(signature))).ToString());
#else
            throw new InvalidOperationException("Update fixtures require a MOCHI_TESTS build.");
#endif
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
                string diagnosticFolder = Path.Combine(root, "local diagnostics");
                string message = UpdateDiagnostics.Report(failure, "Preparing", target, new[] { diagnosticFolder });
                string report = File.ReadAllText(Path.Combine(diagnosticFolder, "update-error.txt"));
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
            string previousElevation = Environment.GetEnvironmentVariable("MOCHI_TEST_ELEVATION");
            Environment.SetEnvironmentVariable("MOCHI_TEST_ELEVATION", "0");
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
                Check(UpdateInfo.Parse(ProductionSignatureVector(),commit).Version==new Version(9,8,7,6),
                    "production pinned-key signature vector was rejected");
                byte[] signed = Manifest(current, hash, size);
                XElement tampered = XElement.Parse(Encoding.UTF8.GetString(signed));
                tampered.Element("Size").Value = (size + 1).ToString();
                Reject(delegate { UpdateInfo.Parse(Encoding.UTF8.GetBytes(tampered.ToString()), commit); }, "tampered signed metadata");
                tampered = XElement.Parse(Encoding.UTF8.GetString(signed)); tampered.Element("Signature").Remove();
                Reject(delegate { UpdateInfo.Parse(Encoding.UTF8.GetBytes(tampered.ToString()), commit); }, "unsigned metadata");
                tampered = XElement.Parse(Encoding.UTF8.GetString(signed)); tampered.Element("Signature").Value = "not-base64";
                Reject(delegate { UpdateInfo.Parse(Encoding.UTF8.GetBytes(tampered.ToString()), commit); }, "malformed signature");
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
                results.Add("PASS: pinned publisher signatures; tampered, missing and malformed signatures rejected; newer/same/older versions; numeric ordering; malformed/oversize metadata; offline and cancellation; no executable download during checks.");

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
                string handoffFolder = Path.Combine(root, "signed handoff"); Directory.CreateDirectory(handoffFolder);
                request.ParentId = Process.GetCurrentProcess().Id; request.ParentStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
                request.Save(handoffFolder);
                Check(InstallRequest.Load(handoffFolder).Update.Signature == update.Signature, "signed handoff did not round trip");
                XElement alteredHandoff = XElement.Load(Path.Combine(handoffFolder, "install.xml"));
                alteredHandoff.Element("Size").Value = (update.Size + 1).ToString(); alteredHandoff.Save(Path.Combine(handoffFolder, "install.xml"));
                Reject(delegate { InstallRequest.Load(handoffFolder); }, "tampered helper handoff");
                bool elevatedLaunch = false; Exception elevatedHelper = null;
                Environment.SetEnvironmentVariable("MOCHI_TEST_ELEVATION", "1");
                FailureAt(delegate { UpdateInstaller.PrepareAndStart(download, update, target, delegate { elevatedLaunch = true; }); }, "Checking updater privileges");
                Check(!elevatedLaunch, "elevated preparation launched the temporary helper");
                Check(UpdateInstaller.RunHelper(download, delegate(Exception error,string ignored) { elevatedHelper = error; }) == 1 &&
                    UpdateDiagnostics.Stage(elevatedHelper, "") == "Checking updater privileges", "elevated helper execution was not refused");
                Environment.SetEnvironmentVariable("MOCHI_TEST_ELEVATION", "0");
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
                        Update = UpdateInfo.Parse(Manifest(new Version(current.Major + 1, 0, 0, 0), UpdateService.Hash(future), new FileInfo(future).Length), commit)
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
                results.Add("PASS: exact download, validation, preparation and launch failure stages; native error codes; LocalAppData-style diagnostic report and fallback; cancellation stays silent; blocked helper leaves app and settings intact.");
                VerifyFrameworkCompatibility(Assembly.GetExecutingAssembly());
                results.Add("PASS: emitted .NET Framework-compatible TrimEnd(char[]) references; valid temporary updater directories and trailing separators; malformed and nested directories rejected.");
                Companion.VerifyUpdateMenu();
                results.Add("PASS: shared pet/tray menu contains Check for updates and Info; displayed version comes from the running assembly.");
                if (AppVersion.ManualDownloadOnly)
                    results.Add("PASS: manual-download edition skips startup update checks, queues a user-requested download-page prompt without starting the updater, and clears the notice on cancellation.");
                File.WriteAllLines(reportPath, results.ToArray());
                return 0;
            } catch (Exception error) {
                results.Add("FAIL: " + error);
                File.WriteAllLines(reportPath, results.ToArray());
                return 1;
            } finally {
                foreach (string directory in downloads) UpdateInstaller.Cleanup(directory);
                Environment.SetEnvironmentVariable("MOCHI_TEST_ELEVATION", previousElevation);
            }
        }
    }
}
