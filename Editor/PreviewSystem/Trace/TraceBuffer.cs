using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace nadena.dev.ndmf.preview.trace
{
    /// <summary>
    /// Records debugging traces in the NDMF preview system
    /// </summary>
    public static class TraceBuffer
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.update += () =>
            {
                _editorFrame++;
            };
        }
        
        private static long _editorFrame;
        private static TraceEvent[] _traceEvents = new TraceEvent[256];
        private static long _totalTraceEvents;
        
        /// <summary>
        /// Records a tracing event.
        ///
        /// Note that arguments that are ComputeContext will be converted to their description string to avoid keeping
        /// those contexts alive inappropriately.
        /// </summary>
        /// <param name="eventType">An internal identifier for this event</param>
        /// <param name="formatEvent">A method that will format this event to a human-readable string</param>
        /// <param name="arg0">Arbitrary data usable by formatEvent</param>
        /// <param name="arg1">Arbitrary data usable by formatEvent</param>
        /// <param name="arg2">Arbitrary data usable by formatEvent</param>
        /// <param name="filename">Optional filename usable by formatEvent</param>
        /// <param name="line">Optional line number usable by formatEvent</param>
        /// <param name="parentEventId">An event ID to associate as the parent of this event; if not provided, the
        /// containing event scope will be used (see TraceEvent.Scope). If negative, no parent will be assigned.</param>
        /// <param name="collapse">If true, and the most recent event had the same eventType, overwrite that event</param>
        /// <param name="level">The tracing detail level (default is INFO)</param>
        /// <returns></returns>
        public static TraceEvent RecordTraceEvent(
            string eventType,
            Func<TraceEvent, string> formatEvent,
            object arg0 = null,
            object arg1 = null,
            object arg2 = null,
            string filename = null,
            int? line = null,
            long? parentEventId = null,
            bool collapse = false,
            TraceEventLevel? level = null
        )
        {
            level = level ?? TraceEventLevel.Info;
            
            lock (_traceEvents)
            {
                TraceEvent traceEvent = new TraceEvent
                {
                    Timestamp = DateTime.Now.Ticks,
                    EditorFrame = _editorFrame,
                    EventType = eventType,
                    FormatEvent = formatEvent,
                    Arg0 = MapArg(arg0),
                    Arg1 = MapArg(arg1),
                    Arg2 = MapArg(arg2),
                    FilePath = filename,
                    Line = line,
                    ParentEventId = parentEventId ?? TraceScope.CurrentTraceEvent.Value,
                    Level = level.Value
                };

                if (traceEvent.ParentEventId < 0)
                {
                    traceEvent.ParentEventId = null;
                }

                if (collapse && _totalTraceEvents > 1)
                {
                    TraceEvent lastEvent = _traceEvents[(int)(_totalTraceEvents - 1) % _traceEvents.Length];
                    if (lastEvent.EventType == traceEvent.EventType)
                    {
                        _totalTraceEvents--;
                    }
                }

                int index = (int)(_totalTraceEvents % _traceEvents.Length);
                traceEvent.EventId = _totalTraceEvents;
                _traceEvents[index] = traceEvent;

                _totalTraceEvents++;

                return traceEvent;
            }
        }

        private static object MapArg(object p0)
        {
            if (p0 is ComputeContext ctx)
            {
                return ctx.Description;
            }
            else
            {
                return p0;
            }
        }

        private static TraceEvent GetTraceEvent(long eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= _totalTraceEvents)
            {
                return new TraceEvent()
                {
                    Timestamp = 0,
                    EditorFrame = 0,
                    EventType = "???",
                    FormatEvent = (ev) => "???",
                    EventId = eventIndex,
                    ParentEventId = null
                };
            }
            
            return _traceEvents[(int)(eventIndex % _traceEvents.Length)];
        }

        internal static List<(string, string)> FormatTraceBuffer(int maxEvents, TraceEventLevel minLevel = TraceEventLevel.Info)
        {
            lock (_traceEvents)
            {
                if (_totalTraceEvents == 0) return new();

                long firstAvailableEvent = _totalTraceEvents - Math.Min(maxEvents, _traceEvents.Length);
                firstAvailableEvent = Math.Max(0, firstAvailableEvent);

                SortedDictionary<long, SortedSet<long>> frameToEvents = new();

                for (long ev = firstAvailableEvent; ev < _totalTraceEvents; ev++)
                {
                    var traceEvent = GetTraceEvent(ev);
                    
                    // Include lower level events only if a child event passes the filter
                    if (traceEvent.Level < minLevel) continue;
                    
                    if (!frameToEvents.TryGetValue(traceEvent.EditorFrame, out SortedSet<long> events))
                    {
                        events = new SortedSet<long>();
                        frameToEvents[traceEvent.EditorFrame] = events;
                    }
                    events.Add(ev);

                    while (traceEvent.ParentEventId.HasValue)
                    {
                        traceEvent = GetTraceEvent(traceEvent.ParentEventId.Value);
                        events.Add(traceEvent.EventId);
                    }
                }

                List<(string, string)> formattedEvents = new();
                StringBuilder buffer = new();
                
                foreach (var (frame, events) in frameToEvents)
                {
                    buffer.Clear();
                    
                    string label = $"Editor frame {frame}";

                    FormatSingleFrameEvents(buffer, frame, events);
                    formattedEvents.Add((label, buffer.ToString()));
                }

                return formattedEvents;
            }
        }

        private static void FormatSingleFrameEvents(StringBuilder builder, long editorFrame, SortedSet<long> events)
        {
            // Generate parentage tree
            Dictionary<long, SortedSet<long>> parentToChildren = new();
            List<long> rootEvents = new();
            
            foreach (long eventId in events)
            {
                TraceEvent traceEvent = GetTraceEvent(eventId);
                if (!traceEvent.ParentEventId.HasValue)
                {
                    rootEvents.Add(eventId);
                    continue;
                }
                
                if (!parentToChildren.TryGetValue(traceEvent.ParentEventId.Value, out SortedSet<long> children))
                {
                    children = new SortedSet<long>();
                    parentToChildren[traceEvent.ParentEventId.Value] = children;
                }

                children.Add(eventId);
            }
            
            foreach (long rootEventId in rootEvents)
            {
                FormatTraceEvent(rootEventId, 0);
            }
            

            void FormatTraceEvent(long eventIndex, int indent)
            {
                TraceEvent traceEvent = GetTraceEvent(eventIndex);

                string continuationPrefix = "";
                if (editorFrame != traceEvent.EditorFrame)
                {
                    continuationPrefix = "... ";
                }
                    
                string formattedEvent = traceEvent.FormatEvent(traceEvent);
                builder.Append(' ', indent * 2);
                builder.AppendFormat("[{1}{0}] ", traceEvent.EventId, continuationPrefix);
                builder.Append(formattedEvent);
                builder.AppendLine();

                if (parentToChildren.TryGetValue(eventIndex, out var children))
                {
                    foreach (int childIndex in children)
                    {
                        FormatTraceEvent(childIndex, indent + 1);
                    }
                }
            }
        }

        public static void Clear()
        {
            lock (_traceEvents)
            {
                _totalTraceEvents = 0;
            }
        }
    }
}