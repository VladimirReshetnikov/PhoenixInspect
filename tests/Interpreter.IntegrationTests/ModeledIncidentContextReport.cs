using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Interpreter.IntegrationTests;

internal enum ModeledIncidentAxis
{
    This,
    Argument,
    Local,
    Static,
    StrongRoot,
}

internal enum RawSelectionObservationKind
{
    Unique,
    Unavailable,
    Ambiguous,
    Partial,
    Conflict,
    Invalid,
}

internal enum RawMemberBytesObservationKind
{
    Exact,
    Unavailable,
    Conflict,
    Invalid,
}

internal enum RawContextAttributionKind
{
    UnavailableStackSlotObservationNotAdmitted,
    UnavailableStaticFieldObservation,
    ExactStaticField,
    ExactStrongHandle,
    Unavailable,
    Conflict,
    Invalid,
}

internal enum ProductQueryObservationKind
{
    Exact,
    Unavailable,
    Invalid,
}

internal sealed record ModeledIncidentAxisDefinition(
    ModeledIncidentAxis Axis,
    string CanonicalName,
    string RuntimeTypeName,
    int ExpectedMarker);

internal sealed record ModeledIncidentAxisMeasurement(
    ModeledIncidentAxisDefinition Definition,
    RawSelectionObservationKind RawSelection,
    RawMemberBytesObservationKind RawMemberBytes,
    RawContextAttributionKind RawContext,
    ProductQueryObservationKind ProductQuery,
    string ProductDiagnosticCode);

internal static class ModeledIncidentContextCorpus
{
    internal const string TargetAssemblyName = "Interpreter.OptimizedContextTestTarget.dll";
    internal const string StaticHolderTypeName =
        "Interpreter.OptimizedContextTestTarget.StaticContextProbe";
    internal const string StaticFieldName = "Root";
    internal const string MarkerFieldName = "Marker";

    internal static ImmutableArray<ModeledIncidentAxisDefinition> Axes { get; } =
    [
        new(
            ModeledIncidentAxis.This,
            "this",
            "Interpreter.OptimizedContextTestTarget.ThisContextProbe",
            0x1A11C001),
        new(
            ModeledIncidentAxis.Argument,
            "argument",
            "Interpreter.OptimizedContextTestTarget.ArgumentContextProbe",
            0x2A22C002),
        new(
            ModeledIncidentAxis.Local,
            "local",
            "Interpreter.OptimizedContextTestTarget.LocalContextProbe",
            0x3A33C003),
        new(
            ModeledIncidentAxis.Static,
            "static",
            "Interpreter.OptimizedContextTestTarget.StaticContextProbe",
            0x4A44C004),
        new(
            ModeledIncidentAxis.StrongRoot,
            "strong-root",
            "Interpreter.OptimizedContextTestTarget.StrongRootContextProbe",
            0x5A55C005),
    ];
}

internal sealed class ModeledIncidentContextReport
{
    internal const string Schema = "interpreter-modeled-incident-context-report/v1";
    internal const string Corpus = "generated-optimized-release-full-dump";
    internal const string Scope = "modeled-incident-not-private-production";

    private readonly ImmutableArray<ModeledIncidentAxisMeasurement> _measurements;

    internal ModeledIncidentContextReport(ImmutableArray<ModeledIncidentAxisMeasurement> measurements)
    {
        if (measurements.IsDefault)
        {
            throw new ArgumentException("Measurements must be initialized.", nameof(measurements));
        }

        if (measurements.Length != ModeledIncidentContextCorpus.Axes.Length)
        {
            throw new ArgumentException("Every predeclared context axis must remain in the report.", nameof(measurements));
        }

        for (var index = 0; index < measurements.Length; index++)
        {
            var measurement = measurements[index]
                ?? throw new ArgumentException("Measurements cannot contain null entries.", nameof(measurements));
            if (measurement.Definition != ModeledIncidentContextCorpus.Axes[index])
            {
                throw new ArgumentException(
                    "Measurements must use the complete predeclared axis order.",
                    nameof(measurements));
            }

            if (measurement.ProductQuery == ProductQueryObservationKind.Exact &&
                !string.Equals(measurement.ProductDiagnosticCode, "none", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An exact product outcome cannot carry a failure diagnostic.",
                    nameof(measurements));
            }

            if (measurement.ProductQuery != ProductQueryObservationKind.Exact &&
                string.Equals(measurement.ProductDiagnosticCode, "none", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A non-exact product outcome requires a stable diagnostic code.",
                    nameof(measurements));
            }
        }

        _measurements = measurements;
    }

    internal int RawMemberBytesNumerator =>
        _measurements.Count(static item => item.RawMemberBytes == RawMemberBytesObservationKind.Exact);

    internal int RawMemberBytesDenominator => _measurements.Length;

    internal int RawContextNumerator =>
        _measurements.Count(static item => item.RawContext is
            RawContextAttributionKind.ExactStaticField or RawContextAttributionKind.ExactStrongHandle);

    internal int RawContextDenominator => _measurements.Length;

    internal int ProductQueryNumerator =>
        _measurements.Count(static item => item.ProductQuery == ProductQueryObservationKind.Exact);

    internal int ProductQueryDenominator => _measurements.Length;

    internal string ToCanonicalText()
    {
        var builder = new StringBuilder(capacity: 1_024);
        Append(builder, "schema", Schema);
        Append(builder, "corpus", Corpus);
        Append(builder, "scope", Scope);
        Append(builder, "corpus-composition", "one-generated-dump-five-predeclared-axes");
        Append(builder, "target-profile", "net10.0-coreclr-windows-x64-release-optimized");
        Append(builder, "capture-mechanism", "diagnostics-client-full-dump");
        Append(builder, "raw-stack-slot-observation", "not-admitted-dotnet10-dac-boundary");
        Append(builder, "raw-member-bytes-numerator", RawMemberBytesNumerator);
        Append(builder, "raw-member-bytes-denominator", RawMemberBytesDenominator);
        Append(builder, "raw-context-attribution-numerator", RawContextNumerator);
        Append(builder, "raw-context-attribution-denominator", RawContextDenominator);
        Append(builder, "product-query-availability-numerator", ProductQueryNumerator);
        Append(builder, "product-query-availability-denominator", ProductQueryDenominator);

        foreach (var measurement in _measurements)
        {
            var contextAvailable = measurement.RawContext is
                RawContextAttributionKind.ExactStaticField or RawContextAttributionKind.ExactStrongHandle;
            Append(
                builder,
                $"raw-context-{measurement.Definition.CanonicalName}-numerator",
                contextAvailable ? 1 : 0);
            Append(builder, $"raw-context-{measurement.Definition.CanonicalName}-denominator", 1);
        }

        foreach (var measurement in _measurements)
        {
            builder.Append("axis=");
            builder.Append(measurement.Definition.CanonicalName);
            builder.Append(";selection=");
            builder.Append(ToCanonical(measurement.RawSelection));
            builder.Append(";member-bytes=");
            builder.Append(ToCanonical(measurement.RawMemberBytes));
            builder.Append(";raw-context=");
            builder.Append(ToCanonical(measurement.RawContext));
            builder.Append(";product-query=");
            builder.Append(ToCanonical(measurement.ProductQuery));
            builder.Append(";diagnostic=");
            builder.Append(measurement.ProductDiagnosticCode);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }

    private static void Append(StringBuilder builder, string name, int value) =>
        Append(builder, name, value.ToString(CultureInfo.InvariantCulture));

    private static string ToCanonical(RawSelectionObservationKind value) => value switch
    {
        RawSelectionObservationKind.Unique => "unique",
        RawSelectionObservationKind.Unavailable => "unavailable",
        RawSelectionObservationKind.Ambiguous => "ambiguous",
        RawSelectionObservationKind.Partial => "partial",
        RawSelectionObservationKind.Conflict => "conflict",
        RawSelectionObservationKind.Invalid => "invalid",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ToCanonical(RawMemberBytesObservationKind value) => value switch
    {
        RawMemberBytesObservationKind.Exact => "exact",
        RawMemberBytesObservationKind.Unavailable => "unavailable",
        RawMemberBytesObservationKind.Conflict => "conflict",
        RawMemberBytesObservationKind.Invalid => "invalid",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ToCanonical(RawContextAttributionKind value) => value switch
    {
        RawContextAttributionKind.UnavailableStackSlotObservationNotAdmitted =>
            "unavailable-stack-slot-observation-not-admitted",
        RawContextAttributionKind.UnavailableStaticFieldObservation =>
            "unavailable-static-field-observation",
        RawContextAttributionKind.ExactStaticField => "exact-static-field",
        RawContextAttributionKind.ExactStrongHandle => "exact-strong-handle",
        RawContextAttributionKind.Unavailable => "unavailable",
        RawContextAttributionKind.Conflict => "conflict",
        RawContextAttributionKind.Invalid => "invalid",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ToCanonical(ProductQueryObservationKind value) => value switch
    {
        ProductQueryObservationKind.Exact => "exact",
        ProductQueryObservationKind.Unavailable => "unavailable",
        ProductQueryObservationKind.Invalid => "invalid",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
