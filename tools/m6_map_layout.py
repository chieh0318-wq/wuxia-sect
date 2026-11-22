"""M6: spread world-map nodes over the whole San10 painting and give every node a controlling faction.

Writes normalized x/y (0..1, y from the top) into worldmap.json and territories / outlaw factions into
factions.json, then verifies: every node pair >= MIN_DIST apart, every node controlled.
Usage: python -I tools/m6_map_layout.py  (run from the repo root)
"""
import json, math, sys
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "Assets" / "StreamingAssets" / "Data"
MIN_DIST, TARGET = 0.075, 0.088
X0, X1, Y0, Y1 = 0.06, 0.94, 0.10, 0.90


def load(name):
    return json.loads((DATA / name).read_text(encoding="utf-8"))


def save(name, value):
    (DATA / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


world, factions = load("worldmap.json"), load("factions.json")
nodes = world["nodes"]
if not any(n["id"] == "ruzhou" for n in nodes):
    nodes.append({"id": "ruzhou", "name": "汝州", "lat": 34.17, "lon": 112.84, "type": "town", "region": "河南道",
                  "desc": "嵩山南麓的州城，少林寺的田莊與香客多半在此落腳，城裡賣素齋的鋪子比酒肆還多。"})
    world["edges"] += [{"a": "songshan", "b": "ruzhou", "days": 1, "ap": 1},
                       {"a": "ruzhou", "b": "funiu", "days": 2, "ap": 1}]

# 1. Geographic seed, stretched to the full painting.
b = world["bounds"]
pts = {n["id"]: [(n["lon"] - b["lonMin"]) / (b["lonMax"] - b["lonMin"]),
                 1 - (n["lat"] - b["latMin"]) / (b["latMax"] - b["latMin"])] for n in nodes}
xs, ys = [p[0] for p in pts.values()], [p[1] for p in pts.values()]
# Blend a linear stretch with a rank (quantile) stretch: dense central China opens up while east stays east.
x_rank = {k: r / (len(pts) - 1) for r, k in enumerate(sorted(pts, key=lambda k: pts[k][0]))}
y_rank = {k: r / (len(pts) - 1) for r, k in enumerate(sorted(pts, key=lambda k: pts[k][1]))}
for k, p in pts.items():
    lx = (p[0] - min(xs)) / (max(xs) - min(xs))
    ly = (p[1] - min(ys)) / (max(ys) - min(ys))
    p[0] = X0 + (0.45 * lx + 0.55 * x_rank[k]) * (X1 - X0)
    p[1] = Y0 + (0.45 * ly + 0.55 * y_rank[k]) * (Y1 - Y0)
home = {k: list(v) for k, v in pts.items()}

# 2. Relaxation: push apart pairs closer than TARGET, with a weak pull back to the geographic seed.
ids = list(pts)
for step in range(4000):
    moved = 0.0
    for i, a in enumerate(ids):
        for c in ids[i + 1:]:
            pa, pc = pts[a], pts[c]
            dx, dy = pc[0] - pa[0], pc[1] - pa[1]
            d = math.hypot(dx, dy)
            if d >= TARGET:
                continue
            if d < 1e-6:
                dx, dy, d = 1e-3, 0.0, 1e-3
            push = (TARGET - d) / 2
            ux, uy = dx / d, dy / d
            pa[0] -= ux * push; pa[1] -= uy * push
            pc[0] += ux * push; pc[1] += uy * push
            moved += push
    pull = 0.004 if step < 3000 else 0.0
    for k, p in pts.items():
        p[0] += (home[k][0] - p[0]) * pull
        p[1] += (home[k][1] - p[1]) * pull
        p[0] = min(X1, max(X0, p[0]))
        p[1] = min(Y1, max(Y0, p[1]))
    if moved < 1e-7 and step > 3000:
        break

for n in nodes:
    n["x"], n["y"] = round(pts[n["id"]][0], 4), round(pts[n["id"]][1], 4)

# 3. Territories: majors take several cities, outlaws take everything else.
OUTLAWS = [
    {"id": "longyou_mazei", "name": "隴右馬賊", "color": "#7A5A3A", "seat": "qinzhou", "leader": "馬賊頭領　禿髮狼",
     "leaderId": "leader_taihang", "martialArts": ["馬上彎刀", "套索"], "disciples": 140, "reputation": 8, "money": 500,
     "desc": "隴右草原上來去如風的馬賊，劫商隊、搶馬場，官軍一到便散入山谷。人多勢雜，卻少有真正的高手。"},
    {"id": "qinling_shanfei", "name": "秦嶺山匪", "color": "#5B6B3F", "seat": "hanzhong", "leader": "山大王　黑鷂子",
     "leaderId": "leader_heisong", "martialArts": ["開山斧", "飛索"], "disciples": 110, "reputation": 6, "money": 400,
     "desc": "棧道兩側的山寨，專收過路錢。寨牆是木柵，守寨的多是逃丁與獵戶，一衝便散。"},
    {"id": "lingnan_dongzhai", "name": "嶺南峒寨", "color": "#3F6B5B", "seat": "guizhou", "leader": "峒主　儂阿貴",
     "leaderId": "leader_taihang", "martialArts": ["吹箭", "蠻刀"], "disciples": 120, "reputation": 10, "money": 450,
     "desc": "嶺南瘴癘之地的峒寨聯盟，不服王化，各自為政。地勢險而人心散，正是立威的好去處。"},
    {"id": "huainan_shuifei", "name": "淮南水匪", "color": "#3A5A7A", "seat": "jingzhou", "leader": "水寨頭目　浪裡蛟",
     "leaderId": "leader_heisong", "martialArts": ["分水刺", "鐵篙"], "disciples": 130, "reputation": 7, "money": 600,
     "desc": "江淮水道上的水寨，劫漕船、收私鹽。上了岸便不成氣候，一場硬仗就能打散。"},
    {"id": "taishan_xiangma", "name": "泰山響馬", "color": "#6B4A3A", "seat": "taishan", "leader": "響馬頭子　鐵臂蒼",
     "leaderId": "leader_taihang", "martialArts": ["響箭", "樸刀"], "disciples": 100, "reputation": 6, "money": 350,
     "desc": "泰山腳下的響馬，劫的是往登州出海的商貨。人不多，膽子倒大。"},
]
for o in OUTLAWS:
    if not any(f["id"] == o["id"] for f in factions["factions"]):
        factions["factions"].append({"id": o["id"], "name": o["name"], "type": "bandit", "special": False,
                                     "color": o["color"], "seat": o["seat"], "territory": [o["seat"]],
                                     "reputation": o["reputation"], "disciples": o["disciples"],
                                     "martialArts": o["martialArts"], "money": o["money"], "stance": "hostile",
                                     "known": True, "leader": o["leader"], "leaderId": o["leaderId"],
                                     "desc": o["desc"], "leaderRank": "C", "leaderCap": "B"})

TERRITORY = {
    "jinwuwei": ["changan", "tongguan"],
    "neishisheng": ["changan", "luoyang"],
    "yushitai": ["changan", "luoyang"],
    "henan_fu": ["luoyang", "bianzhou", "shanzhou"],
    "hexi_jds": ["liangzhou", "ganzhou", "jiuquan", "shazhou", "yumen"],
    "anxi_dhf": ["qiuci", "shule", "yutian", "gaochang"],
    "fanyang_jds": ["youzhou", "dingzhou", "yingzhou", "linyu"],
    "shaolin": ["songshan", "ruzhou"],
    "wulong": ["wudang", "xiangzhou", "longzhong"],
    "louguan": ["zhongnan", "taibai"],
    "emei": ["emei", "chengdu"],
    "chicheng": ["tiantai", "yuezhou"],
    "tubo": ["luoxie", "shibao"],
    "huihe": ["huihe_ya"],
    "tuqishi": ["suiye", "beiting"],
    "nanzhao": ["taihe"],
    "heisong": ["hangu", "xiaoshan"],
    "taihang": ["taihang", "weizhou", "cishan"],
    "yanbang": ["yangzhou", "fuyi"],
    "nongchao": ["hangzhou", "suzhou", "mogan"],
    "longyou_mazei": ["qinzhou", "lintao", "xiping", "lingzhou", "shouxiang", "yizhou"],
    "qinling_shanfei": ["hanzhong", "jianmen", "songzhou"],
    "lingnan_dongzhai": ["guizhou", "guangzhou", "jiaozhou"],
    "huainan_shuifei": ["jingzhou", "hongzhou", "yishan"],
    "taishan_xiangma": ["taishan", "dengzhou"],
}
by_id = {f["id"]: f for f in factions["factions"]}
for fid, nodes_of in TERRITORY.items():
    by_id[fid]["territory"] = nodes_of
    if by_id[fid].get("seat") not in nodes_of:
        by_id[fid]["seat"] = nodes_of[0]

# 4. Verify.
node_ids = {n["id"] for n in nodes}
controlled = {n for f in factions["factions"] for n in [f.get("seat")] + (f.get("territory") or [])}
empty = sorted(node_ids - controlled)
worst = min((math.dist(pts[a], pts[c]), a, c) for i, a in enumerate(ids) for c in ids[i + 1:])
bad = [t for t in controlled if t and t not in node_ids]
print(f"nodes={len(nodes)} empty={empty} unknown={bad} min_pair={worst[0]:.4f} ({worst[1]},{worst[2]})")
print(f"x range {min(p[0] for p in pts.values()):.3f}-{max(p[0] for p in pts.values()):.3f} "
      f"y range {min(p[1] for p in pts.values()):.3f}-{max(p[1] for p in pts.values()):.3f}")
if empty or bad or worst[0] < MIN_DIST:
    sys.exit("verification failed")
save("worldmap.json", world)
save("factions.json", factions)
print("written")
