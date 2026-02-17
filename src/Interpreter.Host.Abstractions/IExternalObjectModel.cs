using Interpreter.Core.Abstractions;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Provides read-only external heap/object access for overlay memory and projection models.
/// </summary>
public interface IExternalObjectModel
{
    /// <summary>Tries to get the runtime type of an external object.</summary>
    bool TryGetObjectType(ExternalObjectRef obj, out TypeHandle runtimeType);

    /// <summary>Tries to read a string object with a maximum character cap.</summary>
    bool TryReadString(ExternalObjectRef obj, int maxChars, out string? value);

    /// <summary>Tries to get array length for an external array object.</summary>
    bool TryGetArrayLength(ExternalObjectRef arrayObj, out int length);

    /// <summary>Tries to read an object field value.</summary>
    bool TryReadField(ExternalObjectRef obj, FieldHandle field, out ExternalValue value);

    /// <summary>Tries to read an array element value.</summary>
    bool TryReadArrayElement(ExternalObjectRef arrayObj, int index, out ExternalValue value);
}
