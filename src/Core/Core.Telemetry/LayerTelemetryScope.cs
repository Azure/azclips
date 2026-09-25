// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Owns producer admission, activity terminal ownership and one isolated layer pipeline.</summary>
public sealed class LayerTelemetryScope : ITelemetryLayerScope, ITelemetryActivityLifecycle
{
    public static readonly TimeSpan ProducerDrainBudget = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan FileCompletionBudget = TimeSpan.FromSeconds(2);
    private readonly object _gate = new();
    private readonly Dictionary<Activity, SpanRegistration> _spans = new(ReferenceEqualityComparer.Instance);
    private readonly AsyncLocal<ProducerLease?> _producerContext = new();
    private readonly AsyncLocal<CreationLease?> _creationContext = new();
    private readonly List<RegisteredCleanup> _cleanups = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationToken _stoppingToken;
    private readonly TelemetryLayerScopeOptions _options;
    private readonly LayerTelemetryDiagnostics _diagnostics;
    private readonly LayerTelemetryProvider _provider;
    private readonly Action? _releaseSource;
    private readonly bool _disabled;
    private bool _unavailable;
    private bool _providerConstructionFailed;
    private TelemetryLayerState _state;
    private TaskCompletionSource _changed = NewSignal();
    private TaskCompletionSource<TelemetryLayerCompletion>? _completion;
    private TaskCompletionSource<bool>? _providerDisposal;
    private TelemetryLayerBudget? _drainBudget;
    private TelemetryLayerBudget? _fileBudget;
    private int _generation;
    private int _producers;
    private int _creations;
    private int _captures;
    private int _acceptedWrites;
    private int _stoppingDisposed;
    private bool _cleanupRegistrationClosed;
    private Activity? _root;
    private Dictionary<string, JsonElement>? _rootTags;
    private ActivityStatusCode? _rootStatus;
    private TraceEntry? _normalRoot;
    private bool _deadline;
    private long _cutoff;
    private int _pendingAtDeadline;

    internal LayerTelemetryScope(
        TelemetryLayer layer,
        TelemetryInvocationContext context,
        string fileId,
        ITraceDirectoryProvider directory,
        IConfigurationService configuration,
        TelemetryLayerScopeOptions options,
        bool disabled,
        bool unavailable,
        Action? releaseSource)
    {
        Layer = layer;
        Context = context;
        _options = options;
        _diagnostics = new LayerTelemetryDiagnostics(options.Diagnostic);
        _disabled = disabled;
        _unavailable = unavailable;
        _releaseSource = releaseSource;
        _stoppingToken = _stopping.Token;
        _provider = new LayerTelemetryProvider(layer.SourceName);
        FileSession = new LayerTraceFileSession(directory, layer, fileId, options, _diagnostics);

        if (!disabled && !unavailable)
        {
            try
            {
                var builder = Sdk.CreateTracerProviderBuilder()
                    .AddSource(layer.SourceName)
                    .SetSampler(new LayerTelemetrySampler(this, options.SamplerFactory()))
                    .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(layer.ServiceName));
                foreach (var factory in options.ProcessorFactories)
                    builder.AddProcessor(factory());
                new LayerFileTelemetrySender(this).ConfigureExporter(builder);
                _provider.SetProvider(builder.Build());
            }
            catch
            {
                _unavailable = true;
                _providerConstructionFailed = true;
                FileSession.MarkFailure("provider_creation_failed");
            }
        }
        if (unavailable)
            _diagnostics.Report("source_already_owned");

        Telemetry = new TelemetryService(_provider, configuration, ClientType.CLI,
            new TelemetryServiceSettings(
                layer.ServiceName, !disabled && !_unavailable, OwnsProvider: false, layer.NameFilter, this));
    }

    public TelemetryLayer Layer { get; }
    public ITelemetryService Telemetry { get; }
    public ITelemetryProvider TelemetryProvider => _provider;
    public TelemetryInvocationContext Context { get; }
    public LayerTraceFileSession FileSession { get; }
    public CancellationToken StoppingToken => _stoppingToken;
    public IReadOnlyList<string> Diagnostics => _diagnostics.Codes;
    public TelemetryLayerState State { get { lock (_gate) return _state; } }
    public TimeSpan RemainingDrainTime
    {
        get { lock (_gate) return _drainBudget?.Remaining ?? ProducerDrainBudget; }
    }

    public IDisposable? TryEnterProducer(bool teardown = false)
    {
        var lease = ReserveProducer(teardown);
        lease?.Install();
        return lease;
    }

    private ProducerLease? ReserveProducer(bool teardown, RegisteredCleanup? cleanup = null)
    {
        lock (_gate)
        {
            if (_state != TelemetryLayerState.Running &&
                !(_state == TelemetryLayerState.Draining && teardown && _drainBudget!.Remaining > TimeSpan.Zero))
                return null;
            if (cleanup is not null)
                cleanup.Started = true;
            _producers++;
            return new ProducerLease(this);
        }
    }

    public void BeginDrain()
    {
        ProducerLease cancellation;
        lock (_gate)
        {
            if (_state != TelemetryLayerState.Running)
                return;
            _state = TelemetryLayerState.Draining;
            _drainBudget = new TelemetryLayerBudget(_options.TimeProvider, ProducerDrainBudget);
            _producers++;
            cancellation = new ProducerLease(this);
            PulseLocked();
        }
        _ = Task.Run(() =>
        {
            cancellation.Install();
            try
            {
                // Synchronous cancellation callbacks are producers too, not work outside the budget.
                _stopping.Cancel();
            }
            catch
            {
                _diagnostics.Report("cancellation_callback_failed");
            }
            finally
            {
                cancellation.Dispose();
            }
        });
    }

    /// <summary>Registers owned cleanup before completion; lower order runs first, then registration order.</summary>
    public void RegisterCleanup(Func<Task> cleanup, int order = 0)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        lock (_gate)
        {
            if (_cleanupRegistrationClosed || _state >= TelemetryLayerState.Fenced)
                throw new InvalidOperationException("The layer no longer accepts cleanup registrations.");
            _cleanups.Add(new RegisteredCleanup(cleanup, order));
            PulseLocked();
        }
    }

    public Task<bool> RunCleanupAsync(Func<Task> cleanup) => RunCleanupCoreAsync(cleanup, null);

    private async Task<bool> RunCleanupCoreAsync(Func<Task> cleanup, RegisteredCleanup? registration)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        BeginDrain();
        var lease = ReserveProducer(teardown: true, registration);
        if (lease is null)
            return false;

        var actual = Task.Run(async () =>
        {
            lease.Install();
            try
            {
                await cleanup().ConfigureAwait(false);
                return true;
            }
            catch
            {
                _diagnostics.Report("cleanup_failed");
                return false;
            }
            finally
            {
                lease.Dispose();
            }
        });
        if (!await _drainBudget!.ObserveAsync(actual).ConfigureAwait(false))
        {
            _diagnostics.Report("cleanup_deadline");
            return false;
        }
        return await actual.ConfigureAwait(false);
    }

    IDisposable? ITelemetryActivityLifecycle.TryBeginActivity()
    {
        CreationLease lease;
        lock (_gate)
        {
            if (_disabled || _unavailable ||
                (_state != TelemetryLayerState.Running &&
                 !(_state == TelemetryLayerState.Draining &&
                    _drainBudget!.Remaining > TimeSpan.Zero && HasActiveProducer())))
                return null;
            _creations++;
            lease = new CreationLease(this, _generation, _creationContext.Value);
        }
        _creationContext.Value = lease;
        try
        {
            _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.CreationReserved);
            return lease;
        }
        catch
        {
            lease.Dispose();
            FileSession.MarkFailure("creation_callback_failed");
            return null;
        }
    }

    internal object? CreationStamp
    {
        get
        {
            var lease = _creationContext.Value;
            lock (_gate)
                return lease is { Active: true } && lease.Generation == _generation &&
                    _state < TelemetryLayerState.Fenced ? lease.Stamp : null;
        }
    }

    void ITelemetryActivityLifecycle.OnActivityCreated(Activity? activity)
    {
        var lease = _creationContext.Value;
        if (activity is null || lease is not { Active: true } ||
            !ReferenceEquals(activity.Source, _provider.ActivitySource))
            return;
        var admitted = ReferenceEquals(activity.GetTagItem(LayerTelemetrySampler.CreationTag), lease.Stamp);
        activity.SetTag(LayerTelemetrySampler.CreationTag, null);
        if (!admitted || !activity.Recorded || !activity.IsAllDataRequested ||
            !Layer.NameFilter.IsAllowed(activity.OperationName))
            return;

        try
        {
            var immutable = new LayerTelemetryRecord(Layer, Context, activity, _options.TimeProvider);
            lock (_gate)
            {
                if (_state >= TelemetryLayerState.Sealed)
                    return;
                _spans.TryAdd(activity, new SpanRegistration(immutable)
                {
                    Owner = ReferenceEquals(activity, _root) ? TerminalOwner.Completion : TerminalOwner.None,
                });
                PulseLocked();
            }
            // A listener may have stopped the activity before StartActivity returned.
            if (activity.IsStopped)
                OnActivityEnded(activity);
        }
        catch
        {
            FileSession.MarkFailure("activity_metadata_failed");
        }
    }

    internal void OnActivityEnded(Activity activity)
    {
        SpanRegistration span;
        int generation;
        Dictionary<string, JsonElement>? rootTags;
        ActivityStatusCode? rootStatus;
        lock (_gate)
        {
            if (_state >= TelemetryLayerState.Fenced || !_spans.TryGetValue(activity, out span!) ||
                span.Owner is not (TerminalOwner.None or TerminalOwner.Completion))
                return;
            if (!activity.Recorded || !activity.IsAllDataRequested || !Layer.NameFilter.IsAllowed(activity.OperationName))
            {
                span.Owner = TerminalOwner.Dropped;
                PulseLocked();
                return;
            }
            if (span.Owner == TerminalOwner.Completion)
                return;
            span.Owner = TerminalOwner.Capture;
            _captures++;
            generation = _generation;
            rootTags = ReferenceEquals(activity, _root) ? _rootTags : null;
            rootStatus = ReferenceEquals(activity, _root) ? _rootStatus : null;
        }

        var capturing = true;
        var writing = false;
        try
        {
            _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.CaptureReserved);
            var entry = span.Identity.CaptureNormal(activity, rootTags, rootStatus);
            lock (_gate)
            {
                _captures--;
                capturing = false;
                if (_state >= TelemetryLayerState.Fenced || generation != _generation)
                {
                    span.Owner = TerminalOwner.None;
                    PulseLocked();
                    return;
                }
                span.Owner = TerminalOwner.Accepted;
                _acceptedWrites++;
                writing = true;
                PulseLocked();
            }
            _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.WriteAccepted);
            Task<bool> write;
            using (SuppressInstrumentationScope.Begin())
                write = FileSession.WriteBatchAsync([entry]);
            writing = false;
            _ = ObserveAcceptedWriteAsync(write);
        }
        catch
        {
            FileSession.MarkFailure("capture_or_write_failed");
        }
        finally
        {
            lock (_gate)
            {
                if (capturing)
                {
                    _captures--;
                    span.Owner = TerminalOwner.Dropped;
                }
                if (writing)
                    _acceptedWrites--;
                PulseLocked();
            }
        }
    }

    private async Task ObserveAcceptedWriteAsync(Task<bool> write)
    {
        try
        {
            await write.ConfigureAwait(false);
        }
        catch
        {
            FileSession.MarkFailure("accepted_write_failed");
        }
        finally
        {
            // Admission belongs to the real write/close task, not to OnEnd returning.
            lock (_gate)
            {
                _acceptedWrites--;
                PulseLocked();
            }
        }
    }

    /// <summary>
    /// Returns a budget-bounded result. After PublicationUnconfirmed, State reflects
    /// the original admitted rename's eventual result; completion is not restarted.
    /// </summary>
    public Task<TelemetryLayerCompletion> CompleteAsync(
        Activity? root = null,
        IReadOnlyDictionary<string, object?>? terminalTags = null,
        ActivityStatusCode? terminalStatus = null)
    {
        lock (_gate)
        {
            if (_completion is not null)
                return _completion.Task;
        }
        Dictionary<string, JsonElement> frozen;
        try
        {
            frozen = LayerTelemetryRecord.FreezeTags(Layer, terminalTags);
        }
        catch
        {
            FileSession.MarkFailure("terminal_metadata_failed");
            frozen = [];
        }

        Task<TelemetryLayerCompletion> result;
        lock (_gate)
        {
            if (_completion is not null)
                return _completion.Task;
            _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            result = _completion.Task;
            _root = root;
            _rootTags = frozen;
            _rootStatus = terminalStatus ?? root?.Status;
            if (root is not null && _spans.TryGetValue(root, out var span) && span.Owner == TerminalOwner.None)
                span.Owner = TerminalOwner.Completion;
        }
        BeginDrain();
        _ = Task.Run(CompleteCoreAsync);
        return result;
    }

    private async Task CompleteCoreAsync()
    {
        var result = TelemetryLayerCompletion.NotPublished;
        try
        {
            var drained = await DrainProducersAsync().ConfigureAwait(false);
            if (drained)
                drained = await StopAndCaptureRootAsync().ConfigureAwait(false);
            Fence(deadline: !drained);

            if (!await WaitUntilAsync(
                    () => _creations == 0 && _captures == 0, _fileBudget).ConfigureAwait(false))
            {
                NotPublished("capture_handoff_unconfirmed");
                return;
            }
            // Normal records admitted before the fence must precede the terminal batch,
            // including an admission that had not reached the file queue at the fence.
            if (!await WaitUntilAsync(() => _acceptedWrites == 0, _fileBudget).ConfigureAwait(false))
            {
                NotPublished("file_io_unconfirmed");
                return;
            }
            var final = SubmitFinalRecords();
            if (!await _fileBudget!.ObserveAsync(final).ConfigureAwait(false) ||
                !await WaitUntilAsync(() => _acceptedWrites == 0, _fileBudget).ConfigureAwait(false) ||
                !await _fileBudget.ObserveAsync(FileSession.WaitForIdleAsync()).ConfigureAwait(false))
            {
                NotPublished("file_io_unconfirmed");
                return;
            }

            var disposal = EnsureProviderDisposal();
            if (!await _fileBudget.ObserveAsync(disposal).ConfigureAwait(false) ||
                !await disposal.ConfigureAwait(false))
            {
                NotPublished("provider_disposal_unconfirmed");
                return;
            }
            if (_unavailable || FileSession.Failed || !FileSession.TrySeal())
            {
                NotPublished("file_not_sealable");
                return;
            }

            lock (_gate)
            {
                _state = TelemetryLayerState.Sealed;
                PulseLocked();
            }
            if (_disabled)
                result = TelemetryLayerCompletion.Disabled;
            else if (!FileSession.HasRecords)
                result = TelemetryLayerCompletion.Empty;
            else
            {
                var publish = FileSession.PublishAsync();
                await _fileBudget.ObserveAsync(publish).ConfigureAwait(false);
                result = ResolvePublication(publish);
            }
        }
        catch
        {
            if (FileSession.PublicationCompletion is { } publish)
            {
                _diagnostics.Report("completion_failed");
                result = ResolvePublication(publish);
            }
            else
                NotPublished("completion_failed");
        }
        finally
        {
            // Releasing a late owner must not schedule another publication attempt.
            _ = EnsureProviderDisposal();
            _completion!.TrySetResult(result);
        }
    }

    private TelemetryLayerCompletion ResolvePublication(Task<bool> publication)
    {
        var commit = FileSession.StopPublicationAttempts();
        if (commit == LayerTracePublicationState.Published)
            return ConfirmPublication();
        if (commit == LayerTracePublicationState.InFlight)
        {
            lock (_gate)
            {
                _state = TelemetryLayerState.PublicationUnconfirmed;
                PulseLocked();
            }
            _diagnostics.Report("publication_unconfirmed");
            _ = ObserveAdmittedPublicationAsync(publication);
            return TelemetryLayerCompletion.PublicationUnconfirmed;
        }

        NotPublished(commit == LayerTracePublicationState.Failed ? "publication_failed" : "publication_not_started");
        return TelemetryLayerCompletion.NotPublished;
    }

    private TelemetryLayerCompletion ConfirmPublication()
    {
        lock (_gate)
        {
            _state = TelemetryLayerState.Published;
            PulseLocked();
        }
        return _deadline
            ? TelemetryLayerCompletion.PublishedAtDeadline
            : TelemetryLayerCompletion.Published;
    }

    private async Task ObserveAdmittedPublicationAsync(Task<bool> publication)
    {
        // Observe only the admitted attempt: never invoke PublishAsync or retry here.
        try
        {
            await publication.ConfigureAwait(false);
        }
        catch
        {
            FileSession.MarkFailure("publication_observation_failed");
        }
        if (FileSession.Published)
        {
            ConfirmPublication();
            _diagnostics.Report("publication_confirmed_late");
        }
        else
        {
            lock (_gate)
            {
                _state = TelemetryLayerState.NotPublished;
                PulseLocked();
            }
            _diagnostics.Report("publication_failed_after_deadline");
        }
    }

    private async Task<bool> DrainProducersAsync()
    {
        while (true)
        {
            RegisteredCleanup? cleanup;
            Task changed;
            lock (_gate)
            {
                cleanup = _cleanups.Where(item => !item.Started).OrderBy(item => item.Order).FirstOrDefault();
                if (cleanup is null && _producers == 0 && _creations == 0 && _captures == 0 &&
                    !_spans.Any(item => !ReferenceEquals(item.Key, _root) && item.Value.Owner == TerminalOwner.None))
                    return true;
                changed = _changed.Task;
            }
            if (_drainBudget!.Remaining == TimeSpan.Zero)
                return false;
            if (cleanup is not null)
                await RunCleanupCoreAsync(cleanup.Callback, cleanup).ConfigureAwait(false);
            else if (!await _drainBudget.ObserveAsync(changed).ConfigureAwait(false))
                return false;
        }
    }

    private async Task<bool> StopAndCaptureRootAsync()
    {
        SpanRegistration? span;
        int generation;
        lock (_gate)
        {
            if (_root is null || !_spans.TryGetValue(_root, out span) || span.Owner != TerminalOwner.Completion)
                return true;
            _captures++;
            generation = _generation;
        }
        var stop = Task.Run(() =>
        {
            try
            {
                if (_drainBudget!.Remaining == TimeSpan.Zero)
                    return;
                foreach (var (key, value) in _rootTags!)
                    _root!.SetTag(key, value);
                if (_rootStatus.HasValue)
                    _root!.SetStatus(_rootStatus.Value);
                _root!.Stop();
                if (!_root.Recorded || !_root.IsAllDataRequested)
                {
                    lock (_gate)
                        span.Owner = TerminalOwner.Dropped;
                    return;
                }
                var entry = span.Identity.CaptureNormal(_root, _rootTags, _rootStatus);
                lock (_gate)
                {
                    if (generation == _generation && _state == TelemetryLayerState.Draining)
                        _normalRoot = entry;
                }
            }
            catch
            {
                FileSession.MarkFailure("root_capture_failed");
            }
            finally
            {
                lock (_gate)
                {
                    _captures--;
                    PulseLocked();
                }
            }
        });
        return await _drainBudget!.ObserveAsync(stop).ConfigureAwait(false) &&
            _drainBudget.Remaining > TimeSpan.Zero;
    }

    private void Fence(bool deadline)
    {
        TelemetryLayerBudget budget;
        lock (_gate)
        {
            if (_state >= TelemetryLayerState.Fenced)
                return;
            _deadline = deadline;
            _cutoff = _options.TimeProvider.GetTimestamp();
            _pendingAtDeadline = _producers + _cleanups.Count(item => !item.Started);
            _cleanupRegistrationClosed = true;
            _state = TelemetryLayerState.Fenced;
            _generation++;
            _fileBudget = budget = new TelemetryLayerBudget(_options.TimeProvider, FileCompletionBudget);
            PulseLocked();
        }
        FileSession.BeginCompletion(budget);
        TryDisposeStoppingSource();
        if (deadline)
            _diagnostics.Report("producer_deadline");
    }

    private Task SubmitFinalRecords()
    {
        List<SpanRegistration> abandoned;
        SpanRegistration? root;
        lock (_gate)
        {
            abandoned = _spans.Where(item => !ReferenceEquals(item.Key, _root) &&
                item.Value.Owner == TerminalOwner.None).Select(item => item.Value).ToList();
            root = null;
            if (_root is not null)
                _spans.TryGetValue(_root, out root);
            if (_root is null || root?.Owner is not (TerminalOwner.None or TerminalOwner.Completion))
                root = null;
            if (abandoned.Count > 0)
                _deadline = true;
            foreach (var span in abandoned)
                span.Owner = TerminalOwner.Completion;
            if (root is not null)
                root.Owner = TerminalOwner.Completion;
            if (abandoned.Count == 0 && root is null)
                return Task.CompletedTask;
            _captures++;
        }

        return Task.Run(() =>
        {
            var capturing = true;
            var writing = false;
            try
            {
                var records = abandoned.Select(span => span.Identity.CaptureAtDeadline(
                    _options.TimeProvider, _cutoff, null, null, root: false,
                    _pendingAtDeadline, abandoned.Count)).ToList();
                if (root is not null)
                {
                    records.Add(_deadline
                        ? root.Identity.CaptureAtDeadline(
                            _options.TimeProvider, _cutoff, _rootTags, _rootStatus, root: true,
                            _pendingAtDeadline, abandoned.Count)
                        : root.Identity.CaptureCompletedRoot(
                            _normalRoot, _options.TimeProvider, _cutoff, _rootTags, _rootStatus));
                }
                lock (_gate)
                {
                    _captures--;
                    capturing = false;
                    if (_state != TelemetryLayerState.Fenced || _fileBudget!.Remaining == TimeSpan.Zero)
                    {
                        PulseLocked();
                        return;
                    }
                    foreach (var span in abandoned)
                        span.Owner = TerminalOwner.Accepted;
                    if (root is not null)
                        root.Owner = TerminalOwner.Accepted;
                    _acceptedWrites++;
                    writing = true;
                    PulseLocked();
                }
                _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.WriteAccepted);
                Task<bool> write;
                using (SuppressInstrumentationScope.Begin())
                    write = FileSession.WriteBatchAsync(records);
                writing = false;
                _ = ObserveAcceptedWriteAsync(write);
            }
            catch
            {
                FileSession.MarkFailure("terminal_capture_or_write_failed");
            }
            finally
            {
                lock (_gate)
                {
                    if (capturing)
                        _captures--;
                    if (writing)
                        _acceptedWrites--;
                    PulseLocked();
                }
            }
        });
    }

    private Task<bool> EnsureProviderDisposal()
    {
        TaskCompletionSource<bool> completion;
        lock (_gate)
        {
            if (_providerDisposal is not null)
                return _providerDisposal.Task;
            _providerDisposal = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await WaitUntilAsync(
                    () => _creations == 0 && _captures == 0 && _acceptedWrites == 0, null).ConfigureAwait(false);
                await FileSession.WaitForIdleAsync().ConfigureAwait(false);
                _options.Checkpoint?.Invoke(TelemetryLayerCheckpoint.BeforeProviderDispose);
                _provider.DisposeOwned();
                Telemetry.Dispose();
                if (_providerConstructionFailed)
                {
                    // Build did not return an owner that can confirm release of
                    // every partially constructed processor/listener.
                    completion.TrySetResult(false);
                    return;
                }
                _releaseSource?.Invoke();
                completion.TrySetResult(true);
            }
            catch
            {
                // An unconfirmed disposal keeps the factory's source reservation.
                FileSession.MarkFailure("provider_disposal_failed");
                completion.TrySetResult(false);
            }
        });
        return completion.Task;
    }

    private void NotPublished(string code)
    {
        Fence(deadline: true);
        lock (_gate)
        {
            _state = TelemetryLayerState.NotPublished;
            PulseLocked();
        }
        FileSession.Abort(code);
    }

    private async Task<bool> WaitUntilAsync(Func<bool> ready, TelemetryLayerBudget? budget)
    {
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (ready())
                    return true;
                changed = _changed.Task;
            }
            if (budget is null)
                await changed.ConfigureAwait(false);
            else if (!await budget.ObserveAsync(changed).ConfigureAwait(false))
                return false;
        }
    }

    private bool HasActiveProducer()
    {
        for (var producer = _producerContext.Value; producer is not null; producer = producer.Previous)
        {
            if (producer.Active)
                return true;
        }
        return false;
    }

    private void TryDisposeStoppingSource()
    {
        lock (_gate)
        {
            if (_state < TelemetryLayerState.Fenced || _producers != 0 ||
                Interlocked.Exchange(ref _stoppingDisposed, 1) != 0)
                return;
        }
        _stopping.Dispose();
    }

    private void PulseLocked()
    {
        var previous = _changed;
        _changed = NewSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<bool> WaitForStateAsync(TelemetryLayerState state) =>
        WaitUntilAsync(() => _state >= state, null);

    internal Task<bool> ProviderDisposalCompletion => EnsureProviderDisposal();

    public async ValueTask DisposeAsync()
    {
        await CompleteAsync().ConfigureAwait(false);
    }

    private enum TerminalOwner { None, Capture, Completion, Accepted, Dropped }

    private sealed class SpanRegistration(LayerTelemetryRecord identity)
    {
        internal LayerTelemetryRecord Identity { get; } = identity;
        internal TerminalOwner Owner { get; set; }
    }

    private sealed class RegisteredCleanup(Func<Task> callback, int order)
    {
        internal Func<Task> Callback { get; } = callback;
        internal int Order { get; } = order;
        internal bool Started { get; set; }
    }

    private sealed class ProducerLease(LayerTelemetryScope scope) : IDisposable
    {
        private int _active = 1;
        internal bool Active => Volatile.Read(ref _active) != 0;
        internal ProducerLease? Previous { get; private set; }

        internal void Install()
        {
            Previous = scope._producerContext.Value;
            scope._producerContext.Value = this;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0)
                return;
            if (ReferenceEquals(scope._producerContext.Value, this))
                scope._producerContext.Value = Previous;
            lock (scope._gate)
            {
                scope._producers--;
                scope.PulseLocked();
            }
            scope.TryDisposeStoppingSource();
        }
    }

    private sealed class CreationLease(
        LayerTelemetryScope scope, int generation, CreationLease? previous) : IDisposable
    {
        private int _active = 1;
        internal bool Active => Volatile.Read(ref _active) != 0;
        internal int Generation { get; } = generation;
        internal object Stamp { get; } = new();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0)
                return;
            if (ReferenceEquals(scope._creationContext.Value, this))
                scope._creationContext.Value = previous;
            lock (scope._gate)
            {
                scope._creations--;
                scope.PulseLocked();
            }
        }
    }
}
