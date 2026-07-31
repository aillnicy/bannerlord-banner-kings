using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace AillGameplaySuite
{
    internal static class Diagnostics
    {
        private static readonly HashSet<string> Displayed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        internal static void Guard(string key, Action action)
        {
            try { action(); }
            catch (Exception ex) { Report(key, ex, true); }
        }

        internal static void GuardQuiet(string key, Action action)
        {
            try { action(); }
            catch (Exception ex) { Report(key, ex, false); }
        }

        internal static void Report(string key, Exception ex, bool display)
        {
            var signature = key + ":" + ex.GetType().FullName + ":" + ex.Message;
            if (!Reported.Add(signature)) return;
            System.Diagnostics.Trace.WriteLine("[AillGameplaySuite] " + signature + Environment.NewLine + ex);
            if (display) DisplayOnce(signature, "Aill Gameplay Suite disabled one failed operation: " + key, Colors.Red);
        }

        internal static void DisplayOnce(string key, string text, Color color)
        {
            if (!Displayed.Add(key)) return;
            try { InformationManager.DisplayMessage(new InformationMessage(text, color)); }
            catch { }
        }
    }
}
