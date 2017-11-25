using System;

namespace BitTorrentSyncIgnore.Extensions
{
    public static class StringExt
    {
        public static string GetUntilOrEmpty(this string text, string stopAt = "-")
        {
            if (!String.IsNullOrWhiteSpace(text))
            {
                int charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);

                if (charLocation > 0)
                {
                    return text.Substring(0, charLocation).Trim();
                }
            }

            return String.Empty;
        }

        public static string GetNameBeforeYear(this string text)
        {
            var folder = text.Trim().GetUntilOrEmpty("(");
            if (folder != string.Empty) return folder;
            
            var lastSpace = text.LastIndexOf(" ");
            if (lastSpace == -1) return text;

            // see if the last text has a year after it... before we cut a word off
            var wordAfter = text.Substring(lastSpace, text.Length - lastSpace);
            if(int.TryParse(wordAfter, out int result))
            {
                return text.Substring(0, lastSpace).Trim();
            }
            else
            {
                // last word is not a year.. so assume its part of the name.
                return text;
            }
        }

        public static int? GetYear(this string text)
        {
            var lastSpace = text.LastIndexOf(" ");
            if (lastSpace == -1) return null;
            var yearString = text.Substring(lastSpace, text.Length - lastSpace);
            yearString = yearString.Replace("(", "").Replace(")", "");
            if (int.TryParse(yearString, out int year)) return year;
            return null;
        }


    }
}
