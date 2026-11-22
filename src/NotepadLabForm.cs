using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Windows.Forms;

namespace Aethereal.VoiceAccessLab
{
    // Human-approved Notepad-only lab. No arbitrary process selection.
    internal sealed class NotepadLabForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(12, 17, 28);
        private static readonly Color Card = Color.FromArgb(20, 29, 47);
        private static readonly Color Ink = Color.FromArgb(230, 238, 250);
        private static readonly Color Muted = Color.FromArgb(160, 180, 205);
        private static readonly Color Accent = Color.FromArgb(105, 178, 255);
        private static readonly Color Green = Color.FromArgb(120, 211, 172);
        private static readonly Color Orange = Color.FromArgb(255, 187, 115);

        private const int EmergencyHotkeyId = 0x4A12;
        private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4;
        private const int WmHotkey = 0x0312;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private TextBox log;
        private Label connection;
        private Label voiceLabel;
        private Label status;
        private CheckBox approved;
        private Button microphoneButton;
        private Button bindButton;
        private Timer captureTimer;
        private int countdown;
        private NotepadTarget target;
        private SpeechRecognitionEngine engine;
        private bool listening;
        private bool hotkeyRegistered;
        private bool closing;
        private int approvals, denials;

        internal NotepadLabForm()
        {
            Text = "Aethereal Aethereal Voice Access Lab v0.4.0-rc1 | Notepad Pilot";
            ClientSize = new Size(920, 610);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Bg;
            ForeColor = Ink;
            Font = new Font("Segoe UI", 10);
            BuildUi();
            captureTimer = new Timer();
            captureTimer.Interval = 1000;
            captureTimer.Tick += CaptureTick;
            Shown += delegate
            {
                hotkeyRegistered = RegisterHotKey(Handle, EmergencyHotkeyId,
                    ModAlt | ModControl | ModShift, (uint)Keys.F12);
                Log(hotkeyRegistered
                    ? "GLOBAL EMERGENCY STOP: Ctrl+Alt+Shift+F12 registered."
                    : "WARNING: global emergency hotkey unavailable. Voice stop and Close remain available.");
                Log("Notepad pilot initialized. Permission OFF. Target not bound.");
            };
            FormClosing += delegate
            {
                closing = true;
                Disconnect();
                captureTimer.Dispose();
                if (hotkeyRegistered) { UnregisterHotKey(Handle, EmergencyHotkeyId); hotkeyRegistered = false; }
            };
        }

        internal bool SmokeReady
        {
            get { return approved != null && !approved.Checked
                && target == null && !listening
                && connection != null && microphoneButton != null && log != null; }
        }

        private Label AddLabel(Control parent, string text, int x, int y, int w, int h,
            int fontSize, Color color, bool bold)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.SetBounds(x,y,w,h);
            lbl.AutoEllipsis = true;
            lbl.ForeColor = color;
            lbl.Font = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            parent.Controls.Add(lbl);
            return lbl;
        }

        private Button AddButton(Control parent, string text, int x, int y, int w, int h,
            EventHandler pressed)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x,y,w,h);
            b.BackColor = Color.FromArgb(36, 64, 96);
            b.ForeColor = Ink;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(57, 101, 146);
            b.Click += pressed;
            parent.Controls.Add(b);
            return b;
        }

        private void BuildUi()
        {
            AddLabel(this, "NOTEPAD PILOT", 18, 13, 890, 35, 22, Ink, true);
            AddLabel(this, "Navigation-only voice control of one named, local test document",
                19, 53, 880, 26, 10, Muted, false);
            Panel left = new Panel { BackColor=Card };
            left.SetBounds(18, 91, 429, 501);
            Controls.Add(left);
            Panel right = new Panel { BackColor=Card };
            right.SetBounds(463, 91, 439, 501);
            Controls.Add(right);

            AddLabel(left,"1  OPEN A SAFE TEST DOCUMENT",15,15,399,25,12,Accent,true);
            AddLabel(left,"Only the sample inside this v0.4.0-rc1 folder is eligible.",
                15,48,399,42,10,Muted,false);
            AddButton(left,"Open test file in Notepad",15,97,398,42,
                delegate { OpenTestFile(); });

            AddLabel(left,"2  BIND THE VISIBLE NOTEPAD",15,151,399,25,12,Accent,true);
            AddLabel(left,"Press Bind, then switch to the test-file tab within 5 seconds.",
                15,183,397,43,9,Muted,false);
            bindButton = AddButton(left,"Bind foreground Notepad in 5 seconds",15,231,398,40,
                delegate { BeginBind(); });
            connection = AddLabel(left,"Target: NOT BOUND",15,282,397,54,10,Orange,false);

            approved = new CheckBox();
            approved.Text = "I approve navigation of this test document only";
            approved.SetBounds(15,340,395,36);
            approved.ForeColor = Ink;
            approved.CheckedChanged += delegate
            {
                if (!approved.Checked)
                {
                    StopVoice();
                    target = null;
                    UpdateConnection();
                    Log("Permission revoked; target disconnected.");
                }
                else Log("Permission enabled for explicitly bound test document only.");
            };
            left.Controls.Add(approved);
            AddButton(left,"Disconnect / revoke",15,400,194,40,
                delegate { approved.Checked=false; Disconnect(); });
            AddButton(left,"EMERGENCY STOP",220,400,193,40,
                delegate { StopVoice(); Log("Emergency stop button activated."); });
            AddLabel(left,"Global stop: Ctrl + Alt + Shift + F12",
                15,455,397,25,9,Orange,true);

            AddLabel(right,"3  LISTEN & NAVIGATE",15,15,408,25,12,Accent,true);
            AddLabel(right,"After starting voice, switch to the Notepad document.",
                15,47,407,35,10,Muted,false);
            microphoneButton = AddButton(right,"Start local voice",15,88,408,45,
                delegate { ToggleVoice(); });
            voiceLabel=AddLabel(right,"MIC OFF  |  No external key events enabled",
                15,142,407,25,9,Muted,false);
            AddLabel(right,"Say: Access line down  /  Access line up",
                15,176,408,24,10,Ink,false);
            AddLabel(right,"Access move left / right  |  Access line start / end",
                15,201,408,34,9,Ink,false);
            AddLabel(right,"Access stop  |  Stop listening",
                15,232,408,23,9,Ink,false);
            status=AddLabel(right,"Waiting for an approved test.",15,265,408,35,10,Green,true);
            AddLabel(right,"4  ACTION AUDIT LOG",15,314,408,25,12,Accent,true);
            log=new TextBox { Multiline=true, ReadOnly=true,
                ScrollBars=ScrollBars.Vertical, BackColor=Color.FromArgb(10,17,30),
                ForeColor=Color.FromArgb(188,207,231), Font=new Font("Consolas",9),
                BorderStyle=BorderStyle.FixedSingle };
            log.SetBounds(15,346,408,123);
            right.Controls.Add(log);
            AddLabel(right,"No typing, saving, clicking, accounts, or website access.",
                15,477,408,20,9,Orange,true);
        }

        private void Log(string message)
        {
            if (log == null) return;
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message
                + Environment.NewLine);
        }

        private string SamplePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "TEST_ONLY", NotepadBridge.SampleFileName); }
        }

        private void OpenTestFile()
        {
            try
            {
                string file = SamplePath;
                string folder = Path.GetDirectoryName(file);
                Directory.CreateDirectory(folder);
                if (!File.Exists(file))
                {
                    File.WriteAllText(file,
                        "AETHEREAL VOICE ACCESS LAB - SAFE NOTEPAD TEST\r\n" +
                        "This document contains no personal information.\r\n" +
                        "Line three: Navigation down should move the caret.\r\n" +
                        "Line four: Navigation up should move the caret.\r\n" +
                        "Line five: Left and right change the caret position.\r\n" +
                        "Line six: Home and End move within a line.\r\n" +
                        "No command in this prototype writes or saves anything.\r\n" +
                        "Say Access stop to disable the microphone.\r\n");
                }
                Process.Start(new ProcessStartInfo {
                    FileName="notepad.exe", Arguments="\"" + file + "\"", UseShellExecute=true
                });
                status.Text="Test file opened. Next: bind its Notepad window.";
                Log("Opened local sample in Notepad (user-requested).");
            }
            catch(Exception error) { Log("Could not open test document: "+error.Message); }
        }

        private void BeginBind()
        {
            StopVoice();
            target = null;
            approved.Checked = false;
            countdown=5;
            bindButton.Enabled=false;
            captureTimer.Start();
            connection.Text="Switch to Notepad! Capturing in 5 seconds.";
            Log("Binding requested. Switch to the SAFE-TEST document now.");
        }

        private void CaptureTick(object sender, EventArgs e)
        {
            countdown--;
            if (countdown>0)
            {
                connection.Text="Switch to Notepad: "+countdown+" seconds remaining.";
                return;
            }
            captureTimer.Stop();
            bindButton.Enabled=true;
            NotepadTarget candidate;
            string reason;
            if (NotepadBridge.TryCaptureForeground(out candidate,out reason))
            {
                target=candidate;
                connection.Text="BOUND: "+candidate.WindowTitle;
                Log("Bound Notepad PID "+candidate.ProcessId+" / "+candidate.WindowTitle);
                status.Text="Return here and tick approval, then start voice.";
            }
            else
            {
                target=null;
                UpdateConnection();
                Log("BIND DENIED: "+reason);
                status.Text=reason;
            }
        }

        private void UpdateConnection()
        {
            if (connection!=null)
                connection.Text=target==null?"Target: NOT BOUND":"BOUND: "+target.WindowTitle;
        }

        private void ToggleVoice()
        {
            if (listening) { StopVoice(); return; }
            if (!approved.Checked || target==null)
            {
                status.Text="Bind the test document and approve it before starting speech.";
                Log("Voice start denied: explicit authorization or bound target missing.");
                return;
            }
            SpeechRecognitionEngine candidate=null;
            try
            {
                RecognizerInfo info=SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(x=>x.Culture.Name.StartsWith("en",
                        StringComparison.OrdinalIgnoreCase));
                if (info==null) throw new InvalidOperationException("No offline English recognizer installed.");
                candidate=new SpeechRecognitionEngine(info);
                Choices commands=new Choices(new string[]{
                    "access line down","access line up",
                    "access move left","access move right",
                    "access line start","access line end",
                    "access stop","stop listening"
                });
                GrammarBuilder grammar=new GrammarBuilder(commands);
                grammar.Culture=info.Culture;
                candidate.LoadGrammar(new Grammar(grammar));
                candidate.SetInputToDefaultAudioDevice();
                candidate.SpeechRecognized+=delegate(object s, SpeechRecognizedEventArgs e)
                {
                    string phrase=e.Result.Text;
                    float score=e.Result.Confidence;
                    if (!closing && !IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate { if(listening) ProcessVoice(phrase,score); });
                };
                candidate.SpeechRecognitionRejected+=delegate
                {
                    if (!closing && !IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate { if (listening) Log("Recognizer rejected non-command speech."); });
                };
                engine=candidate;
                listening=true;
                engine.RecognizeAsync(RecognizeMode.Multiple);
                Log("Microphone ON. Switch to bound Notepad and click inside its text editor.");
                UpdateMicrophone();
            }
            catch(Exception error)
            {
                listening=false;
                if(candidate!=null) { try { candidate.Dispose(); } catch {} }
                engine=null;
                Log("Voice unavailable: "+error.Message);
                UpdateMicrophone();
            }
        }

        private void ProcessVoice(string spoken,float confidence)
        {
            NoteAction action=NotepadGrammar.Parse(spoken);
            NoteContext observation=NotepadBridge.Observe(target,action,approved.Checked,
                listening,confidence);
            PolicyDecision result=NotepadPolicy.Evaluate(observation);
            if (!result.Allowed)
            {
                denials++;
                status.ForeColor=Orange;
                status.Text="DENIED: "+result.Explanation;
                Log("DENY "+spoken+" @"+(confidence*100).ToString("0")+"% | "+result.Explanation);
                return;
            }
            if (action==NoteAction.Stop)
            {
                StopVoice();
                status.Text="Microphone stopped.";
                Log("ALLOW stop -> microphone OFF.");
                return;
            }
            // One last target and focus check immediately before sending two key events.
            NoteContext fresh=NotepadBridge.Observe(target,action,approved.Checked,
                listening,confidence);
            PolicyDecision freshResult=NotepadPolicy.Evaluate(fresh);
            if (!freshResult.Allowed)
            {
                denials++;
                Log("DENY stale window: "+freshResult.Explanation);
                return;
            }
            if (NotepadBridge.TrySendNavigation(target,action))
            {
                approvals++;
                status.ForeColor=Green;
                status.Text="Key events sent: "+action+" (verify caret movement).";
                Log("SENT "+spoken+" | verified target; actual caret move needs visual check.");
            }
            else
            {
                denials++;
                status.ForeColor=Orange;
                status.Text="Input not sent or Windows rejected it.";
                Log("DENY/FAIL SendInput "+spoken+"; no retry.");
            }
        }

        private void StopVoice()
        {
            listening=false;
            SpeechRecognitionEngine old=engine;
            engine=null;
            if(old!=null)
            {
                try { old.RecognizeAsyncCancel(); } catch {}
                try { old.Dispose(); } catch {}
            }
            UpdateMicrophone();
        }

        private void Disconnect()
        {
            captureTimer.Stop();
            StopVoice();
            target=null;
            if(approved!=null) approved.Checked=false;
            UpdateConnection();
        }

        private void UpdateMicrophone()
        {
            if(microphoneButton==null)return;
            microphoneButton.Text=listening?"Stop listening":"Start local voice";
            voiceLabel.Text=listening
                ? "MIC ON | voice-only navigation (bound Notepad required)"
                : "MIC OFF | no voice actions active";
        }

        protected override void WndProc(ref Message message)
        {
            if(message.Msg==WmHotkey && message.WParam.ToInt32()==EmergencyHotkeyId)
            {
                StopVoice();
                if(log!=null)Log("GLOBAL EMERGENCY STOP pressed.");
                if(status!=null)status.Text="Global emergency stop: microphone OFF.";
                return;
            }
            base.WndProc(ref message);
        }
    }
}
