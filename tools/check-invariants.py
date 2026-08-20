#!/usr/bin/env python3
"""فحص ثوابت طبقة اللغة.

يفحص ما لا يفحصه المترجم: تطابق ملفّي الترجمة، ومفاتيح لا يستعملها شيء،
وقيماً بنيوية تسلّلت إلى ملفّ الترجمة، ونصّاً ظاهراً للمستخدم مثبَّتاً في C#.
يُشغَّل من أيّ مكان:

    python3 tools/check-invariants.py

يُرجع ١ إن سقط فحصٌ مانع، و٠ إن مرّت كلّها. الملاحظات الاستشارية لا تُسقطه.
"""

import collections
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "ProjectManagmentFlow"))

SKIP_DIRS = {"bin", "obj", ".git", "node_modules", "wwwroot", "Migrations"}

# ملفّات يُنتظر فيها نصّ عربيّ مثبَّت، ولكلٍّ سببه:
ARABIC_LITERALS_ALLOWED = {
    "Data/DbInitializer.cs":                "بذور — الأسماء المعروضة تأتي من DisplayNames",
    "Services/Security/AuthService.cs":     "سجلّات تشغيل موجَّهة للمطوّر",
}

ARABIC = re.compile(r"[؀-ۿ]")


def sources(exts, skip=()):
    for base, dirs, files in os.walk(ROOT):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS and d not in skip]
        for f in files:
            if f.endswith(exts):
                p = os.path.join(base, f)
                yield os.path.relpath(p, ROOT), open(p, encoding="utf-8", errors="ignore").read()


def main():
    failures, notes = [], []

    ar = json.load(open(os.path.join(ROOT, "Resources/ar.json"), encoding="utf-8"))
    en = json.load(open(os.path.join(ROOT, "Resources/en.json"), encoding="utf-8"))
    # ملفّا الترجمة مستثنيان: لو مسحناهما لبدا كلّ مفتاح «مستعملاً» بنفسه.
    code = list(sources((".cs", ".cshtml", ".js"), skip=("Resources",)))

    # ١ — تطابق اللغتين. مفتاحٌ في واحدة دون الأخرى يظهر خامّاً للمستخدم.
    only_ar, only_en = set(ar) - set(en), set(en) - set(ar)
    if only_ar or only_en:
        failures.append(f"مفاتيح غير متطابقة — في ar فقط {sorted(only_ar)} · في en فقط {sorted(only_en)}")

    # ٢ — مفاتيح يتيمة تُغري المترجم بترجمة ما لا يُعرض.
    used = {k for k in ar for _, t in code if f'"{k}"' in t}
    prefixes = {m.group(1) for _, t in code for m in re.finditer(r'\$"([A-Za-z0-9_]+_)\{', t)}
    derived = {k for k in ar for p in prefixes if k.startswith(p)}
    orphans = sorted(set(ar) - used - derived)
    if orphans:
        failures.append(f"مفاتيح لا يستعملها شيء ({len(orphans)}): {orphans}")

    # ٣ — قيمة «عربية» بلا حرف عربيّ: هكذا تتسلّل القيم البنيوية (rtl، en-US)
    #     إلى ملفّ الترجمة فتبدو سليمة لأنّها تنقلب مع اللغة — حتى يعدّلها مترجم.
    structural = [k for k, v in ar.items() if v.strip() and not ARABIC.search(v)]
    if structural:
        failures.append(f"قيم في ar.json بلا حرف عربيّ (قيمة بنيوية تسلّلت؟): {structural}")

    # ٤ — قيم فارغة.
    blank = [k for k, v in ar.items() if not v.strip()] + [k for k, v in en.items() if not v.strip()]
    if blank:
        failures.append(f"قيم خالية: {blank}")

    # ٥ — نصّ عربيّ مثبَّت في C# خارج المواضع المأذونة.
    strlit = re.compile(r'"((?:[^"\\]|\\.)*)"')
    stray = []
    for path, text in sources((".cs",)):
        if path in ARABIC_LITERALS_ALLOWED:
            continue
        for i, line in enumerate(text.splitlines(), 1):
            if line.strip().startswith(("//", "///", "*", "/*")):
                continue
            for m in strlit.finditer(line):
                if ARABIC.search(m.group(1)):
                    stray.append(f"{path}:{i}  «{m.group(1)[:50]}»")
    if stray:
        failures.append("نصّ عربيّ مثبَّت خارج ملفّات الترجمة:\n      " + "\n      ".join(stray))

    # ٦ — استشاريّ: قيم متطابقة عربياً ومتباعدة إنجليزياً.
    same = collections.defaultdict(list)
    for k, v in ar.items():
        same[v].append(k)
    drifted = {v: ks for v, ks in same.items() if len(ks) > 1 and len({en[k] for k in ks}) > 1}
    if drifted:
        notes.append(f"مفاتيح تحمل النصّ العربيّ نفسه وتختلف إنجليزياً ({len(drifted)} مجموعة) — راجعها عند التعديل")

    for f in failures:
        print("✗ " + f)
    for n in notes:
        print("• " + n)
    if not failures:
        print(f"✓ الثوابت سليمة — {len(ar)} مفتاحاً، لا يتيم ولا نصّ مثبَّت.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
