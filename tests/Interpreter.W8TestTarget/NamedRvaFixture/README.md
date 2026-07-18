# Named FieldRVA fixture

`Interpreter.W8NamedRvaTarget.il` is the authoritative source for two explicitly named static fields with
four-byte and eight-byte RVA data. C# can materialize anonymous implementation-detail fields for data blobs, but it
does not expose source syntax that assigns a chosen field declaration an RVA. Keeping this tiny IL assembly separate
preserves the distinction between the named branch and the compiler-generated comparison already present in the main
target.

The checked-in DLL was assembled with Microsoft.NETCore.ILAsm 10.0.9 using `/dll /det /nologo /quiet`. Reassembling
the IL with those switches produces SHA-256
`E4E02DFA15FDDBE8D2A85C0E9FEBDA080AA370FA7E85E2FF66C2A0557FE5D44C`. The target references the DLL and reads both
values before announcing the `rva-frame` readiness marker, so a load or data mismatch fails deterministically.
