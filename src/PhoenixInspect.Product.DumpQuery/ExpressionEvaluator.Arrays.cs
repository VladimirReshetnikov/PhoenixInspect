using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PhoenixInspect.Product.DumpQuery;

/// <content>
/// Array creation in every C# spelling — sized with C#'s zero-fill semantics, initialized, typed empty,
/// collection expressions with spreads, and <c>Array.Empty&lt;T&gt;()</c> — plus the pure functional
/// <see cref="Array"/> surface over virtual sequences. The mutating statics are typed stops: a sequence is
/// immutable evidence, and each stop names the functional operator that answers the same question.
/// </content>
public static partial class ExpressionEvaluator
{
    /// <summary>Describes one resolved array element type across every operand domain.</summary>
    private readonly record struct ElementDescriptor(
        OperandKind Kind,
        NumericKind Numeric,
        TemporalKind Temporal,
        BclValueKind Value,
        EnumShape? Enum,
        string DisplayName)
    {
        /// <summary>Gets the default element C# zero-fills a new array with.</summary>
        public Operand DefaultElement => Kind switch
        {
            OperandKind.String => Operand.Null(),
            OperandKind.Null => Operand.Null(),
            OperandKind.Char => Operand.FromChar('\0'),
            OperandKind.Boolean => Operand.FromBoolean(false),
            OperandKind.Int32 => Operand.FromInt32(0),
            OperandKind.Numeric => Operand.FromNumeric(Numeric, BoxFromBigInteger(Numeric, 0)),
            OperandKind.Temporal => Operand.FromTemporal(Temporal, Temporal switch
            {
                TemporalKind.DateTime => default(DateTime),
                TemporalKind.DateTimeOffset => default(DateTimeOffset),
                TemporalKind.TimeSpan => default(TimeSpan),
                TemporalKind.DateOnly => default(DateOnly),
                _ => default(TimeOnly),
            }),
            OperandKind.BclValue when Value == BclValueKind.Guid => Operand.FromBclValue(BclValueKind.Guid, Guid.Empty),
            OperandKind.BclValue => Operand.Null(),
            _ => Enum is { } shape ? EnumOperand(shape, 0) : Operand.FromInt32(0),
        };

        /// <summary>Builds the empty or filled payload for this element type.</summary>
        public SequencePayload Payload(ImmutableArray<Operand> items) => new(
            items,
            Kind,
            Kind is OperandKind.Int32 or OperandKind.Numeric ? Numeric : default,
            DisplayName);
    }

    /// <summary>Resolves an array element type across the evaluator's operand domains.</summary>
    private static bool TryResolveElementType(
        TypeSyntax type,
        FoldContext context,
        out ElementDescriptor descriptor,
        out FoldOutcome? error)
    {
        descriptor = default;
        error = null;

        // 'object[]' is the reflection argument carrier: its elements keep their own folded domains, so a mixed
        // argument list like 'new object[] { 1, "a" }' passes to Invoke without a common-element conversion.
        // 'dynamic[]' erases to object[], so it shares the same descriptor.
        if (type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
            or IdentifierNameSyntax { Identifier.ValueText: "dynamic" })
        {
            descriptor = new ElementDescriptor(OperandKind.Null, default, default, default, null, "Object");
            return true;
        }

        if (TryResolveCastType(type, out var castTarget, out var numericKind, out var nullable) && !nullable)
        {
            descriptor = castTarget switch
            {
                CastTarget.String => new ElementDescriptor(
                    OperandKind.String, default, default, default, null, "String"),
                CastTarget.Char => new ElementDescriptor(OperandKind.Char, default, default, default, null, "Char"),
                CastTarget.Boolean => new ElementDescriptor(
                    OperandKind.Boolean, default, default, default, null, "Boolean"),
                _ => new ElementDescriptor(
                    numericKind == NumericKind.Int32 ? OperandKind.Int32 : OperandKind.Numeric,
                    numericKind,
                    default,
                    default,
                    null,
                    numericKind.ToString()),
            };
            return true;
        }

        if (TryReadTemporalTypeName(type, out var temporalKind))
        {
            descriptor = new ElementDescriptor(
                OperandKind.Temporal, default, temporalKind, default, null, temporalKind.ToString());
            return true;
        }

        if (TryReadBclValueTypeName(type, out var valueKind))
        {
            descriptor = new ElementDescriptor(
                OperandKind.BclValue, default, default, valueKind, null, valueKind.ToString());
            return true;
        }

        var enumName = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified when TryReadDottedName(qualified, out var dotted) => dotted,
            _ => null,
        };
        if (enumName is not null)
        {
            error = TryResolveEnumShape(enumName, context, out var shape);
            if (error is not null)
            {
                return false;
            }

            if (shape is not null)
            {
                descriptor = new ElementDescriptor(
                    OperandKind.Enum, shape.Underlying, default, default, shape, shape.ShortName);
                return true;
            }
        }

        return false;
    }

    // ---- Creation expressions ---------------------------------------------------------------------------------------

    /// <summary>The greatest rectangular array rank one creation admits, a deterministic bound.</summary>
    private const int MaximumArrayRank = 8;

    /// <summary>
    /// Folds every supported explicit array creation: an initializer, an exact size with C#'s zero-fill
    /// semantics, both together (the counts must agree, as C# requires), a typed empty array, and the
    /// rectangular forms 'new T[m,n]' and 'new T[,]{{…},{…}}'. A creation whose element type the evaluator
    /// does not model stays not-folded, and a jagged creation is a typed stop.
    /// </summary>
    private static FoldOutcome FoldArrayCreation(ArrayCreationExpressionSyntax creation, FoldContext context)
    {
        if (creation.Type.RankSpecifiers.Count != 1)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Jagged array creation is not modeled; a collection expression such as [[1, 2], [3, 4]] builds "
                + "a sequence of sequences.");
        }

        if (creation.Type.RankSpecifiers[0].Sizes.Count > 1)
        {
            return FoldRectangularArrayCreation(creation, context);
        }

        var elementKnown = TryResolveElementType(creation.Type.ElementType, context, out var element, out var error);
        if (error is not null)
        {
            return error.Value;
        }

        var sizeExpression = creation.Type.RankSpecifiers[0].Sizes[0];
        int? declaredLength = null;
        if (sizeExpression is not OmittedArraySizeExpressionSyntax)
        {
            var size = Fold(sizeExpression, context);
            if (size.Disposition != FoldDisposition.Folded)
            {
                return size;
            }

            if (!TryImplicitInt32(size.Operand, out var length))
            {
                return FoldOutcome.Error(OperandTypeCode, "An array length must be an exact Int32 value.");
            }

            if (length < 0)
            {
                return FoldOutcome.Error("System.OverflowException", "An array length cannot be negative.");
            }

            if (length > MaximumSequenceLength)
            {
                return FoldOutcome.Error(
                    SequenceBoundCode,
                    $"The array would hold {length.ToString(CultureInfo.InvariantCulture)} elements, beyond the "
                    + $"deterministic bound of {MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.");
            }

            declaredLength = length;
        }

        if (creation.Initializer is { } initializer)
        {
            if (declaredLength is { } declared && declared != initializer.Expressions.Count)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "The declared array length must equal the initializer's element count, as C# requires.");
            }

            if (initializer.Expressions.Count == 0)
            {
                return elementKnown
                    ? CreateSequence(element.Payload([]))
                    : FoldOutcome.NotArithmetic();
            }

            // An object[] initializer skips element inference: each element keeps its own folded domain.
            if (elementKnown && element.Kind == OperandKind.Null)
            {
                var mixed = ImmutableArray.CreateBuilder<Operand>(initializer.Expressions.Count);
                foreach (var expression in initializer.Expressions)
                {
                    var folded = Fold(expression, context);
                    if (folded.Disposition != FoldDisposition.Folded)
                    {
                        return folded;
                    }

                    mixed.Add(folded.Operand);
                }

                return CreateSequence(element.Payload(mixed.MoveToImmutable()));
            }

            // A non-empty initializer infers exactly as the implicit form does; the declared element type is
            // honored when the evaluator models it by converting the inferred elements.
            var initialized = FoldInitializer(initializer, context);
            if (!elementKnown || initialized.Disposition != FoldDisposition.Folded)
            {
                return initialized;
            }

            return ConvertSequenceToElement(initialized.Operand, element);
        }

        if (declaredLength is not { } fillLength)
        {
            return FoldOutcome.NotArithmetic();
        }

        if (!elementKnown)
        {
            return FoldOutcome.NotArithmetic();
        }

        // 'new T[n]' zero-fills: that is C#'s defined semantics, not a fabricated value.
        return CreateSequence(element.Payload([.. Enumerable.Repeat(element.DefaultElement, fillLength)]));
    }

    /// <summary>
    /// Folds a rectangular array creation: 'new int[,]{{1,2},{3,4}}' infers its dimensions from the nested
    /// initializer, 'new int[2,3]' zero-fills, and both together must agree, as C# requires. Items are stored
    /// flat in row-major order — the order .NET enumerates a rectangular array in — so the whole sequence
    /// surface applies unchanged and Length stays the total element count.
    /// </summary>
    private static FoldOutcome FoldRectangularArrayCreation(
        ArrayCreationExpressionSyntax creation,
        FoldContext context)
    {
        var sizes = creation.Type.RankSpecifiers[0].Sizes;
        var rank = sizes.Count;
        if (rank > MaximumArrayRank)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"An array rank beyond the deterministic bound of "
                + $"{MaximumArrayRank.ToString(CultureInfo.InvariantCulture)} is not modeled.");
        }

        var elementKnown = TryResolveElementType(creation.Type.ElementType, context, out var element, out var error);
        if (error is not null)
        {
            return error.Value;
        }

        if (!elementKnown)
        {
            return FoldOutcome.NotArithmetic();
        }

        var declaredSizes = new int[rank];
        var haveSizes = false;
        var haveOmitted = false;
        for (var dimension = 0; dimension < rank; dimension++)
        {
            if (sizes[dimension] is OmittedArraySizeExpressionSyntax)
            {
                haveOmitted = true;
                continue;
            }

            haveSizes = true;
            var size = Fold(sizes[dimension], context);
            if (size.Disposition != FoldDisposition.Folded)
            {
                return size;
            }

            if (!TryImplicitInt32(size.Operand, out var length))
            {
                return FoldOutcome.Error(OperandTypeCode, "An array dimension length must be an exact Int32 value.");
            }

            if (length < 0)
            {
                return FoldOutcome.Error("System.OverflowException", "An array length cannot be negative.");
            }

            declaredSizes[dimension] = length;
        }

        if (haveSizes && haveOmitted)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "Every dimension length must be given, or every one omitted, as C# requires.");
        }

        if (creation.Initializer is { } initializer)
        {
            var initialized = FoldRectangularInitializer(initializer, rank, element, context, out var dimensions);
            if (initialized.Disposition != FoldDisposition.Folded)
            {
                return initialized;
            }

            if (haveSizes && !dimensions.SequenceEqual(declaredSizes))
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "The declared dimension lengths must equal the initializer's shape, as C# requires.");
            }

            return CreateSequence(PayloadOf(initialized.Operand).WithShape(dimensions, lowerBounds: default));
        }

        if (!haveSizes)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "A rectangular array creation needs dimension lengths or an initializer.");
        }

        return CreateRectangularZeroFilled(element, [.. declaredSizes], lowerBounds: default);
    }

    /// <summary>
    /// Folds one rank-deep nested initializer into a flat row-major sequence of the declared element type,
    /// requiring the uniform shape C# requires, and reports the inferred dimension lengths.
    /// </summary>
    private static FoldOutcome FoldRectangularInitializer(
        InitializerExpressionSyntax initializer,
        int rank,
        ElementDescriptor element,
        FoldContext context,
        out ImmutableArray<int> dimensions)
    {
        var lengths = new int[rank];
        var seen = new bool[rank];
        var items = ImmutableArray.CreateBuilder<Operand>();
        dimensions = default;
        if (Walk(initializer, 0) is { } failure)
        {
            return failure;
        }

        dimensions = [.. lengths];
        var flat = items.ToImmutable();
        if (element.Kind == OperandKind.Null || flat.Length == 0)
        {
            // An object[,] initializer keeps per-element domains, and an empty shape needs no inference.
            return CreateSequence(element.Payload(flat));
        }

        var inferred = CreateInferredSequence(flat);
        if (inferred.Disposition != FoldDisposition.Folded)
        {
            return inferred;
        }

        return ConvertSequenceToElement(inferred.Operand, element);

        FoldOutcome? Walk(InitializerExpressionSyntax level, int depth)
        {
            if (!seen[depth])
            {
                lengths[depth] = level.Expressions.Count;
                seen[depth] = true;
            }
            else if (lengths[depth] != level.Expressions.Count)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "A rectangular initializer must be uniform: every sub-initializer at one depth needs the "
                    + "same length, as C# requires.");
            }

            foreach (var expression in level.Expressions)
            {
                if (depth == rank - 1)
                {
                    if (expression is InitializerExpressionSyntax)
                    {
                        return FoldOutcome.Error(
                            OperandTypeCode,
                            "The initializer nests deeper than the declared rank.");
                    }

                    var folded = Fold(expression, context);
                    if (folded.Disposition != FoldDisposition.Folded)
                    {
                        return folded;
                    }

                    items.Add(folded.Operand);
                }
                else if (expression is InitializerExpressionSyntax nested)
                {
                    if (Walk(nested, depth + 1) is { } nestedFailure)
                    {
                        return nestedFailure;
                    }
                }
                else
                {
                    return FoldOutcome.Error(
                        OperandTypeCode,
                        "Each element at this depth must be a nested { … } initializer, matching the declared "
                        + "rank.");
                }
            }

            return null;
        }
    }

    /// <summary>Builds a zero-filled rectangular array under the product bound.</summary>
    private static FoldOutcome CreateRectangularZeroFilled(
        ElementDescriptor element,
        ImmutableArray<int> lengths,
        ImmutableArray<int> lowerBounds)
    {
        long total = 1;
        foreach (var length in lengths)
        {
            total *= length;
            if (total > MaximumSequenceLength)
            {
                return FoldOutcome.Error(
                    SequenceBoundCode,
                    $"The array would hold {total.ToString(CultureInfo.InvariantCulture)} elements, beyond the "
                    + $"deterministic bound of {MaximumSequenceLength.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        var payload = element.Payload([.. Enumerable.Repeat(element.DefaultElement, (int)total)]);
        return CreateSequence(
            lengths.Length == 1 && (lowerBounds.IsDefaultOrEmpty || lowerBounds[0] == 0)
                ? payload
                : payload.WithShape(lengths, lowerBounds));
    }

    /// <summary>Converts an inferred sequence to a declared element type, when a conversion exists.</summary>
    private static FoldOutcome ConvertSequenceToElement(Operand sequence, ElementDescriptor element)
    {
        var payload = PayloadOf(sequence);
        if (payload.ElementKind == element.Kind &&
            (element.Kind is not (OperandKind.Int32 or OperandKind.Numeric) ||
                payload.ElementNumeric == element.Numeric))
        {
            return CreateSequence(element.Payload(payload.Items));
        }

        if (element.Kind is OperandKind.Int32 or OperandKind.Numeric &&
            payload.Items.All(static item => item.IsNumeric && item.Kind != OperandKind.Enum))
        {
            var converted = ImmutableArray.CreateBuilder<Operand>(payload.Items.Length);
            foreach (var item in payload.Items)
            {
                var conversion = ConvertNumericOperand(item, element.Numeric);
                if (conversion.Disposition != FoldDisposition.Folded)
                {
                    return conversion;
                }

                converted.Add(conversion.Operand);
            }

            return CreateSequence(element.Payload(converted.ToImmutable()));
        }

        return FoldOutcome.Error(
            OperandTypeCode,
            "The initializer's elements do not convert to the declared element type.");
    }

    /// <summary>
    /// Folds a collection expression: expression elements fold as values and spreads splice folded sequences,
    /// with the element domain inferred exactly as an array initializer infers it.
    /// </summary>
    private static FoldOutcome FoldCollectionExpression(
        CollectionExpressionSyntax collection,
        FoldContext context)
    {
        var items = ImmutableArray.CreateBuilder<Operand>();
        foreach (var elementSyntax in collection.Elements)
        {
            switch (elementSyntax)
            {
                case ExpressionElementSyntax expression:
                    var folded = Fold(expression.Expression, context);
                    if (folded.Disposition != FoldDisposition.Folded)
                    {
                        return folded;
                    }

                    items.Add(folded.Operand);
                    break;
                case SpreadElementSyntax spread:
                    var source = Fold(spread.Expression, context);
                    if (source.Disposition != FoldDisposition.Folded)
                    {
                        return source;
                    }

                    if (source.Operand.Kind != OperandKind.Sequence)
                    {
                        return FoldOutcome.Error(OperandTypeCode, "A spread element requires a sequence.");
                    }

                    items.AddRange(PayloadOf(source.Operand).Items);
                    break;
                default:
                    return FoldOutcome.NotArithmetic();
            }
        }

        if (items.Count == 0)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                "An empty collection expression has no element type to infer; use 'new T[0]' or Array.Empty<T>().");
        }

        return CreateInferredSequence(items.ToImmutable());
    }

    // ---- The System.Array static surface ----------------------------------------------------------------------------

    /// <summary>The largest length the runtime permits for a one-dimensional array, fixed by the CLR contract.</summary>
    private const int ArrayMaxLength = 0X7FFFFFC7;

    /// <summary>
    /// The pure functional <see cref="Array"/> statics. Everything that would mutate its argument — Sort,
    /// Reverse, Fill, Clear, Copy, Resize — is a typed stop naming the functional operator that answers the
    /// same question over immutable sequences.
    /// </summary>
    private static FoldOutcome DispatchSystemArray(
        string name,
        SeparatedSyntaxList<TypeSyntax> typeArguments,
        List<Operand> arguments,
        FoldContext context)
    {
        switch (name)
        {
            case "Sort" or "Reverse" or "Fill" or "Clear" or "Copy" or "ConstrainedCopy" or "Resize" or "SetValue":
                return FoldOutcome.Error(
                    MemberUnsupportedCode,
                    $"Array.{name} mutates its argument, and a sequence is immutable evidence; use Order(), "
                    + "OrderBy, Reverse(), Select, or slicing, which produce new sequences.");
            case "Empty":
                if (typeArguments is not [{ } emptyElement])
                {
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "Array.Empty needs a generic element type the evaluator models.");
                }

                if (!TryResolveElementType(emptyElement, context, out var descriptor, out var emptyError))
                {
                    return emptyError ?? FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "Array.Empty's element type is outside the evaluator's value domains.");
                }

                return CreateSequence(descriptor.Payload([]));
            case "IndexOf" or "LastIndexOf"
                when arguments is [{ Kind: OperandKind.Sequence } sequence, { } probe]:
                var items = PayloadOf(sequence).Items;
                var backwards = name == "LastIndexOf";
                for (var offset = 0; offset < items.Length; offset++)
                {
                    var index = backwards ? items.Length - 1 - offset : offset;
                    var equals = OperandsEqual(items[index], probe);
                    if (equals.Disposition != FoldDisposition.Folded)
                    {
                        return equals;
                    }

                    if (equals.Operand.Boolean)
                    {
                        return FoldOutcome.Folded(Operand.FromInt32(index));
                    }
                }

                return FoldOutcome.Folded(Operand.FromInt32(-1));
            case "BinarySearch" when arguments is [{ Kind: OperandKind.Sequence } sequence, { } probe]:
                return SequenceBinarySearch(PayloadOf(sequence), probe);
            case "TrueForAll" or "Exists" or "Find" or "FindLast" or "FindAll" or "ConvertAll" or "FindIndex" or
                "FindLastIndex":
                return FoldOutcome.Error(
                    MemberUnsupportedCode,
                    $"Array.{name} takes a lambda; write it with a lambda literal so it routes through the "
                    + "expression-lambda surface.");
            case "CreateInstance":
                return FoldArrayCreateInstance(arguments);
            default:
                return MemberUnsupported($"Array.{name}");
        }
    }

    /// <summary>
    /// Folds <see cref="Array.CreateInstance(Type, int[])"/> in its modeled shapes: a type plus one or more
    /// Int32 lengths, a type plus a lengths sequence, or a type plus lengths and lower bounds sequences. The
    /// result zero-fills with the element type's C# default, exactly as the runtime does; non-zero lower bounds
    /// are honored by element access and the bound-reporting members.
    /// </summary>
    private static FoldOutcome FoldArrayCreateInstance(List<Operand> arguments)
    {
        if (arguments.Count < 2 || arguments[0].Kind != OperandKind.Type)
        {
            return FoldOutcome.Error(
                MemberUnsupportedCode,
                "Array.CreateInstance takes typeof(T) and dimension lengths, with optional lower bounds.");
        }

        var type = (TypeRef)arguments[0].Box!;
        if (!TryElementDescriptorOfType(type, out var element))
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"'{type.CSharpName}' is not a modeled array element type.");
        }

        ImmutableArray<int> lengths;
        var lowerBounds = ImmutableArray<int>.Empty;
        if (arguments is [_, { Kind: OperandKind.Sequence } lengthSequence])
        {
            if (!TryReadInt32Sequence(lengthSequence, out lengths))
            {
                return FoldOutcome.Error(OperandTypeCode, "The lengths must be an Int32 sequence.");
            }
        }
        else if (arguments is
            [_, { Kind: OperandKind.Sequence } lengthsPart, { Kind: OperandKind.Sequence } boundsPart])
        {
            if (!TryReadInt32Sequence(lengthsPart, out lengths) ||
                !TryReadInt32Sequence(boundsPart, out lowerBounds))
            {
                return FoldOutcome.Error(
                    OperandTypeCode, "The lengths and lower bounds must be Int32 sequences.");
            }

            if (lengths.Length != lowerBounds.Length)
            {
                return FoldOutcome.Error(
                    "System.ArgumentException",
                    "The lengths and lower bounds must have the same rank.");
            }
        }
        else
        {
            var direct = ImmutableArray.CreateBuilder<int>(arguments.Count - 1);
            foreach (var argument in arguments.Skip(1))
            {
                if (!TryImplicitInt32(argument, out var length))
                {
                    return FoldOutcome.Error(
                        MemberUnsupportedCode,
                        "Array.CreateInstance takes typeof(T) and dimension lengths, with optional lower "
                        + "bounds.");
                }

                direct.Add(length);
            }

            lengths = direct.MoveToImmutable();
        }

        if (lengths.IsEmpty)
        {
            return FoldOutcome.Error("System.ArgumentException", "At least one dimension length is required.");
        }

        if (lengths.Length > MaximumArrayRank)
        {
            return FoldOutcome.Error(
                OperandTypeCode,
                $"An array rank beyond the deterministic bound of "
                + $"{MaximumArrayRank.ToString(CultureInfo.InvariantCulture)} is not modeled.");
        }

        foreach (var length in lengths)
        {
            if (length < 0)
            {
                return FoldOutcome.Error(
                    "System.ArgumentOutOfRangeException", "A dimension length cannot be negative.");
            }
        }

        // All-zero bounds are the ordinary shape; only genuinely offset arrays carry their bounds.
        if (lowerBounds.All(static bound => bound == 0))
        {
            lowerBounds = [];
        }

        return CreateRectangularZeroFilled(element, lengths, lowerBounds);
    }

    private static bool TryReadInt32Sequence(Operand sequence, out ImmutableArray<int> values)
    {
        var items = PayloadOf(sequence).Items;
        var read = ImmutableArray.CreateBuilder<int>(items.Length);
        foreach (var item in items)
        {
            if (!TryImplicitInt32(item, out var value))
            {
                values = default;
                return false;
            }

            read.Add(value);
        }

        values = read.MoveToImmutable();
        return true;
    }

    /// <summary>Maps a resolved <c>typeof(...)</c> reference to an array element descriptor, when modeled.</summary>
    private static bool TryElementDescriptorOfType(TypeRef type, out ElementDescriptor descriptor)
    {
        descriptor = default;
        if (type.IsArray || type.IsGenericDefinition || type.IsConstructedGeneric || type.IsInterfaceType)
        {
            return false;
        }

        if (type is { IsEnum: true, Shape: { } shape })
        {
            descriptor = new ElementDescriptor(
                OperandKind.Enum, shape.Underlying, default, default, shape, shape.ShortName);
            return true;
        }

        switch (type.Name)
        {
            case "Object":
                descriptor = new ElementDescriptor(OperandKind.Null, default, default, default, null, "Object");
                return true;
            case "String":
                descriptor = new ElementDescriptor(OperandKind.String, default, default, default, null, "String");
                return true;
            case "Char":
                descriptor = new ElementDescriptor(OperandKind.Char, default, default, default, null, "Char");
                return true;
            case "Boolean":
                descriptor = new ElementDescriptor(
                    OperandKind.Boolean, default, default, default, null, "Boolean");
                return true;
        }

        if (Enum.TryParse<NumericKind>(type.Name, out var numeric))
        {
            descriptor = new ElementDescriptor(
                numeric == NumericKind.Int32 ? OperandKind.Int32 : OperandKind.Numeric,
                numeric,
                default,
                default,
                null,
                numeric.ToString());
            return true;
        }

        if (Enum.TryParse<TemporalKind>(type.Name, out var temporal))
        {
            descriptor = new ElementDescriptor(
                OperandKind.Temporal, default, temporal, default, null, temporal.ToString());
            return true;
        }

        if (Enum.TryParse<BclValueKind>(type.Name, out var value))
        {
            descriptor = new ElementDescriptor(
                OperandKind.BclValue, default, default, value, null, value.ToString());
            return true;
        }

        return false;
    }

    /// <summary>Faithful <see cref="Array.BinarySearch(Array, object)"/> over the comparable element domains.</summary>
    private static FoldOutcome SequenceBinarySearch(SequencePayload payload, Operand probe)
    {
        if (payload.ElementKind == OperandKind.String)
        {
            return CultureSensitive(
                "BinarySearch over strings",
                "IndexOf or an ordinal transformation; default string ordering is culture-sensitive");
        }

        var low = 0;
        var high = payload.Items.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var comparison = CompareForSearch(payload.Items[middle], probe);
            if (comparison is null)
            {
                return FoldOutcome.Error(
                    OperandTypeCode,
                    "BinarySearch requires elements and the probe to share one comparable domain.");
            }

            if (comparison == 0)
            {
                return FoldOutcome.Folded(Operand.FromInt32(middle));
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return FoldOutcome.Folded(Operand.FromInt32(~low));
    }

    private static int? CompareForSearch(Operand element, Operand probe)
    {
        if (TryCompareTemporal(element, probe, out var temporal))
        {
            return temporal;
        }

        if (TryCompareBclValue(element, probe, out var value))
        {
            return value;
        }

        if (element.Kind == OperandKind.Boolean && probe.Kind == OperandKind.Boolean)
        {
            return element.Boolean.CompareTo(probe.Boolean);
        }

        if (element.IsNumeric && probe.IsNumeric)
        {
            if (!TryPromote(element, probe, out var target, out _))
            {
                return null;
            }

            return target switch
            {
                NumericKind.Single or NumericKind.Double => Comparer<double>.Default.Compare(
                    ToDoubleValue(NumericKindOf(element), BoxOf(element)),
                    ToDoubleValue(NumericKindOf(probe), BoxOf(probe))),
                NumericKind.Decimal => ToDecimalValue(element).CompareTo(ToDecimalValue(probe)),
                _ => ToBigInteger(NumericKindOf(element), BoxOf(element)).CompareTo(
                    ToBigInteger(NumericKindOf(probe), BoxOf(probe))),
            };
        }

        return null;
    }
}
