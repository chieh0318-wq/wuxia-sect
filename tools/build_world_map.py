"""Render a Tang-era parchment from Natural Earth 1:50m vectors and shaded relief."""
import io
import json
from pathlib import Path
from urllib.request import urlopen
from zipfile import ZipFile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import shapefile

ROOT = Path(__file__).resolve().parents[1]
W, H = 4096, 2480
data = ROOT / "Assets/StreamingAssets/Data/worldmap.json"
bounds = json.loads(data.read_text(encoding="utf-8-sig")).get("bounds", {}) if data.exists() else {}
west, east = bounds.get("lonMin", 70), bounds.get("lonMax", 136)
south, north = bounds.get("latMin", 15), bounds.get("latMax", 55)
cache = ROOT / "Library" / ".natural-earth"
cache.mkdir(parents=True, exist_ok=True)

def download(path, url):
    if not path.exists():
        with urlopen(url, timeout=90) as response:
            path.write_bytes(response.read())
    return path

def shapes(name, named=False):
    filename = "ne_50m_" + name
    path = cache / (filename + ".zip")
    download(path, f"https://naturalearth.s3.amazonaws.com/50m_physical/{filename}.zip")
    with ZipFile(path) as archive:
        dbf = io.BytesIO(archive.read(filename + ".dbf")) if named else None
        with shapefile.Reader(shp=io.BytesIO(archive.read(filename + ".shp")), dbf=dbf) as reader:
            features = ((feature.shape, str(feature.record.as_dict().get("name", "")).casefold())
                        for feature in reader.iterShapeRecords()) if named else ((shape, "") for shape in reader.iterShapes())
            for shape, label in features:
                if not hasattr(shape, "bbox") or shape.bbox[0] > east or shape.bbox[2] < west or shape.bbox[1] > north or shape.bbox[3] < south:
                    continue
                ends = list(shape.parts) + [len(shape.points)]
                for a,b in zip(ends, ends[1:]):
                    line = [((x-west)/(east-west)*W, (north-y)/(north-south)*H)
                            for x,y in shape.points[a:b]]
                    yield (line, label) if named else line

def shaded_relief():
    path = download(cache / "SR_50M.zip", "https://naturalearth.s3.amazonaws.com/50m_raster/SR_50M.zip")
    with ZipFile(path) as archive:
        raster = next(name for name in archive.namelist() if name.lower().endswith(".tif"))
        with Image.open(io.BytesIO(archive.read(raster))) as terrain:
            px, py = terrain.width / 360, terrain.height / 180
            crop = terrain.convert("L").resize(
                (W, H), Image.Resampling.BICUBIC,
                ((west+180)*px, (90-north)*py, (east+180)*px, (90-south)*py))
    soft = np.asarray(crop.filter(ImageFilter.GaussianBlur(1.2)), dtype=np.float32)
    return np.clip((248 - soft) / 125, 0, 1)

rng = np.random.default_rng(123)
def wash(width, height):
    noise = Image.fromarray(rng.integers(0, 255, (height, width), dtype=np.uint8))
    return np.asarray(noise.resize((W,H), Image.Resampling.BICUBIC), dtype=np.float32)/127 - 1

land = Image.new("L", (W,H))
pen = ImageDraw.Draw(land)
for polygon in shapes("land"):
    pen.polygon(polygon, fill=255)
for polygon in shapes("lakes"):
    pen.polygon(polygon, fill=0)
mask = np.asarray(land, dtype=np.float32) / 255
coast = mask - np.asarray(land.filter(ImageFilter.GaussianBlur(3)), dtype=np.float32)/255
x = np.linspace(west,east,W,dtype=np.float32)[None,:]
y = np.linspace(north,south,H,dtype=np.float32)[:,None]

def hill(cx, cy, sx, sy, slope=0):
    return np.exp(-((x-cx)/sx)**2-((y-cy-(x-cx)*slope)/sy)**2)

mountains = (hill(86,32,15,4.2,.08)*.65 + hill(86,36,10,1.15,-.19)*.75
             + hill(81,42,8,1.25,-.04) + hill(104,34.2,6.3,.75)*.68
             + hill(113.1,37.4,1.0,3.7,.45)*.6 + hill(91,29,10,1.3,-.15)*.72
             + hill(100,29,2,7,-1.05)*.46 + hill(91,48,9,1.7,-.45)*.58)
sand = hill(84,39,7,2.1)*.9 + hill(104,43,11,3.1)*.62
broad = wash(44,29)
rough = wash(230,150)
relief = shaded_relief()
grain = rng.normal(0, 2.1, (H,W)).astype(np.float32)
vignette = ((x-(west+east)/2)/(east-west)*2)**4 + ((y-(north+south)/2)/(north-south)*2)**4
land_value = -relief*(44 + 24*mountains) - mountains*10 + broad*9 + rough*4
sea_value = broad*8 + rough*5 + wash(14,10)*8
pixels = np.empty((H,W,3),dtype=np.uint8)
for i,(paper,water) in enumerate(zip((220,196,151),(136,154,152))):
    desert = sand * (10, -4, -19)[i]
    value = (paper+land_value+desert)*mask + (water+sea_value)*(1-mask)
    pixels[:,:,i] = np.clip(value + grain - vignette*9 - abs(coast)*47,0,255)
image = Image.fromarray(pixels)
rivers = Image.new("L", (W,H))
pen = ImageDraw.Draw(rivers)
for line, name in shapes("rivers_lake_centerlines", named=True):
    principal = name in {"yangtze", "huang", "huang he", "yellow river", "chang jiang", "jinsha"}
    pen.line(line, fill=225 if principal else 126, width=7 if principal else 3)
image.paste((71,108,109),mask=rivers.filter(ImageFilter.GaussianBlur(.6)))
output = ROOT / "Assets/Art/Map/world_map.jpg"
output.parent.mkdir(parents=True,exist_ok=True)
image.save(output,quality=91,optimize=True)
print(f"{output.name}: {output.stat().st_size:,} bytes")
