// mono stub: System.Diagnostics.Eventing.Reader (Windows-only in .NET Framework's System.Core). Throws EventLogException at use.
namespace System.Diagnostics.Eventing.Reader {
  public class EventLogException : Exception { public EventLogException(string m) : base(m) { } }
  public enum PathType { LogName = 1, FilePath = 2 }
  public class EventLogQuery { public EventLogQuery(string p, PathType t, string q) { } public bool ReverseDirection { get; set; } }
  public class EventRecord : IDisposable { public DateTime? TimeCreated { get { return null; } } public int Id { get { return 0; } } public string ProviderName { get { return ""; } } public string FormatDescription() { return ""; } public void Dispose() { } }
  public class EventLogReader : IDisposable { public EventLogReader(EventLogQuery q) { throw new EventLogException("mono stub: no Windows event log"); } public EventRecord ReadEvent() { return null; } public void Dispose() { } }
}
