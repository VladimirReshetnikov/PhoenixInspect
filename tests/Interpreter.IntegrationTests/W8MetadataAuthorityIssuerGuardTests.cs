using System.Reflection;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Guards the W8.2 cutover: metadata identities are issued from authority rows, never W7 candidates.</summary>
public sealed class W8MetadataAuthorityIssuerGuardTests
{
    /// <summary>
    /// Proves no public metadata-identity factory outside the explicit W7 compatibility surface accepts a
    /// caller-authored W7 TypeDef candidate, and that the pre-authority identity chain no longer exists.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Metadata_identity_factories_accept_no_w7_candidates_outside_compatibility()
    {
        var assembly = typeof(MetadataTypeDefinitionAuthorityIdentity).Assembly;
        var identityTypes = assembly.GetExportedTypes()
            .Where(static type =>
                type.Namespace == "Interpreter.Product.DumpQuery" &&
                type.Name.StartsWith("Metadata", StringComparison.Ordinal) &&
                type.Name.EndsWith("Identity", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(identityTypes);

        foreach (var type in identityTypes)
        {
            if (type.Name.Contains("Compatibility", StringComparison.Ordinal) ||
                type.Name.Contains("W7", StringComparison.Ordinal))
            {
                continue;
            }
            var factories = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static method =>
                    method.Name == "Create" || method.Name.StartsWith("From", StringComparison.Ordinal));
            foreach (var factory in factories)
            {
                Assert.DoesNotContain(
                    factory.GetParameters(),
                    static parameter => parameter.ParameterType == typeof(StaticFieldTypeDefinitionIdentity));
            }
        }

        var deletedTypeNames = new[]
        {
            "Interpreter.Product.DumpQuery.MetadataRawTypeDefinitionIdentity",
            "Interpreter.Product.DumpQuery.MetadataTypeDefinitionIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterOwnerIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterIdentity",
            "Interpreter.Product.DumpQuery.MetadataDelegateTypeAncestryIdentity",
            "Interpreter.Product.DumpQuery.MetadataBaseTypeEdgeIdentity",
            "Interpreter.Product.DumpQuery.MetadataTypeArgumentBindingIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericMethodDeclarationCertificateIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterOwnerDeclarationIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterTableCatalogIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterOwnerSetIdentity",
            "Interpreter.Product.DumpQuery.MetadataGenericParameterBindingLedgerIdentity",
        };
        foreach (var deletedTypeName in deletedTypeNames)
        {
            Assert.Null(assembly.GetType(deletedTypeName));
            Assert.Null(Type.GetType(deletedTypeName));
        }
    }
}
