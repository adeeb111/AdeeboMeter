using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AdeeboMeter
{
    // The first paint of a page in a fresh start was 250-570 ms on a real PC (warm: 10-43 ms): nearly all of it is the
    // runtime compiling the drawing code the first time it runs. A low-priority thread compiles it right after start,
    // off the window thread, so the first look at a page is as quick as the second. Once per start; a few hundred ms of one core.
    static class Warm
    {
        public static void Start()
        {
            var t = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "warm" };
            t.Start();
        }
        static void Run()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew(); int n = 0;
            foreach (var ty in new[] { typeof(FullView), typeof(Theme), typeof(Draw), typeof(Kit), typeof(Glyphs), typeof(Typing) })
                foreach (var m in ty.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters) continue;
                    try { RuntimeHelpers.PrepareMethod(m.MethodHandle); n++; } catch (Exception e) { if (!(e is ArgumentException || e is TypeLoadException || e is BadImageFormatException)) throw; }
                }
            Program.Log("warm: " + n + " methods in " + sw.ElapsedMilliseconds + " ms");
        }
    }
}
