namespace TrSetup.Core.ConfigWriting;

/// <summary>
/// What an upsert of a managed marker block did to the target file (REQ-FN-018).
/// </summary>
public enum ManagedBlockOutcome
{
    /// <summary>The file did not exist; it was created containing only the managed block.</summary>
    CreatedFile = 0,

    /// <summary>The file existed without this block; the block was appended at the end.</summary>
    AppendedBlock = 1,

    /// <summary>The block already existed; it was replaced in place — everything outside the markers untouched.</summary>
    ReplacedBlock = 2,

    /// <summary>The block already contained exactly this content; the file was not rewritten.</summary>
    Unchanged = 3
}
