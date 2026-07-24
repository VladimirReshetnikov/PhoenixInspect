using System.Reflection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

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
                type.Namespace == "PhoenixInspect.Product.DumpQuery" &&
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
            "PhoenixInspect.Product.DumpQuery.MetadataRawTypeDefinitionIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataTypeDefinitionIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterOwnerIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataDelegateTypeAncestryIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataBaseTypeEdgeIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataTypeArgumentBindingIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericMethodDeclarationCertificateIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterOwnerDeclarationIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterTableCatalogIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterOwnerSetIdentity",
            "PhoenixInspect.Product.DumpQuery.MetadataGenericParameterBindingLedgerIdentity",
        };
        foreach (var deletedTypeName in deletedTypeNames)
        {
            Assert.Null(assembly.GetType(deletedTypeName));
            Assert.Null(Type.GetType(deletedTypeName));
        }
    }
}
