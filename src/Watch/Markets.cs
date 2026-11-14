// AdeeboMeter - part of the source; see src/Program.cs for the overview and docs/THEORY-OF-EVERYTHING.md for the story.
using System;

namespace AdeeboMeter
{
    // ================================================================== Market days (PLAN-modes.md 3)
    // When the US (NYSE/Nasdaq) and Toronto (TSX) exchanges are open, worked out from the published holiday rules - no
    // download, no list to keep up to date. Regular hours 9:30-16:00 Eastern; the half days close at 13:00.
    // The exchanges can add a one-off closing (a national day of mourning, a storm): those aren't known here.
    static class Markets
    {
        // "us" or "ca": the holiday's name on that day, or null on a trading day (weekends return "Weekend")
        public static string Holiday(DateTime day, string market)
        {
            var d = day.Date; int y = d.Year;
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return "Weekend";
            var easter = Easter(y);
            if (d == easter.AddDays(-2)) return "Good Friday";
            if (market == "us")
            {
                // New Year's Day on a Saturday is not moved back to Friday (the year-end close stays open)
                if (d == UsObserved(new DateTime(y, 1, 1)) && d.Month == 1) return "New Year's Day";
                if (d == NthWeekday(y, 1, DayOfWeek.Monday, 3)) return "Martin Luther King Jr. Day";
                if (d == NthWeekday(y, 2, DayOfWeek.Monday, 3)) return "Presidents' Day";
                if (d == LastWeekday(y, 5, DayOfWeek.Monday)) return "Memorial Day";
                if (y >= 2022 && d == UsObserved(new DateTime(y, 6, 19))) return "Juneteenth";
                if (d == UsObserved(new DateTime(y, 7, 4))) return "Independence Day";
                if (d == NthWeekday(y, 9, DayOfWeek.Monday, 1)) return "Labor Day";
                if (d == NthWeekday(y, 11, DayOfWeek.Thursday, 4)) return "Thanksgiving";
                if (d == UsObserved(new DateTime(y, 12, 25))) return "Christmas";
                return null;
            }
            // Toronto: a holiday on a weekend moves to the next weekday; Christmas and Boxing Day move together
            if (d == CaObserved(new DateTime(y, 1, 1))) return "New Year's Day";
            if (d == NthWeekday(y, 2, DayOfWeek.Monday, 3)) return "Family Day";
            var may25 = new DateTime(y, 5, 25); var victoria = may25.AddDays(-(((int)may25.DayOfWeek + 6) % 7 == 0 ? 7 : ((int)may25.DayOfWeek + 6) % 7));
            if (d == victoria) return "Victoria Day";
            if (d == CaObserved(new DateTime(y, 7, 1))) return "Canada Day";
            if (d == NthWeekday(y, 8, DayOfWeek.Monday, 1)) return "Civic Holiday";
            if (d == NthWeekday(y, 9, DayOfWeek.Monday, 1)) return "Labour Day";
            if (d == NthWeekday(y, 10, DayOfWeek.Monday, 2)) return "Thanksgiving";
            DateTime xmas, boxing; ChristmasPair(y, out xmas, out boxing);
            if (d == xmas) return "Christmas";
            if (d == boxing) return "Boxing Day";
            return null;
        }
        // the close on a trading day: 13:00 on the half days, 16:00 otherwise
        public static TimeSpan Close(DateTime day, string market)
        {
            var d = day.Date; int y = d.Year;
            if (market == "us")
            {
                if (d == NthWeekday(y, 11, DayOfWeek.Thursday, 4).AddDays(1)) return new TimeSpan(13, 0, 0);   // the day after Thanksgiving
                if (d.Month == 12 && d.Day == 24) return new TimeSpan(13, 0, 0);
                if (d.Month == 7 && d.Day == 3 && UsObserved(new DateTime(y, 7, 4)).Day == 4) return new TimeSpan(13, 0, 0);
            }
            else if (d.Month == 12 && (d.Day == 24 || d.Day == 31)) return new TimeSpan(13, 0, 0);   // Toronto: Christmas Eve and New Year's Eve
            return new TimeSpan(16, 0, 0);
        }
        public static bool Open(DateTime et, string market)
        {
            if (Holiday(et, market) != null) return false;
            var t = et.TimeOfDay; return t >= new TimeSpan(9, 30, 0) && t < Close(et, market);
        }
        // a line for the page: "US markets closed today (Thanksgiving) · Toronto open" - empty on a plain trading day
        public static string TodayNote(DateTime et)
        {
            string us = Holiday(et, "us"), ca = Holiday(et, "ca");
            if (us == "Weekend") return "";
            string a = us != null ? L.T("US markets closed today") + " (" + L.T(us) + ")" : Close(et, "us").Hours == 13 ? L.T("US markets close early today, 13:00 Eastern") : "";
            string b = ca != null ? L.T("Toronto closed today") + " (" + L.T(ca) + ")" : Close(et, "ca").Hours == 13 ? L.T("Toronto closes early today, 13:00 Eastern") : "";
            if (a == "" && b == "") return "";
            if (a != "" && b == "") b = L.T("Toronto open");
            if (b != "" && a == "") a = L.T("US markets open");
            return a + " · " + b;
        }

        static DateTime UsObserved(DateTime h) { return h.DayOfWeek == DayOfWeek.Saturday ? h.AddDays(-1) : h.DayOfWeek == DayOfWeek.Sunday ? h.AddDays(1) : h; }
        static DateTime CaObserved(DateTime h) { return h.DayOfWeek == DayOfWeek.Saturday ? h.AddDays(2) : h.DayOfWeek == DayOfWeek.Sunday ? h.AddDays(1) : h; }
        static void ChristmasPair(int y, out DateTime xmas, out DateTime boxing)
        {
            xmas = new DateTime(y, 12, 25); boxing = new DateTime(y, 12, 26);
            if (xmas.DayOfWeek == DayOfWeek.Saturday) { xmas = xmas.AddDays(2); boxing = boxing.AddDays(2); }        // Mon 27, Tue 28
            else if (xmas.DayOfWeek == DayOfWeek.Sunday) { xmas = xmas.AddDays(1); boxing = boxing.AddDays(1); }     // Mon 26, Tue 27
            else if (boxing.DayOfWeek == DayOfWeek.Saturday) boxing = boxing.AddDays(2);                               // Fri 25, Mon 28
        }
        static DateTime NthWeekday(int y, int month, DayOfWeek dow, int n)
        {
            var d = new DateTime(y, month, 1); while (d.DayOfWeek != dow) d = d.AddDays(1); return d.AddDays(7 * (n - 1));
        }
        static DateTime LastWeekday(int y, int month, DayOfWeek dow)
        {
            var d = new DateTime(y, month, DateTime.DaysInMonth(y, month)); while (d.DayOfWeek != dow) d = d.AddDays(-1); return d;
        }
        // Easter Sunday (the Gregorian computus)
        public static DateTime Easter(int y)
        {
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(y, month, day);
        }
    }
}
