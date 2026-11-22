using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;

namespace Aethereal.VoiceAccessLab
{
    // This UI intentionally controls ONLY its own in-memory practice list.
    // There are no screen captures, website calls, window enumeration, or synthetic OS clicks.
    internal sealed class DemoForm : Form
    {
        private static readonly Color Background = Color.FromArgb(12, 17, 28);
        private static readonly Color PanelColor = Color.FromArgb(20, 29, 47);
        private static readonly Color Ink = Color.FromArgb(230, 238, 250);
        private static readonly Color Subtle = Color.FromArgb(163, 179, 201);
        private static readonly Color Accent = Color.FromArgb(101, 174, 255);
        private static readonly Color Good = Color.FromArgb(119, 210, 165);
        private static readonly Color Warning = Color.FromArgb(255, 190, 115);

        private readonly string[] names = new string[]
        {
            "Read a sample instruction", "Review a sample image",
            "Mark a demonstration item", "Explore local navigation",
            "Practice the emergency stop"
        };
        private readonly bool[] selectedItems = new bool[5];

        private CheckBox approve;
        private CheckBox speakRead;
        private TextBox commandBox;
        private TextBox log;
        private Label voiceStatus;
        private Label selectedStatus;
        private Label outcome;
        private Button voiceButton;
        private ListBox demoList;
        private SpeechRecognitionEngine engine;
        private SpeechSynthesizer speaker;
        private bool listening;
        private int actionsAllowed;
        private int actionsDenied;

        internal DemoForm()
        {
            Text = "Aethereal Voice Access Lab v0.4.0-rc1";
            ClientSize = new Size(1000, 650);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Background;
            ForeColor = Ink;
            Font = new Font("Segoe UI", 10F);
            KeyPreview = true;
            BuildUi();
            RefreshDemoList();
            Log("Practice mode started. Permission OFF. Open Notepad Pilot for separately approved navigation.");
            FormClosing += delegate { StopVoice(); if (speaker != null) { speaker.Dispose(); speaker = null; } };
        }

        private Label Label(Control parent, string text, int x, int y, int w, int h, float size, Color color, bool bold)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, y, w, h);
            label.ForeColor = color;
            label.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            label.AutoEllipsis = true;
            parent.Controls.Add(label);
            return label;
        }

        private Button Button(Control parent, string text, int x, int y, int w, int h, EventHandler handler)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.BackColor = Color.FromArgb(39, 63, 92);
            b.ForeColor = Ink;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(65, 102, 143);
            b.Cursor = Cursors.Hand;
            b.Click += handler;
            parent.Controls.Add(b);
            return b;
        }

        private void BuildUi()
        {
            Label(this, "AETHEREAL VOICE ACCESS LAB", 20, 14, 650, 38, 23F, Ink, true);
            Label(this, "Aethereal prototype / Offline voice + permission-gated local controls", 21, 53, 930, 20, 9F, Subtle, false);
            Button(this, "OPEN NOTEPAD PILOT", 765, 9, 217, 41, delegate
            {
                StopVoice();
                using (NotepadLabForm pilot = new NotepadLabForm())
                    pilot.ShowDialog(this);
            });

            Panel left = new Panel();
            left.SetBounds(18, 87, 465, 548);
            left.BackColor = PanelColor;
            Controls.Add(left);
            Panel right = new Panel();
            right.SetBounds(499, 87, 482, 548);
            right.BackColor = PanelColor;
            Controls.Add(right);

            Label(left, "1  AUTHORIZE THE DEMO", 18, 17, 423, 26, 13F, Accent, true);
            Label(left, "This permission is for the practice window only.", 18, 47, 422, 22, 10F, Ink, false);
            approve = new CheckBox();
            approve.Text = "Allow commands to control THIS practice list";
            approve.SetBounds(18, 80, 425, 33);
            approve.ForeColor = Ink;
            approve.CheckedChanged += delegate
            {
                Log(approve.Checked ? "Consent ENABLED for local demo." : "Consent REVOKED.");
                if (!approve.Checked) StopVoice();
                UpdateVoiceLabel();
            };
            left.Controls.Add(approve);

            Label(left, "2  LISTEN OR TYPE", 18, 132, 422, 25, 13F, Accent, true);
            voiceButton = Button(left, "Start local voice", 18, 165, 204, 43, delegate { ToggleVoice(); });
            Button(left, "EMERGENCY STOP", 233, 165, 209, 43, delegate { Command("stop", false, 1F); });
            voiceStatus = Label(left, "MIC: OFF / No audio captured", 18, 214, 425, 23, 9F, Subtle, false);
            Label(left, "Try: access next | access select | access read", 18, 246, 425, 20, 9F, Subtle, false);
            commandBox = new TextBox();
            commandBox.SetBounds(18, 274, 307, 31);
            commandBox.BackColor = Color.FromArgb(11, 18, 31);
            commandBox.ForeColor = Ink;
            commandBox.BorderStyle = BorderStyle.FixedSingle;
            commandBox.Text = "access next";
            commandBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Command(commandBox.Text, false, 1F);
                }
            };
            left.Controls.Add(commandBox);
            Button(left, "Run locally", 335, 272, 107, 35, delegate { Command(commandBox.Text, false, 1F); });
            Label(left, "3  TRANSPARENT AUDIT LOG", 18, 326, 424, 23, 13F, Accent, true);
            log = new TextBox();
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.SetBounds(18, 356, 424, 160);
            log.BackColor = Color.FromArgb(11, 18, 31);
            log.ForeColor = Color.FromArgb(185, 205, 223);
            log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 9F);
            left.Controls.Add(log);

            Label(right, "APPROVED PRACTICE WORKSPACE", 18, 19, 446, 27, 13F, Accent, true);
            Label(right, "Commands alter only these five in-memory demo items.", 18, 53, 445, 38, 9F, Subtle, false);
            demoList = new ListBox();
            demoList.SetBounds(18, 101, 446, 192);
            demoList.BackColor = Color.FromArgb(11, 18, 31);
            demoList.ForeColor = Ink;
            demoList.Font = new Font("Segoe UI", 11F);
            demoList.BorderStyle = BorderStyle.FixedSingle;
            demoList.SelectedIndexChanged += delegate { UpdateSelectionLabel(); };
            right.Controls.Add(demoList);
            Button(right, "Previous", 18, 306, 104, 40, delegate { Command("access previous", false, 1F); });
            Button(right, "Next", 131, 306, 99, 40, delegate { Command("access next", false, 1F); });
            Button(right, "Select", 239, 306, 99, 40, delegate { Command("access select", false, 1F); });
            Button(right, "Read", 347, 306, 117, 40, delegate { Command("access read", false, 1F); });
            selectedStatus = Label(right, "Selected: none", 18, 368, 445, 27, 10F, Ink, false);
            speakRead = new CheckBox();
            speakRead.Text = "Read demo item aloud using Windows offline speech";
            speakRead.SetBounds(18, 399, 446, 28);
            speakRead.ForeColor = Subtle;
            right.Controls.Add(speakRead);
            outcome = Label(right, "Waiting for a local command.", 18, 437, 446, 30, 11F, Good, true);
            Label(right, "NO browser control  |  NO OS mouse injection", 18, 490, 445, 23, 9F, Warning, true);
            Label(right, "NO network  |  NO financial or casino actions", 18, 514, 445, 22, 9F, Warning, true);
        }

        private void RefreshDemoList()
        {
            int previous = demoList.SelectedIndex;
            demoList.BeginUpdate();
            demoList.Items.Clear();
            for (int i = 0; i < names.Length; i++)
                demoList.Items.Add((selectedItems[i] ? "[x] " : "[ ] ") + names[i]);
            demoList.EndUpdate();
            demoList.SelectedIndex = previous >= 0 && previous < names.Length ? previous : 0;
            UpdateSelectionLabel();
        }

        private void UpdateSelectionLabel()
        {
            selectedStatus.Text = demoList.SelectedIndex >= 0
                ? "Selected: " + names[demoList.SelectedIndex]
                : "No item selected.";
        }

        private void Log(string text)
        {
            if (log == null) return;
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }

        private void Command(string spoken, bool isVoice, float confidence)
        {
            DemoAction action = ActionPolicy.Parse(spoken);
            ActionContext context = new ActionContext
            {
                Action = action,
                UserApproved = approve.Checked,
                OwnDemoWindow = true, // There is no external target API in this build.
                WindowForeground = ContainsFocus,
                HasSelection = demoList.SelectedIndex >= 0,
                IsVoice = isVoice,
                MicrophoneListening = listening,
                Confidence = confidence
            };
            PolicyDecision decision = ActionPolicy.Evaluate(context);
            if (!decision.Allowed)
            {
                actionsDenied++;
                outcome.ForeColor = Warning;
                outcome.Text = "DENIED - " + decision.Explanation;
                Log("DENY [" + (isVoice ? "voice" : "manual") + "] " + spoken + " | " + decision.Explanation);
                return;
            }
            actionsAllowed++;
            if (action == DemoAction.Stop)
            {
                StopVoice();
                outcome.Text = "Stopped. Microphone is off.";
            }
            else if (action == DemoAction.Help)
            {
                outcome.Text = "Use Access: next, previous, select, read, help, stop.";
            }
            else if (action == DemoAction.Next || action == DemoAction.Previous)
            {
                int delta = action == DemoAction.Next ? 1 : -1;
                demoList.SelectedIndex = (demoList.SelectedIndex + delta + names.Length) % names.Length;
                outcome.Text = "Moved selection locally.";
            }
            else if (action == DemoAction.Select)
            {
                int i = demoList.SelectedIndex;
                selectedItems[i] = !selectedItems[i];
                RefreshDemoList();
                outcome.Text = selectedItems[i] ? "Marked a demo item." : "Unmarked a demo item.";
            }
            else if (action == DemoAction.Read)
            {
                string name = names[demoList.SelectedIndex];
                outcome.Text = "Reading: " + name;
                if (speakRead.Checked) Speak(name);
            }
            outcome.ForeColor = Good;
            Log("ALLOW [" + (isVoice ? "voice" : "manual") + "] " + spoken + " | " + outcome.Text);
        }

        private void Speak(string text)
        {
            try
            {
                if (speaker == null)
                {
                    speaker = new SpeechSynthesizer();
                    speaker.SetOutputToDefaultAudioDevice();
                }
                speaker.SpeakAsyncCancelAll();
                speaker.SpeakAsync("Practice item: " + text);
            }
            catch (Exception error) { Log("Offline speech output unavailable: " + error.Message); }
        }

        private void ToggleVoice()
        {
            if (listening) { StopVoice(); return; }
            if (!approve.Checked)
            {
                Command("access help", true, 1F); // records a denied request
                return;
            }
            SpeechRecognitionEngine candidate = null;
            try
            {
                RecognizerInfo info = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(x => x.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase));
                if (info == null)
                {
                    voiceStatus.Text = "MIC: no offline English recognizer installed; typed demo works.";
                    Log("Voice unavailable: no installed offline English speech recognizer.");
                    return;
                }
                candidate = new SpeechRecognitionEngine(info);
                Choices words = new Choices(new string[]
                {
                    "access next", "access previous", "access select",
                    "access read", "access help", "access stop", "stop listening"
                });
                GrammarBuilder builder = new GrammarBuilder(words);
                builder.Culture = info.Culture;
                candidate.LoadGrammar(new Grammar(builder));
                candidate.SetInputToDefaultAudioDevice();
                candidate.SpeechRecognized += delegate(object sender, SpeechRecognizedEventArgs args)
                {
                    string text = args.Result.Text;
                    float score = args.Result.Confidence;
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate { if (listening) Command(text, true, score); });
                };
                candidate.SpeechRecognitionRejected += delegate(object sender, SpeechRecognitionRejectedEventArgs args)
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate { Log("Speech rejected by local recognizer."); });
                };
                engine = candidate;
                listening = true;
                engine.RecognizeAsync(RecognizeMode.Multiple);
                Log("Microphone started. Engine: " + info.Culture.Name + ". Local speech only.");
                UpdateVoiceLabel();
            }
            catch (Exception error)
            {
                listening = false;
                try { if (candidate != null) candidate.Dispose(); } catch { }
                engine = null;
                Log("Speech unavailable: " + error.Message);
                UpdateVoiceLabel();
            }
        }

        private void StopVoice()
        {
            listening = false;
            SpeechRecognitionEngine toStop = engine;
            engine = null;
            if (toStop != null)
            {
                try { toStop.RecognizeAsyncCancel(); } catch { }
                try { toStop.Dispose(); } catch { }
            }
            UpdateVoiceLabel();
        }

        private void UpdateVoiceLabel()
        {
            if (voiceButton == null) return;
            voiceButton.Text = listening ? "Stop listening" : "Start local voice";
            voiceStatus.Text = listening
                ? "MIC: ON - Offline recognition, closed grammar"
                : "MIC: OFF - No audio captured";
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (key == Keys.Escape)
            {
                Command("stop", false, 1F);
                return true;
            }
            return base.ProcessCmdKey(ref message, key);
        }
    }
}
