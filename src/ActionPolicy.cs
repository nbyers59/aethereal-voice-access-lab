using System;
using System.Text.RegularExpressions;

namespace Aethereal.VoiceAccessLab
{
    internal enum DemoAction { Unknown, Next, Previous, Select, Read, Help, Stop }

    // Independent action policy. The UI and speech layers cannot bypass this gate.
    // This prototype intentionally does not expose a generic Windows click method.
    internal sealed class ActionContext
    {
        public DemoAction Action;
        public bool UserApproved;
        public bool OwnDemoWindow;
        public bool WindowForeground;
        public bool HasSelection;
        public bool IsVoice;
        public bool MicrophoneListening;
        public float Confidence;
    }

    internal sealed class PolicyDecision
    {
        public readonly bool Allowed;
        public readonly string Explanation;
        public PolicyDecision(bool allowed, string explanation)
        {
            Allowed = allowed;
            Explanation = explanation;
        }
    }

    internal static class ActionPolicy
    {
        internal const float MinimumVoiceConfidence = 0.72F;

        internal static DemoAction Parse(string input)
        {
            if (input == null) return DemoAction.Unknown;
            string s = Regex.Replace(input.Trim().ToLowerInvariant(), @"\s+", " ");
            if (s == "stop" || s == "stop listening" || s == "access stop")
                return DemoAction.Stop;
            // Wake phrase is required for non-emergency commands.
            if (!s.StartsWith("access ", StringComparison.Ordinal)) return DemoAction.Unknown;
            string command = s.Substring("access ".Length);
            if (command == "next") return DemoAction.Next;
            if (command == "previous") return DemoAction.Previous;
            if (command == "select") return DemoAction.Select;
            if (command == "read") return DemoAction.Read;
            if (command == "help") return DemoAction.Help;
            return DemoAction.Unknown;
        }

        internal static PolicyDecision Evaluate(ActionContext c)
        {
            if (c == null) return new PolicyDecision(false, "Invalid request.");
            // An emergency stop must never depend on consent, confidence, or window focus.
            if (c.Action == DemoAction.Stop)
                return new PolicyDecision(true, "Emergency stop is always allowed.");
            if (c.Action == DemoAction.Unknown)
                return new PolicyDecision(false, "Command not in the closed command list.");
            if (!c.UserApproved)
                return new PolicyDecision(false, "Control not approved. Tick the authorization checkbox.");
            if (!c.OwnDemoWindow)
                return new PolicyDecision(false, "Refused: target is not the built-in demo workspace.");
            if (!c.WindowForeground)
                return new PolicyDecision(false, "Refused: demo window is not in the foreground.");
            if (c.IsVoice && !c.MicrophoneListening)
                return new PolicyDecision(false, "Refused: microphone is not listening.");
            if (c.IsVoice && c.Confidence < MinimumVoiceConfidence)
                return new PolicyDecision(false, "Speech confidence is below the safety threshold.");
            if ((c.Action == DemoAction.Select || c.Action == DemoAction.Read) && !c.HasSelection)
                return new PolicyDecision(false, "There is no selected demo item.");
            return new PolicyDecision(true, "Authorized for this demo window only.");
        }
    }
}
