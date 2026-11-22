using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;

namespace Aethereal.VoiceAccessLab
{
    // Single-window UI for two distinct, narrowly scoped capability profiles.
    // The original policies and NotepadBridge remain the execution boundaries.
    internal sealed class UnifiedForm : Form
    {
        private static readonly Color Background = Color.FromArgb(12, 18, 30);
        private static readonly Color Surface = Color.FromArgb(21, 31, 49);
        private static readonly Color TextInk = Color.FromArgb(232, 239, 250);
        private static readonly Color Quiet = Color.FromArgb(166, 185, 207);
        private static readonly Color Blue = Color.FromArgb(112, 185, 255);
        private static readonly Color Green = Color.FromArgb(126, 212, 167);
        private static readonly Color Orange = Color.FromArgb(255, 192, 122);
        private const int EmergencyHotkeyId = 0x4A13;
        private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4;
        private const int WmHotkey = 0x0312;

        [DllImport("user32.dll", SetLastError=true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private readonly string[] demoNames = {
            "Read a sample instruction", "Review a sample image",
            "Mark a demonstration item", "Explore local navigation",
            "Practice the emergency stop"
        };
        private readonly bool[] demoMarks = new bool[5];

        private TabControl tabs;
        private TabPage practicePage, notepadPage;
        private CheckBox practiceApproval, notepadApproval, readAloud, diagnosticOnly;
        private TextBox commandInput, audit;
        private ListBox practiceList;
        private Label feedback, microphoneStatus, targetStatus, bindStatus;
        private ProgressBar microphoneMeter;
        private Button voiceButton, bindButton;
        private SpeechRecognitionEngine speech;
        private SpeechSynthesizer speaker;
        private bool listening, closing, globalHotkeyRegistered;
        private bool initialized;
        private int voiceGeneration, bindCountdown, allowed, denied;
        private System.Windows.Forms.Timer captureTimer;
        private NotepadTarget target;

        internal UnifiedForm()
        {
            Text = "Aethereal Voice Access Lab 0.3.1 — Accessibility Prototype";
            ClientSize = new Size(1020, 720);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1036, 759);
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Font = new Font("Segoe UI", 10f);
            BackColor = Background;
            ForeColor = TextInk;
            KeyPreview = true;
            BuildUi();
            PopulatePractice();
            captureTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            captureTimer.Tick += CaptureTick;
            initialized = true;

            Shown += delegate
            {
                globalHotkeyRegistered = RegisterHotKey(Handle, EmergencyHotkeyId,
                    ModAlt | ModControl | ModShift, (uint)Keys.F12);
                Log(globalHotkeyRegistered
                    ? "Global stop available: Ctrl+Alt+Shift+F12."
                    : "Warning: global stop hotkey unavailable. Use ESC or the Stop button.");
                Log("Ready. Permission OFF. Microphone OFF. No external target bound.");
            };
            FormClosing += delegate
            {
                closing = true;
                StopListening();
                captureTimer.Stop();
                captureTimer.Dispose();
                if (speaker != null) { try { speaker.Dispose(); } catch { } speaker = null; }
                if (globalHotkeyRegistered)
                {
                    UnregisterHotKey(Handle, EmergencyHotkeyId);
                    globalHotkeyRegistered = false;
                }
            };
        }

        internal bool UiSmokeReady
        {
            get
            {
                return tabs != null && tabs.TabPages.Count == 2
                    && practiceApproval != null && !practiceApproval.Checked
                    && notepadApproval != null && !notepadApproval.Checked
                    && practiceList != null && practiceList.Items.Count == 5
                    && target == null && !listening
                    && speech == null && voiceButton != null
                    && audit != null && captureTimer != null && !captureTimer.Enabled;
            }
        }

        private Label Label(Control owner, string text, int x, int y, int width, int height,
            float size, Color color, bool bold=false)
        {
            Label label = new Label {
                Text=text, ForeColor=color, AutoEllipsis=true,
                Font=new Font("Segoe UI",size,bold ? FontStyle.Bold : FontStyle.Regular)
            };
            label.SetBounds(x,y,width,height);
            owner.Controls.Add(label);
            return label;
        }

        private Button Button(Control owner, string text, int x, int y, int w, int h,
            EventHandler action, bool prominent=false)
        {
            Button b = new Button {
                Text=text, BackColor=prominent ? Color.FromArgb(42,81,118) : Color.FromArgb(34,55,80),
                ForeColor=TextInk, FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(64, 107, 150);
            b.SetBounds(x,y,w,h);
            b.Click += action;
            owner.Controls.Add(b);
            return b;
        }

        private Panel Panel(Control owner, int x,int y,int w,int h)
        {
            Panel p = new Panel { BackColor=Surface };
            p.SetBounds(x,y,w,h);
            owner.Controls.Add(p);
            return p;
        }

        private void BuildUi()
        {
            Label(this,"AETHEREAL  /  VOICE ACCESS LAB",20,13,650,37,19,TextInk,true);
            Label(this,"v0.4.0-rc1  •  offline  •  local-only  •  development prototype",21,52,610,22,10,Quiet);
            voiceButton = Button(this,"Start voice",687,17,142,43,
                delegate { ToggleListening(); },true);
            Button(this,"STOP / REVOKE",838,17,162,43,
                delegate { EmergencyStop("Emergency stop button."); });
            microphoneStatus = Label(this,"MIC OFF  |  Standby",689,68,310,20,9,Quiet);
            microphoneMeter = new ProgressBar {
                Minimum=0, Maximum=100, Value=0, Style=ProgressBarStyle.Continuous
            };
            microphoneMeter.SetBounds(690,91,307,7);
            Controls.Add(microphoneMeter);

            tabs = new TabControl();
            tabs.SetBounds(17,109,987,389);
            tabs.Appearance=TabAppearance.Normal;
            tabs.Font=new Font("Segoe UI",11f,FontStyle.Bold);
            tabs.TabPages.Add(practicePage = new TabPage("  Practice  "));
            tabs.TabPages.Add(notepadPage = new TabPage("  Notepad pilot  "));
            foreach(TabPage pg in tabs.TabPages)
            {
                pg.BackColor=Background; pg.ForeColor=TextInk;
            }
            Controls.Add(tabs);
            BuildPracticeTab();
            BuildNotepadTab();
            tabs.SelectedIndexChanged += delegate
            {
                if(!initialized) return;
                StopListening();
                if(practiceApproval != null) practiceApproval.Checked=false;
                if(notepadApproval != null) notepadApproval.Checked=false;
                target=null;
                if(captureTimer != null) captureTimer.Stop();
                UpdateTarget();
                SetFeedback("Changed workspace. Re-authorize before using voice.",Orange);
                Log("MODE CHANGE: permissions and target reset; microphone off.");
            };

            Label(this,"ACTIVITY & DIAGNOSTICS",21,509,500,26,12,Blue,true);
            diagnosticOnly = new CheckBox {
                Text="Voice test only (no actions)", ForeColor=Quiet,
                AutoSize=true, Checked=false
            };
            diagnosticOnly.SetBounds(768,514,236,25);
            diagnosticOnly.CheckedChanged += delegate {
                if (listening) StopListening();
                Log("Voice diagnostics "+(diagnosticOnly.Checked?"enabled":"disabled")+".");
            };
            Controls.Add(diagnosticOnly);

            audit = new TextBox {
                Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(10,17,29),
                ForeColor=Color.FromArgb(184,204,226),
                Font=new Font("Consolas",9.3f), BorderStyle=BorderStyle.FixedSingle
            };
            audit.SetBounds(19,541,983,120);
            Controls.Add(audit);
            feedback = Label(this,"Select a workspace to begin.",20,671,815,25,10,Green,true);
            Label(this,"ESC or Ctrl+Alt+Shift+F12 stops voice",
                733,697,268,15,8,Orange);
        }

        private void BuildPracticeTab()
        {
            Panel left=Panel(practicePage,8,11,450,334);
            Panel right=Panel(practicePage,470,11,479,334);
            Label(left,"1  APPROVE PRACTICE",16,15,415,25,13,Blue,true);
            Label(left,"Commands affect only the five sample items in this window.",
                16,45,417,33,10,Quiet);
            practiceApproval = new CheckBox {
                Text="Allow commands in this practice workspace",
                ForeColor=TextInk, AutoSize=false
            };
            practiceApproval.SetBounds(17,85,417,34);
            practiceApproval.CheckedChanged += delegate {
                if(!practiceApproval.Checked)StopListening();
                Log("PRACTICE "+(practiceApproval.Checked?"APPROVED":"REVOKED")+".");
            };
            left.Controls.Add(practiceApproval);
            Label(left,"2  TRY A COMMAND",16,136,415,27,13,Blue,true);
            Label(left,"Access next  |  Access select  |  Access read",
                16,168,415,22,9,Quiet);
            commandInput = new TextBox {
                Text="access next", BackColor=Color.FromArgb(11,19,33),
                ForeColor=TextInk, BorderStyle=BorderStyle.FixedSingle
            };
            commandInput.SetBounds(16,204,290,32);
            commandInput.KeyDown += delegate(object sender, KeyEventArgs args) {
                if(args.KeyCode==Keys.Enter) {
                    args.SuppressKeyPress=true;
                    PerformPractice(commandInput.Text,false,1f);
                }
            };
            left.Controls.Add(commandInput);
            Button(left,"Run locally",318,201,116,38,
                delegate { PerformPractice(commandInput.Text,false,1f); });
            Label(left,"Offline voice: Access next / previous / select / read / help / stop",
                16,270,420,52,9,Quiet);

            Label(right,"SAMPLE WORKSPACE",16,15,442,25,13,Blue,true);
            practiceList = new ListBox {
                BackColor=Color.FromArgb(11,19,33),ForeColor=TextInk,
                BorderStyle=BorderStyle.FixedSingle,
                Font=new Font("Segoe UI",11.2f)
            };
            practiceList.SetBounds(17,53,444,178);
            right.Controls.Add(practiceList);
            Button(right,"Previous",17,243,104,39,
                delegate { PerformPractice("access previous",false,1f); });
            Button(right,"Next",130,243,95,39,
                delegate { PerformPractice("access next",false,1f); });
            Button(right,"Select",235,243,100,39,
                delegate { PerformPractice("access select",false,1f); });
            Button(right,"Read",344,243,117,39,
                delegate { PerformPractice("access read",false,1f); });
            readAloud = new CheckBox { Text="Read sample aloud (offline Windows voice)",
                ForeColor=Quiet,AutoSize=false };
            readAloud.SetBounds(17,292,443,26);
            right.Controls.Add(readAloud);
        }

        private void BuildNotepadTab()
        {
            Panel left=Panel(notepadPage,8,11,450,334);
            Panel right=Panel(notepadPage,470,11,479,334);
            Label(left,"1  PREPARE A SAFE TEST",16,15,419,26,13,Blue,true);
            Label(left,"A test file is created only in this development folder.",
                16,44,420,24,9,Quiet);
            Button(left,"Open the test document in Notepad",16,75,418,39,
                delegate { OpenSafeFile(); },true);

            Label(left,"2  BIND THE VISIBLE WINDOW",16,128,418,27,13,Blue,true);
            Label(left,"Click Bind and switch to the test Notepad tab before countdown ends.",
                16,156,417,42,9,Quiet);
            bindButton=Button(left,"Bind Notepad in 5 seconds",16,200,418,39,
                delegate { BeginBinding(); });
            targetStatus=Label(left,"TARGET: NOT BOUND",16,247,418,38,9,Orange,true);
            notepadApproval=new CheckBox {
                Text="I approve this TEST document for navigation only",
                ForeColor=TextInk,AutoSize=false
            };
            notepadApproval.SetBounds(16,288,419,30);
            notepadApproval.CheckedChanged += delegate {
                if(!notepadApproval.Checked)
                {
                    StopListening();
                    target=null; UpdateTarget();
                }
                Log("NOTEPAD "+(notepadApproval.Checked?"APPROVED":"REVOKED")+".");
            };
            left.Controls.Add(notepadApproval);

            Label(right,"SUPPORTED VOICE ACTIONS",16,15,442,27,13,Blue,true);
            Label(right,"Access line down / line up",16,62,444,25,11,TextInk);
            Label(right,"Access move left / move right",16,100,444,25,11,TextInk);
            Label(right,"Access line start / line end",16,138,444,25,11,TextInk);
            Label(right,"Access stop / Stop listening",16,176,444,25,11,TextInk);
            bindStatus=Label(right,
                "Notepad must be foreground and its text editor focused. "+
                "Unknown targets and other tabs are denied.",
                16,222,440,61,10,Quiet);
            Button(right,"Disconnect & revoke",16,294,442,35,
                delegate {
                    notepadApproval.Checked=false;
                    target=null; UpdateTarget();
                    StopListening(); Log("Notepad disconnected and permission revoked.");
                });
        }

        private void PopulatePractice()
        {
            int old = practiceList.SelectedIndex;
            practiceList.BeginUpdate();
            practiceList.Items.Clear();
            for(int i=0;i<demoNames.Length;i++)
                practiceList.Items.Add((demoMarks[i]?"[x] ":"[ ] ")+demoNames[i]);
            practiceList.EndUpdate();
            practiceList.SelectedIndex=old>=0 && old<demoNames.Length ? old : 0;
        }

        private void Log(string message)
        {
            if(audit == null) return;
            audit.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);
        }

        private void SetFeedback(string text,Color color)
        {
            if(feedback!=null) { feedback.Text=text; feedback.ForeColor=color; }
        }

        private void PerformPractice(string phrase,bool fromVoice,float confidence)
        {
            DemoAction action=ActionPolicy.Parse(phrase);
            ActionContext context=new ActionContext {
                Action=action,UserApproved=practiceApproval.Checked,
                OwnDemoWindow=true,WindowForeground=ContainsFocus,
                HasSelection=practiceList.SelectedIndex>=0,
                IsVoice=fromVoice,MicrophoneListening=listening,Confidence=confidence
            };
            PolicyDecision policy=ActionPolicy.Evaluate(context);
            if(!policy.Allowed) {
                denied++;
                SetFeedback("DENIED: "+policy.Explanation,Orange);
                Log("DENY practice "+phrase+" | "+policy.Explanation);
                return;
            }
            if(action==DemoAction.Stop) {
                EmergencyStop("Practice stop command.");
                return;
            }
            if(diagnosticOnly.Checked && fromVoice) {
                Log("VOICE TEST ONLY: "+phrase+" ("+(confidence*100).ToString("0")+"%).");
                return;
            }
            if(action==DemoAction.Next||action==DemoAction.Previous) {
                int delta=action==DemoAction.Next?1:-1;
                practiceList.SelectedIndex=(practiceList.SelectedIndex+delta+demoNames.Length)%demoNames.Length;
            } else if(action==DemoAction.Select) {
                int index=practiceList.SelectedIndex;
                demoMarks[index]=!demoMarks[index];
                PopulatePractice();
            } else if(action==DemoAction.Read && readAloud.Checked) {
                Speak("Practice item: "+demoNames[practiceList.SelectedIndex]);
            }
            allowed++;
            SetFeedback("Practice: "+action.ToString()+" applied to the local sample.",Green);
            Log("ALLOW practice "+phrase+" | demo changed / locally handled.");
        }

        private void Speak(string value)
        {
            try {
                if(speaker==null) {
                    speaker=new SpeechSynthesizer();
                    speaker.SetOutputToDefaultAudioDevice();
                }
                speaker.SpeakAsyncCancelAll();
                speaker.SpeakAsync(value);
            } catch(Exception e) { Log("Local speech output unavailable: "+e.Message); }
        }

        private string SafeFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "TEST_ONLY",NotepadBridge.SampleFileName); }
        }

        private void OpenSafeFile()
        {
            try {
                string file=SafeFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                if(!File.Exists(file))
                    File.WriteAllText(file,
                        "AETHEREAL VOICE ACCESS LAB - TEST ONLY\r\n"+
                        "Line two. Move down with Access line down.\r\n"+
                        "Line three. Move up with Access line up.\r\n"+
                        "Line four. Move the caret left and right.\r\n"+
                        "Line five. Home and End navigate within this line.\r\n"+
                        "This prototype cannot type, save, or click.\r\n"+
                        "Say Access stop to end listening.\r\n");
                Process.Start(new ProcessStartInfo {
                    FileName="notepad.exe",Arguments="\""+file+"\"",UseShellExecute=true
                });
                Log("Opened the explicitly named local SAFE TEST file.");
                SetFeedback("Test document opened. Bind its Notepad window next.",Green);
            } catch(Exception error) {
                SetFeedback("Could not open sample: "+error.Message,Orange);
                Log("OPEN FAILED: "+error.Message);
            }
        }

        private void BeginBinding()
        {
            if(tabs.SelectedTab!=notepadPage)return;
            StopListening();
            notepadApproval.Checked=false;
            target=null;
            bindCountdown=5;
            bindButton.Enabled=false;
            captureTimer.Start();
            targetStatus.Text="SWITCH TO NOTEPAD: 5 seconds";
            Log("Binding countdown started. Go to the named test document.");
        }

        private void CaptureTick(object sender,EventArgs args)
        {
            bindCountdown--;
            if(bindCountdown>0) {
                targetStatus.Text="SWITCH TO NOTEPAD: "+bindCountdown+" seconds";
                return;
            }
            captureTimer.Stop();
            bindButton.Enabled=true;
            NotepadTarget candidate;
            string message;
            if(NotepadBridge.TryCaptureForeground(out candidate,out message)) {
                target=candidate;
                UpdateTarget();
                Log("BOUND Notepad PID "+target.ProcessId+": "+target.WindowTitle);
                SetFeedback("Bound test file. Return and enable authorization.",Green);
            } else {
                target=null;UpdateTarget();
                Log("BIND DENIED: "+message);
                SetFeedback(message,Orange);
            }
        }

        private void UpdateTarget()
        {
            if(targetStatus!=null) {
                targetStatus.Text=target==null?"TARGET: NOT BOUND":
                    "BOUND: "+target.WindowTitle;
            }
        }

        private bool OnPractice { get { return tabs.SelectedTab==practicePage; } }
        private void ToggleListening()
        {
            if(listening) { StopListening();return; }
            if(OnPractice && !practiceApproval.Checked) {
                Log("VOICE START DENIED: practice authorization off.");
                SetFeedback("Enable practice authorization first.",Orange);return;
            }
            if(!OnPractice && (!notepadApproval.Checked||target==null)) {
                Log("VOICE START DENIED: Notepad authorization or binding missing.");
                SetFeedback("Bind and authorize the safe Notepad document first.",Orange);return;
            }
            SpeechRecognitionEngine candidate=null;
            try {
                RecognizerInfo info=SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(x=>x.Culture.Name.StartsWith("en",
                        StringComparison.OrdinalIgnoreCase));
                if(info==null)throw new InvalidOperationException("No installed offline English recognizer.");
                candidate=new SpeechRecognitionEngine(info);
                string[] grammar=OnPractice ? new string[]{
                    "access next","access previous","access select",
                    "access read","access help","access stop","stop listening"
                } : new string[]{
                    "access line down","access line up",
                    "access move left","access move right",
                    "access line start","access line end",
                    "access stop","stop listening"
                };
                GrammarBuilder g=new GrammarBuilder(new Choices(grammar));
                g.Culture=info.Culture;
                candidate.LoadGrammar(new Grammar(g));
                candidate.SetInputToDefaultAudioDevice();
                int capturedGeneration=++voiceGeneration;
                bool modeAtStart=OnPractice;
                candidate.SpeechRecognized+=delegate(object sender,SpeechRecognizedEventArgs args) {
                    string phrase=args.Result.Text;
                    float confidence=args.Result.Confidence;
                    if(closing||IsDisposed||!IsHandleCreated)return;
                    try { BeginInvoke((Action)delegate {
                        if(listening && capturedGeneration==voiceGeneration && modeAtStart==OnPractice)
                            OnVoice(phrase,confidence);
                    }); } catch(InvalidOperationException) {}
                };
                candidate.SpeechRecognitionRejected+=delegate {
                    if(closing||IsDisposed||!IsHandleCreated)return;
                    try { BeginInvoke((Action)delegate {
                        if(listening && capturedGeneration==voiceGeneration)
                            Log("Speech rejected by local recognizer (not a policy denial).");
                    }); } catch(InvalidOperationException) {}
                };
                candidate.AudioLevelUpdated+=delegate(object sender,AudioLevelUpdatedEventArgs args) {
                    int level=args.AudioLevel;
                    if(closing||IsDisposed||!IsHandleCreated)return;
                    try { BeginInvoke((Action)delegate {
                        if(listening && capturedGeneration==voiceGeneration)
                            microphoneMeter.Value=Math.Max(0,Math.Min(100,level));
                    }); } catch(InvalidOperationException) {}
                };
                speech=candidate;
                listening=true;
                candidate.RecognizeAsync(RecognizeMode.Multiple);
                UpdateVoiceUi();
                Log("MIC ON. Offline "+info.Culture.Name+" recognizer. "+
                    (OnPractice?"Practice":"Bound Notepad")+" grammar.");
                SetFeedback("Listening. Say Access stop at any time.",Green);
            } catch(Exception error) {
                listening=false;
                try { if(candidate!=null)candidate.Dispose(); } catch {}
                speech=null;
                UpdateVoiceUi();
                Log("VOICE FAILED: "+error.Message);
                SetFeedback("Microphone unavailable; typed practice remains usable.",Orange);
            }
        }

        private void OnVoice(string phrase,float confidence)
        {
            Log("HEARD: "+phrase+" ("+(confidence*100).ToString("0")+"%).");
            if(NotepadGrammar.Parse(phrase)==NoteAction.Stop
                || ActionPolicy.Parse(phrase)==DemoAction.Stop) {
                EmergencyStop("Voice stop accepted.");
                return;
            }
            // Diagnostic mode is read-only: it never calls either action executor.
            if(diagnosticOnly.Checked) {
                Log("VOICE DIAGNOSTIC ONLY: "+phrase+
                    " ("+(confidence*100).ToString("0")+"%). No action attempted.");
                SetFeedback("Heard "+phrase+" — test-only mode.",Blue);
                return;
            }
            if(OnPractice) {
                PerformPractice(phrase,true,confidence);
                return;
            }
            PerformNotepad(phrase,confidence);
        }

        private void PerformNotepad(string phrase,float confidence)
        {
            NoteAction command=NotepadGrammar.Parse(phrase);
            NoteContext check=NotepadBridge.Observe(target,command,
                notepadApproval.Checked,listening,confidence);
            PolicyDecision result=NotepadPolicy.Evaluate(check);
            if(!result.Allowed) {
                denied++;
                SetFeedback("DENIED: "+result.Explanation,Orange);
                Log("DENY Notepad "+phrase+" | "+result.Explanation);
                return;
            }
            if(diagnosticOnly.Checked) {
                Log("VOICE TEST ONLY (Notepad): "+phrase+" | no key event.");
                return;
            }
            NoteContext fresh=NotepadBridge.Observe(target,command,
                notepadApproval.Checked,listening,confidence);
            PolicyDecision freshResult=NotepadPolicy.Evaluate(fresh);
            if(!freshResult.Allowed) {
                denied++;
                Log("DENY changed target before key: "+freshResult.Explanation);
                SetFeedback(freshResult.Explanation,Orange);
                return;
            }
            bool sent=NotepadBridge.TrySendNavigation(target,command);
            if(sent) {
                allowed++;
                Log("SENT "+phrase+" | target verified; human must verify caret movement.");
                SetFeedback("Notepad: "+command+" key dispatched.",Green);
            } else {
                denied++;
                Log("BLOCKED: Notepad input not sent or Windows rejected it.");
                SetFeedback("Navigation not sent. No retry.",Orange);
            }
        }

        private void StopListening()
        {
            listening=false;
            voiceGeneration++;
            SpeechRecognitionEngine old=speech;
            speech=null;
            if(old!=null) {
                try { old.RecognizeAsyncCancel(); } catch {}
                try { old.Dispose(); } catch {}
            }
            UpdateVoiceUi();
        }

        private void EmergencyStop(string reason)
        {
            StopListening();
            if(practiceApproval!=null)practiceApproval.Checked=false;
            if(notepadApproval!=null)notepadApproval.Checked=false;
            target=null;
            if(captureTimer!=null)captureTimer.Stop();
            if(bindButton!=null)bindButton.Enabled=true;
            UpdateTarget();
            Log("EMERGENCY STOP: "+reason+" Permissions revoked.");
            SetFeedback("Stopped. Microphone OFF; permissions revoked.",Orange);
        }

        private void UpdateVoiceUi()
        {
            if(voiceButton==null)return;
            voiceButton.Text=listening?"Stop voice":"Start voice";
            if(microphoneStatus!=null)microphoneStatus.Text=
                listening?"MIC ON  |  "+(OnPractice?"Practice":"Notepad")+" mode":
                    "MIC OFF  |  Standby";
            if(microphoneMeter!=null && !listening)microphoneMeter.Value=0;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if(keyData==Keys.Escape) {
                EmergencyStop("ESC pressed.");
                return true;
            }
            return base.ProcessCmdKey(ref msg,keyData);
        }

        protected override void WndProc(ref Message msg)
        {
            if(msg.Msg==WmHotkey && msg.WParam.ToInt32()==EmergencyHotkeyId) {
                EmergencyStop("Global Ctrl+Alt+Shift+F12.");
                return;
            }
            base.WndProc(ref msg);
        }
    }
}
