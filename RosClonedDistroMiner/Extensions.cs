using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RosClonedDistroMiner
{
    public static class Extensions
    {

        public static string HorizontalLine(int width)
        {
            StringBuilder str = new();
            for (int i = 0; i < width; i++) str.Append("-");
            str.Append("\n");
            return str.ToString();
        }
        public static string SideBySide(string str1, string str2, int columnWidth)
        {
            var lines1 = str1.Split('\n');
            var lines2 = str2.Split('\n');

            var wrappedLines1 = lines1.SelectMany(line => CharWrap(line, columnWidth).Split('\n')).ToArray();
            var wrappedLines2 = lines2.SelectMany(line => CharWrap(line, columnWidth).Split('\n')).ToArray();

            int maxLines = Math.Max(wrappedLines1.Length, wrappedLines2.Length);

            StringBuilder output = new();

            for (int i = 0; i < maxLines; i++)
            {
                string line1 = i < wrappedLines1.Length ? wrappedLines1[i].PadRight(columnWidth) : new string(' ', columnWidth);
                string line2 = i < wrappedLines2.Length ? wrappedLines2[i].PadRight(columnWidth) : new string(' ', columnWidth);

                output.Append(line1);
                output.Append(" | ");
                output.Append(line2);
                output.Append('\n');
            }
            return output.ToString();
        }

        public static void PrintSideBySide(string str1, string str2, int columnWidth)
        {
            Console.Write(SideBySide(str1, str2, columnWidth));
        }
        static string CharWrap(string text, int maxWidth)
        {
            var result = "";
            for (int i = 0; i < text.Length; i += maxWidth)
            {
                result += text.Substring(i, Math.Min(maxWidth, text.Length - i)) + "\n";
            }
            return result.TrimEnd();
        }
    }
}
