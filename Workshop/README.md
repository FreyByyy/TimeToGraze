# Steam Workshop page

- `description_en.bbcode`, `description_ru.bbcode` — page text in Steam BBCode. Paste each into the matching language of the Workshop item.
- `images/b_fieldguide/<card>_<lang>.png` — description images (1280 px wide, Steam scales them to the column).
- `art/` — the four illustrations used in the cards.
- `mockups/` — HTML sources of the images. Texts live in `mockups/content.js`; run `python -I mockups/render.py` to re-render everything (needs Microsoft Edge and Pillow).

## Image links

Steam does not host description images, so the descriptions link to the PNGs in this repository through `https://raw.githubusercontent.com/FreyByyy/TimeToGraze/main/...`. After changing an image, push it to `main` and Steam picks it up (it may take a while because of caching). Keep the paths below stable, or update the links in both descriptions.

| Section | Image |
|---|---|
| `{IMG_HEADER}` | `About/Preview.png` (cover) |
| `{IMG_CONTENTS}` | `images/b_fieldguide/header_<lang>.png` |
| `{IMG_PASTURE}` | `images/b_fieldguide/pasture_<lang>.png` |
| `{IMG_TAB}` | `images/b_fieldguide/tab_<lang>.png` |
| `{IMG_LEAD}` | `images/b_fieldguide/lead_<lang>.png` |
| `{IMG_GUARDS}` | `images/b_fieldguide/guards_<lang>.png` |
| `{IMG_DANGER}` | `images/b_fieldguide/danger_<lang>.png` |
| `{IMG_COMPAT}` | `images/b_fieldguide/compat_<lang>.png` |
