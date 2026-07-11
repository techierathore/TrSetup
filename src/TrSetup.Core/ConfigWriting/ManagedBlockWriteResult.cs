namespace TrSetup.Core.ConfigWriting;

/// <summary>
/// The evidence trail of one managed-block upsert (REQ-FN-018).
/// </summary>
/// <param name="Outcome">What the upsert did to the file.</param>
/// <param name="FilePath">Absolute path of the file that was written (or verified unchanged).</param>
/// <param name="BlockId">The stable id of the managed block.</param>
/// <param name="Evidence">Human-readable one-liner for the fix evidence trail.</param>
public sealed record ManagedBlockWriteResult(
    ManagedBlockOutcome Outcome,
    string FilePath,
    string BlockId,
    string Evidence);
