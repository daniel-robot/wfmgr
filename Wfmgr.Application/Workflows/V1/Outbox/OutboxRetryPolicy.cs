using Wfmgr.Domain.Integrations;

namespace Wfmgr.Application.Workflows.V1.Outbox;

/// <summary>
/// Per-action retry policy for outbox messages. Replaces the hardcoded
/// <c>CompensationRetryThreshold = 5</c> constant previously embedded in <c>OutboxWorker</c>.
/// </summary>
/// <param name="MaxAttempts">Maximum delivery attempts (inclusive of the first attempt) before escalating to compensation.</param>
/// <param name="InitialDelay">Backoff applied after the first failure.</param>
/// <param name="Multiplier">Multiplier applied to the previous delay for exponential growth (1.0 = constant).</param>
/// <param name="MaxDelay">Upper bound on the backoff delay.</param>
/// <param name="JitterFraction">Fractional jitter applied to the computed delay (0.0–1.0). 0.2 = +/-20%.</param>
public sealed record OutboxRetryPolicy(
    int MaxAttempts,
    TimeSpan InitialDelay,
    double Multiplier,
    TimeSpan MaxDelay,
    double JitterFraction)
{
    /// <summary>Built-in default that mirrors the previous hardcoded behaviour (5 attempts, 2× growth, capped at 60 minutes).</summary>
    public static readonly OutboxRetryPolicy Default = new(
        MaxAttempts: 5,
        InitialDelay: TimeSpan.FromMinutes(1),
        Multiplier: 2.0,
        MaxDelay: TimeSpan.FromMinutes(60),
        JitterFraction: 0.0);

    /// <summary>
    /// Computes the next retry delay for a given attempt count (1-based — the count
    /// after incrementing <c>RetryCount</c> on a failed attempt).
    /// </summary>
    public TimeSpan ComputeNextDelay(int attempt, Random? rng = null)
    {
        if (attempt <= 0) attempt = 1;
        var raw = InitialDelay.TotalSeconds * Math.Pow(Math.Max(Multiplier, 1.0), attempt - 1);
        var capped = Math.Min(raw, MaxDelay.TotalSeconds);
        if (JitterFraction > 0)
        {
            rng ??= Random.Shared;
            var jitter = (rng.NextDouble() * 2.0 - 1.0) * JitterFraction;
            capped *= 1.0 + jitter;
        }
        return TimeSpan.FromSeconds(Math.Max(0.0, capped));
    }
}

/// <summary>
/// Static map of <see cref="OutboxActions"/> values to their retry policies.
/// Lookups for unmapped actions fall back to <see cref="OutboxRetryPolicy.Default"/>.
/// </summary>
public static class OutboxRetryPolicyMap
{
    private static readonly Dictionary<string, OutboxRetryPolicy> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Schedule sync to MSQ tolerates longer outages — keep retrying for ~3 h.
        [OutboxActions.SyncSchedule] = new(
            MaxAttempts: 8, InitialDelay: TimeSpan.FromMinutes(1),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(60), JitterFraction: 0.2),

        // Monaco import is on a tighter clinical SLA — escalate sooner.
        [OutboxActions.SendToMonacoImport] = new(
            MaxAttempts: 4, InitialDelay: TimeSpan.FromSeconds(30),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(15), JitterFraction: 0.2),

        // Contouring dispatch — moderate retries.
        [OutboxActions.SendImagesToContourTool] = new(
            MaxAttempts: 5, InitialDelay: TimeSpan.FromMinutes(1),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(30), JitterFraction: 0.2),

        // Prescription generation — moderate retries.
        [OutboxActions.GeneratePrescription] = new(
            MaxAttempts: 5, InitialDelay: TimeSpan.FromMinutes(1),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(30), JitterFraction: 0.2),

        // Status polls are cheap; retry quickly but give up sooner.
        [OutboxActions.QueryContourStatus] = new(
            MaxAttempts: 3, InitialDelay: TimeSpan.FromSeconds(30),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(10), JitterFraction: 0.2),
        [OutboxActions.QueryTreatmentProgress] = new(
            MaxAttempts: 3, InitialDelay: TimeSpan.FromSeconds(30),
            Multiplier: 2.0, MaxDelay: TimeSpan.FromMinutes(10), JitterFraction: 0.2),
    };

    public static OutboxRetryPolicy Get(string action) =>
        Map.TryGetValue(action, out var p) ? p : OutboxRetryPolicy.Default;
}
