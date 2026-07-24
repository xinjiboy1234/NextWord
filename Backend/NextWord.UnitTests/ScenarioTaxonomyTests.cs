using NextWord.Domain.Scenarios;

namespace NextWord.UnitTests;

public sealed class ScenarioTaxonomyTests
{
    [Fact]
    public void Taxonomy_Has7CategoriesAnd20SubScenarios()
    {
        Assert.Equal(20, ScenarioTaxonomy.All.Count);
        Assert.Equal(7, ScenarioTaxonomy.Categories.Count);
        Assert.Equal(20, ScenarioTaxonomy.All.Select(item => item.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("daily_routine", "daily_life")]
    [InlineData("home_cooking", "daily_life")]
    [InlineData("housing_chores", "daily_life")]
    [InlineData("directions", "getting_around")]
    [InlineData("transport", "getting_around")]
    [InlineData("travel_lodging", "getting_around")]
    [InlineData("shopping", "shopping_money")]
    [InlineData("dining_out", "shopping_money")]
    [InlineData("payment_services", "shopping_money")]
    [InlineData("small_talk", "social")]
    [InlineData("making_plans", "social")]
    [InlineData("requests_gratitude", "social")]
    [InlineData("emotions", "feelings_opinions")]
    [InlineData("opinions", "feelings_opinions")]
    [InlineData("agree_disagree", "feelings_opinions")]
    [InlineData("describing", "describing_narrating")]
    [InlineData("past_experiences", "describing_narrating")]
    [InlineData("future_plans", "describing_narrating")]
    [InlineData("study_talk", "study_work")]
    [InlineData("work_smalltalk", "study_work")]
    public void SubScenario_MapsToExpectedCategory(string key, string expectedCategory)
    {
        var item = ScenarioTaxonomy.Find(key);
        Assert.NotNull(item);
        Assert.Equal(expectedCategory, item!.CategoryKey);
        Assert.False(string.IsNullOrWhiteSpace(item.ZhName));
    }

    [Theory]
    [InlineData("daily_routine", true)]
    [InlineData("medical", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSubScenarioKey_ValidatesKeys(string? key, bool expected)
    {
        Assert.Equal(expected, ScenarioTaxonomy.IsSubScenarioKey(key));
    }
}
