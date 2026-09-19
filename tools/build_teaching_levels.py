# -*- coding: utf-8 -*-
"""Build teaching stages 1-5 into 关卡表.xlsx and level_config.json."""
from __future__ import annotations

import json
import shutil
from copy import copy
from pathlib import Path

import openpyxl
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter

ROOT = Path(r"D:\UnityWorkspace\Projects\doordoor")
XLSX_PATH = ROOT / "关卡表.xlsx"
JSON_PATH = ROOT / "GooseMergeDemoProject" / "Assets" / "Resources" / "Config" / "level_config.json"
BACKUP_PATH = ROOT / "关卡表_备份_20260919.xlsx"

Y0 = 13  # first visible row, matching the existing workbook
Y_MAX = 43

LEVEL_META = {
    1: {
        "name": "第1关：鸡笼与倍增门",
        "tip": "走进鹅栏会加人。红门要先开枪打绿再走；先救人再打门，门上的数跳得更快。关底「鸡」",
    },
    2: {
        "name": "第2关：武器架",
        "tip": "栏杆上的武器要打碎才会到手。弓更快，法杖一次 4 发。只加人不够清更密的鸡。关底「你」",
    },
    3: {
        "name": "第3关：火门",
        "tip": "先打碎火门锁再走，整群变成火鹅。火克大公鸡。关底「太」",
    },
    4: {
        "name": "第4关：电门",
        "tip": "先打碎电门锁再走，整群变成电鹅。电会跳，克成群小鸡。关底「美」",
    },
    5: {
        "name": "第5关：激光与坤坤",
        "tip": "先走一种元素门，再走另一种颜色，攻击变成激光。坤坤在最后。关底「鸡你太美」",
    },
}

# (stage, t_sec, left, mid, right) — teaching-doc tokens. Empty string = 空.
# Blockers sit 1s earlier on the same lane ("门前挡").
RAW_EVENTS = [
    # ===== 1 母鸡 ← 笼 & 倍增门 =====
    (1, 0, "", "母鸡2", ""),
    (1, 2, "笼+3", "", "笼+5"),
    (1, 6, "母鸡4", "母鸡6", "母鸡4"),
    (1, 10, "笼+4", "", "倍-2"),
    (1, 12, "倍+4", "", "笼+4"),
    (1, 16, "母鸡6", "母鸡8", "母鸡6"),
    (1, 20, "", "倍-20", ""),
    (1, 19, "", "母鸡3", ""),  # 门前挡
    (1, 22, "笼+6", "", "倍-16"),
    (1, 21, "母鸡2", "", "母鸡2"),
    (1, 24, "", "倍-32", ""),
    (1, 23, "", "母鸡6", ""),
    (1, 25, "母鸡8", "母鸡12", "母鸡8"),
    # ===== 2 密鸡群 ← 弓架/杖架 =====
    (2, 0, "", "笼+4", ""),
    (2, 3, "倍-1", "", "笼+3"),
    (2, 6, "母鸡6", "母鸡10", "母鸡6"),
    (2, 10, "弓架/5", "", "倍-6"),
    (2, 13, "", "弓架/2", ""),  # 路中间保底，必须脆
    (2, 14, "", "杖架/6", ""),
    (2, 16, "母鸡8", "母鸡12", "母鸡8"),
    (2, 20, "", "倍-42", ""),
    (2, 19, "", "母鸡4", ""),
    (2, 22, "笼+8", "", "倍-36"),
    (2, 21, "母鸡3", "", "母鸡3"),
    (2, 24, "", "倍-58", ""),
    (2, 23, "", "母鸡8", ""),
    (2, 25, "母鸡10", "母鸡16", "母鸡10"),
    # ===== 3 大公鸡 ← 火门 =====
    (3, 0, "笼+3", "", "笼+5"),
    (3, 3, "", "弓架/5", ""),
    (3, 4, "", "倍-6", ""),
    (3, 6, "母鸡3", "大公鸡2", "母鸡3"),
    (3, 11, "", "火锁/3", ""),
    (3, 14, "", "大公鸡1", ""),
    (3, 16, "大公鸡1", "大公鸡2", "大公鸡1"),
    (3, 17, "母鸡2", "母鸡3", "母鸡2"),  # 缝里挤一点母鸡
    (3, 20, "", "倍-48", ""),
    (3, 19, "", "母鸡4", ""),
    (3, 22, "杖架/6", "", "笼+8"),
    (3, 24, "", "倍-72", ""),
    (3, 23, "", "大公鸡1", ""),
    (3, 25, "母鸡6+大公鸡1", "母鸡8+大公鸡2", "母鸡6+大公鸡1"),
    # ===== 4 小鸡 ← 电门 =====
    (4, 0, "笼+3", "", "笼+5"),
    (4, 3, "", "弓架/5", ""),
    (4, 4, "", "倍-6", ""),
    (4, 6, "小鸡8", "小鸡12", "小鸡8"),
    (4, 11, "", "电锁/3", ""),
    (4, 14, "小鸡6", "小鸡8", "小鸡6"),
    (4, 16, "小鸡10", "小鸡16", "小鸡10"),
    (4, 20, "", "倍-50", ""),
    (4, 19, "", "小鸡8", ""),
    (4, 22, "杖架/6", "", "笼+8"),
    (4, 24, "", "倍-75", ""),
    (4, 23, "", "小鸡14", ""),
    (4, 25, "小鸡10", "小鸡16", "小鸡10"),
    (4, 27, "", "大公鸡1", ""),
    # ===== 5 混合 + 坤坤 ← 火+电=激光 =====
    (5, 0, "笼+4", "", "笼+6"),
    (5, 3, "", "倍-4", ""),
    (5, 6, "小鸡8", "大公鸡2+母鸡4", "小鸡8"),
    (5, 10, "火锁/4", "弓架/5", "电锁/4"),
    (5, 13, "电锁/4", "", "火锁/4"),
    (5, 15, "", "杖架/6", ""),
    (5, 17, "小鸡10+母鸡4", "大公鸡2+母鸡6", "小鸡10+母鸡4"),
    (5, 20, "", "倍-12", ""),
    (5, 19, "", "母鸡6", ""),
    (5, 22, "笼+8", "", "倍-18"),
    (5, 21, "大公鸡1", "", "大公鸡1"),
    (5, 24, "火锁/2", "倍-28", "电锁/2"),
    (5, 23, "", "小鸡8+母鸡4", ""),
    (5, 25, "小鸡12+母鸡6", "大公鸡2+母鸡8", "小鸡12+母鸡6"),
    (5, 27, "", "坤坤", ""),
]


def y_of(t: float) -> int:
    return Y0 + int(round(t))


def split_tokens(cell: str) -> list[str]:
    if not cell:
        return []
    parts = []
    for chunk in cell.replace("＋", "+").split("+"):
        token = chunk.strip()
        if token:
            parts.append(token)
    # 笼+3 / 倍+8 were split by '+'. Rejoin known prefixes.
    rebuilt = []
    i = 0
    prefixes = ("笼", "倍", "门")
    while i < len(parts):
        p = parts[i]
        if p in prefixes and i + 1 < len(parts) and (parts[i + 1][:1].isdigit() or parts[i + 1][:1] in "+-"):
            rebuilt.append(p + "+" + parts[i + 1])
            i += 2
            continue
        rebuilt.append(p)
        i += 1
    return rebuilt


def parse_int_tail(text: str, default: int = 1) -> int:
    digits = ""
    sign = ""
    for ch in text:
        if ch in "+-" and not digits:
            sign = ch
        elif ch.isdigit():
            digits += ch
    if not digits:
        return default
    value = int(digits)
    return -value if sign == "-" else value


def to_engine_token(token: str) -> str | None:
    """Map teaching-doc token to 关卡表.xlsx cell text."""
    t = token.strip()
    if not t or t == "空":
        return None

    if t.startswith("笼+"):
        n = parse_int_tail(t, 1)
        return f"鹅笼/血{n}/{n}"
    if t.startswith("倍") or t.startswith("门"):
        n = parse_int_tail(t, 0)
        return f"门{n}"
    if t.startswith("弓架"):
        hp = parse_int_tail(t.split("/")[-1], 5) if "/" in t else 5
        return f"武器架/弓箭/{hp}"
    if t.startswith("杖架"):
        hp = parse_int_tail(t.split("/")[-1], 6) if "/" in t else 6
        return f"武器架/法杖/{hp}"
    if t.startswith("火锁") or t.startswith("火门"):
        hp = parse_int_tail(t.split("/")[-1], 3) if "/" in t else 3
        return f"火门/{hp}"
    if t.startswith("电锁") or t.startswith("雷门") or t.startswith("雷锁"):
        hp = parse_int_tail(t.split("/")[-1], 3) if "/" in t else 3
        return f"雷门/{hp}"
    if t.startswith("母鸡") or (t.startswith("鸡") and not t.startswith("小鸡")):
        n = parse_int_tail(t, 1)
        return "鸡" if n <= 1 else f"鸡{n}"
    if t.startswith("大公鸡"):
        n = parse_int_tail(t, 1)
        return "大公鸡" if n <= 1 else f"大公鸡{n}"
    if t.startswith("小鸡"):
        n = parse_int_tail(t, 1)
        return "小鸡" if n <= 1 else f"小鸡{n}"
    if t == "坤坤" or t == "鸡boss":
        return "坤坤"
    raise ValueError(f"unknown teaching token: {token!r}")


def token_to_events(token: str, lane: int, y: int) -> list[dict]:
    t = token.strip()
    base = {
        "time": 0.0,
        "kind": 0,
        "lane": lane,
        "value": 0,
        "health": 0,
        "element": 0,
        "weapon": 0,
        "chicken": 0,
        "count": 0,
        "y": float(y),
    }

    def evt(**kwargs):
        row = dict(base)
        row.update(kwargs)
        return row

    if t.startswith("鹅笼/"):
        # 鹅笼/血H/N
        parts = t.split("/")
        health = parse_int_tail(parts[1], 1)
        count = parse_int_tail(parts[2], 1) if len(parts) > 2 else 1
        return [evt(kind=4, health=health, count=count)]
    if t.startswith("门"):
        return [evt(kind=0, value=parse_int_tail(t, 0))]
    if t.startswith("武器架/弓箭"):
        hp = parse_int_tail(t.split("/")[-1], 5)
        return [evt(kind=2, weapon=1, health=hp)]
    if t.startswith("武器架/法杖"):
        hp = parse_int_tail(t.split("/")[-1], 6)
        return [evt(kind=2, weapon=2, health=hp)]
    if t.startswith("火门"):
        hp = parse_int_tail(t.split("/")[-1], 3)
        return [evt(kind=1, element=1, health=hp)]
    if t.startswith("雷门"):
        hp = parse_int_tail(t.split("/")[-1], 3)
        return [evt(kind=1, element=2, health=hp)]
    if t.startswith("鸡") and not t.startswith("小鸡"):
        n = parse_int_tail(t, 1)
        return [evt(kind=3, chicken=0, count=max(1, n))]
    if t.startswith("大公鸡"):
        n = parse_int_tail(t, 1)
        return [evt(kind=3, chicken=1, count=max(1, n))]
    if t.startswith("小鸡"):
        n = parse_int_tail(t, 1)
        return [evt(kind=3, chicken=2, count=max(1, n))]
    if t == "坤坤":
        return [evt(kind=3, chicken=3, count=1)]
    raise ValueError(f"unknown engine token: {token!r}")


def join_cell(tokens: list[str]) -> str:
    return "+".join(tokens)


def build_grid() -> dict[tuple[int, int], list[str]]:
    """(stage, y) -> [left, mid, right] lists of engine tokens (may be multiple)."""
    grid: dict[tuple[int, int], list[list[str]]] = {}
    for stage in range(1, 6):
        for y in range(1, Y_MAX + 1):
            grid[(stage, y)] = [[], [], []]
    for stage, t, left, mid, right in RAW_EVENTS:
        y = y_of(t)
        if not (1 <= y <= Y_MAX):
            raise ValueError(f"y {y} out of range for t={t}")
        for lane_i, cell in enumerate((left, mid, right)):
            for teaching in split_tokens(cell):
                engine = to_engine_token(teaching)
                if engine:
                    grid[(stage, y)][lane_i].append(engine)
    return grid


def build_json(grid) -> dict:
    levels = []
    for stage in range(1, 6):
        events = []
        for y in range(1, Y_MAX + 1):
            left, mid, right = grid[(stage, y)]
            for lane, tokens in ((-1, left), (0, mid), (1, right)):
                for token in tokens:
                    events.extend(token_to_events(token, lane, y))
        meta = LEVEL_META[stage]
        levels.append(
            {
                "stageId": stage,
                "name": meta["name"],
                "tip": meta["tip"],
                "events": events,
            }
        )
    return {"levels": levels}


def style_header(cell, fill):
    cell.font = Font(name="微软雅黑", bold=True, size=11, color="FFFFFF")
    cell.fill = fill
    cell.alignment = Alignment(horizontal="center", vertical="center")


def write_xlsx(grid) -> None:
    if XLSX_PATH.exists() and not BACKUP_PATH.exists():
        shutil.copy2(XLSX_PATH, BACKUP_PATH)

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "Sheet1"

    header_fill = PatternFill("solid", fgColor="305496")
    lane_fill = PatternFill("solid", fgColor="D6DCE4")
    stage_fills = {
        1: PatternFill("solid", fgColor="E2EFDA"),
        2: PatternFill("solid", fgColor="FFF2CC"),
        3: PatternFill("solid", fgColor="FCE4D6"),
        4: PatternFill("solid", fgColor="DDEBF7"),
        5: PatternFill("solid", fgColor="E4DFEC"),
    }
    thin = Border(
        left=Side(style="thin", color="B0B0B0"),
        right=Side(style="thin", color="B0B0B0"),
        top=Side(style="thin", color="B0B0B0"),
        bottom=Side(style="thin", color="B0B0B0"),
    )
    center = Alignment(horizontal="center", vertical="center", wrap_text=True)

    ws.append(["id", "y", "x1", "x2", "x3"])
    ws.append([None, None, -1, 0, 1])
    for col in range(1, 6):
        style_header(ws.cell(1, col), header_fill)
        ws.cell(2, col).fill = lane_fill
        ws.cell(2, col).alignment = center
        ws.cell(2, col).border = thin

    r = 3
    for stage in range(1, 6):
        if stage > 1:
            r += 1  # blank separator like the old workbook
        for y in range(1, Y_MAX + 1):
            left, mid, right = grid[(stage, y)]
            values = [stage, y, join_cell(left), join_cell(mid), join_cell(right)]
            for c, v in enumerate(values, 1):
                cell = ws.cell(r, c, v if v != "" else None)
                cell.alignment = center
                cell.border = thin
                cell.font = Font(name="微软雅黑", size=10)
                if any(values[2:]):
                    cell.fill = stage_fills[stage]
            r += 1

    ws.freeze_panes = "A3"
    widths = [8, 8, 28, 28, 28]
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = w
    ws.row_dimensions[1].height = 22
    ws.auto_filter.ref = "A1:E2"

    legend = wb.create_sheet("词条")
    legend.append(["教学文档词条", "关卡表词条", "JSON kind", "说明"])
    for col in range(1, 5):
        style_header(legend.cell(1, col), header_fill)
    rows = [
        ("空", "", "—", "不生成事件"),
        ("笼+N", "鹅笼/血N/N", "4 GooseCage", "走进去固定 +N；health/count 均为 N"),
        ("倍-N / 倍+N", "门-N / 门N", "0 Gate", "倍增门，每枪数字 +1。靠后的膨胀门用大负数起手，目标大约打到 +10～+30，而不是从绿门再往上打"),
        ("弓架", "武器架/弓箭/H", "2 WeaponRack weapon=1", "H 为锁血。第2关第一只 H=5≈1.5s，保底 H=2≈0.6s"),
        ("杖架", "武器架/法杖/H", "2 WeaponRack weapon=2", "H=6≈1.2s"),
        ("火锁", "火门/H", "1 ElementGate element=1", "第3关 H=3≈0.5–1s；第5关主选择 H=4，补门 H=2"),
        ("电锁", "雷门/H", "1 ElementGate element=2", "同上"),
        ("母鸡n", "鸡 / 鸡n", "3 ChickenGroup chicken=0", "普通鸡，actor 4"),
        ("大公鸡n", "大公鸡 / 大公鸡n", "3 ChickenGroup chicken=1 Fat", "厚血，actor 5 长矛鸡"),
        ("小鸡n", "小鸡 / 小鸡n", "3 ChickenGroup chicken=2 Fast", "薄血群攻，actor 6 剑盾鸡"),
        ("坤坤", "坤坤", "3 ChickenGroup chicken=3 Boss", "第5关 27s 关底，actor 6"),
        ("门前挡", "比门小 1 的 y", "同鸡群", "y 越小越先碰到。挡子弹的鸡写在门的上一行"),
    ]
    for row in rows:
        legend.append(row)
    for col, w in enumerate((18, 22, 34, 56), 1):
        legend.column_dimensions[get_column_letter(col)].width = w

    timeline = wb.create_sheet("教学时间轴")
    timeline.append(["关卡", "秒", "y", "左", "中", "右", "阶段"])
    for col in range(1, 8):
        style_header(timeline.cell(1, col), header_fill)
    phase_of = lambda t: (
        "①初始" if t < 5 else
        "②出题" if t < 10 else
        "③解题" if t < 15 else
        "④再出题" if t < 20 else
        "⑤膨胀" if t < 25 else
        "⑥割草"
    )
    for stage, t, left, mid, right in RAW_EVENTS:
        timeline.append([stage, t, y_of(t), left or "空", mid or "空", right or "空", phase_of(t)])
    for col, w in enumerate((8, 8, 8, 22, 28, 22, 12), 1):
        timeline.column_dimensions[get_column_letter(col)].width = w

    estimate = wb.create_sheet("膨胀预估")
    estimate.append(["关卡", "秒", "门", "起手", "预估人数", "武器", "发/秒/鹅", "有效射击秒", "挡子弹", "预估打高", "走进去大约", "目标区间"])
    for col in range(1, 13):
        style_header(estimate.cell(1, col), header_fill)
    # hits ≈ geese * rps * window * (1 - soak). Window after blockers ~1.6s.
    estimate_rows = [
        (1, 12, "中 倍+4 解题", 4, 7, "弹弓", 1, 1.5, "无", 11, "+15", "解题优解 15–18，保持绿门"),
        (1, 20, "中 倍-20", -20, 16, "弹弓", 1, 1.6, "母鸡3", 18, "-2～+8", "刚进膨胀，打绿再走"),
        (1, 22, "右 倍-16", -16, 18, "弹弓", 1, 1.5, "母鸡2", 20, "0～+10", "左右二选一"),
        (1, 24, "中 倍-32", -32, 24, "弹弓", 1, 1.8, "母鸡6", 28, "-8～+12", "收束到 25–40"),
        (2, 10, "右 倍-6", -6, 4, "弹弓", 1, 1.4, "无", 6, "0～+4", "弓架 vs 加人，加人侧不要暴涨"),
        (2, 20, "中 倍-42", -42, 16, "弓/杖", 2.5, 1.6, "母鸡4", 48, "0～+16", "弓2/杖3，取中间 2.5"),
        (2, 22, "右 倍-36", -36, 20, "弓/杖", 2.5, 1.5, "母鸡3", 50, "0～+20", "左右二选一"),
        (2, 24, "中 倍-58", -58, 28, "杖", 3, 1.7, "母鸡8", 71, "-10～+20", "目标 30–50，不要上百"),
        (3, 4, "中 倍-6", -6, 6, "弓", 2, 1.2, "无", 14, "+4～+12", "①发育，目标仍 4–7 走进去前打一点"),
        (3, 20, "中 倍-48", -48, 14, "弓/火", 2, 1.6, "母鸡4", 36, "-18～+4", "火不改打门速度"),
        (3, 24, "中 倍-72", -72, 24, "杖", 3, 1.8, "大公鸡1", 78, "-10～+20", "大公鸡挡得久；目标 30–50"),
        (4, 4, "中 倍-6", -6, 6, "弓", 2, 1.2, "无", 14, "+4～+12", "同第3关①"),
        (4, 20, "中 倍-50", -50, 14, "弓/电", 2, 1.6, "小鸡8", 32, "-22～+0", "小鸡挡得短，所以堆数量"),
        (4, 24, "中 倍-75", -75, 24, "杖", 3, 1.7, "小鸡14", 73, "-15～+15", "目标 30–50"),
        (5, 3, "中 倍-4", -4, 6, "弹弓", 1, 1.4, "无", 8, "+2～+8", "①发育 4–7"),
        (5, 20, "中 倍-12", -12, 16, "激光或弓杖", 0 if False else 2, 1.4, "母鸡6", 8, "-12～+10", "激光不打门：走激光≈-12；走单元素才能打高"),
        (5, 22, "右 倍-18", -18, 18, "激光或弓杖", 2, 1.4, "大公鸡1", 10, "-18～+8", "同上，浅负避免激光玩家吃大红门"),
        (5, 24, "中 倍-28", -28, 24, "激光或弓杖", 2, 1.5, "小鸡8+母鸡4", 12, "-28～+8", "已有激光走中间加人；没激光的人打一打再走"),
    ]
    for row in estimate_rows:
        estimate.append(list(row))
    for col, w in enumerate((8, 8, 18, 10, 12, 14, 12, 12, 14, 12, 14, 36), 1):
        estimate.column_dimensions[get_column_letter(col)].width = w
    estimate["A22"] = "公式：打高 ≈ 人数 × 发/秒/鹅 × 有效射击秒。有效射击秒按挡子弹后的 1.5–1.8s，不是门从远处落到脚下的全程。"
    estimate["A23"] = "运行时：弹弓 atkspd=1；弓=2；杖=3。每枪门数字 +1（Damage/10 向下取整后至少 1）。MaxGooseCount=40。"
    estimate["A24"] = "激光走 FireLaser，不打倍增门。第5关膨胀门因此只做浅负，防止走对解题的人踩到 -50。"
    estimate["A25"] = "若实机膨胀仍快：优先加门前挡的数量/厚度，再把起手再负一档（例如 -58→-80）。不要先把笼子变小。"
    estimate["A22"].alignment = Alignment(wrap_text=True)
    estimate.row_dimensions[22].height = 28
    estimate.merge_cells("A22:L22")
    estimate.merge_cells("A23:L23")
    estimate.merge_cells("A24:L24")
    estimate.merge_cells("A25:L25")

    note = wb.create_sheet("配关说明")
    note["A1"] = "教学关 1–5 配关说明"
    note["A1"].font = Font(name="微软雅黑", bold=True, size=14)
    notes = [
        "空间轴：与现有 关卡表.xlsx 相同。id=关卡，y=逻辑高度，x1/x2/x3=左/中/右。表上 y 越小越先碰到。",
        "运行时 time 全为 0：开局按 y 一次性刷完，物体往玩家落。y = 13 + 秒。",
        "不要把关卡写进 model.xlsx。model.xlsx 只提供 actor/buff/怪物数值；摆放在本表。",
        "旧 Demo 四关已备份为 关卡表_备份_20260919.xlsx。",
        "改完本表后，用同目录脚本或直接改 JSON 同步 Assets/Resources/Config/level_config.json；游戏启动读 JSON。",
        "Unity 菜单 Tools/Demo/Import Level Table 仍是旧的两路时间轴导入器，不能直接吃本表。",
        "鸡种映射受 GooseMergeDemoBootstrap.ChickenActorId 限制：母鸡=普通鸡(4)，大公鸡=长矛鸡(5/Fat)，小鸡与坤坤=剑盾鸡(6)。",
        "膨胀段倍增门改为红门大负数起手（例如 -32、-58、-75）。弹弓每枪 +1；弓 2 发/鹅/秒；杖约 3 发/鹅/秒。激光不打门。",
        "预估：门上最终约 start + 鹅数 × 射速 × 有效射击秒。有效射击秒按 ~1.4s（挡子弹的鸡会再吃掉一部分）。目标走进去时大约 +8～+30，不要上百。",
        "第 5 关凑齐激光后子弹不再打门，所以膨胀门只做浅负数（-12/-18/-28），靠鸡挡和笼子加人，避免走激光的人吃到大红门。",
        "第3关只给一扇火门，第4关只给一扇电门，第5关进关不给激光，坤坤只出现在 27s。",
    ]
    for i, text in enumerate(notes, 3):
        note[f"A{i}"] = text
        note[f"A{i}"].alignment = Alignment(wrap_text=True, vertical="center")
        note.row_dimensions[i].height = 28
    note.column_dimensions["A"].width = 110

    wb.save(XLSX_PATH)


def validate(data: dict) -> None:
    assert len(data["levels"]) == 5
    for level in data["levels"]:
        assert level["events"], level["name"]
        for e in level["events"]:
            assert e["lane"] in (-1, 0, 1)
            assert e["y"] >= 13
            if e["kind"] == 0:
                assert e["value"] != 0
            elif e["kind"] == 1:
                assert e["element"] in (1, 2) and e["health"] > 0
            elif e["kind"] == 2:
                assert e["weapon"] in (1, 2) and e["health"] > 0
            elif e["kind"] == 3:
                assert e["chicken"] in (0, 1, 2, 3) and e["count"] > 0
            elif e["kind"] == 4:
                assert e["health"] > 0 and e["count"] > 0
            else:
                raise AssertionError(e)
    # Teaching constraints
    kinds_by_stage = {}
    for level in data["levels"]:
        s = level["stageId"]
        kinds_by_stage[s] = {(e["kind"], e.get("element", 0), e.get("weapon", 0), e.get("chicken", 0)) for e in level["events"]}
    # L1: no weapon, no element, no fat/fast/boss
    for e in data["levels"][0]["events"]:
        assert e["kind"] not in (1, 2)
        assert not (e["kind"] == 3 and e["chicken"] != 0)
    # L2: no element, no roosters/chicks/boss
    for e in data["levels"][1]["events"]:
        assert e["kind"] != 1
        assert not (e["kind"] == 3 and e["chicken"] != 0)
    # L3: fire only, no lightning, no chicks/boss
    for e in data["levels"][2]["events"]:
        if e["kind"] == 1:
            assert e["element"] == 1
        if e["kind"] == 3:
            assert e["chicken"] in (0, 1)
    # L4: lightning only, no fire, no 坤坤; one rooster at the end is allowed
    for e in data["levels"][3]["events"]:
        if e["kind"] == 1:
            assert e["element"] == 2
        if e["kind"] == 3:
            assert e["chicken"] in (0, 1, 2)
            assert e["chicken"] != 3
    # L5: no ice; boss present
    bosses = [e for e in data["levels"][4]["events"] if e["kind"] == 3 and e["chicken"] == 3]
    assert len(bosses) == 1 and bosses[0]["y"] == float(y_of(27))
    for e in data["levels"][4]["events"]:
        if e["kind"] == 1:
            assert e["element"] in (1, 2)


def main() -> None:
    grid = build_grid()
    data = build_json(grid)
    validate(data)
    write_xlsx(grid)
    JSON_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")
    counts = [len(lv["events"]) for lv in data["levels"]]
    print("xlsx", XLSX_PATH, "backup", BACKUP_PATH.exists())
    print("json", JSON_PATH)
    print("events per stage", counts, "total", sum(counts))
    # Print a compact occupancy map
    for stage in range(1, 6):
        filled = sum(1 for y in range(1, Y_MAX + 1) if any(grid[(stage, y)]))
        print(f"  stage {stage}: {filled} occupied y-rows, {counts[stage-1]} events")


if __name__ == "__main__":
    main()
