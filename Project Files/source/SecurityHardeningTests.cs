#if MOCHI_TESTS
using System;
using System.IO;

namespace MochiDesktop {
    static class SecurityHardeningTests {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static bool LowerHex(string value){
            if(value==null || value.Length!=32)return false;
            foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;
            return true;
        }
        public static int Run(string reportPath){
            string root=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)),"security-fixtures-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try{
                string huge=Path.Combine(root,"huge.xml");
                File.WriteAllText(huge,"<Mochi><LastReleaseNotesVersion>"+new string('x',(int)Preferences.MaximumCharacters)+"</LastReleaseNotesVersion></Mochi>");
                Preferences rejected=Preferences.Load(huge);
                Check(!rejected.LoadedFromFile && rejected.Size==176 && rejected.Roam,"oversize settings were accepted or changed safe defaults");

                string dtd=Path.Combine(root,"dtd.xml");
                File.WriteAllText(dtd,"<!DOCTYPE Mochi [<!ENTITY value 'expanded'>]><Mochi><LastReleaseNotesVersion>&value;</LastReleaseNotesVersion></Mochi>");
                rejected=Preferences.Load(dtd);
                Check(!rejected.LoadedFromFile && rejected.LastReleaseNotesVersion=="","DTD/entity settings were accepted");

                string wrongRoot=Path.Combine(root,"wrong-root.xml");
                File.WriteAllText(wrongRoot,"<NotMochi><Size>224</Size><Roam>false</Roam></NotMochi>");
                rejected=Preferences.Load(wrongRoot);
                Check(!rejected.LoadedFromFile && rejected.Size==176 && rejected.Roam,"unexpected settings root was accepted");

                string normalized=Path.Combine(root,"normalized.xml");
                File.WriteAllText(normalized,"<Mochi><Size>999</Size><Frequency>99</Frequency><Roam>false</Roam><PlayfulMode>true</PlayfulMode></Mochi>");
                Preferences safe=Preferences.Load(normalized);
                Check(safe.LoadedFromFile && safe.Size==176 && safe.Frequency==2 && !safe.Roam && safe.PlayfulMode,"valid settings were not loaded and normalized");

                string settings=Path.Combine(root,"settings.xml"),fixedTemporary=settings+".tmp";
                File.WriteAllText(fixedTemporary,"sentinel");
                Preferences saved=new Preferences{Size=224,Frequency=0,Roam=false,PlayfulMode=true,StaticWallpaper=true,LastReleaseNotesVersion="1.2.13"};
                saved.Save(settings);
                Preferences roundTrip=Preferences.Load(settings);
                Check(roundTrip.LoadedFromFile && roundTrip.Size==224 && roundTrip.Frequency==0 && !roundTrip.Roam && roundTrip.PlayfulMode && roundTrip.StaticWallpaper && roundTrip.LastReleaseNotesVersion=="1.2.13","atomic settings round trip failed");
                Check(File.ReadAllText(fixedTemporary)=="sentinel","fixed predictable temporary settings file was reused");
                Check(Directory.GetFiles(root,".settings.xml.*.tmp").Length==0,"random settings temporary file was left behind");

                string blocked=Path.Combine(root,"blocked.xml");Directory.CreateDirectory(blocked);
                saved.Save(blocked);
                Check(Directory.GetFiles(root,".blocked.xml.*.tmp").Length==0,"failed settings save left a temporary file behind");

                string executable=Path.Combine(root,"Mochi.exe");
                string scope=Program.InstanceScope(executable,"S-1-5-21-test");
                Check(LowerHex(scope),"instance scope is not a bounded opaque identifier");
                Check(scope==Program.InstanceScope(Path.Combine(root,"mochi.EXE"),"S-1-5-21-test"),"instance scope changed with path casing");
                Check(scope!=Program.InstanceScope(Path.Combine(root,"copy","Mochi.exe"),"S-1-5-21-test"),"separate installs shared an instance scope");
                Check(scope!=Program.InstanceScope(executable,"S-1-5-21-other"),"separate users shared an instance scope");
                Check(scope.IndexOf("S-1-5",StringComparison.Ordinal)<0 && scope.IndexOf("Mochi",StringComparison.OrdinalIgnoreCase)<0,"instance scope exposed user or path data");

                File.WriteAllText(reportPath,
                    "PASS: settings XML has explicit size, DTD and root guards; malformed input retains safe defaults.\r\n"+
                    "PASS: settings save uses collision-resistant temporary names, cleans failures, preserves existing fixed-name files, and round-trips atomically.\r\n"+
                    "PASS: named-object scopes are deterministic, opaque, case-stable, and separated by user and installation path.\r\n");
                return 0;
            }catch(Exception error){File.WriteAllText(reportPath,"FAIL: "+error);return 1;}
            finally{try{Directory.Delete(root,true);}catch{}}
        }
    }
}
#endif
