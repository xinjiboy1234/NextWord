# #11 阅读查词

- 时间：2026-07-29T00:09:19　耗时：5144ms　模型：qwen-plus　HTTP：200
- usage：{"total_tokens": 678, "completion_tokens": 298, "prompt_tokens": 380, "prompt_tokens_details": {"cached_tokens": 0}}

## [system]

```
You return compact, valid JSON for contextual word definitions.
```

## [user]

```
You are a vocabulary assistant for English learners. Explain a word in reading context.

Word: digital
Context Sentence: Digital privacy has become an important concern for students and professionals who use online services daily.
Feedback Language: zh-CN (Chinese (Simplified))

Return only JSON:
{
  "phonetics": "string",
  "meanings": [
    {
      "definition": "string",
      "is_contextual": true
    }
  ],
  "collocations": ["string"],
  "examples": [
    {
      "kind": "contextual",
      "sentence": "English sentence tied to the context",
      "explanation": "essence note in Chinese (Simplified)"
    },
    {
      "kind": "general",
      "sentence": "English sentence from another scenario",
      "explanation": "essence note in Chinese (Simplified)"
    }
  ],
  "special_usage": "string",
  "difficulty_level": "basic|intermediate|advanced",
  "cefr_level": "A1|A2|B1|B2|C1|C2"
}

Rules:
- meanings[0] must explain how the word is used in the given context sentence.
- Write definition, special_usage, collocation glosses, and example explanations in Chinese (Simplified).
- Keep example sentences in natural English.
- examples[0] (contextual) must reflect usage in the given context sentence.
- examples[1] (general) should come from a different everyday scenario; omit if the word is too rare, too specialized, or not worth illustrating at this level.
- Return 0-2 examples. Be concise: one primary contextual meaning, up to 2 collocations.
```

## [assistant 响应]

```
{
  "phonetics": "/ˈdɪdʒ.ɪ.təl/",
  "meanings": [
    {
      "definition": "与数字技术相关的，尤其是指通过计算机、互联网或其他电子设备创建、存储或传输的信息或服务",
      "is_contextual": true
    }
  ],
  "collocations": ["数字隐私", "数字时代"],
  "examples": [
    {
      "kind": "contextual",
      "sentence": "Digital privacy has become an important concern for students and professionals who use online services daily.",
      "explanation": "此处“digital”强调与互联网和电子设备相关的隐私问题，即个人信息在数字环境（如社交媒体、云存储）中被收集、使用或泄露的风险。"
    },
    {
      "kind": "general",
      "sentence": "Many museums now offer digital tours that let visitors explore exhibits online.",
      "explanation": "指利用数字技术（如高清图像、3D建模、网络平台）提供的虚拟参观服务，区别于实地参观。"
    }
  ],
  "special_usage": "常作定语修饰名词（如digital footprint, digital literacy），不单独作表语或谓语；在中文里多译为“数字的”，但实际涵盖电子化、信息化、网络化等综合含义。",
  "difficulty_level": "intermediate",
  "cefr_level": "B2"
}
```
