# -*- coding: utf-8 -*-
"""T-008 词表数据质量修复：
1. 单数 example 字段统一为 examples 数组；
2. 补全 70 条空音标（IPA，人工给定）；
3. 修正 shop around / have 的场景标注。
幂等：重复运行结果不变。
"""
import io
import json
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

PATH = "Backend/NextWord.Infrastructure/Data/wordlist-scenarios.json"

PHONETICS = {
    "by now": "/baɪ naʊ/",
    "for now": "/fə naʊ/",
    "from time to time": "/frɒm taɪm tə taɪm/",
    "in a bit": "/ɪn ə bɪt/",
    "in no time": "/ɪn nəʊ taɪm/",
    "in the long run": "/ɪn ðə lɒŋ rʌn/",
    "little by little": "/ˈlɪtl baɪ ˈlɪtl/",
    "no longer": "/nəʊ ˈlɒŋɡə/",
    "once in a while": "/wʌns ɪn ə waɪl/",
    "sooner or later": "/ˈsuːnər ɔː ˈleɪtə/",
    "the other day": "/ði ˈʌðə deɪ/",
    "these days": "/ðiːz deɪz/",
    "up to now": "/ʌp tə naʊ/",
    "at once": "/ət wʌns/",
    "for ages": "/fər ˈeɪdʒɪz/",
    "all the time": "/ɔːl ðə taɪm/",
    "at the moment": "/ət ðə ˈməʊmənt/",
    "every now and then": "/ˈevri naʊ ənd ðen/",
    "for good": "/fə ɡʊd/",
    "from now on": "/frɒm naʊ ɒn/",
    "just now": "/dʒʌst naʊ/",
    "in time": "/ɪn taɪm/",
    "straight away": "/streɪt əˈweɪ/",
    "day by day": "/deɪ baɪ deɪ/",
    "at last": "/ət lɑːst/",
    "got it": "/ɡɒt ɪt/",
    "i get it": "/aɪ ɡet ɪt/",
    "that's true": "/ðæts truː/",
    "that's right": "/ðæts raɪt/",
    "that's a shame": "/ðæts ə ʃeɪm/",
    "lucky you": "/ˈlʌki juː/",
    "poor you": "/pɔː juː/",
    "well done": "/wel dʌn/",
    "nice one": "/naɪs wʌn/",
    "oh no": "/əʊ nəʊ/",
    "you're joking": "/jɔː ˈdʒəʊkɪŋ/",
    "i can't believe it": "/aɪ kɑːnt bɪˈliːv ɪt/",
    "never mind": "/ˈnevə maɪnd/",
    "forget it": "/fəˈɡet ɪt/",
    "it doesn't matter": "/ɪt ˈdʌznt ˈmætə/",
    "you're welcome": "/jɔː ˈwelkəm/",
    "not at all": "/nɒt ət ɔːl/",
    "thanks a lot": "/θæŋks ə lɒt/",
    "thanks anyway": "/θæŋks ˈeniweɪ/",
    "i hope so": "/aɪ həʊp səʊ/",
    "i think so": "/aɪ θɪŋk səʊ/",
    "i don't think so": "/aɪ dəʊnt θɪŋk səʊ/",
    "i doubt it": "/aɪ daʊt ɪt/",
    "probably not": "/ˈprɒbəbli nɒt/",
    "why not": "/waɪ nɒt/",
    "me too": "/miː tuː/",
    "me neither": "/miː ˈnaɪðə/",
    "tell me about it": "/tel miː əˈbaʊt ɪt/",
    "i know what you mean": "/aɪ nəʊ wɒt juː miːn/",
    "how so": "/haʊ səʊ/",
    "bless you": "/bles juː/",
    "you too": "/juː tuː/",
    "help yourself": "/help jɔːˈself/",
    "and so on": "/ənd səʊ ɒn/",
    "or so": "/ɔː səʊ/",
    "or whatever": "/ɔː wɒtˈevə/",
    "something like that": "/ˈsʌmθɪŋ laɪk ðæt/",
    "plenty of": "/ˈplenti əv/",
    "a couple of": "/ə ˈkʌpl əv/",
    "loads of": "/ləʊdz əv/",
    "a bunch of": "/ə bʌntʃ əv/",
    "most of": "/məʊst əv/",
    "the rest of": "/ðə rest əv/",
    "none of": "/nʌn əv/",
    "that kind of thing": "/ðæt kaɪnd əv θɪŋ/",
}

with open(PATH, encoding="utf-8") as f:
    doc = json.load(f)
words = doc["words"]

fixed_example = 0
fixed_phonetic = 0
fixed_scenarios = 0

for w in words:
    # 1. example -> examples
    if "example" in w:
        single = w.pop("example")
        if single:
            existing = w.get("examples") or []
            if single not in existing:
                existing = existing + [single]
            w["examples"] = existing
        fixed_example += 1

    # 2. empty phonetics
    if not (w.get("phonetics") or "").strip():
        ipa = PHONETICS.get(w["lemma"])
        if ipa is None:
            print("MISSING IPA for:", w["lemma"])
            sys.exit(1)
        w["phonetics"] = ipa
        fixed_phonetic += 1

# 3. scenario annotation fixes
for w in words:
    if w["lemma"] == "shop around" and w.get("scenarios") != ["shopping"]:
        w["scenarios"] = ["shopping"]
        fixed_scenarios += 1
    if w["lemma"] == "have" and w.get("scenarios") != []:
        w["scenarios"] = []
        fixed_scenarios += 1

with open(PATH, "w", encoding="utf-8") as f:
    json.dump(doc, f, ensure_ascii=False, indent=1)
    f.write("\n")

print(f"example->examples: {fixed_example}")
print(f"phonetics filled: {fixed_phonetic}")
print(f"scenario fixes: {fixed_scenarios}")
print(f"total words: {len(words)}")

# sanity checks
assert all("example" not in w for w in words), "still has example key"
assert all((w.get("phonetics") or "").strip() for w in words), "still empty phonetics"
print("sanity OK")
