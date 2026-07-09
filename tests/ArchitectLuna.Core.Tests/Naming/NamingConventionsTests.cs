using ArchitectLuna.Core.Naming;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class NamingConventionsTests
{
    [Theory]
    [InlineData("CreateInvoice", "create-invoice")]
    [InlineData("GetInvoiceById", "get-invoice-by-id")]
    [InlineData("Invoices", "invoices")]
    public void ToKebabCase_SplitsPascalCaseWords(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.ToKebabCase(input));
    }

    [Theory]
    [InlineData("CreateInvoice", "create_invoice")]
    [InlineData("AmountCents", "amount_cents")]
    public void ToSnakeCase_SplitsPascalCaseWords(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("Id", "id")]
    [InlineData("CustomerId", "customerId")]
    public void ToCamelCase_LowercasesFirstLetter(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.ToCamelCase(input));
    }

    [Fact]
    public void ToPascalCase_NormalizesKebabInput()
    {
        Assert.Equal("CreateInvoice", NamingConventions.ToPascalCase("create-invoice"));
    }

    [Theory]
    [InlineData("Invoice", "Invoices")]
    [InlineData("Category", "Categories")]
    [InlineData("Box", "Boxes")]
    [InlineData("Bus", "Buses")]
    public void Pluralize_HandlesCommonEnglishSuffixes(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.Pluralize(input));
    }
}
