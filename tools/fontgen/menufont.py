"""Builds the font of the menus and of the display of a run (content/Fonts/TripleWheelDisplay.ttf) from Orbitron
(art/fonts/Orbitron[wght].ttf, SIL Open Font License 1.1, by Matt McInerney and the Orbitron Project Authors), which
the font the game had since 2016 (a precompiled .xnb of ASCII only) was close to:

- the weight ExtraBold of the variable font, as a static font;
- the Czech capitals Orbitron lacks (Ě Č Ř Ů Ť Ď Ň), each its base letter with the caron or the ring of the font,
  centred over it and raised as on its own Š and Å;
- capitals only: every small letter shows its capital (as the font of 2016 did), the Czech ones too;
- named Triple Wheel Display: the licence reserves the name Orbitron, which a modified version may not carry.

    python tools/fontgen/menufont.py

Needs Python 3 with fontTools (pip install fonttools). It also writes content/Fonts/menuFont.spritefont, from which the
content pipeline builds the game's font: the size and the spacing below, and every character the font has among ASCII,
the Latin letters with accents (up to U+017F), the dashes, the quotes, the ellipsis and the euro (Orbitron lacks some
of them, e.g. ^ and the copyright sign; the game draws a character it lacks as "?"). Run the script after changing it
and commit its output.
"""
import os
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.varLib import instancer

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SOURCE = os.path.join(ROOT, 'art', 'fonts', 'Orbitron[wght].ttf')
OUTPUT = os.path.join(ROOT, 'content', 'Fonts', 'TripleWheelDisplay.ttf')
SPRITEFONT = os.path.join(ROOT, 'content', 'Fonts', 'menuFont.spritefont')
# The size and the spacing of the game's font: a capital as high as in the font of 2016 (38 pixels), the text about as
# wide, so that the layouts stay as they were (a line is 68 pixels instead of 64)
SIZE, SPACING = 40, 1
# The characters the game's font may have (those the font has of them)
WANTED = list(range(0x20, 0x7F)) + list(range(0xA0, 0x180)) + [0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026, 0x20AC]

WEIGHT = 800
FAMILY, STYLE = 'Triple Wheel Display', 'ExtraBold'
POSTSCRIPT = 'TripleWheelDisplay-ExtraBold'
CARON, RING = 'uni030C', 'uni030A'
# The Czech capitals to add: their base letter and their mark
CZECH = {0x011A: ('E', CARON), 0x010C: ('C', CARON), 0x0158: ('R', CARON), 0x016E: ('U', RING),
         0x0164: ('T', CARON), 0x010E: ('D', CARON), 0x0147: ('N', CARON)}


def unicode_tables(font):
    return [table for table in font['cmap'].tables if table.isUnicode()]


def centre(glyf, name):
    glyph = glyf[name]
    glyph.recalcBounds(glyf)
    return (glyph.xMin + glyph.xMax) / 2


def main():
    font = instancer.instantiateVariableFont(TTFont(SOURCE), {'wght': WEIGHT})
    glyf, hmtx = font['glyf'], font['hmtx']

    # The marks stand as high over a capital as on the font's own Š
    lift = next(c.y for c in glyf['Scaron'].components if c.glyphName == CARON)
    for codepoint, (base, mark) in CZECH.items():
        name = 'uni%04X' % codepoint
        glyph = Glyph()
        glyph.numberOfContours = -1
        glyph.components = []
        for part, x, y in ((base, 0, 0), (mark, round(centre(glyf, base) - centre(glyf, mark)), lift)):
            component = GlyphComponent()
            component.glyphName, component.x, component.y = part, x, y
            # The advance and the side bearings of the base letter
            component.flags = 0x4 if part == base else 0
            glyph.components.append(component)
        glyf[name] = glyph
        hmtx[name] = hmtx[base]
        for table in unicode_tables(font):
            table.cmap[codepoint] = name

    # Capitals only: a small letter maps to the glyph of its capital
    capitals = dict(font.getBestCmap())
    for table in unicode_tables(font):
        for codepoint in list(table.cmap):
            upper = chr(codepoint).upper()
            if len(upper) == 1 and upper != chr(codepoint) and ord(upper) in capitals:
                table.cmap[codepoint] = capitals[ord(upper)]
        for codepoint in CZECH:
            table.cmap[ord(chr(codepoint).lower())] = 'uni%04X' % codepoint

    # The new name; the copyright, the licence and the designer stay
    names = font['name']
    for record in list(names.names):
        if record.nameID in (1, 2, 3, 4, 6, 16, 17, 21, 22, 25):
            names.removeNames(nameID=record.nameID)
    for name_id, value in ((1, FAMILY + ' ' + STYLE), (2, 'Regular'), (3, POSTSCRIPT + ';Triple Wheel 3D'),
                           (4, FAMILY + ' ' + STYLE), (6, POSTSCRIPT), (16, FAMILY), (17, STYLE)):
        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)
    description = ('Triple Wheel Display is a modified version of Orbitron (the ExtraBold weight, the Czech capitals added, '
                   'small letters shown as capitals), made for the game Triple Wheel 3D.')
    names.setName(description, 10, 3, 1, 0x409)
    font.save(OUTPUT)
    missing = [c for c in 'ĚŠČŘŽÝÁÍÉŮÚŤĎŇÓěščřžýáíéůúťďňó' if ord(c) not in font.getBestCmap()]
    print(f'{OUTPUT}: {len(font.getGlyphOrder())} glyphs, missing Czech letters: {"".join(missing) or "none"}')
    write_spritefont(font.getBestCmap())


def write_spritefont(cmap):
    """The description of the game's font for the content pipeline: the character regions the font has."""
    runs = []
    for codepoint in WANTED:
        if codepoint not in cmap:
            continue
        if runs and runs[-1][1] == codepoint - 1:
            runs[-1][1] = codepoint
        else:
            runs.append([codepoint, codepoint])
    regions = ''.join(f'      <CharacterRegion>\n        <Start>&#x{a:04X};</Start>\n        <End>&#x{b:04X};</End>\n      </CharacterRegion>\n'
                      for a, b in runs)
    text = f'''<?xml version="1.0" encoding="utf-8"?>
<!--
  Written by tools/fontgen/menufont.py: do not edit, change the script and run it.
  The font of the menus, the titles and the display of a run: {FAMILY} ({os.path.basename(OUTPUT)}, SIL Open Font
  License 1.1, see TripleWheelDisplay-OFL.txt), a version of Orbitron with capitals only and the Czech letters. A
  capital is as high as in the font of 2016; a character it lacks is drawn as "?".
-->
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>{os.path.basename(OUTPUT)}</FontName>
    <Size>{SIZE}</Size>
    <Spacing>{SPACING}</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>?</DefaultCharacter>
    <CharacterRegions>
{regions}    </CharacterRegions>
  </Asset>
</XnaContent>
'''
    with open(SPRITEFONT, 'w', encoding='utf-8', newline='\n') as file:
        file.write(text)
    print(f'{SPRITEFONT}: {sum(b - a + 1 for a, b in runs)} characters in {len(runs)} regions')


if __name__ == '__main__':
    main()
