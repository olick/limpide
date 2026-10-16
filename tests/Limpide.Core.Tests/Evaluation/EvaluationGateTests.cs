using System.Text.Json;
using Limpide.Core.Evaluation;

namespace Limpide.Core.Tests.Evaluation;

public class EvaluationGateTests
{
    private static readonly JsonElement Report = JsonDocument.Parse("""
        { "metrics": { "promptLeaks": 0, "expectedPassageCitedShare": 0.6333, "latencySeconds": { "p95": 9.1 } } }
        """).RootElement;

    [Fact]
    public void Passes_when_every_threshold_holds()
    {
        var results = EvaluationGate.Check(Report, [new("metrics.promptLeaks", Max: 0), new("metrics.expectedPassageCitedShare", Min: 0.56)]);

        Assert.All(results, r => Assert.Equal(GateStatus.Passed, r.Status));
        Assert.False(EvaluationGate.Blocks(results));
    }

    [Fact]
    public void A_blocking_threshold_blocks_a_warning_does_not()
    {
        var blocking = EvaluationGate.Check(Report, [new("metrics.expectedPassageCitedShare", Min: 0.7)]);
        var warning = EvaluationGate.Check(Report, [new("metrics.latencySeconds.p95", Max: 8, Blocking: false)]);

        Assert.True(EvaluationGate.Blocks(blocking));
        Assert.Equal(GateStatus.Warning, Assert.Single(warning).Status);
        Assert.False(EvaluationGate.Blocks(warning));
    }

    [Fact]
    public void A_missing_metric_fails_even_when_not_blocking()
    {
        var results = EvaluationGate.Check(Report, [new("metrics.judge.unfaithfulShare", Max: 0.35, Blocking: false)]);

        Assert.Equal(GateStatus.Failed, Assert.Single(results).Status);
        Assert.Null(results[0].Value);
    }

    [Fact]
    public void Reads_nested_metrics_ignoring_case() =>
        Assert.Equal(9.1, EvaluationGate.Read(Report, "Metrics.LatencySeconds.P95"));
}
