# -*- coding: utf-8 -*-
"""一次性脚本：按待办文案里的客户/项目前缀写入分组，供桌面待办的分组功能使用。

用法：
    python GroupExistingTodos.py [输入文件] [输出文件]
    默认：输入 = %APPDATA%\\DesktopTodo\\todos.json，输出 = 输入文件本身（先备份）

注意：todos.json 里的日期是 .NET JavaScriptSerializer 的格式，必须写成
\\/Date(毫秒)\\/（斜杠前面带反斜杠转义）。少写反斜杠会让程序解析失败、整份数据读不出来，
所以下面用正则精确地把 "/Date(...)/" 重新转义，而不是简单字符串替换。
"""
import io
import json
import os
import re
import shutil
import sys
import uuid

DEFAULT = os.path.join(os.environ["APPDATA"], "DesktopTodo", "todos.json")

# 分组顺序 = 界面里的显示顺序（按当前未完成条数从多到少排）
ORDER = ["海洋", "奥星", "特缆", "特纤", "南非", "联飞", "汉川", "欧缆", "通用/跨项目"]
MULTI = "通用/跨项目"


def prefix_of(text):
    for sep in ("：", ":"):
        if sep in text:
            return text.split(sep)[0]
    return text


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    dst = sys.argv[2] if len(sys.argv) > 2 else src

    with io.open(src, encoding="utf-8") as f:
        data = json.load(f)

    groups = [{"Id": uuid.uuid4().hex, "Name": n} for n in ORDER]
    by_name = {g["Name"]: g["Id"] for g in groups}

    stats = {}
    for it in data.get("Items", []):
        text = it.get("Text", "")
        p = prefix_of(text)
        hits = [n for n in ORDER if n != MULTI and n in p]
        cross = any(k in text for k in ("每个项目", "所有项目", "各项目"))
        if len(hits) == 1 and not cross:
            gid = by_name[hits[0]]
            label = hits[0]
        elif len(hits) > 0 or cross:
            gid = by_name[MULTI]
            label = MULTI
        else:
            gid = None
            label = "未分组"
        if gid:
            it["GroupId"] = gid
        else:
            it.pop("GroupId", None)
        stats[label] = stats.get(label, 0) + 1

    # 保持原有键顺序（Items / Settings），只追加 Groups
    data["Groups"] = groups

    text = json.dumps(data, ensure_ascii=False, separators=(",", ":"))
    # 关键一步：把日期重新转义成 \/Date(ms)\/
    text = re.sub(r"(?<!\\)/Date\((-?\d+)\)/", r"\\/Date(\1)\\/", text)

    if dst == src:
        bak = src + ".bak-groups"
        if not os.path.exists(bak):
            shutil.copy2(src, bak)

    with io.open(dst, "w", encoding="utf-8", newline="") as f:
        f.write(text)

    for k, v in sorted(stats.items(), key=lambda kv: -kv[1]):
        print("%-8s %d" % (k, v))


if __name__ == "__main__":
    main()
