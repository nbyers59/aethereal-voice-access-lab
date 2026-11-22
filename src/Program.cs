using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Aethereal.VoiceAccessLab
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Any(x => string.Equals(x, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = SelfTest.Run(Path.Combine(appFolder, "SELF_TEST_RESULTS.txt"));
                return;
            }
            if (args.Any(x => string.Equals(x, "--v03-ui-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                string report = Path.Combine(appFolder, "UNIFIED_UI_SMOKE_RESULTS.txt");
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (UnifiedForm form = new UnifiedForm())
                    {
                        bool ok = form.UiSmokeReady;
                        File.WriteAllText(report, (ok ? "PASS" : "FAIL")
                            + ": one unified window, two workspaces, permissions OFF, microphone OFF, Notepad unbound, five sample items."
                            + Environment.NewLine
                            + "No window displayed, no external application launched, no keys sent." + Environment.NewLine);
                        Environment.ExitCode = ok ? 0 : 1;
                    }
                }
                catch(Exception error)
                {
                    File.WriteAllText(report, "FAIL: " + error.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Any(x => string.Equals(x, "--notepad-self-test", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = NotepadSelfTest.Run(
                    Path.Combine(appFolder, "NOTEPAD_POLICY_TEST_RESULTS.txt"));
                return;
            }
            if (args.Any(x => string.Equals(x, "--notepad-ui-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                string report = Path.Combine(appFolder, "NOTEPAD_UI_SMOKE_RESULTS.txt");
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (NotepadLabForm pilot = new NotepadLabForm())
                    {
                        bool ok = pilot.SmokeReady;
                        File.WriteAllText(report, (ok ? "PASS" : "FAIL") +
                            ": Notepad pilot initialized, consent off, no target, no microphone." +
                            Environment.NewLine +
                            "No Notepad process launched; no OS key input sent." + Environment.NewLine);
                        Environment.ExitCode = ok ? 0 : 1;
                    }
                }
                catch (Exception error)
                {
                    File.WriteAllText(report, "FAIL: " + error.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Any(x => string.Equals(x, "--ui-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                // Instantiates controls but does not Show() the window or start a microphone.
                string report = Path.Combine(appFolder, "UI_SMOKE_RESULTS.txt");
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (DemoForm form = new DemoForm())
                    {
                        Panel[] panels = form.Controls.OfType<Panel>().ToArray();
                        bool hasTwoPanels = panels.Length == 2;
                        bool deniedByDefault = panels.SelectMany(p => p.Controls.OfType<CheckBox>())
                            .Any(x => x.Text.Contains("THIS practice list") && !x.Checked);
                        bool practiceList = panels.SelectMany(p => p.Controls.OfType<ListBox>())
                            .Any(x => x.Items.Count == 5);
                        bool pass = hasTwoPanels && deniedByDefault && practiceList;
                        File.WriteAllText(report, (pass ? "PASS" : "FAIL")
                            + ": UI controls created without showing a window or activating a microphone."
                            + Environment.NewLine
                            + "Two panels=" + hasTwoPanels
                            + ", permission off by default=" + deniedByDefault
                            + ", 5 demo items=" + practiceList + Environment.NewLine);
                        Environment.ExitCode = pass ? 0 : 1;
                    }
                }
                catch (Exception error)
                {
                    File.WriteAllText(report, "FAIL: " + error.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new UnifiedForm());
            }
            catch (Exception error)
            {
                string path = Path.Combine(appFolder, "STARTUP_ERROR.txt");
                try { File.WriteAllText(path, error.ToString()); } catch { }
                MessageBox.Show("The prototype could not start. See STARTUP_ERROR.txt beside the executable.",
                    "Aethereal Voice Access Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
