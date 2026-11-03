"""Builds the font of the menus and of the display of a run (content/Fonts/TripleWheelDisplay.ttf) from Orbitron
(art/fonts/Orbitron[wght].ttf, SIL Open Font License 1.1, by Matt McInerney and the Orbitron Project Authors), which
the font the game had since 2016 (a precompiled .xnb of capitals and ASCII only) was close to:

- the weight ExtraBold of the variable font, as a static font;
- the Czech letters Orbitron lacks: the capitals Ě Č Ř Ů Ť Ď Ň and the small ě č ř ů ň, each its base letter with the
  caron or the ring of the font, centred over it as on its own Š, š, Å and å; the small ť and ď with the caron as an
  apostrophe beside the stem, as Czech typography writes them;
- named Triple Wheel Display: the licence reserves the name Orbitron, which a modified version may not carry.

    python tools/fontgen/menufont.py

Needs Python 3 with fontTools (pip install fonttools). It also writes the descriptions the content pipeline builds the
game's font from, content/Fonts/menuFont<size>.spritefont, in several sizes, so that the game draws a text from the
nearest larger size, never enlarged (GameFont): a size of 40 has a capital as high as in the font of 2016 (38
pixels), its text about as wide, and is the size the layouts are measured in. The sizes up to 80 have every character
the font has among ASCII, the Latin letters with accents (up to U+017F), the dashes, the quotes, the ellipsis and the
euro (Orbitron lacks some of them, e.g. ^ and the copyright sign; the game draws a character it lacks as "?"); the
largest size, for the countdown and the titles, only the digits, the capitals and a little punctuation, so that its
texture stays within 2048 pixels (the Reach profile of phones). Run the script after changing it and commit its output.
"""
import os
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.varLib import instancer

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SOURCE = os.path.join(ROOT, 'art', 'fonts', 'Orbitron[wght].ttf')
OUTPUT = os.path.join(ROOT, 'content', 'Fonts', 'TripleWheelDisplay.ttf')
SPRITEFONTS = os.path.join(ROOT, 'content', 'Fonts', 'menuFont{size}.spritefont')
# The sizes of the game's font (GameFont reads the same list), the size its layouts are measured in, and its spacing
# there (in pixels; the other sizes in proportion)
SIZES = (20, 40, 80, 160)
BASE_SIZE, BASE_SPACING = 40, 1
# The characters of the sizes (those the font has of them): all of them, or only those of the countdown and the titles
WANTED = list(range(0x20, 0x7F)) + list(range(0xA0, 0x180)) + [0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026, 0x20AC]
LARGE = [ord(c) for c in ' !"\'(),-.:?0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ']
LARGE_FROM = 160

WEIGHT = 800
FAMILY, STYLE = 'Triple Wheel Display', 'ExtraBold'
POSTSCRIPT = 'TripleWheelDisplay-ExtraBold'
CARON, RING, APOSTROPHE = 'uni030C', 'uni030A', 'quoteright'
# The Czech letters to add: their base letter and their mark, centred over it
MARKED = {0x011A: ('E', CARON), 0x010C: ('C', CARON), 0x0158: ('R', CARON), 0x016E: ('U', RING),
          0x0164: ('T', CARON), 0x010E: ('D', CARON), 0x0147: ('N', CARON),
          0x011B: ('e', CARON), 0x010D: ('c', CARON), 0x0159: ('r', CARON), 0x016F: ('u', RING), 0x0148: ('n', CARON)}
# The small ť and ď: the base letter and an apostrophe beside its stem
APOSTROPHED = {0x0165: 't', 0x010F: 'd'}


def unicode_tables(font):
    return [table for table in font['cmap'].tables if table.isUnicode()]


def bounds(glyf, name):
    glyph = glyf[name]
    glyph.recalcBounds(glyf)
    return glyph.xMin, glyph.yMin, glyph.xMax, glyph.yMax


def component(name, x, y, metrics=False):
    part = GlyphComponent()
    part.glyphName, part.x, part.y = name, x, y
    # USE_MY_METRICS: the advance and the side bearings of the base letter
    part.flags = 0x4 if metrics else 0
    return part


def add_glyph(font, codepoint, parts, advance):
    glyph = Glyph()
    glyph.numberOfContours = -1
    glyph.components = parts
    name = 'uni%04X' % codepoint
    font['glyf'][name] = glyph
    font['hmtx'][name] = advance
    for table in unicode_tables(font):
        table.cmap[codepoint] = name


def main():
    font = instancer.instantiateVariableFont(TTFont(SOURCE), {'wght': WEIGHT})
    glyf, hmtx = font['glyf'], font['hmtx']

    # A mark stands as high over a capital as on the font's own Š, over a small letter as on its š
    lift_capital = next(c.y for c in glyf['Scaron'].components if c.glyphName == CARON)
    lift_small = next(c.y for c in glyf['scaron'].components if c.glyphName == CARON)
    for codepoint, (base, mark) in MARKED.items():
        b, m = bounds(glyf, base), bounds(glyf, mark)
        x = round((b[0] + b[2]) / 2 - (m[0] + m[2]) / 2)
        add_glyph(font, codepoint, [component(base, 0, 0, True), component(mark, x, lift_capital if base.isupper() else lift_small)],
                  hmtx[base])

    # The apostrophe beside the stem: its top at the top of the letter, a little gap after the letter's right edge;
    # the advance grows by what it adds
    a = bounds(glyf, APOSTROPHE)
    gap = 30
    for codepoint, base in APOSTROPHED.items():
        b = bounds(glyf, base)
        x = round(b[2] + gap - a[0])
        y = round(b[3] - a[3])
        advance, bearing = hmtx[base]
        right = x + a[2]
        add_glyph(font, codepoint, [component(base, 0, 0), component(APOSTROPHE, x, y)],
                  (max(advance, right + (advance - b[2])), bearing))

    # The new name; the copyright, the licence and the designer stay
    names = font['name']
    for record in list(names.names):
        if record.nameID in (1, 2, 3, 4, 6, 16, 17, 21, 22, 25):
            names.removeNames(nameID=record.nameID)
    for name_id, value in ((1, FAMILY + ' ' + STYLE), (2, 'Regular'), (3, POSTSCRIPT + ';Triple Wheel 3D'),
                           (4, FAMILY + ' ' + STYLE), (6, POSTSCRIPT), (16, FAMILY), (17, STYLE)):
        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)
    description = ('Triple Wheel Display is a modified version of Orbitron (the ExtraBold weight, the Czech letters added), '
                   'made for the game Triple Wheel 3D.')
    names.setName(description, 10, 3, 1, 0x409)
    font.save(OUTPUT)
    missing = [c for c in 'ĚŠČŘŽÝÁÍÉŮÚŤĎŇÓěščřžýáíéůúťďňó' if ord(c) not in font.getBestCmap()]
    print(f'{OUTPUT}: {len(font.getGlyphOrder())} glyphs, missing Czech letters: {"".join(missing) or "none"}')
    for size in SIZES:
        write_spritefont(font.getBestCmap(), size)


def write_spritefont(cmap, size):
    """The description of a size of the game's font for the content pipeline: the character regions the font has."""
    runs = []
    for codepoint in (LARGE if size >= LARGE_FROM else WANTED):
        if codepoint not in cmap:
            continue
        if runs and runs[-1][1] == codepoint - 1:
            runs[-1][1] = codepoint
        else:
            runs.append([codepoint, codepoint])
    regions = ''.join(f'      <CharacterRegion>\n        <Start>&#x{a:04X};</Start>\n        <End>&#x{b:04X};</End>\n      </CharacterRegion>\n'
                      for a, b in runs)
    spacing = BASE_SPACING * size / BASE_SIZE
    what = 'the digits, the capitals and a little punctuation' if size >= LARGE_FROM else 'every character it has of Latin'
    text = f'''<?xml version="1.0" encoding="utf-8"?>
<!--
  Written by tools/fontgen/menufont.py: do not edit, change the script and run it.
  The font of the menus, the titles and the display of a run in the size {size} ({what}): {FAMILY}
  ({os.path.basename(OUTPUT)}, SIL Open Font License 1.1, see TripleWheelDisplay-OFL.txt), a version of Orbitron with
  the Czech letters. GameFont draws a text from the nearest larger size; a character it lacks is drawn as "?".
-->
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>{os.path.basename(OUTPUT)}</FontName>
    <Size>{size}</Size>
    <Spacing>{spacing:g}</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>?</DefaultCharacter>
    <CharacterRegions>
{regions}    </CharacterRegions>
  </Asset>
</XnaContent>
'''
    path = SPRITEFONTS.format(size=size)
    with open(path, 'w', encoding='utf-8', newline='\n') as file:
        file.write(text)
    print(f'{path}: {sum(b - a + 1 for a, b in runs)} characters in {len(runs)} regions')


if __name__ == '__main__':
    main()
