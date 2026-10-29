using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TW3D.Shared
{
    /// <summary>
    /// Reader of the .lev level format of the 2016 version. Records are separated by '|':
    /// a block is "name x,y,shift", a dynamic object is "Type model x,y". The middle layer comes first,
    /// '*' starts the back layer and '#' the front layer.
    /// </summary>
    internal static class LegacyLevelFormat
    {
        public static LevelData Parse(string text)
        {
            LevelData level = new LevelData();
            List<BlockPlacement> layer = level.Middle;

            foreach (string rawRecord in text.Split('|'))
            {
                string record = rawRecord.Trim();

                //Layer markers precede the first record of the layer
                while (record.Length > 0 && (record[0] == '*' || record[0] == '#'))
                {
                    layer = record[0] == '*' ? level.Back : level.Front;
                    record = record.Substring(1).TrimStart();
                }
                if (record.Length == 0)
                    continue;

                //The 2016 editor wrote numbers with the decimal separator of the system, so on Czech Windows
                //a coordinate like 94.00001 became "94,00001", adding extra commas. The numbers are read
                //the same way as the 2016 game read them: X and Y up to the first non-digit, the shift as a decimal number.
                string[] parts = record.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string[] numbers = SplitNumbers(parts[1], 3, record);
                    layer.Add(new BlockPlacement
                    {
                        Block = parts[0],
                        X = ParseWholeNumber(numbers[0], record),
                        Y = ParseWholeNumber(numbers[1], record),
                        Shift = (int)Math.Round(ParseDecimal(string.Join(".", numbers, 2, numbers.Length - 2), record))
                    });
                }
                else if (parts.Length == 3)
                {
                    string[] numbers = SplitNumbers(parts[2], 2, record);
                    level.Objects.Add(new ObjectPlacement
                    {
                        Type = parts[0],
                        Model = parts[1],
                        X = ParseWholeNumber(numbers[0], record),
                        Y = ParseWholeNumber(numbers[1], record)
                    });
                }
                else
                    throw new InvalidDataException($"Unexpected record \"{record}\" in a .lev file.");
            }

            return level;
        }

        private static string[] SplitNumbers(string text, int minCount, string record)
        {
            string[] parts = text.Split(',');
            if (parts.Length < minCount)
                throw new InvalidDataException($"Expected {minCount} numbers in the record \"{record}\" in a .lev file.");
            return parts;
        }

        private static int ParseWholeNumber(string text, string record)
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
                throw new InvalidDataException($"Invalid number \"{text}\" in the record \"{record}\" in a .lev file.");
            return number;
        }

        private static float ParseDecimal(string text, string record)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                throw new InvalidDataException($"Invalid number \"{text}\" in the record \"{record}\" in a .lev file.");
            return number;
        }
    }
}
