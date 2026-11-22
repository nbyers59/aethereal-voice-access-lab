using System;
using System.Collections.Generic;
using System.IO;

namespace Aethereal.VoiceAccessLab
{
    internal static class NotepadSelfTest
    {
        private static NoteContext Valid(NoteAction action)
        {
            return new NoteContext {
                Action = action,
                Permission = true,
                Bound = true,
                ProcessTrusted = true,
                SameWindow = true,
                Foreground = true,
                ExactSampleTitle = true,
                EditorFocused = true,
                Listening = true,
                Confidence = 0.97f
            };
        }
        internal static int Run(string filename)
        {
            List<string> log = new List<string>();
            int passed = 0, failed = 0;
            Action<string, bool> check = delegate(string label, bool condition)
            {
                if (condition) passed++; else failed++;
                log.Add((condition ? "PASS " : "FAIL ") + label);
            };
            check("Wake word required", NotepadGrammar.Parse("line down") == NoteAction.Unknown);
            check("Down parsed", NotepadGrammar.Parse("Access line down") == NoteAction.Down);
            check("Up parsed", NotepadGrammar.Parse("access line up") == NoteAction.Up);
            check("Left parsed", NotepadGrammar.Parse("access move left") == NoteAction.Left);
            check("Right parsed", NotepadGrammar.Parse("access move right") == NoteAction.Right);
            check("Home parsed", NotepadGrammar.Parse("access line start") == NoteAction.Home);
            check("End parsed", NotepadGrammar.Parse("access line end") == NoteAction.End);
            check("Emergency stop parsed", NotepadGrammar.Parse("stop listening") == NoteAction.Stop);
            check("No typing commands", NotepadGrammar.Parse("access type hello") == NoteAction.Unknown);
            check("No save commands", NotepadGrammar.Parse("access save") == NoteAction.Unknown);
            check("No casino commands", NotepadGrammar.Parse("access call") == NoteAction.Unknown);
            check("No compound commands", NotepadGrammar.Parse("access line down and save") == NoteAction.Unknown);
            check("Unknown action denied", !NotepadPolicy.Evaluate(Valid(NoteAction.Unknown)).Allowed);
            check("Valid voice navigation allowed", NotepadPolicy.Evaluate(Valid(NoteAction.Down)).Allowed);
            NoteContext c = Valid(NoteAction.Down); c.Permission = false;
            check("Consent revoked denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.Bound = false;
            check("Unbound target denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.ProcessTrusted = false;
            check("Non-Notepad process denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.SameWindow = false;
            check("Changed window denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.Foreground = false;
            check("Wrong foreground denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.ExactSampleTitle = false;
            check("Wrong Notepad tab denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.EditorFocused = false;
            check("Editor unfocused denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.Listening = false;
            check("Mic inactive denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.Confidence = 0.30f;
            check("Low confidence denies", !NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Down); c.Confidence = NotepadPolicy.MinimumConfidence;
            check("Confidence at threshold allows", NotepadPolicy.Evaluate(c).Allowed);
            c = Valid(NoteAction.Stop); c.Permission = false; c.Bound = false;
            c.ProcessTrusted = false; c.SameWindow = false; c.Foreground = false;
            c.EditorFocused = false; c.Listening = false; c.Confidence = 0;
            check("Emergency stop bypasses restrictive gates", NotepadPolicy.Evaluate(c).Allowed);
            check("Safe test document recognized",
                NotepadBridge.LooksLikeSampleTitle(
                    "VOICE-ACCESS-LAB-SAFE-TEST.txt - Notepad"));
            check("Unrelated Notepad document rejected",
                !NotepadBridge.LooksLikeSampleTitle("personal-notes.txt - Notepad"));
            check("Spoofed title without Notepad suffix rejected",
                !NotepadBridge.LooksLikeSampleTitle(
                    "VOICE-ACCESS-LAB-SAFE-TEST.txt - Chrome"));
            log.Add("TOTAL PASS " + passed);
            log.Add("TOTAL FAIL " + failed);
            log.Add("NOTE: Pure policy tests. No Notepad window opened and no OS key event sent.");
            File.WriteAllLines(filename, log.ToArray());
            return failed == 0 ? 0 : 1;
        }
    }
}
