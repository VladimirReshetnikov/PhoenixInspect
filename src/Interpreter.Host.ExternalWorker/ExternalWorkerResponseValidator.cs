namespace Interpreter.Host.ExternalWorker;

internal static class ExternalWorkerResponseValidator
{
    private const int MaximumProvenanceEntries = 64;
    private const int MaximumDiagnostics = 8;

    private static readonly IReadOnlyDictionary<string, string> OutcomeMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WORKER_QUERY_RESULT"] = "The constrained worker returned one bounded query result.",
            ["WORKER_REQUEST_INVALID"] = "The external worker request violates the fixed query bounds.",
            ["WORKER_PLATFORM_UNSUPPORTED"] = "The external worker requires 64-bit Windows containment.",
            ["WORKER_DAC_TRUST_REJECTED"] = "The runtime-adjacent DAC does not match the compiled trust pin.",
            ["WORKER_ARTIFACT_REJECTED"] =
                "The inherited dump artifact is unavailable, invalid, unsupported, or exceeds a bound.",
            ["WORKER_ADAPTER_POLICY_REJECTED"] =
                "The dump adapter did not retain the required offline explicit-DAC policy.",
            ["WORKER_INTERNAL_FAILURE"] = "The external worker could not complete the bounded request.",
        };

    private static readonly IReadOnlyDictionary<string, string> DiagnosticMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["QUERY_EXPRESSION_REQUIRED"] = "A dump-query expression is required.",
            ["QUERY_EXPRESSION_TOO_LONG"] = "The dump-query expression exceeds the deterministic length limit.",
            ["QUERY_ROOT_NAME_TOO_LONG"] = "The configured root name exceeds the deterministic identifier limit.",
            ["QUERY_ROOT_NAME_INVALID"] = "The configured root name is not a supported identifier.",
            ["QUERY_ROOT_MISMATCH"] = "The expression does not reference the configured root name exactly.",
            ["QUERY_SYNTAX_UNSUPPORTED"] =
                "The expression contains syntax outside the supported dump-query grammar.",
            ["QUERY_MEMBER_ACCESS_REQUIRED"] = "The supported grammar requires one instance-field access.",
            ["QUERY_IDENTIFIER_TOO_LONG"] = "An expression identifier exceeds the deterministic identifier limit.",
            ["QUERY_IDENTIFIER_INVALID"] = "The expression contains a missing or unsupported identifier.",
            ["QUERY_LITERAL_REQUIRED"] = "Null coalescing requires one supported literal.",
            ["QUERY_LITERAL_UNSUPPORTED"] =
                "The expression uses a literal outside the supported null, Int32, and string set.",
            ["QUERY_INT32_LITERAL_INVALID"] = "The integer literal is outside the supported Int32 range.",
            ["QUERY_STRING_LITERAL_INVALID"] = "String literals cannot contain unescaped line breaks.",
            ["QUERY_STRING_ESCAPE_UNSUPPORTED"] = "The string literal contains an unsupported escape sequence.",
            ["QUERY_STRING_LITERAL_TOO_LONG"] =
                "The string literal exceeds the deterministic decoded-length limit.",
            ["QUERY_ROOT_UNAVAILABLE"] = "No exact root object is available for the dump query.",
            ["QUERY_FIELD_TYPE_UNSUPPORTED"] =
                "The selected field type is outside the supported Int32 and string query domain.",
            ["QUERY_COALESCE_TYPE_UNSUPPORTED"] =
                "The null-coalescing literal is incompatible with the selected field type.",
            ["DUMP_SNAPSHOT_MISMATCH"] = "Evidence belongs to a different immutable dump snapshot.",
            ["DUMP_MODULE_UNAVAILABLE"] = "The selected runtime module is unavailable.",
            ["DUMP_METADATA_UNAVAILABLE"] = "The selected runtime module has no complete metadata image.",
            ["DUMP_ARTIFACT_UNAVAILABLE"] = "The dump artifact could not be opened.",
            ["DUMP_ARTIFACT_INVALID"] = "The dump artifact is structurally invalid.",
            ["DUMP_RUNTIME_UNSUPPORTED"] = "The dump runtime configuration is outside the supported profile.",
            ["DUMP_OBJECT_UNAVAILABLE"] = "The selected runtime object is unavailable.",
            ["DUMP_FIELD_UNAVAILABLE"] = "The requested runtime field is unavailable.",
            ["DUMP_TYPE_UNAVAILABLE"] = "The requested runtime type is unavailable.",
            ["DUMP_METHOD_UNAVAILABLE"] = "The requested runtime method is unavailable.",
            ["DUMP_AMBIGUOUS_MATCH"] = "More than one runtime candidate matched the request.",
            ["DUMP_METHOD_BODY_UNAVAILABLE"] = "The runtime method has no supported IL body evidence.",
            ["DUMP_METHOD_BODY_LAYOUT_UNSUPPORTED"] =
                "The runtime module layout cannot yet map a MethodDef RVA to counted target memory.",
            ["DUMP_METHOD_HEADER_UNSUPPORTED"] =
                "The dump method header uses an extensible encoding outside the current profile.",
            ["DUMP_METHOD_SECTION_UNSUPPORTED"] =
                "The dump method body declares an unsupported extra-section kind.",
            ["DUMP_METHOD_IDENTITY_MISMATCH"] =
                "Runtime-selected method identity conflicts with counted dump metadata.",
            ["DUMP_TYPE_MISMATCH"] = "The selected runtime evidence has an incompatible type.",
            ["DUMP_MEMORY_UNAVAILABLE"] = "Required dump-memory bytes are incomplete or unavailable.",
            ["DUMP_INVALID_DATA"] = "Captured runtime evidence violates a supported layout invariant.",
            ["DUMP_LIMIT_EXCEEDED"] = "A deterministic evidence bound truncated the operation.",
        };

    internal static bool IsValid(ExternalDumpQueryResponse? response)
    {
        if (response is null ||
            !Enum.IsDefined(response.Outcome) ||
            response.AppliedBounds != ExternalWorkerPolicy.AppliedBounds ||
            response.Attestation is null ||
            response.Diagnostics is null ||
            response.Diagnostics.Length > MaximumDiagnostics ||
            response.Diagnostics.Any(static diagnostic => !IsKnownDiagnostic(diagnostic)) ||
            !OutcomeMessages.TryGetValue(response.Code ?? string.Empty, out var expectedMessage) ||
            !string.Equals(response.Message, expectedMessage, StringComparison.Ordinal))
        {
            return false;
        }

        return response.Outcome == ExternalWorkerOutcome.Completed
            ? IsValidCompletedResponse(response)
            : IsValidFailureResponse(response);
    }

    private static bool IsValidCompletedResponse(ExternalDumpQueryResponse response)
    {
        if (!string.Equals(response.Code, "WORKER_QUERY_RESULT", StringComparison.Ordinal) ||
            !string.Equals(response.SemanticMode, "DerivedQuery", StringComparison.Ordinal) ||
            !IsOneOf(response.Completion, "Completed", "Blocked", "BudgetExhausted", "Cancelled", "Invalid") ||
            !IsOneOf(response.Completeness, "Complete", "Partial", "None") ||
            !IsOneOf(response.Evidence, "Exact", "Partial", "Unavailable", "Conflict", "Invalid") ||
            !string.Equals(response.Effects, "None", StringComparison.Ordinal) ||
            !string.Equals(response.EvidenceSource, "DumpMemory", StringComparison.Ordinal) ||
            !string.Equals(response.Fallback, "None", StringComparison.Ordinal) ||
            response.SnapshotIdentity is null ||
            !IsLowercaseSha256(response.SnapshotIdentity.Sha256) ||
            !string.Equals(
                response.SnapshotIdentity.MemorySourceId,
                $"dump-sha256:{response.SnapshotIdentity.Sha256}",
                StringComparison.Ordinal) ||
            response.ProvenanceCount is < 0 or > MaximumProvenanceEntries)
        {
            return false;
        }

        if (response.ModuleIdentity is { } module &&
            (!string.Equals(module.SnapshotSha256, response.SnapshotIdentity.Sha256, StringComparison.Ordinal) ||
             module.ModuleAddress == 0))
        {
            return false;
        }

        var hasValue = response.Value is not null;
        if (string.Equals(response.Completeness, "None", StringComparison.Ordinal) == hasValue ||
            !string.Equals(response.Completion, "Completed", StringComparison.Ordinal) &&
            string.Equals(response.Completeness, "Complete", StringComparison.Ordinal))
        {
            return false;
        }

        return response.Value is null || IsValidValue(response.Value, response.Completeness!);
    }

    private static bool IsValidFailureResponse(ExternalDumpQueryResponse response)
    {
        var expectedCode = response.Outcome switch
        {
            ExternalWorkerOutcome.InvalidRequest => "WORKER_REQUEST_INVALID",
            ExternalWorkerOutcome.ArtifactRejected => "WORKER_ARTIFACT_REJECTED",
            ExternalWorkerOutcome.TrustedDacRejected => "WORKER_DAC_TRUST_REJECTED",
            ExternalWorkerOutcome.ContainmentUnavailable when response.Code == "WORKER_PLATFORM_UNSUPPORTED" =>
                "WORKER_PLATFORM_UNSUPPORTED",
            ExternalWorkerOutcome.ContainmentUnavailable => "WORKER_ADAPTER_POLICY_REJECTED",
            ExternalWorkerOutcome.WorkerFailure => "WORKER_INTERNAL_FAILURE",
            _ => null,
        };
        return expectedCode is not null &&
               string.Equals(response.Code, expectedCode, StringComparison.Ordinal) &&
               response.SemanticMode is null &&
               response.Completion is null &&
               response.Completeness is null &&
               response.Evidence is null &&
               response.Effects is null &&
               response.SnapshotIdentity is null &&
               response.ModuleIdentity is null &&
               string.Equals(response.EvidenceSource, "None", StringComparison.Ordinal) &&
               string.Equals(response.Fallback, "None", StringComparison.Ordinal) &&
               response.Value is null &&
               response.ProvenanceCount == 0 &&
               response.Diagnostics.Length == 0;
    }

    private static bool IsValidValue(ExternalDumpQueryValue value, string completeness) => value.Kind switch
    {
        "Null" => value.Int32Value is null && value.StringValue is null && completeness == "Complete",
        "Int32" => value.Int32Value is not null && value.StringValue is null && completeness == "Complete",
        "String" => value.Int32Value is null &&
                    value.StringValue is { Length: <= ExternalWorkerPolicy.MaximumObservedStringCharacters },
        _ => false,
    };

    private static bool IsKnownDiagnostic(ExternalWorkerDiagnostic? diagnostic) =>
        diagnostic is not null &&
        (DiagnosticMessages.TryGetValue(diagnostic.Code ?? string.Empty, out var expectedMessage) &&
         string.Equals(diagnostic.Message, expectedMessage, StringComparison.Ordinal) ||
         string.Equals(diagnostic.Code, "QUERY_STRING_LITERAL_INVALID", StringComparison.Ordinal) &&
         IsOneOf(
             diagnostic.Message,
             "The string literal has an incomplete escape sequence.",
             "The string literal is not terminated."));

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsOneOf(string? value, params string[] candidates) =>
        candidates.Contains(value, StringComparer.Ordinal);
}
