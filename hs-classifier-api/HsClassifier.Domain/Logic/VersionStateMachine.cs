using HsClassifier.Domain.Enums;

namespace HsClassifier.Domain.Logic;

public static class VersionStateMachine
{
    private static readonly Dictionary<ImportState, ImportState[]> _allowed = new()
    {
        [ImportState.Uploaded]      = [ImportState.Parsing],
        [ImportState.Parsing]       = [ImportState.Mapped],
        [ImportState.Mapped]        = [ImportState.Validating],
        [ImportState.Validating]    = [ImportState.DryRunPassed, ImportState.DryRunFailed],
        [ImportState.DryRunFailed]  = [ImportState.Validating],
        [ImportState.DryRunPassed]  = [ImportState.ReadyToCommit],
        [ImportState.ReadyToCommit] = [ImportState.Committed],
        [ImportState.Committed]     = [ImportState.Indexed],
        [ImportState.Indexed]       = [ImportState.Active],
        [ImportState.Active]        = [],
    };

    public static bool CanTransition(ImportState current, ImportState next)
        => _allowed.TryGetValue(current, out var allowed) && allowed.Contains(next);
}
