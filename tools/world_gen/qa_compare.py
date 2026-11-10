"""Compare two geometry reports (qa_geometry.py): problem counts per check, before -> after, and which runs are new.

Run:  python3 qa_compare.py before.json after.json
"""
import json
import sys

a, b = (json.load(open(p)) for p in sys.argv[1:3])
order = ["hole", "intrusion", "covered", "overhead", "chase_cam", "coplanar", "height", "edge_drop", "edge_rise", "tight"]


def points(rep, check):
    return sum(r["count"] for r in rep["issues"] if r["check"] == check)


print(f"{'check':<11}{'runs before':>12}{'runs after':>11}{'points before':>15}{'points after':>13}")
for c in order:
    print(f"{c:<11}{a['summary_runs'].get(c, 0):>12}{b['summary_runs'].get(c, 0):>11}{points(a, c):>15}{points(b, c):>13}")


def key(r):
    return (r["check"], r["object"], r["side"], round(r["s_from"] / 10))


old = {key(r) for r in a["issues"]}
new = [r for r in b["issues"] if key(r) not in old and r["check"] not in ("tight", "coplanar")]
print(f"\nnew problem runs after the change (excluding tight/coplanar): {len(new)}")
for r in new[:40]:
    print(f"  {r['check']:<10} s={r['s_from']:>6}..{r['s_to']:<6} {r['side']:<5} {r['object'][:50]:<50} worst={r['worst']}")
