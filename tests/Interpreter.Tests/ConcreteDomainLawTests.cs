using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Verifies the algebraic laws and evidence-separation rules required of the concrete validation domain.
/// </summary>
public sealed class ConcreteDomainLawTests
{
    private readonly ConcreteDomain domain = new();

    /// <summary>Checks reflexivity, antisymmetry, and transitivity over representative lifted-flat values.</summary>
    [Fact]
    public void LiftedFlatInt32DomainSatisfiesPartialOrderLaws()
    {
        var values = Values();

        foreach (var value in values)
        {
            Assert.True(domain.IsLessThanOrEqual(value, value));
        }

        foreach (var left in values)
        {
            foreach (var right in values)
            {
                if (domain.IsLessThanOrEqual(left, right) && domain.IsLessThanOrEqual(right, left))
                {
                    Assert.Equal(left, right);
                }

                foreach (var third in values)
                {
                    if (domain.IsLessThanOrEqual(left, right) && domain.IsLessThanOrEqual(right, third))
                    {
                        Assert.True(domain.IsLessThanOrEqual(left, third));
                    }
                }
            }
        }
    }

    /// <summary>Checks that join is a lawful least upper bound independent of operand order and grouping.</summary>
    [Fact]
    public void JoinIsACommutativeAssociativeIdempotentLeastUpperBound()
    {
        var values = Values();
        foreach (var left in values)
        {
            Assert.Equal(left, domain.Join(left, left));
            foreach (var right in values)
            {
                var join = domain.Join(left, right);
                Assert.Equal(join, domain.Join(right, left));
                Assert.True(domain.IsLessThanOrEqual(left, join));
                Assert.True(domain.IsLessThanOrEqual(right, join));

                foreach (var third in values)
                {
                    Assert.Equal(
                        domain.Join(domain.Join(left, right), third),
                        domain.Join(left, domain.Join(right, third)));
                    if (domain.IsLessThanOrEqual(left, third) && domain.IsLessThanOrEqual(right, third))
                    {
                        Assert.True(domain.IsLessThanOrEqual(join, third));
                    }
                }
            }
        }
    }

    /// <summary>Checks that meet is a lawful greatest lower bound independent of operand order and grouping.</summary>
    [Fact]
    public void MeetIsACommutativeAssociativeIdempotentGreatestLowerBound()
    {
        var values = Values();
        foreach (var left in values)
        {
            Assert.Equal(left, domain.Meet(left, left));
            foreach (var right in values)
            {
                var meet = domain.Meet(left, right);
                Assert.Equal(meet, domain.Meet(right, left));
                Assert.True(domain.IsLessThanOrEqual(meet, left));
                Assert.True(domain.IsLessThanOrEqual(meet, right));

                foreach (var third in values)
                {
                    Assert.Equal(
                        domain.Meet(domain.Meet(left, right), third),
                        domain.Meet(left, domain.Meet(right, third)));
                    if (domain.IsLessThanOrEqual(third, left) && domain.IsLessThanOrEqual(third, right))
                    {
                        Assert.True(domain.IsLessThanOrEqual(third, meet));
                    }
                }
            }
        }
    }

    /// <summary>Checks widening coverage and confirms repeated top construction is one semantic element.</summary>
    [Fact]
    public void WidenCoversBothInputsAndTopIsCanonical()
    {
        var firstTop = domain.Top(ConcreteDomain.Int32Type);
        var secondTop = domain.Top(ConcreteDomain.Int32Type);

        Assert.Equal(firstTop, secondTop);

        foreach (var left in Values())
        {
            foreach (var right in Values())
            {
                var widened = domain.Widen(left, right);
                Assert.True(domain.IsLessThanOrEqual(left, widened));
                Assert.True(domain.IsLessThanOrEqual(right, widened));
            }
        }
    }

    /// <summary>Checks that diagnostic formatting cannot disclose raw string or numeric target payloads.</summary>
    [Fact]
    public void DiagnosticFormattingDoesNotDisclosePayloads()
    {
        var value = domain.ConstString("credential-value");

        Assert.DoesNotContain("credential-value", value.ToString(), StringComparison.Ordinal);
        Assert.Contains("length=16", value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", domain.ConstInt32(123456789).ToString(), StringComparison.Ordinal);
    }

    private ConcreteValue[] Values() =>
    [
        domain.Bottom(ConcreteDomain.Int32Type),
        domain.ConstInt32(-1),
        domain.ConstInt32(0),
        domain.ConstInt32(1),
        domain.ConstInt32(2),
        domain.Top(ConcreteDomain.Int32Type),
    ];
}
