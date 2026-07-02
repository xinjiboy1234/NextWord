export interface CefrMeta {
  label: string
  description: string
}

export const CEFR_META: Record<string, CefrMeta> = {
  A1: { label: '入门', description: '能理解并使用日常用语和简单短语。' },
  A2: { label: '基础', description: '能应对简单日常交流，描述背景与需求。' },
  B1: { label: '中级', description: '能应对多数日常场景，表达熟悉话题的观点。' },
  B2: { label: '中高级', description: '能理解复杂文本，与母语者流利交流。' },
  C1: { label: '高级', description: '能灵活运用语言于学术与职场场景。' },
  C2: { label: '精通', description: '接近母语水平，能理解几乎所有内容。' },
}

export const DIMENSION_HINTS: Record<string, string> = {
  词汇: '核心词族与高频搭配',
  拼写: '常见拼写规律',
  造句: '连贯表达与语法',
  阅读: '篇章理解与推断',
}

export function getCefrMeta(level: string): CefrMeta {
  return CEFR_META[level] ?? { label: '待测评', description: '完成水平测评后将显示等级说明。' }
}
