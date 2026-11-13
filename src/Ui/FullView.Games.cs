// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AdeeboMeter
{
    // Learn › Games: the painted screens. The logic and the made-up networks are in LearnGames; nothing here touches
    // the real network. A one-second timer runs only while a timed game is on screen.
    sealed partial class FullView
    {
        LearnGames.Room room; readonly List<string> roomOut = new List<string>(); DateTime roomStart; int roomHints, roomMiss, roomStage; string roomMsg = "";   // stage: 0 looking, 1 fault found, 2 escaped, 3 out of time / wrong
        LearnGames.Case thief; int thiefClues = 1; string thiefPick = "";
        int detIdx; int detPick = -1;
        LearnGames.Quick quick; string quickMsg = "", quickEarn;
        static readonly StringFormat GameNoWrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        System.Windows.Forms.Timer gameTimer;
        static readonly Random GameRnd = new Random();

        DateTime gameAway = DateTime.MinValue;   // when the game page was left with a clock running: the clock waits for you
        // like Check yourself: right first time earns - an answer you were just shown doesn't
        string EarnFirst(string key) { return Points.Me.Tried(key) ? "" : EarnPts(key, true, true); }
        void GameTick(bool on)
        {
            if (on && gameAway != DateTime.MinValue) { var away = DateTime.UtcNow - gameAway; roomStart += away; outStart += away; if (quick != null) quick.Start += away; gameAway = DateTime.MinValue; }
            if (gameTimer == null) { gameTimer = new System.Windows.Forms.Timer { Interval = 1000 }; FormClosed += delegate { gameTimer.Dispose(); }; gameTimer.Tick += delegate { if (!learnPage.Visible || !Visible || !learnSel.StartsWith("game:", StringComparison.Ordinal)) { gameTimer.Stop(); gameAway = DateTime.UtcNow; return; } learnInfo.Invalidate(); }; }
            if (on && !gameTimer.Enabled) gameTimer.Start(); else if (!on && gameTimer.Enabled) gameTimer.Stop();
        }
        static string Clock2(int secs) { return (secs / 60).ToString(CultureInfo.InvariantCulture) + ":" + (secs % 60).ToString("00", CultureInfo.InvariantCulture); }
        static float GameHead(Graphics g, string id, float x, float y, float w)
        {
            var d = LearnGames.All.First(z => z[0] == id);
            y = Title2(g, L.T(d[1]), x, y, w);
            int done = LearnGames.Keys().Count(k => k.StartsWith(GamePrefix(id), StringComparison.Ordinal) && Points.Me.Has(k)), all = LearnGames.Keys().Count(k => k.StartsWith(GamePrefix(id), StringComparison.Ordinal));
            y = Lead2(g, L.T(d[2]) + (Cfg.Points ? "  ·  " + done + " / " + all + " " + L.T("solved") : "") + "  ·  " + L.T("everything here is made up - nothing real is sent or changed"), x, y, w);
            return y;
        }
        static string GamePrefix(string id) { return id == "room" ? "game.room." : id == "thief" ? "game.thief." : id == "detective" ? "game.detective." : id == "heist" ? "game.heist" : id == "outage" ? "game.outage" : id == "dungeon" ? "game.dungeon" : "game.quick." + id; }
        float Option(Graphics g, RectangleF r, string text, int state, Action a)   // state: 0 open, 1 right, 2 wrong, 3 dimmed
        {
            Theme.Fill(g, r, 10 * S, state == 1 ? Theme.LvSoft(0) : state == 2 ? Theme.LvSoft(2) : Theme.Soft);
            if (state == 1) Theme.Stroke(g, r, 10 * S, Theme.Green, 2);
            using (var tb = new SolidBrush(state == 3 ? Theme.Muted : Theme.Ink)) using (var c = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord })
                g.DrawString(L.T(text), Draw.fBody, tb, new RectangleF(r.X + 12 * S, r.Y, r.Width - 20 * S, r.Height), c);
            if (a != null) Act(learnInfo, r, a);
            return r.Bottom;
        }
        float Options(Graphics g, string[] opts, float x, float y, float w, Func<string, int> state, Action<string> pick)
        {
            float ow = (w - 12 * S) / 2, oh = 44 * S;
            for (int i = 0; i < opts.Length; i++)
            {
                string o = opts[i]; var r = new RectangleF(x + (i % 2) * (ow + 12 * S), y + (i / 2) * (oh + 10 * S), ow, oh);
                Option(g, r, o, state(o), pick == null ? null : (Action)delegate { pick(o); });
            }
            return y + (float)Math.Ceiling(opts.Length / 2.0) * (oh + 10 * S) + 4 * S;
        }
        static float Say(Graphics g, string t, int lv, float x, float y, float w)
        {
            var h = Draw.Measure(L.T(t), Draw.fBodyB, (int)w).Height;
            using (var b = new SolidBrush(lv == 0 ? Theme.LvText(0) : lv == 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString(L.T(t), Draw.fBodyB, b, new RectangleF(x, y, w, h + 4));
            return y + h + 10 * S;
        }

        float PaintGame(Graphics g, string id, float x, float y, float w)
        {
            y = GameHead(g, id, x, y, w);
            switch (id)
            {
                case "room": return PaintRoom(g, x, y, w);
                case "thief": return PaintThief(g, x, y, w);
                case "detective": return PaintDetective(g, x, y, w);
                case "heist": return PaintHeist(g, x, y, w);
                case "outage": return PaintOutage(g, x, y, w);
                case "dungeon": return PaintDungeon(g, x, y, w);
                default: return PaintQuick(g, id, x, y, w);
            }
        }

        // ---------- escape room
        void NewRoom()
        {
            var open = LearnGames.Faults.Where(f => !Points.Me.Has("game.room." + f.Id)).ToList(); if (open.Count == 0) open = LearnGames.Faults.ToList();
            var f0 = open[GameRnd.Next(open.Count)]; room = LearnGames.NewRoom(GameRnd.Next(), f0.Id);
            roomOut.Clear(); roomOut.Add(L.T("Your PC can't get to example.com. Everything below is a simulation - run the commands and find out why.")); roomStart = DateTime.UtcNow; roomHints = 0; roomMiss = 0; roomStage = 0; roomMsg = "";
        }
        int RoomLeft() { return Math.Max(0, 600 - (roomHints + roomMiss) * 60 - (int)(DateTime.UtcNow - roomStart).TotalSeconds); }
        float PaintRoom(Graphics g, float x, float y, float w)
        {
            if (room == null) NewRoom();
            int left = RoomLeft(); if (left == 0 && roomStage < 2) { roomStage = 3; roomMsg = L.T("Time's up. It was") + ": " + room.F.Name + ". " + L.T("Fix") + ": " + room.F.Fix; }
            GameTick(roomStage < 2);
            using (var mb = new SolidBrush(left < 60 && roomStage < 2 ? Theme.LvText(2) : Theme.Ink)) g.DrawString((roomStage >= 2 ? L.T("Done") : Clock2(left) + " " + L.T("left")) + (roomHints > 0 ? "  ·  " + roomHints + " " + L.T(roomHints == 1 ? "hint used" : "hints used") : "") + (roomMiss > 0 ? "  ·  " + roomMiss + " " + L.T(roomMiss == 1 ? "wrong guess" : "wrong guesses") : ""), Draw.fHead, mb, x, y);
            y += 34 * S;
            // the console's commands: buttons that wrap
            float bx = x;
            foreach (var c in LearnGames.Commands)
            {
                string cid = c[0], label = c[1]; var ts = g.MeasureString(label, Draw.fBodyB); float bw = ts.Width + 28 * S;
                if (bx + bw > x + w) { bx = x; y += 40 * S; }
                bx += Button(learnInfo, g, label, false, delegate { if (roomStage >= 2) return; roomOut.Add("> " + label); roomOut.Add(LearnGames.Run(room, cid)); while (roomOut.Count > 30) roomOut.RemoveAt(0); learnInfo.Invalidate(); }, bx, y) + 8 * S;
            }
            y += 44 * S;
            // the console itself: the newest at the bottom, about 22 lines
            var lines = string.Join("\n", roomOut).Split('\n'); var shown = lines.Skip(Math.Max(0, lines.Length - 22)).ToArray();
            float ch = Math.Max(140 * S, shown.Length * 17 * S + 20 * S); var box = new RectangleF(x, y, w, ch);
            using (var bg = new SolidBrush(Color.FromArgb(24, 28, 35))) g.FillRectangle(bg, box);
            using (var tb = new SolidBrush(Color.FromArgb(214, 222, 232))) using (var hb = new SolidBrush(Color.FromArgb(120, 200, 140)))
                for (int i = 0; i < shown.Length; i++) g.DrawString(shown[i], Draw.fMono, shown[i].StartsWith("> ", StringComparison.Ordinal) ? hb : tb, new RectangleF(x + 10 * S, y + 10 * S + i * 17 * S, w - 20 * S, 17 * S), GameNoWrap);
            y += ch + 12 * S;
            if (roomStage == 0)
            {
                float hb2 = Button(learnInfo, g, roomHints == 0 ? "Hint (-1 min)" : roomHints == 1 ? "Another hint (-1 min)" : "No more hints", false, delegate { if (roomHints < 2) { roomOut.Add("HINT: " + L.T(roomHints == 0 ? room.F.Hint1 : room.F.Hint2)); roomHints++; learnInfo.Invalidate(); } }, x, y);
                Button(learnInfo, g, "New scenario", false, delegate { NewRoom(); learnInfo.Invalidate(); }, x + hb2 + 10 * S, y);
                y += 46 * S;
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("WHAT'S BROKEN?"), Draw.fTiny, mb, x, y); y += 20 * S;
                y = Options(g, room.FaultChoices, x, y, w, o => 0, o => { if (o == room.F.Name) { roomStage = 1; roomMsg = L.T("Right. Now - how do you fix it?"); } else { roomMiss++; roomMsg = L.T("Not that - it costs a minute. Look again."); } learnInfo.Invalidate(); });
            }
            else if (roomStage == 1)
            {
                y = Say(g, room.F.Name, 0, x, y, w);
                using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("THE FIX?"), Draw.fTiny, mb, x, y); y += 20 * S;
                y = Options(g, room.FixChoices, x, y, w, o => 0, o =>
                {
                    if (o == room.F.Fix) { roomStage = 2; string earned = roomMiss >= 3 ? "" : EarnFirst("game.room." + room.F.Id);   /* clicking through every option isn't solving it */ roomMsg = L.T("You escaped in") + " " + Clock2((int)(DateTime.UtcNow - roomStart).TotalSeconds) + (roomHints + roomMiss > 0 ? " (" + L.T("the clock lost") + " " + (roomHints + roomMiss) + " " + L.T("min to hints and guesses") + ")" : "") + ". " + earned; }
                    else { roomMiss++; roomMsg = L.T("That wouldn't fix it - another minute gone."); }
                    learnInfo.Invalidate();
                });
            }
            if (roomMsg != "") y = Say(g, roomMsg, roomStage == 2 ? 0 : roomStage == 3 ? 2 : 1, x, y, w);
            if (roomStage >= 2) { y = Lead2(g, L.T("The fix") + ": " + L.T(room.F.Fix), x, y, w); Button(learnInfo, g, "Next room", true, delegate { NewRoom(); learnInfo.Invalidate(); }, x, y); y += 46 * S; }
            return y;
        }

        // ---------- catch the thief
        void NewThief()
        {
            var open = LearnGames.ThiefCases.Where(c => !Points.Me.Has("game.thief." + c)).ToList(); if (open.Count == 0) open = LearnGames.ThiefCases.ToList();
            thief = LearnGames.NewCase(GameRnd.Next(), open[GameRnd.Next(open.Count)]); thiefClues = 1; thiefPick = "";
        }
        float PaintThief(Graphics g, float x, float y, float w)
        {
            if (thief == null) NewThief(); GameTick(false);
            y = Say(g, thief.Story, 1, x, y, w);
            using (var mb = new SolidBrush(Theme.Muted)) using (var ink = new SolidBrush(Theme.Ink)) using (var sub = new SolidBrush(Theme.Sub))
            using (var nw = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(L.T("DEVICES ON THE NETWORK  ·  CLICK ONE TO ACCUSE IT"), Draw.fTiny, mb, x, y); y += 22 * S;
                float[] cols = { 0, 0.22f, 0.37f, 0.60f, 0.75f, 0.89f }; string[] heads = { "Name", "Address", "Hardware address", "Maker", "First seen", "Data" };
                for (int i = 0; i < heads.Length; i++) g.DrawString(L.T(heads[i]), Draw.fSmall, mb, x + cols[i] * w, y); y += 22 * S;
                foreach (var d in thief.Devices)
                {
                    var rr = new RectangleF(x - 6 * S, y - 3 * S, w + 12 * S, 26 * S);
                    bool picked = thiefPick == d.Mac, done = thiefPick != "";
                    if (done && d.Thief) using (var b = new SolidBrush(Theme.LvSoft(0))) g.FillRectangle(b, rr);
                    else if (picked) using (var b = new SolidBrush(Theme.LvSoft(2))) g.FillRectangle(b, rr);
                    string[] cells = { d.Name, d.Ip, d.Mac, d.Maker, d.First, d.Data };
                    for (int i = 0; i < cells.Length; i++) g.DrawString(cells[i], i == 2 ? Draw.fMono : Draw.fBody, i == 0 ? ink : sub, new RectangleF(x + cols[i] * w, y, (i + 1 < cols.Length ? cols[i + 1] : 1f) * w - cols[i] * w - 6 * S, 20 * S), nw);
                    if (!done && d.Name != "Router") { var dd = d; Act(learnInfo, rr, delegate { thiefPick = dd.Mac; if (dd.Thief) EarnFirst("game.thief." + thief.Id); else Points.Me.MarkTried("game.thief." + thief.Id); learnInfo.Invalidate(); }); }
                    y += 26 * S;
                }
                y += 10 * S;
                if (thief.Log.Count > 0)
                {
                    g.DrawString(L.T("THE ROUTER'S LOG"), Draw.fTiny, mb, x, y); y += 20 * S;
                    foreach (var l in thief.Log) { g.DrawString(l, Draw.fMono, sub, new RectangleF(x, y, w, 18 * S), nw); y += 19 * S; }
                    y += 10 * S;
                }
                g.DrawString(L.T("CLUES"), Draw.fTiny, mb, x, y); y += 20 * S;
                for (int i = 0; i < thiefClues && i < thief.Clues.Length; i++) y = Lead2(g, (i + 1) + ". " + L.T(thief.Clues[i]), x, y, w);
            }
            if (thiefPick == "")
            {
                if (thiefClues < thief.Clues.Length) { Button(learnInfo, g, "Another clue", false, delegate { thiefClues++; learnInfo.Invalidate(); }, x, y); y += 46 * S; }
            }
            else
            {
                var t = thief.Devices.First(d => d.Thief); bool right = thief.Devices.Any(d => d.Mac == thiefPick && d.Thief);
                y = Say(g, right ? L.T("Caught!") + " " + t.Name + "." : L.T("Not them - the thief was") + " " + t.Name + ".", right ? 0 : 2, x, y, w);
                y = Lead2(g, L.T(thief.Explain), x, y, w);
                Button(learnInfo, g, "Next case", true, delegate { NewThief(); learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }

        // ---------- packet detective
        float PaintDetective(Graphics g, float x, float y, float w)
        {
            GameTick(false); var p = LearnGames.Detective[detIdx % LearnGames.Detective.Length]; int wrong = int.Parse(p[2], CultureInfo.InvariantCulture);
            y = Say(g, (detIdx % LearnGames.Detective.Length + 1) + " / " + LearnGames.Detective.Length + "  ·  " + L.T(p[0]), 1, x, y, w);
            using (var mb = new SolidBrush(Theme.Muted)) g.DrawString(L.T("ONE OF THESE PACKETS IS WRONG - CLICK IT"), Draw.fTiny, mb, x, y); y += 22 * S;
            var pk = p[1].Split('~');
            using (var sub = new SolidBrush(Theme.Sub)) using (var ink = new SolidBrush(Theme.Ink))
                for (int i = 0; i < pk.Length; i++)
                {
                    var f = pk[i].Split('|'); var r = new RectangleF(x, y, w, 40 * S);
                    int st = detPick < 0 ? 0 : i == wrong ? 1 : i == detPick ? 2 : 3;
                    Theme.Fill(g, r, 10 * S, st == 1 ? Theme.LvSoft(0) : st == 2 ? Theme.LvSoft(2) : Theme.Soft);
                    g.DrawString(f[0] + " s", Draw.fMono, sub, x + 12 * S, y + 11 * S);
                    g.DrawString(f[1], Draw.fBodyB, ink, x + 90 * S, y + 10 * S);
                    g.DrawString(L.T(f[2]), Draw.fBody, ink, new RectangleF(x + 260 * S, y + 10 * S, w - 270 * S, 22 * S), GameNoWrap);
                    int ii = i; if (detPick < 0) Act(learnInfo, r, delegate { detPick = ii; string dk = "game.detective." + (detIdx % LearnGames.Detective.Length + 1).ToString(CultureInfo.InvariantCulture); if (ii == wrong) EarnFirst(dk); else Points.Me.MarkTried(dk); learnInfo.Invalidate(); });
                    y += 48 * S;
                }
            if (detPick >= 0)
            {
                y = Say(g, detPick == wrong ? "Spotted." : "Not that one - the green one is wrong.", detPick == wrong ? 0 : 2, x, y + 4 * S, w);
                y = Lead2(g, L.T(p[3]), x, y, w);
                Button(learnInfo, g, "Next puzzle", true, delegate { detIdx++; detPick = -1; learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }

        // ---------- break the Adeebo Award: how the card's claim code works, told as ten attacks - and the two that do get through
        int heistIdx, heistPick = -1, heistRight; string heistEarn; static readonly string[] HeistOpts = { "It works", "It fails" };
        float PaintHeist(Graphics g, float x, float y, float w)
        {
            GameTick(false);
            if (heistIdx == 0 && heistPick < 0)
            {
                y = Lead2(g, L.T("How a card's claim code is made - and checked:"), x, y, w);
                y = DrawKnowledgeLadder(g, learnInfo, L.T("AdeeboMeter > AdeeboMeter: name + points + date, mixed with a secret key (HMAC-SHA256)") + " | " + L.T("AdeeboMeter > You: your card + its code") + " | " + L.T("You > Hall of Fame: the card") + " | " + L.T("Hall of Fame > Hall of Fame: same mix, same key - same code? You're in"), x, y, w);
            }
            if (heistIdx >= LearnGames.Heist.Length)
            {
                bool won = heistRight >= LearnGames.HeistWin;
                if (heistEarn == null) heistEarn = won ? EarnFirst("game.heist") : "";
                y = Say(g, (won ? L.T("You know the Adeebo Award inside out") : L.T("Mallory fooled you a few times")) + " - " + heistRight + " / " + LearnGames.Heist.Length + ". " + heistEarn, won ? 0 : 2, x, y, w);
                y = Lead2(g, L.T("The code stops the easy cheats. The honest limit of an offline app: points live on your own PC, so the Hall of Fame is for bragging rights, not prizes."), x, y, w);
                if (!won) Points.Me.MarkTried("game.heist");
                Button(learnInfo, g, "Play again", true, delegate { heistIdx = 0; heistPick = -1; heistRight = 0; heistEarn = null; learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            var h = LearnGames.Heist[heistIdx]; bool works = h[1] == "1";
            y = Say(g, (heistIdx + 1) + " / " + LearnGames.Heist.Length + "  ·  " + L.T(h[0]), 1, x, y, w);
            string answer = works ? "It works" : "It fails", picked = heistPick == 0 ? "It works" : "It fails";
            y = Options(g, HeistOpts, x, y, w, o => heistPick < 0 ? 0 : o == answer ? 1 : o == picked ? 2 : 3,
                heistPick >= 0 ? (Action<string>)null : o => { heistPick = o == "It works" ? 0 : 1; if ((heistPick == 0) == works) heistRight++; learnInfo.Invalidate(); });
            if (heistPick >= 0)
            {
                bool right = (heistPick == 0) == works;
                y = Say(g, right ? (works ? L.T("Right - this one gets through.") : L.T("Right - it fails.")) : (works ? L.T("Not quite - this one actually works.") : L.T("Not quite - it fails.")), right ? 0 : 2, x, y, w);
                y = Lead2(g, L.T(h[2]), x, y, w);
                Button(learnInfo, g, heistIdx + 1 < LearnGames.Heist.Length ? "Next attack" : "See how you did", true, delegate { heistIdx++; heistPick = -1; learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }

        // ---------- survive the outage: eight made-up broken networks, fifteen seconds each; right and fast scores more
        List<int> outOrder; int outAt, outRight, outScore, outPick = -1; DateTime outStart; string outEarn; string[] outOpts;
        void NewOutage() { outOrder = Enumerable.Range(0, LearnGames.Outage.Length).OrderBy(i => GameRnd.Next()).Take(LearnGames.OutageRounds).ToList(); outAt = 0; outRight = 0; outScore = 0; outPick = -1; outEarn = null; OutRound(); }
        void OutRound() { if (outAt < outOrder.Count) { var o = LearnGames.Outage[outOrder[outAt]]; outOpts = new[] { o[3], o[4], o[5], o[6] }.OrderBy(z => GameRnd.Next()).ToArray(); } outStart = DateTime.UtcNow; outPick = -1; }
        float PaintOutage(Graphics g, float x, float y, float w)
        {
            if (outOrder == null)
            {
                GameTick(false);
                y = Lead2(g, L.T("Your widget turns red. Fifteen seconds to say why. Right answers score 100, plus 10 for every second left. Six right survives."), x, y, w);
                if (Cfg.OutageBest > 0) y = Lead2(g, L.T("Your best:") + " " + Cfg.OutageBest, x, y, w);
                Button(learnInfo, g, "Start", true, delegate { NewOutage(); learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            if (outAt >= outOrder.Count)
            {
                GameTick(false);
                bool won = outRight >= LearnGames.OutageWin, best = outScore > Cfg.OutageBest;
                if (outEarn == null) { outEarn = won ? EarnFirst("game.outage") : ""; if (!won) Points.Me.MarkTried("game.outage"); if (best) { Cfg.OutageBest = outScore; Cfg.Save(); } }
                y = Say(g, (won ? Voice.P("You survived. ", "Passed. ") : Voice.P("The outage won this time. ", "Not passed. ")) + outRight + " / " + outOrder.Count + "  ·  " + outScore + " " + L.T("points of speed") + ". " + outEarn, won ? 0 : 2, x, y, w);
                if (outScore >= Cfg.OutageBest && outScore > 0) y = Lead2(g, L.T("A new personal best - kept on this PC."), x, y, w);
                Button(learnInfo, g, "Play again", true, delegate { NewOutage(); learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            var o = LearnGames.Outage[outOrder[outAt]];
            int left = Math.Max(0, LearnGames.OutageSecs - (int)(DateTime.UtcNow - outStart).TotalSeconds);
            if (left == 0 && outPick < 0) outPick = 99;   // out of time: counts as wrong
            GameTick(outPick < 0);
            using (var mb = new SolidBrush(left <= 5 && outPick < 0 ? Theme.LvText(2) : Theme.Ink)) g.DrawString((outPick < 0 ? left + " s" : L.T("Done")) + "  ·  " + (outAt + 1) + " / " + outOrder.Count + "  ·  " + outScore, Draw.fHead, mb, x, y);
            y += 40 * S;
            var snap = new Snap(); snap.Path = o[0].Split(',').Select(z => int.Parse(z, CultureInfo.InvariantCulture)).ToArray();
            Draw.PathStrip(g, snap, new RectangleF(x, y + 6 * S, w - (Cfg.Buddy ? 70 * S : 0), 60 * S), false);
            if (Cfg.Buddy) Draw.Buddy(g, new RectangleF(x + w - 52 * S, y, 46 * S, 46 * S), snap, false);
            y += 66 * S;
            y = Lead2(g, "• " + L.T(o[1]) + "\n• " + L.T(o[2]), x, y, w);
            y = Options(g, outOpts, x, y, w, z => outPick < 0 ? 0 : z == o[3] ? 1 : outPick < 4 && outOpts[outPick] == z ? 2 : 3,
                outPick >= 0 ? (Action<string>)null : z => { if ((DateTime.UtcNow - outStart).TotalSeconds >= LearnGames.OutageSecs) { outPick = 99; learnInfo.Invalidate(); return; }   /* the clock ran out between two ticks: too late */
                    outPick = Array.IndexOf(outOpts, z); if (z == o[3]) { outRight++; outScore += 100 + 10 * Math.Max(0, LearnGames.OutageSecs - (int)(DateTime.UtcNow - outStart).TotalSeconds); } learnInfo.Invalidate(); });
            if (outPick >= 0)
            {
                bool right = outPick < 4 && outOpts[outPick] == o[3];
                y = Say(g, right ? Voice.P("Called it.", "Right.") : outPick == 99 ? L.T("Out of time.") : L.T("Not this time."), right ? 0 : 2, x, y, w);
                y = Lead2(g, L.T(o[7]), x, y, w);
                Button(learnInfo, g, outAt + 1 < outOrder.Count ? "Next outage" : "See how you did", true, delegate { outAt++; OutRound(); learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }

        // ---------- the packet dungeon: the Theory of Everything, one room per layer; three hearts; every room leaves a scroll
        int dunRoom, dunHearts = LearnGames.DungeonHearts, dunPick = -1; string dunEarn; string[] dunOpts; bool dunStarted;
        void DunOpts() { var d = LearnGames.Dungeon[dunRoom]; dunOpts = new[] { d[2], d[3], d[4] }.OrderBy(z => GameRnd.Next()).ToArray(); dunPick = -1; }
        float PaintDungeon(Graphics g, float x, float y, float w)
        {
            GameTick(false);
            if (!dunStarted)
            {
                y = Lead2(g, L.T("Eight rooms stand between you and the treasure - from the cable at the bottom to the encrypted vault at the top. Each has a guardian with a riddle. A wrong answer costs a heart; lose all three and you start again."), x, y, w);
                Button(learnInfo, g, "Enter the dungeon", true, delegate { dunStarted = true; dunRoom = 0; dunHearts = LearnGames.DungeonHearts; dunEarn = null; DunOpts(); learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            // the map: one square per room, cleared ones green
            float sq = Math.Min(34 * S, (w - 7 * 8 * S) / 8);
            for (int i = 0; i < LearnGames.Dungeon.Length; i++)
            {
                var r = new RectangleF(x + i * (sq + 8 * S), y, sq, sq);
                Theme.Fill(g, r, 6 * S, i < dunRoom ? Theme.LvSoft(0) : i == dunRoom ? Theme.Blue : Theme.Soft);
                using (var tb = new SolidBrush(i == dunRoom ? Color.White : Theme.Ink)) using (var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(i < dunRoom ? "✓" : (i + 1).ToString(CultureInfo.InvariantCulture), Draw.fBodyB, tb, r, c);
            }
            float hx = x + LearnGames.Dungeon.Length * (sq + 8 * S) + 10 * S;   // hearts: full ones red, lost ones grey (one glyph - the outline heart is missing from some fonts)
            for (int i = 0; i < LearnGames.DungeonHearts; i++)
                using (var hb = new SolidBrush(i < dunHearts ? Theme.Red : Theme.Track))
                {
                    float hs = 22 * S, h0 = hx + i * (hs + 8 * S), v0 = y + (sq - hs) / 2;   // drawn, not a font glyph: the same size on every PC
                    g.FillEllipse(hb, h0, v0, hs * 0.55f, hs * 0.55f); g.FillEllipse(hb, h0 + hs * 0.45f, v0, hs * 0.55f, hs * 0.55f);
                    g.FillPolygon(hb, new[] { new PointF(h0 + hs * 0.02f, v0 + hs * 0.36f), new PointF(h0 + hs * 0.98f, v0 + hs * 0.36f), new PointF(h0 + hs / 2, v0 + hs * 0.95f) });
                }
            y += sq + 16 * S;
            if (dunRoom >= LearnGames.Dungeon.Length)
            {
                if (dunEarn == null) dunEarn = EarnFirst("game.dungeon");
                y = Say(g, Voice.P("The treasure is yours: a packet that made it all the way from the cable to the vault - just like every packet you send. ", "All eight rooms cleared. ") + dunEarn, 0, x, y, w);
                Button(learnInfo, g, "Play again", true, delegate { dunStarted = false; learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            if (dunHearts <= 0)
            {
                if (dunEarn == null) { Points.Me.MarkTried("game.dungeon"); dunEarn = ""; }
                y = Say(g, Voice.P("Out of hearts in " + L.T(LearnGames.Dungeon[dunRoom][0]) + ". The dungeon keeps its treasure - for now.", "Out of hearts."), 2, x, y, w);
                Button(learnInfo, g, "Try again", true, delegate { dunStarted = false; learnInfo.Invalidate(); }, x, y); return y + 46 * S;
            }
            var d = LearnGames.Dungeon[dunRoom];
            y = Say(g, L.T(d[0]), 1, x, y, w);
            y = Lead2(g, L.T(d[1]), x, y, w);
            int right = Array.IndexOf(dunOpts, d[2]);
            y = Options(g, dunOpts, x, y, w, z => dunPick < 0 ? 0 : z == d[2] ? (dunPick == right ? 1 : 3) : dunOpts[dunPick] == z ? 2 : 3,
                dunPick >= 0 && dunPick == right ? (Action<string>)null : z => { int at = Array.IndexOf(dunOpts, z); if (at == dunPick) return; dunPick = at; if (dunPick != right) dunHearts--; learnInfo.Invalidate(); });
            if (dunPick >= 0)
            {
                bool ok = dunPick == right;
                y = Say(g, ok ? Voice.P("The guardian steps aside.", "Right.") : Voice.P("Ouch - a heart lost. Try again.", "Not right - one heart lost."), ok ? 0 : 2, x, y, w);
                if (ok)
                {
                    y = Lead2(g, L.T(d[5]), x, y, w);
                    string topic = d[6]; var tk = Knowledge.Learn().FirstOrDefault(t => t.Id == topic);
                    if (tk != null)
                    {
                        string sc = L.T("Scroll found:") + " " + L.T(tk["title"]) + "  ›"; var ss = g.MeasureString(sc, Draw.fBodyB);
                        using (var lb = new SolidBrush(Theme.Dark ? Theme.BlueDark : Theme.Blue)) g.DrawString(sc, Draw.fBodyB, lb, x, y);
                        Act(learnInfo, new RectangleF(x, y, ss.Width, ss.Height), delegate { learnSel = "topic:" + topic; learnPkts = null; learnInfo.AutoScrollPosition = Point.Empty; learnList.Invalidate(); learnInfo.Invalidate(); });
                        y += ss.Height + 10 * S;
                    }
                    Button(learnInfo, g, dunRoom + 1 < LearnGames.Dungeon.Length ? "Next room" : "Open the treasure", true, delegate { dunRoom++; if (dunRoom < LearnGames.Dungeon.Length) DunOpts(); learnInfo.Invalidate(); }, x, y); y += 46 * S;
                }
            }
            return y;
        }

        // ---------- quick-fire
        float PaintQuick(Graphics g, string id, float x, float y, float w)
        {
            if (quick == null || quick.Kind != id) { quick = null; quickMsg = ""; }
            if (quick == null)
            {
                GameTick(false);
                y = Lead2(g, L.T("Ten questions, sixty seconds. Eight right within the minute wins."), x, y, w);
                Button(learnInfo, g, "Start", true, delegate { quick = LearnGames.NewQuick(id, GameRnd.Next()); quickMsg = ""; quickEarn = null; learnInfo.Invalidate(); }, x, y);
                return y + 46 * S;
            }
            int left = LearnGames.SecondsLeft(quick); bool over = quick.At >= quick.Q.Count || left == 0;
            GameTick(!over);
            using (var mb = new SolidBrush(left < 10 && !over ? Theme.LvText(2) : Theme.Ink)) g.DrawString((over ? L.T("Done") : left + " s") + "  ·  " + Math.Min(quick.At, quick.Q.Count) + " / " + quick.Q.Count + "  ·  " + quick.Right + " " + L.T("right"), Draw.fHead, mb, x, y);
            y += 36 * S;
            if (!over)
            {
                var q = quick.Q[quick.At];
                y = Say(g, q[0], 1, x, y, w);
                y = Options(g, q.Skip(2).ToArray(), x, y, w, o => 0, o => { bool ok = o == q[1]; if (ok) quick.Right++; quickMsg = ok ? L.T("Right") : L.T("No") + " - " + L.T(q[1]); quick.At++; learnInfo.Invalidate(); });
                if (quickMsg != "") using (var b = new SolidBrush(quickMsg == L.T("Right") ? Theme.LvText(0) : Theme.LvText(2))) g.DrawString(quickMsg, Draw.fSmall, b, x, y);
                y += 26 * S;
            }
            else
            {
                bool won = LearnGames.Won(quick); if (quickEarn == null) quickEarn = won ? EarnPts("game.quick." + id, true, true) : ""; string earned = quickEarn;
                y = Say(g, won ? L.T("You won") + " - " + quick.Right + " / 10. " + earned : quick.Right + " / 10" + (left == 0 && quick.At < quick.Q.Count ? " - " + L.T("out of time") : "") + ". " + L.T("Eight wins - try again."), won ? 0 : 2, x, y, w);
                Button(learnInfo, g, "Play again", true, delegate { quick = LearnGames.NewQuick(id, GameRnd.Next()); quickMsg = ""; quickEarn = null; learnInfo.Invalidate(); }, x, y); y += 46 * S;
            }
            return y;
        }
    }
}
