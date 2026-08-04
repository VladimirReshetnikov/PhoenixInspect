# Named FieldRVA fixture

`PhoenixInspect.W8NamedRvaTarget.il` is the authoritative source for two explicitly named static fields with
four-byte and eight-byte RVA data. C# can materialize anonymous implementation-detail fields for data blobs, but it
does not expose source syntax that assigns a chosen field declaration an RVA. Keeping this tiny IL assembly separate
preserves the distinction between the named branch and the compiler-generated comparison already present in the main
target.

The checked-in DLL was assembled with Microsoft.NETCore.ILAsm 10.0.9 using `/dll /det /nologo /quiet`. Reassembling
the IL with those switches produces SHA-256
`4AA76F7410236333877576B167A9524B29863FBE484028E959DCEEF68CB0E3E5`. The target references the DLL and reads both
values before announcing the `rva-frame` readiness marker, so a load or data mismatch fails deterministically.

The `System.Runtime` extern declares the framework public key token `B03F5F7F11D50A3A` exactly as compiled
assemblies do. The original fixture omitted it, which no compiler output reproduces, and a token-free reference
cannot identity-bind the strong-named framework assembly definition under the product's exact AssemblyRef matching
rule — the fixture's `System.Object` base then resolved to no composed module and its owner classification carried
no role. The correction makes the fixture representative; the matching rule is unchanged.
