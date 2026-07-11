using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class DefaultValidationRulesTests
{
    [Fact]
    public void String_GetsNotEmptyByDefault()
    {
        var field = new FieldModel { Name = "Reference", Type = "string" };

        Assert.Equal(new[] { "NotEmpty()" }, DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void NullableString_GetsNoDefault()
    {
        var field = new FieldModel { Name = "Reference", Type = "string?" };

        Assert.Empty(DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void Guid_GetsNotEmptyByDefault()
    {
        var field = new FieldModel { Name = "CustomerId", Type = "Guid" };

        Assert.Equal(new[] { "NotEmpty()" }, DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void NullableGuid_GetsNoDefault()
    {
        var field = new FieldModel { Name = "CustomerId", Type = "Guid?" };

        Assert.Empty(DefaultValidationRules.DefaultsFor(field));
    }

    [Theory]
    [InlineData("pageSize")]
    [InlineData("pageNumber")]
    [InlineData("PageSize")]
    public void PagingIntFields_GetGreaterThanZero(string name)
    {
        var field = new FieldModel { Name = name, Type = "int" };

        Assert.Equal(new[] { "GreaterThan(0)" }, DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void OrdinaryIntField_GetsNoDefault()
    {
        var field = new FieldModel { Name = "RetryCount", Type = "int" };

        Assert.Empty(DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void Bool_GetsNoDefault()
    {
        var field = new FieldModel { Name = "IsActive", Type = "bool" };

        Assert.Empty(DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void EmailNamedStringField_GetsEmailAddressRule()
    {
        var field = new FieldModel { Name = "CustomerEmail", Type = "string" };

        Assert.Equal(new[] { "NotEmpty()", "EmailAddress()" }, DefaultValidationRules.DefaultsFor(field));
    }

    [Fact]
    public void CurrencyNamedStringField_GetsMaximumLengthThree()
    {
        var field = new FieldModel { Name = "Currency", Type = "string" };

        Assert.Equal(new[] { "NotEmpty()", "MaximumLength(3)" }, DefaultValidationRules.DefaultsFor(field));
    }
}
