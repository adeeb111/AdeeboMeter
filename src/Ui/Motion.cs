// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;

namespace AdeeboMeter
{
    // ================================================================== may the app animate right now? (23 Nov motion review)
    // Windows' own "Show animations in Windows" switch (Settings › Accessibility › Visual effects - SPI_GETCLIENTAREAANIMATION,
    // not the older UI-effects flag), and never in Penny mode: the lightest mode keeps only instant state changes.
    static class Motion
    {
        static bool cached = true; static DateTime at = DateTime.MinValue;
        public static bool On
        {
            get
            {
                if (Cfg.Preset == "penny") return false;
                if ((DateTime.UtcNow - at).TotalSeconds < 10) return cached;   // read at most every 10 s - paints ask often
                at = DateTime.UtcNow;
                try { bool v; if (Native.SystemParametersInfo(0x1042, 0, out v, 0)) cached = v; } catch (EntryPointNotFoundException) { cached = true; } catch (DllNotFoundException) { cached = true; }
                return cached;
            }
        }
    }
}
