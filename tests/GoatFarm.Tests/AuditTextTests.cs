using GoatFarm.Application.Common;

namespace GoatFarm.Tests;

public class AuditTextTests
{
    [Theory]
    [InlineData("Finance.AddExpense", "Expense added")]
    [InlineData("Goat.Create", "Goat added")]
  [InlineData("Milk.AddProduction", "Milking recorded")]
    public void Maps_known_controller_actions(string code, string expected) =>
        Assert.Equal(expected, AuditText.ToText(code));

    [Fact]
    public void Unknown_dotted_action_uses_method_name_only() =>
        Assert.Equal("SaveFoo", AuditText.ToText("SomeModule.SaveFoo"));
}