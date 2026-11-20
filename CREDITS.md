# Asset credits

Everything shipped with the game is listed here by category with its licence. Content written for this project (story, data, code, UI kit) is original work.

## Fonts (SIL Open Font License 1.1)

| Role | Font | Source |
| --- | --- | --- |
| Narration / serif body | Noto Serif TC Regular | [Google Fonts](https://github.com/google/fonts/tree/main/ofl) |
| Speaker names | Noto Serif TC Bold | [Google Fonts](https://github.com/google/fonts/tree/main/ofl) |
| Titles | Noto Serif TC Black | [Google Fonts](https://github.com/google/fonts/tree/main/ofl) |
| Interface / buttons | Noto Sans TC Medium | [Google Fonts](https://github.com/google/fonts/tree/main/ofl) |
| Numbers | Noto Sans TC Bold | [Google Fonts](https://github.com/google/fonts/tree/main/ofl) |
| Dialogue | LXGW WenKai TC Regular | [LXGW WenKai TC](https://github.com/lxgw/LxgwWenkaiTC) |

The six files under `Assets/Art/Fonts/Resources/Fonts/` are subsets built by `tools/build_fonts.py`. The full OFL 1.1 notices are in `Assets/Art/Fonts/` (`NotoSerifTC-OFL.txt`, `NotoSansTC-OFL.txt`, `LXGWWenKaiTC-OFL.txt`). Characters missing from a subset fall back to the system font at runtime.

## Audio

The WAV files below are original synthesized assets by Codex B. `tools/build_audio.py` generates them, and they are dedicated to the public domain under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).

| File | Title |
| --- | --- |
| `Assets/Audio/Music/courtyard.wav` | Courtyard |
| `Assets/Audio/Music/map.wav` | Across the Post Roads |
| `Assets/Audio/Music/prologue.wav` | Parting at Dusk |
| `Assets/Audio/Sfx/arrival.wav` | Arrival |
| `Assets/Audio/Sfx/complete.wav` | Construction Complete |
| `Assets/Audio/Sfx/departure.wav` | Departure |
| `Assets/Audio/Sfx/month.wav` | Month Bell |
| `Assets/Audio/Sfx/place.wav` | Wood Placement |
| `Assets/Audio/Sfx/recruit.wav` | Disciple Accepted |

Kenney published the OGG files below under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).

| File | Original clip | Pack |
| --- | --- | --- |
| `Assets/Audio/Sfx/click.ogg` | click_002 | [Interface Sounds](https://opengameart.org/content/interface-sounds) |
| `Assets/Audio/Sfx/hover.ogg` | tick_002 | [Interface Sounds](https://opengameart.org/content/interface-sounds) |
| `Assets/Audio/Sfx/page.ogg` | bookFlip2 | [50 RPG sound effects](https://opengameart.org/content/50-rpg-sound-effects) |
| `Assets/Audio/Sfx/coin.ogg` | handleCoins2 | [50 RPG sound effects](https://opengameart.org/content/50-rpg-sound-effects) |

## Map data (public domain)

`Assets/Art/Map/world_map.jpg` is an original rendering by Codex B with terrain and parchment refinements by Codex D. The `tools/build_world_map.py` source uses public-domain Natural Earth 1:50m Land, Lakes, Rivers + Lake Centerlines, and Shaded Relief. Node positions and roads in `worldmap.json` were authored for this project. [Physical vectors](https://www.naturalearthdata.com/downloads/50m-physical-vectors/) · [Shaded relief](https://www.naturalearthdata.com/downloads/50m-raster-data/50m-shaded-relief/) · [Terms of use](https://www.naturalearthdata.com/about/terms-of-use/)

## Textures (CC0)

These textures come from Poly Haven under [CC0](https://creativecommons.org/publicdomain/zero/1.0/).

- Grey Roof Tiles by Rob Tuytel. https://polyhaven.com/a/grey_roof_tiles
- Cobblestone Floor 04 by Rob Tuytel. https://polyhaven.com/a/cobblestone_floor_04
- Forest Floor by eye-candy.xyz. https://polyhaven.com/a/forest_floor
- White Rough Plaster by Rob Tuytel. https://polyhaven.com/a/white_rough_plaster
- Wood Planks Grey by Rob Tuytel. https://polyhaven.com/a/wood_planks_grey
- Grass Ground by Charlotte Baglioni. https://polyhaven.com/a/grass_ground
- Fir Tree 01 twig diffuse and opacity atlas by Rob Tuytel and Rico Cilliers. https://polyhaven.com/a/fir_tree_01
- Tree Small 02 leaf diffuse and opacity atlas by Rico Cilliers. https://polyhaven.com/a/tree_small_02

## Portraits and story CG

Portraits and story CG generated for this project by Grok Bot (AI-generated). The files are in `Assets/Resources/Portraits/` and `Assets/Resources/Story/`.

## Interface and characters

- The ink UI kit in `Assets/Art/UI/Resources/InkUi/` was generated in the editor for this project by `Assets/Editor/InkUiGenerator.cs`. Its look was informed by references, but it contains no third-party images.
- Codex B built the procedural fallback character meshes and poses (`Assets/Scripts/World/CharacterFactory.cs`). They use the project's URP solid material.
- The courtyard buildings, terrain and vegetation placement are procedural code written for this project.

## 3D characters

Quaternius released all three Standard packs under CC0 1.0. Licence files accompany the imported assets in `Assets/ThirdParty/Quaternius/`. [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)

- Universal Base Characters provides the rigged adults, face textures and hairstyles. [Quaternius](https://quaternius.com/packs/universalbasecharacters.html)
- Universal Animation Library provides the humanoid idle and walk loops. [Quaternius](https://quaternius.com/packs/universalanimationlibrary.html)
- Modular Character Outfits – Fantasy provides the peasant trousers and footwear. [Quaternius](https://quaternius.com/packs/modularcharacteroutfitsfantasy.html)

The Tang outer garments and hair ornaments were made for this project.
