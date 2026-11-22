using System;
using System.Collections.Generic;
using System.IO;

namespace Aethereal.VoiceAccessLab
{
    internal static class SelfTest
    {
        private static ActionContext C(DemoAction command)
        {
            return new ActionContext
            {
                Action=command, UserApproved=true, OwnDemoWindow=true,
                WindowForeground=true, HasSelection=true,
                IsVoice=false, MicrophoneListening=false, Confidence=1F
            };
        }

        internal static int Run(string output)
        {
            List<string> lines = new List<string>();
            int passed = 0, failed = 0;
            Action<string, bool> check = delegate(string name, bool ok)
            {
                lines.Add((ok ? "PASS " : "FAIL ") + name);
                if (ok) passed++; else failed++;
            };
            check("Next parsed with wake word", ActionPolicy.Parse("Access next")==DemoAction.Next);
            check("Previous parsed with wake word", ActionPolicy.Parse("access previous")==DemoAction.Previous);
            check("Select parsed with wake word", ActionPolicy.Parse("access select")==DemoAction.Select);
            check("Read parsed with wake word", ActionPolicy.Parse("access read")==DemoAction.Read);
            check("Help parsed with wake word", ActionPolicy.Parse("access help")==DemoAction.Help);
            check("Emergency stop without wake word", ActionPolicy.Parse("stop")==DemoAction.Stop);
            check("Emergency stop listening", ActionPolicy.Parse("stop listening")==DemoAction.Stop);
            check("Wake phrase whitespace normalized", ActionPolicy.Parse("  Access  next ")==DemoAction.Next);
            check("Reject missing wake word", ActionPolicy.Parse("select")==DemoAction.Unknown);
            check("Reject combined commands", ActionPolicy.Parse("access next and select")==DemoAction.Unknown);
            check("Reject historical casino action", ActionPolicy.Parse("access fold")==DemoAction.Unknown);
            check("Reject another historical casino action", ActionPolicy.Parse("access call")==DemoAction.Unknown);
            check("Reject arbitrary web command", ActionPolicy.Parse("access open browser")==DemoAction.Unknown);
            check("Reject unknown action", !ActionPolicy.Evaluate(C(DemoAction.Unknown)).Allowed);
            ActionContext c = C(DemoAction.Select);
            c.UserApproved = false;
            check("Consent OFF means deny", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.OwnDemoWindow = false;
            check("Foreign window means deny", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.WindowForeground = false;
            check("Background window means deny", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.HasSelection = false;
            check("No selection means deny Select", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Read); c.HasSelection = false;
            check("No selection means deny Read", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Next); c.UserApproved = true;
            check("Approved local navigation allowed", ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.IsVoice = true; c.MicrophoneListening = false;
            check("Voice while mic off means deny", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.IsVoice = true; c.MicrophoneListening = true; c.Confidence=0.31F;
            check("Low-confidence voice means deny", !ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Select); c.IsVoice = true; c.MicrophoneListening = true; c.Confidence=0.90F;
            check("Approved confident local voice allowed", ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Stop); c.UserApproved = false; c.OwnDemoWindow=false;
            c.WindowForeground=false; c.IsVoice=true; c.Confidence=0; c.MicrophoneListening=false;
            check("Emergency stop overrides every gate", ActionPolicy.Evaluate(c).Allowed);
            c = C(DemoAction.Help); c.UserApproved=false;
            check("Help obeys authorization gate", !ActionPolicy.Evaluate(c).Allowed);
            lines.Add("TOTAL PASS " + passed);
            lines.Add("TOTAL FAIL " + failed);
            lines.Add("NOTE: deterministic policy tests only. No microphone/audio/GUI or real OS input tested.");
            File.WriteAllLines(output, lines.ToArray());
            return failed == 0 ? 0 : 1;
        }
    }
}
