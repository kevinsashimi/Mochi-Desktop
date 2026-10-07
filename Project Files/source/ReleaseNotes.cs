using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MochiDesktop {
    sealed class ReleaseEntry {
        public Version Version;
        public string Title, Body;
        public override string ToString(){return (Version==null?"Early days":AppVersion.Display(Version))+"  ·  "+Title;}
        public string[] Highlights {
            get {
                List<string> lines=new List<string>();
                foreach(string line in Body.Split('\n'))if(line.StartsWith("- ")){lines.Add(line.Substring(2));if(lines.Count==2)break;}
                return lines.ToArray();
            }
        }
    }

    static class ReleaseCatalog {
        // The repository changelog is embedded at build time: one source, no network requests.
        public static readonly ReleaseEntry[] Entries=Load();
        static ReleaseEntry[] Load(){
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mochi.ReleaseNotes"))
            using(StreamReader reader=new StreamReader(stream,Encoding.UTF8))return Parse(reader.ReadToEnd());
        }
        internal static ReleaseEntry[] Parse(string markdown){
            List<ReleaseEntry> entries=new List<ReleaseEntry>();ReleaseEntry entry=null;
            foreach(string raw in markdown.Replace("\r","").Split('\n')){
                Match heading=Regex.Match(raw,@"^## (\d+\.\d+\.\d+|Early days) - (.+)$");
                if(heading.Success){
                    Version version=heading.Groups[1].Value=="Early days"?null:Version.Parse(heading.Groups[1].Value+".0");
                    if(entries.Count>0 && (entries[entries.Count-1].Version==null || (version!=null && version>=entries[entries.Count-1].Version)))throw new InvalidDataException("Release notes must list distinct versions newest first, followed by early history.");
                    entry=new ReleaseEntry{Version=version,Title=heading.Groups[2].Value,Body=""};entries.Add(entry);
                }else if(entry!=null)entry.Body+=raw+"\n";
            }
            foreach(ReleaseEntry item in entries){item.Body=item.Body.Trim();if(item.Highlights.Length==0)throw new InvalidDataException("Release notes need a highlight.");}
            if(entries.Count==0)throw new InvalidDataException("Release notes are empty.");
            return entries.ToArray();
        }
        public static ReleaseEntry Current {
            get {foreach(ReleaseEntry entry in Entries)if(entry.Version==AppVersion.Current)return entry;throw new InvalidDataException("Release notes are missing for this build.");}
        }
        public static bool ShouldAnnounce(string seen,Version current,bool existingSettings,bool updateRestart){
            Version previous;
            if(Version.TryParse(seen,out previous))return current>previous;
            return existingSettings || updateRestart;
        }
    }

    static class ReleaseStyle {
        public static readonly Color Ocean=Color.FromArgb(27,66,91),Muted=Color.FromArgb(80,103,117),Paper=Color.FromArgb(245,250,253);
        public static Label Label(string text,float size,Color color){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size),ForeColor=color,Margin=new Padding(0,0,0,10)};}
        public static Button Button(string text,string name){return new Button{Text=text,Name=name,AutoSize=true,MinimumSize=new Size(114,34),Margin=new Padding(8,0,0,0),UseVisualStyleBackColor=true};}
        public static void Fit(Form form,Rectangle area){
            form.MinimumSize=new Size(Math.Min(form.MinimumSize.Width,area.Width),Math.Min(form.MinimumSize.Height,area.Height));
            form.Size=new Size(Math.Min(form.Width,area.Width),Math.Min(form.Height,area.Height));
            form.Location=Motion.Clamp(form.Location,form.Size,area);
        }
        public static void Center(Form form,Rectangle area){
            Fit(form,area);
            form.Location=new Point(area.Left+(area.Width-form.Width)/2,area.Top+(area.Height-form.Height)/2);
        }
    }

    sealed class ReleaseNotesDialog : Form {
        readonly RichTextBox details;
        readonly Font sectionFont;
        public ReleaseNotesDialog(){
            SuspendLayout();
            Text="What's New · Mochi";Name="ReleaseNotesDialog";StartPosition=FormStartPosition.CenterParent;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(620,510);MinimumSize=new Size(460,350);MaximizeBox=false;MinimizeBox=false;
            BackColor=ReleaseStyle.Paper;Font=new Font("Segoe UI",10);sectionFont=new Font(Font,FontStyle.Bold);
            TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(24)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<5;i++)layout.RowStyles.Add(new RowStyle(i==3?SizeType.Percent:SizeType.AutoSize,i==3?100:0));
            Label title=ReleaseStyle.Label("Small updates. Happy fins.",20,ReleaseStyle.Ocean);
            Label subtitle=ReleaseStyle.Label("You're swimming with Mochi "+AppVersion.Text+". Explore a release below.",10,ReleaseStyle.Muted);
            title.Dock=subtitle.Dock=DockStyle.Fill;
            ComboBox releases=new ComboBox{Name="ReleasePicker",AccessibleName="Choose a release",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,Margin=new Padding(0,4,0,16)};
            releases.Items.AddRange(ReleaseCatalog.Entries);
            details=new RichTextBox{Name="ReleaseDetails",AccessibleName="Release notes",ReadOnly=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,
                BackColor=ReleaseStyle.Paper,ForeColor=ReleaseStyle.Ocean,Font=Font,DetectUrls=false,ScrollBars=RichTextBoxScrollBars.Vertical,Margin=Padding.Empty};
            releases.SelectedIndexChanged+=delegate{ShowEntry((ReleaseEntry)releases.SelectedItem);};
            FlowLayoutPanel footer=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill,Margin=new Padding(0,16,0,0)};
            Button close=ReleaseStyle.Button("Back to swimming","CloseReleaseNotes");close.DialogResult=DialogResult.Cancel;footer.Controls.Add(close);
            layout.Controls.Add(title,0,0);layout.Controls.Add(subtitle,0,1);layout.Controls.Add(releases,0,2);layout.Controls.Add(details,0,3);layout.Controls.Add(footer,0,4);
            Controls.Add(layout);CancelButton=close;AcceptButton=close;
            releases.SelectedItem=ReleaseCatalog.Current;
            Shown+=delegate{ReleaseStyle.Fit(this,Screen.FromControl(this).WorkingArea);};
            ResumeLayout(true);
        }
        void ShowEntry(ReleaseEntry entry){
            details.Clear();
            foreach(string line in entry.Body.Split('\n')){
                bool heading=line.StartsWith("### ");
                details.SelectionFont=heading?sectionFont:Font;
                details.SelectionColor=heading?ReleaseStyle.Ocean:ReleaseStyle.Muted;
                details.AppendText((heading?line.Substring(4):line.StartsWith("- ")?"• "+line.Substring(2):line)+"\n");
            }
            details.Select(0,0);details.ScrollToCaret();
        }
        protected override void Dispose(bool disposing){if(disposing)sectionFont.Dispose();base.Dispose(disposing);}
    }

    sealed class ReleaseWelcome : Form {
        readonly Bitmap portrait;
        protected override bool ShowWithoutActivation {get{return true;}}
        public ReleaseWelcome(ReleaseEntry release,Bitmap sprite,Action readMore){
            SuspendLayout();
            Text="Mochi's little update";Name="ReleaseWelcome";ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
            FormBorderStyle=FormBorderStyle.FixedToolWindow;MaximizeBox=false;MinimizeBox=false;TopMost=false;
            BackColor=ReleaseStyle.Paper;Font=new Font("Segoe UI",10);
            TableLayoutPanel layout=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,Padding=new Padding(20)};
            Label title=ReleaseStyle.Label("Fresh out of the tide!",18,ReleaseStyle.Ocean);title.MaximumSize=new Size(290,0);
            Label version=ReleaseStyle.Label("Mochi "+AppVersion.Display(release.Version)+" is here.",10,ReleaseStyle.Muted);
            portrait=new Bitmap(sprite);PictureBox picture=new PictureBox{Image=portrait,SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(72,78),Margin=new Padding(12,0,0,8),TabStop=false};
            layout.Controls.Add(title,0,0);layout.Controls.Add(version,0,1);layout.Controls.Add(picture,1,0);layout.SetRowSpan(picture,2);
            int row=2;
            foreach(string text in release.Highlights){Label highlight=ReleaseStyle.Label("• "+text,10,ReleaseStyle.Ocean);highlight.MaximumSize=new Size(370,0);layout.Controls.Add(highlight,0,row++);layout.SetColumnSpan(highlight,2);}
            Label hint=ReleaseStyle.Label("Missed a detail? Settings → What's New keeps it all.",9,ReleaseStyle.Muted);hint.MaximumSize=new Size(370,0);layout.Controls.Add(hint,0,row++);layout.SetColumnSpan(hint,2);
            FlowLayoutPanel buttons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Anchor=AnchorStyles.Right,Margin=new Padding(0,8,0,0)};
            Button dismiss=ReleaseStyle.Button("Keep swimming","DismissReleaseWelcome"),more=ReleaseStyle.Button("What's New","ReadReleaseNotes");
            dismiss.Click+=delegate{Close();};more.Click+=delegate{Close();readMore();};
            buttons.Controls.AddRange(new Control[]{dismiss,more});layout.Controls.Add(buttons,0,row);layout.SetColumnSpan(buttons,2);
            Controls.Add(layout);CancelButton=dismiss;AcceptButton=more;
            ResumeLayout(true);
        }
        protected override void Dispose(bool disposing){if(disposing)portrait.Dispose();base.Dispose(disposing);}
    }

    sealed partial class Companion {
        bool releaseWelcomePending;
        double releaseWelcomeAt;
        ReleaseWelcome releaseWelcome;
        void PrepareReleaseWelcome(){
            releaseWelcomePending=ReleaseCatalog.ShouldAnnounce(prefs.LastReleaseNotesVersion,AppVersion.Current,prefs.LoadedFromFile,Program.RestartedAfterUpdate);
            releaseWelcomeAt=Now+1.2;
            Version seen;
            if(!releaseWelcomePending && !Version.TryParse(prefs.LastReleaseNotesVersion,out seen))RememberReleaseNotes();
        }
        void RememberReleaseNotes(){prefs.LastReleaseNotesVersion=AppVersion.Current.ToString();Save();}
        bool CanShowReleaseWelcome(){return releaseWelcomePending && Now>=releaseWelcomeAt && releaseWelcome==null && !closing && !modal && !menu.Visible && !down && !choosingDestination && !swimming && feeding==null && reaction==null && playful==null;}
        void PumpReleaseWelcome(){
            if(!CanShowReleaseWelcome())return;
            ReleaseWelcome card=new ReleaseWelcome(ReleaseCatalog.Current,atlas.Frames[0,0],ShowReleaseNotes);
            releaseWelcome=card;
            card.FormClosed+=delegate{if(releaseWelcome==card)releaseWelcome=null;};
            Rectangle area=Screen.FromRectangle(Bounds).WorkingArea;
            card.Shown+=delegate{ReleaseStyle.Center(card,area);};
            card.Show();releaseWelcomePending=false;RememberReleaseNotes();
        }
        void CloseReleaseWelcome(){if(releaseWelcome!=null)releaseWelcome.Close();}
        void ShowReleaseNotes(){
            BeginUpdateDialog();
            try{using(ReleaseNotesDialog dialog=new ReleaseNotesDialog())dialog.ShowDialog();}
            finally{EndUpdateDialog();}
        }
    }
}
