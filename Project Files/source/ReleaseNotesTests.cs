using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MochiDesktop {
    static class ReleaseNotesTests {
        internal static void Check(bool condition,string message){if(!condition)throw new Exception("Release notes: "+message);}
        public static int Run(string path){
            try {
                Version current=AppVersion.Current,older=new Version(1,1,8,0),newer=new Version(9,0,0,0);
                Check(ReleaseCatalog.Current.Version==current && ReleaseCatalog.Entries[0].Version==current,"current build has no matching release notes");
                foreach(string value in new[]{"1.0.0.0","1.1.0.0","1.1.1.0","1.1.2.0","1.1.3.0"}){
                    ReleaseEntry found=Array.Find(ReleaseCatalog.Entries,delegate(ReleaseEntry e){return e.Version==Version.Parse(value);});
                    Check(found!=null,"recovered release missing: "+value);
                    if(value!="1.0.0.0")Check(found.Title.Contains("preview"),"unpublished build must be labelled as a preview");
                }
                ReleaseEntry early=ReleaseCatalog.Entries[ReleaseCatalog.Entries.Length-1];
                Check(early.Version==null && early.ToString().StartsWith("Early days") && early.Body.Contains("September 2026"),"unversioned origins missing or given a made-up version");
                foreach(string invalid in new[]{"## Early days - Origins\n- Born\n## 1.0.0 - Later\n- Update","## Early days - Origins\n- Born\n## Early days - Again\n- Born","## 1.0.0 - First\n- Hello\n## 1.0.0 - Duplicate\n- Hello"}){
                    bool rejected=false;try{ReleaseCatalog.Parse(invalid);}catch(InvalidDataException){rejected=true;}
                    Check(rejected,"malformed historical ordering was accepted");
                }
                Check(!ReleaseCatalog.ShouldAnnounce("",current,false,false),"fresh install should start quietly");
                Check(ReleaseCatalog.ShouldAnnounce("",current,true,false),"legacy installation should get its first update welcome");
                Check(ReleaseCatalog.ShouldAnnounce("",current,false,true),"updater restart should get a welcome without existing preferences");
                Check(ReleaseCatalog.ShouldAnnounce(older.ToString(),current,true,false),"manual upgrade was missed");
                Check(!ReleaseCatalog.ShouldAnnounce(current.ToString(),current,true,true),"seen release announced twice");
                Check(!ReleaseCatalog.ShouldAnnounce(newer.ToString(),current,true,false),"downgrade announced as an update");
                Check(ReleaseCatalog.ShouldAnnounce("invalid",current,true,false),"legacy/malformed marker recovery");
                string fixture=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"release-preferences.xml");
                File.WriteAllText(fixture,"<Mochi><Size>224</Size><Roam>false</Roam><PlayfulMode>true</PlayfulMode><NextFeed>2</NextFeed></Mochi>");
                Preferences prefs=Preferences.Load(fixture);
                Check(prefs.LoadedFromFile && prefs.LastReleaseNotesVersion=="","legacy preferences migration");
                prefs.LastReleaseNotesVersion=current.ToString();prefs.Save(fixture);
                Preferences restored=Preferences.Load(fixture);
                Check(restored.LastReleaseNotesVersion==current.ToString() && restored.Size==224 && !restored.Roam && restored.PlayfulMode && restored.NextFeed==2,"release marker did not survive or changed preferences");
                Check(!ReleaseCatalog.ShouldAnnounce(restored.LastReleaseNotesVersion,current,true,false),"restart repeats the welcome");
                Companion.VerifyReleaseWelcome();
                using(ReleaseNotesDialog dialog=new ReleaseNotesDialog()){
                    ComboBox picker=(ComboBox)dialog.Controls.Find("ReleasePicker",true)[0];
                    RichTextBox details=(RichTextBox)dialog.Controls.Find("ReleaseDetails",true)[0];
                    Check(details.ReadOnly && picker.Items.Count==ReleaseCatalog.Entries.Length,"history reader missing");
                    foreach(ReleaseEntry entry in ReleaseCatalog.Entries){picker.SelectedItem=entry;Check(details.Text.Contains(entry.Highlights[0]) && !details.Text.Contains("### "),"selected release content missing or unformatted");}
                }
                using(SettingsDialog settings=new SettingsDialog(restored))Check(settings.Controls.Find("WhatsNew",true).Length==1,"Settings entry missing");
                File.WriteAllText(path,"PASS: embedded current release and ordered history; fresh/legacy installs, updater restarts, manual upgrades, repeated launches and downgrades; persisted release marker preserves preferences.\r\nPASS: deferred welcome respects controls and ongoing actions; no lost pending notice, no timer resets, no repeated welcome; fresh install records a quiet baseline.\r\nPASS: offline release selection and readable formatted content; dedicated Settings entry.\r\n");
                return 0;
            }catch(Exception error){File.WriteAllText(path,"FAIL: "+error);return 1;}
        }
    }

    sealed partial class Companion {
        internal static void VerifyReleaseWelcome(){
            using(Companion pet=new Companion(true)){
                pet.prefs.LastReleaseNotesVersion="1.1.8.0";pet.PrepareReleaseWelcome();
                double idle=pet.nextIdleActivity,prank=pet.nextPlayful,swim=pet.nextSwim;
                ReleaseNotesTests.Check(pet.releaseWelcomePending && !pet.CanShowReleaseWelcome(),"startup delay missing");
                pet.simulatedTime=2;
                ReleaseNotesTests.Check(pet.CanShowReleaseWelcome(),"eligible welcome not ready");
                pet.modal=true;pet.PumpReleaseWelcome();pet.modal=false;
                pet.down=true;pet.PumpReleaseWelcome();pet.down=false;
                pet.choosingDestination=true;pet.PumpReleaseWelcome();pet.choosingDestination=false;
                pet.swimming=true;pet.PumpReleaseWelcome();pet.swimming=false;
                ReleaseNotesTests.Check(pet.releaseWelcomePending && pet.releaseWelcome==null,"busy user lost or received a welcome");
                ReleaseNotesTests.Check(pet.nextIdleActivity==idle && pet.nextPlayful==prank && pet.nextSwim==swim,"welcome changed activity timers");
                pet.RememberReleaseNotes();pet.PrepareReleaseWelcome();
                ReleaseNotesTests.Check(!pet.releaseWelcomePending,"seen release repeated");
            }
            using(Companion pet=new Companion(true)){
                pet.PrepareReleaseWelcome();
                ReleaseNotesTests.Check(!pet.releaseWelcomePending && pet.prefs.LastReleaseNotesVersion==AppVersion.Current.ToString(),"new install did not set a quiet baseline");
            }
        }
    }
}
