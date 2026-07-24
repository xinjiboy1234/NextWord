namespace NextWord.Domain.Enums;

/// <summary>
/// 表达效用：日常口语使用频率 × 表达不可替代性（设计方案 §3）。Low 不入库。
/// </summary>
public enum WordUtility
{
    Low = 0,
    Medium = 1,
    High = 2
}
