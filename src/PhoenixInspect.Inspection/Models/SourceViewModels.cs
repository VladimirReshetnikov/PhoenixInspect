using System.Collections.Immutable;

namespace PhoenixInspect.Inspection;

/// <summary>
/// States what the host proved about the analysis-machine file it found (or failed to find) at a frame's
/// build-recorded document path.
/// </summary>
/// <remarks>
/// Only <see cref="VerifiedExact"/> permits presenting file content as the frame's source. Every other value is a
/// reported limit: showing an unverified or mismatching file as if it were the code the dump captured would be a
/// fabrication, which the product refuses everywhere else.
/// </remarks>
public enum SourceContentVerification
{
    /// <summary>No verification was attempted because the frame did not resolve to a document.</summary>
    NotApplicable,

    /// <summary>The on-disk bytes reproduce the PDB's document checksum; the content is the build's source.</summary>
    VerifiedExact,

    /// <summary>A file exists at the recorded path, but its bytes do not reproduce the document checksum.</summary>
    ContentMismatch,

    /// <summary>No file exists at the build-recorded path on this machine.</summary>
    FileMissing,

    /// <summary>The document carries no checksum this host can verify, so content is deliberately not shown.</summary>
    ChecksumUnavailable,

    /// <summary>The file exists but exceeds the display byte bound, so content is deliberately not shown.</summary>
    FileTooLarge,

    /// <summary>
    /// No matching PDB or verified source was available, so the shown content is C# decompiled from the IL of an
    /// on-disk assembly whose complete metadata content identity matches the dump module's. It is a faithful
    /// reconstruction labelled as such — never presented as the original source.
    /// </summary>
    DecompiledFromValidatedAssembly,
}

/// <summary>One rendered source line.</summary>
/// <param name="Number">The one-based line number.</param>
/// <param name="Text">The exact line text with tabs preserved.</param>
/// <param name="IsHighlighted">Whether this line is inside the frame's mapped span.</param>
public sealed record SourceLineRow(int Number, string Text, bool IsHighlighted);

/// <summary>
/// The complete display projection of one frame-to-source resolution, including the honest statement of what the
/// viewer is and is not looking at.
/// </summary>
public sealed record SourceViewResult
{
    /// <summary>Gets whether the frame resolved to an exact document and line span.</summary>
    public required bool IsResolved { get; init; }

    /// <summary>Gets the primary display line for the tool-window header.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a plain-language statement of the outcome, exact or not.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the build-recorded document path, or null when the frame did not resolve.</summary>
    public string? DocumentPath { get; init; }

    /// <summary>Gets the one-based first line of the mapped span, or zero when unresolved.</summary>
    public int StartLine { get; init; }

    /// <summary>Gets the one-based last line of the mapped span, or zero when unresolved.</summary>
    public int EndLine { get; init; }

    /// <summary>Gets what was proven about the on-disk file before any content was shown.</summary>
    public SourceContentVerification Verification { get; init; } = SourceContentVerification.NotApplicable;

    /// <summary>Gets the rendered lines; empty unless <see cref="Verification"/> is <c>VerifiedExact</c>.</summary>
    public ImmutableArray<SourceLineRow> Lines { get; init; } = [];

    /// <summary>Gets labelled resolution and verification facts for the evidence pane.</summary>
    public ImmutableArray<PropertyRow> Facts { get; init; } = [];

    /// <summary>Gets the canonical replay digest of the underlying observation, when one was produced.</summary>
    public string? Sha256 { get; init; }
}
