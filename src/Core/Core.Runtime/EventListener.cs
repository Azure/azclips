namespace Core.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Abstractions.Runtime;
    using GetEventData = Func<object>;

    /// <summary>
    /// An Implementation of the IEventListener that supports subscribing to events and dispatching them
    /// (used for manually using the lowlevel interface)
    /// </summary>
    public class EventListener : CancellationTokenSource, IEnumerable<KeyValuePair<string, Event>>, IEventListener
    {
        private Dictionary<string, Event> calls = new Dictionary<string, Event>();
        public StartActivityDelegate StartActivity { get; set; } = null!;
        public IEnumerator<KeyValuePair<string, Event>> GetEnumerator() => calls.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => calls.GetEnumerator();
        public EventListener()
        {
        }

        public new Action Cancel => base.Cancel;
        private Event tracer = null!;

        public EventListener(params (string name, Event callback)[] initializer)
        {
            foreach (var each in initializer)
            {
                Add(each.name, each.callback);
            }
        }

        public void Add(string name, SynchEvent callback)
        {
            Add(name, (message) => { callback(message); return Task.CompletedTask; });
        }

        public void Add(string name, Event callback)
        {
            if (callback != null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    if (calls.ContainsKey(name))
                    {
                        tracer += callback;
                    }
                    else
                    {
                        tracer = callback;
                    }
                }
                else
                {
                    if (calls.ContainsKey(name))
                    {
                        calls[name ?? System.String.Empty] += callback;
                    }
                    else
                    {
                        calls[name ?? System.String.Empty] = callback;
                    }
                }
            }
        }


        public async Task Signal(string id, CancellationToken token, GetEventData createMessage)
        {
            using (RuntimeExtensions.NoSynchronizationContext)
            {
                if (!string.IsNullOrEmpty(id) && (calls.TryGetValue(id, out Event? listener) || tracer != null))
                {
                    EventData message = (EventData)createMessage();
                    message.Id = id;

                    if (listener != null)
                    {
                        await listener.Invoke(message);
                    }
                    if (tracer != null)
                    {
                        await tracer.Invoke(message);
                    }

                    if (token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException($"Canceled by event {id} ", this.Token);
                    }
                }
            }
        }
    }
}
