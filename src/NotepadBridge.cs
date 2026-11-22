using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace Aethereal.VoiceAccessLab
{
    // Dedicated navigation-only bridge. No text insertion, saving, browser access,
    // mouse clicks, process scanning, or control of an arbitrary window.
    internal enum NoteAction { Unknown, Down, Up, Left, Right, Home, End, Stop }

    internal static class NotepadGrammar
    {
        internal static NoteAction Parse(string input)
        {
            if (input == null) return NoteAction.Unknown;
            string value = System.Text.RegularExpressions.Regex.Replace(
                input.Trim().ToLowerInvariant(), @"\s+", " ");
            if (value == "stop" || value == "stop listening" || value == "access stop")
                return NoteAction.Stop;
            if (!value.StartsWith("access ", StringComparison.Ordinal))
                return NoteAction.Unknown;
            string command = value.Substring("access ".Length);
            if (command == "line down") return NoteAction.Down;
            if (command == "line up") return NoteAction.Up;
            if (command == "move left") return NoteAction.Left;
            if (command == "move right") return NoteAction.Right;
            if (command == "line start") return NoteAction.Home;
            if (command == "line end") return NoteAction.End;
            return NoteAction.Unknown;
        }
    }

    internal sealed class NoteContext
    {
        internal NoteAction Action;
        internal bool Permission;
        internal bool Bound;
        internal bool ProcessTrusted;
        internal bool SameWindow;
        internal bool Foreground;
        internal bool ExactSampleTitle;
        internal bool EditorFocused;
        internal bool Listening;
        internal float Confidence;
    }

    internal static class NotepadPolicy
    {
        internal const float MinimumConfidence = 0.72f;

        internal static PolicyDecision Evaluate(NoteContext x)
        {
            if (x == null) return new PolicyDecision(false, "No context.");
            if (x.Action == NoteAction.Stop)
                return new PolicyDecision(true, "Emergency stop is always available.");
            if (x.Action == NoteAction.Unknown)
                return new PolicyDecision(false, "Command not in the Notepad navigation grammar.");
            if (!x.Permission)
                return new PolicyDecision(false, "User has not approved this test document.");
            if (!x.Bound || !x.SameWindow)
                return new PolicyDecision(false, "Target not bound or target window changed.");
            if (!x.ProcessTrusted)
                return new PolicyDecision(false, "Target is not trusted Microsoft Notepad.");
            if (!x.ExactSampleTitle)
                return new PolicyDecision(false, "Notepad is not showing the named Aethereal test document.");
            if (!x.Foreground)
                return new PolicyDecision(false, "Bound Notepad window is not in the foreground.");
            if (!x.EditorFocused)
                return new PolicyDecision(false, "Click inside the Notepad text editor first.");
            if (!x.Listening)
                return new PolicyDecision(false, "Voice listener is inactive.");
            if (x.Confidence < MinimumConfidence)
                return new PolicyDecision(false, "Voice confidence is too low.");
            return new PolicyDecision(true, "Approved navigation-only command.");
        }
    }

    internal sealed class NotepadTarget
    {
        internal IntPtr Window;
        internal uint ProcessId;
        internal string ExePath;
        internal string WindowTitle;
    }

    internal static class NotepadBridge
    {
        internal const string SampleFileName = "VOICE-ACCESS-LAB-SAFE-TEST.txt";
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint number, INPUT[] items, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT key;
            [FieldOffset(0)] public MOUSEINPUT mouse;
            [FieldOffset(0)] public HARDWAREINPUT hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort virtualKey;
            public ushort scanCode;
            public uint flags;
            public uint time;
            public IntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, flags, time;
            public IntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint message;
            public ushort low, high;
        }

        private static bool TrustedNotepadProcess(uint id, out string path)
        {
            path = null;
            if (id == 0) return false;
            try
            {
                using (Process p = Process.GetProcessById((int)id))
                {
                    if (!string.Equals(p.ProcessName, "notepad", StringComparison.OrdinalIgnoreCase))
                        return false;
                    path = Path.GetFullPath(p.MainModule.FileName);
                }
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string system = Path.Combine(windows, "System32", "notepad.exe");
                string legacy = Path.Combine(windows, "notepad.exe");
                string appRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "WindowsApps") + Path.DirectorySeparatorChar;
                return string.Equals(path, system, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, legacy, StringComparison.OrdinalIgnoreCase)
                    || (path.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)
                        && Path.GetFileName(path).Equals("Notepad.exe", StringComparison.OrdinalIgnoreCase)
                        && path.IndexOf("Microsoft.WindowsNotepad_", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { return false; } // permissions or inaccessible process -> fail closed
        }

        internal static bool LooksLikeSampleTitle(string title)
        {
            return !string.IsNullOrEmpty(title)
                && title.IndexOf(SampleFileName, StringComparison.OrdinalIgnoreCase) >= 0
                && title.IndexOf("Notepad", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Title(IntPtr hwnd)
        {
            StringBuilder b = new StringBuilder(512);
            if (hwnd == IntPtr.Zero) return "";
            GetWindowText(hwnd, b, b.Capacity);
            return b.ToString();
        }

        internal static bool TryCaptureForeground(out NotepadTarget target, out string reason)
        {
            target = null;
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) { reason = "No foreground window."; return false; }
            uint id;
            GetWindowThreadProcessId(hwnd, out id);
            string image;
            if (!TrustedNotepadProcess(id, out image))
            {
                reason = "Foreground process is not trusted Notepad.";
                return false;
            }
            string title = Title(hwnd);
            if (!LooksLikeSampleTitle(title))
            {
                reason = "Wrong Notepad tab or test document title.";
                return false;
            }
            target = new NotepadTarget {
                Window = hwnd, ProcessId = id, ExePath = image, WindowTitle = title
            };
            reason = "Bound to trusted Notepad showing the safe test document.";
            return true;
        }

        // Evaluates target state fresh at each command. No other application's handle
        // or window can be substituted after the user binds the test document.
        internal static NoteContext Observe(NotepadTarget target, NoteAction action,
            bool permission, bool listening, float confidence)
        {
            NoteContext x = new NoteContext {
                Action = action, Permission = permission,
                Bound = target != null,
                Listening = listening, Confidence = confidence
            };
            if (target == null) return x;
            IntPtr foreground = GetForegroundWindow();
            x.Foreground = foreground == target.Window;
            uint id;
            GetWindowThreadProcessId(target.Window, out id);
            x.SameWindow = target.Window != IntPtr.Zero && id == target.ProcessId;
            string path;
            x.ProcessTrusted = x.SameWindow && TrustedNotepadProcess(id, out path)
                && string.Equals(path, target.ExePath, StringComparison.OrdinalIgnoreCase);
            x.ExactSampleTitle = LooksLikeSampleTitle(Title(target.Window));
            if (x.Foreground && x.ProcessTrusted && x.ExactSampleTitle)
                x.EditorFocused = IsEditorFocused(target.Window);
            return x;
        }

        private static bool IsEditorFocused(IntPtr targetWindow)
        {
            try
            {
                AutomationElement focus = AutomationElement.FocusedElement;
                if (focus == null) return false;
                AutomationElement current = focus;
                int count = 0;
                bool editor = false;
                while (current != null && count++ < 12)
                {
                    if (new IntPtr(current.Current.NativeWindowHandle) == targetWindow
                        && current.Current.ControlType == ControlType.Window)
                        return editor;
                    // Windows Notepad editor should expose an Edit/Document control.
                    if (current.Current.ControlType == ControlType.Document
                        || current.Current.ControlType == ControlType.Edit)
                        editor = true;
                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }
                return false; // unverifiable focused element -> no OS input
            }
            catch { return false; }
        }

        private static ushort KeyFor(NoteAction action)
        {
            switch (action)
            {
                case NoteAction.Down: return 0x28;
                case NoteAction.Up: return 0x26;
                case NoteAction.Left: return 0x25;
                case NoteAction.Right: return 0x27;
                case NoteAction.Home: return 0x24;
                case NoteAction.End: return 0x23;
                default: return 0;
            }
        }

        // Only arrow, Home or End key. Notepad must STILL be foreground.
        // Returning true confirms synthetic events were sent, not caret movement.
        internal static bool TrySendNavigation(NotepadTarget target, NoteAction action)
        {
            ushort vk = KeyFor(action);
            if (vk == 0 || target == null) return false;
            if (GetForegroundWindow() != target.Window) return false;
            uint pid;
            GetWindowThreadProcessId(target.Window, out pid);
            if (pid != target.ProcessId) return false;
            INPUT[] events = new INPUT[2];
            events[0].type = INPUT_KEYBOARD;
            events[0].union.key.virtualKey = vk;
            events[1].type = INPUT_KEYBOARD;
            events[1].union.key.virtualKey = vk;
            events[1].union.key.flags = KEYEVENTF_KEYUP;
            // Never send a second attempt automatically.
            return SendInput(2, events, Marshal.SizeOf(typeof(INPUT))) == 2;
        }
    }
}
