"""Build the six Wuxia font roles from the upstream SIL OFL TrueType sources."""
import argparse
import pathlib
import re
import string

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = pathlib.Path(__file__).resolve().parents[1]
ROLES = (
    ("wenkai", None, "Dialogue"),
    ("serif", 400, "Narration"),
    ("serif", 700, "Names"),
    ("serif", 900, "Titles"),
    ("sans", 500, "Interface"),
    ("sans", 700, "Numbers"),
)


def character_set():
    chars = set(string.printable + "，。！？；：、（）《》「」『』〈〉【】…—−＋／◆")
    for lead in range(0xA4, 0xC7):
        for trail in (*range(0x40, 0x7F), *range(0xA1, 0xFF)):
            code = lead * 256 + trail
            if code <= 0xC67E:
                chars.update(bytes((lead, trail)).decode("big5", errors="ignore"))
    data = ROOT / "Assets/StreamingAssets/Data"
    for path in data.glob("*.json"):
        chars.update(path.read_text(encoding="utf-8-sig"))
    for path in (ROOT / "Assets/Scripts").rglob("*.cs"):
        chars.update("".join(re.findall(r'"(?:\\.|[^"\\])*"', path.read_text(encoding="utf-8-sig"))))
    return "".join(sorted(chars))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", type=pathlib.Path, help="Folder containing serif.ttf, sans.ttf, and wenkai.ttf")
    args = parser.parse_args()
    target = ROOT / "Assets/Art/Fonts/Resources/Fonts"
    target.mkdir(parents=True, exist_ok=True)
    chars = character_set()
    for source, weight, role in ROLES:
        with TTFont(args.sources / f"{source}.ttf") as font:
            options = subset.Options()
            options.name_IDs = ["*"]
            options.layout_features = ["*"]
            sub = subset.Subsetter(options=options)
            sub.populate(text=chars)
            sub.subset(font)
            if weight is not None:
                instantiateVariableFont(font, {"wght": weight}, inplace=True)
            output = target / f"{role}.ttf"
            font.save(output)
            size = output.stat().st_size
            if size >= 6_000_000:
                raise RuntimeError(f"{output}: {size} bytes exceeds the font budget")
            print(f"{role}: {size:,} bytes")


if __name__ == "__main__":
    main()
