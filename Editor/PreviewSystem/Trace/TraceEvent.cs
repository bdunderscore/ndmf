using System;
using System.Threading;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.preview.trace
{
    public enum TraceEventLevel
    {
        /// <summary>
        /// Events at TRACE will be hidden unless they have child events
        /// </summary>
        Trace,
        Info,
    }
    
    public struct TraceEvent
    {
        public long Timestamp;
        public long EditorFrame;
        
        public string EventType;
        public Func<TraceEvent, string> FormatEvent;
        public object Arg0, Arg1, Arg2;
        
        [CanBeNull] public string FilePath;

        public string Filename
        {
            get
            {
                if (FilePath == null) return "???";
                
                var lastSlash = FilePath.LastIndexOf('/');
                return lastSlash >= 0 ? FilePath.Substring(lastSlash + 1) : FilePath;
            }
        }
        public int? Line;
        
        public long EventId;
        public long? ParentEventId;
        
        public TraceEventLevel Level;

        /// <summary>
        /// Enters a dynamic AsyncLocal scope in which this event is the parent event. The scope stack will be restored
        /// to its original state on Dispose.
        /// </summary>
        /// <returns></returns>
        public TraceScope Scope()
        {
            return new TraceScope(EventId);
        }

        /// <summary>
        /// Enters a dynamic AsyncLocal scope in which the specified event ID is the parent event. The scope stack will
        /// be restored to its original state on Dispose.
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public static TraceScope Scope(long eventId)
        {
            return new TraceScope(eventId);
        }
        
        public long? CurrentEventId => TraceScope.CurrentTraceEvent.Value;
    }
    
    public class TraceScope : IDisposable
    {
        internal static AsyncLocal<long?> CurrentTraceEvent = new();
        
        private readonly long? _previousEventId;
        
        public TraceScope(long eventId)
        {
            _previousEventId = CurrentTraceEvent.Value;
            CurrentTraceEvent.Value = eventId >= 0 ? eventId : null;
        }

        public void Dispose()
        {
            CurrentTraceEvent.Value = _previousEventId;
        }
    }
}