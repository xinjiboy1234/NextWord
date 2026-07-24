namespace NextWord.Domain.Scenarios;

/// <summary>
/// 生活表达场景 taxonomy（docs/DESIGN-scenario-taxonomy.md §2）：两层，7 大类 × 20 子场景。
/// 子场景 key 是稳定标识，落库于 WordScenario.ScenarioKey；词标 0 个子场景时进 core 通用桶。
/// </summary>
public static class ScenarioTaxonomy
{
    public sealed record SubScenario(string Key, string ZhName, string CategoryKey, string CategoryZhName);

    private static readonly SubScenario[] AllSubScenarios =
    [
        // 居家生活 daily_life
        new("daily_routine", "日常起居", "daily_life", "居家生活"),
        new("home_cooking", "下厨饮食", "daily_life", "居家生活"),
        new("housing_chores", "居住与家务", "daily_life", "居家生活"),
        // 出门在外 getting_around
        new("directions", "问路导航", "getting_around", "出门在外"),
        new("transport", "交通出行", "getting_around", "出门在外"),
        new("travel_lodging", "旅行住宿", "getting_around", "出门在外"),
        // 消费交易 shopping_money
        new("shopping", "购物", "shopping_money", "消费交易"),
        new("dining_out", "点餐就餐", "shopping_money", "消费交易"),
        new("payment_services", "付款与办事", "shopping_money", "消费交易"),
        // 社交表达 social
        new("small_talk", "寒暄闲聊", "social", "社交表达"),
        new("making_plans", "邀约安排", "social", "社交表达"),
        new("requests_gratitude", "求助与致谢", "social", "社交表达"),
        // 情感观点 feelings_opinions
        new("emotions", "表达情绪", "feelings_opinions", "情感观点"),
        new("opinions", "表达观点", "feelings_opinions", "情感观点"),
        new("agree_disagree", "同意与反对", "feelings_opinions", "情感观点"),
        // 描述叙述 describing_narrating
        new("describing", "描述人事物", "describing_narrating", "描述叙述"),
        new("past_experiences", "讲述经历", "describing_narrating", "描述叙述"),
        new("future_plans", "计划打算", "describing_narrating", "描述叙述"),
        // 学习与工作（生活化） study_work
        new("study_talk", "谈论学习", "study_work", "学习与工作（生活化）"),
        new("work_smalltalk", "日常工作沟通", "study_work", "学习与工作（生活化）"),
    ];

    private static readonly Dictionary<string, SubScenario> ByKey =
        AllSubScenarios.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SubScenario> All => AllSubScenarios;

    public static IReadOnlyList<(string Key, string ZhName)> Categories => AllSubScenarios
        .Select(item => (item.CategoryKey, item.CategoryZhName))
        .Distinct()
        .ToList();

    public static bool IsSubScenarioKey(string? key) => key is not null && ByKey.ContainsKey(key);

    public static SubScenario? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var item) ? item : null;
}
