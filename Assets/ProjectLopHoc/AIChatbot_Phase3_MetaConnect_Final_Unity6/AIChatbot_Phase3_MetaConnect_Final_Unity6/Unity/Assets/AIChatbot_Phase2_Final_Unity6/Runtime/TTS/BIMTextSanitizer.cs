using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BIM4D.AI.Speech
{
    /// <summary>
    /// Làm sạch văn bản AI trước khi đọc: bỏ định dạng Markdown/HTML/code, chuẩn hóa khoảng trắng,
    /// và chia đoạn theo câu. KHÔNG tóm tắt, KHÔNG đổi nội dung chuyên môn (BIM, IFC, LOD 300, ISO 19650...).
    /// Hàm thuần (không phụ thuộc Unity) nên dễ kiểm thử.
    /// </summary>
    public static class BIMTextSanitizer
    {
        private static readonly Regex CodeFence = new Regex("```[\\s\\S]*?(```|$)");
        private static readonly Regex InlineCode = new Regex("`([^`\\n]+)`");
        private static readonly Regex ImageMd = new Regex("!\\[([^\\]]*)\\]\\([^)]*\\)");
        private static readonly Regex LinkMd = new Regex("\\[([^\\]]+)\\]\\(([^)]*)\\)");
        private static readonly Regex RawUrl = new Regex("https?://\\S+");
        private static readonly Regex HtmlTag = new Regex("</?[a-zA-Z][^>]*>");
        private static readonly Regex HeadingMark = new Regex("^\\s{0,3}#{1,6}\\s+");
        private static readonly Regex QuoteMark = new Regex("^\\s*>+\\s?");
        private static readonly Regex HorizontalRule = new Regex("^\\s*([-*_]\\s*){3,}$");
        private static readonly Regex BulletMark = new Regex("^\\s*[-*+•]\\s+");
        private static readonly Regex NumberMark = new Regex("^\\s*\\d{1,3}[.)]\\s+");
        private static readonly Regex TableSeparator = new Regex("^\\s*\\|?\\s*:?-{2,}:?\\s*(\\|\\s*:?-{2,}:?\\s*)*\\|?\\s*$");
        private static readonly Regex Bold = new Regex("\\*\\*(.+?)\\*\\*");
        private static readonly Regex BoldUnderscore = new Regex("__(.+?)__");
        private static readonly Regex Strike = new Regex("~~(.+?)~~");
        private static readonly Regex ItalicStar = new Regex("(?<![\\w*])\\*(?!\\s)(.+?)(?<!\\s)\\*(?![\\w*])");
        private static readonly Regex ItalicUnderscore = new Regex("(?<!\\w)_(?!\\s)(.+?)(?<!\\s)_(?!\\w)");
        private static readonly Regex Spaces = new Regex("[ \\t\\u00A0]+");
        private static readonly Regex SentenceSplit = new Regex("(?<=[\\.\\!\\?…])\\s+|\\n+");
        private static readonly Regex VietnameseMarks = new Regex(
            "[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ" +
            "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ]");

        // ------------------------------------------------------------------ làm sạch

        public static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            string s = input.Replace("\r\n", "\n").Replace('\r', '\n');
            s = CodeFence.Replace(s, "\n");        // bỏ khối code/JSON
            s = ImageMd.Replace(s, "$1");
            s = LinkMd.Replace(s, "$1");           // giữ chữ, bỏ đường dẫn
            s = RawUrl.Replace(s, "");
            s = HtmlTag.Replace(s, "");
            s = InlineCode.Replace(s, "$1");

            var sb = new StringBuilder(s.Length);
            foreach (string rawLine in s.Split('\n'))
            {
                string line = rawLine;
                if (line.Trim().Length == 0) { sb.Append('\n'); continue; }
                if (HorizontalRule.IsMatch(line) || TableSeparator.IsMatch(line)) continue;

                bool structural = false; // dòng tiêu đề / gạch đầu dòng / bảng cần dấu chấm để TTS ngắt nghỉ

                if (HeadingMark.IsMatch(line)) { line = HeadingMark.Replace(line, ""); structural = true; }
                line = QuoteMark.Replace(line, "");
                if (BulletMark.IsMatch(line)) { line = BulletMark.Replace(line, ""); structural = true; }
                else if (NumberMark.IsMatch(line)) { line = NumberMark.Replace(line, ""); structural = true; }

                if (line.IndexOf('|') >= 0)
                {
                    // Bảng: mỗi ô cách nhau dấu phẩy để TTS ngắt nghỉ, không để thừa khoảng trắng quanh ô.
                    string[] cells = line.Trim().Trim('|').Split('|');
                    for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                    line = string.Join(", ", cells);
                    structural = true;
                }

                line = Bold.Replace(line, "$1");
                line = BoldUnderscore.Replace(line, "$1");
                line = Strike.Replace(line, "$1");
                line = ItalicStar.Replace(line, "$1");
                line = ItalicUnderscore.Replace(line, "$1");
                line = Spaces.Replace(line, " ").Trim();
                if (line.Length == 0) continue;

                if (structural && !EndsWithPunctuation(line)) line += ".";
                sb.Append(line).Append('\n');
            }

            string result = Regex.Replace(sb.ToString(), "\\n{2,}", "\n");
            return result.Trim();
        }

        private static bool EndsWithPunctuation(string s)
        {
            char c = s[s.Length - 1];
            return c == '.' || c == '!' || c == '?' || c == '…' || c == ':' || c == ';' || c == ',';
        }

        // ------------------------------------------------------------------ ngôn ngữ

        /// <summary>"vi" nếu có dấu tiếng Việt, ngược lại "en". Chỉ là heuristic cho chế độ Tự động.</summary>
        public static string DetectLanguage(string text)
        {
            return !string.IsNullOrEmpty(text) && VietnameseMarks.IsMatch(text) ? "vi" : "en";
        }

        // ------------------------------------------------------------------ chia đoạn

        /// <summary>
        /// Chia theo câu, giữ đúng thứ tự, không cắt giữa từ. Đoạn đầu ngắn (firstMax) để bắt đầu phát sớm,
        /// các đoạn sau tối đa maxChars.
        /// </summary>
        public static List<string> SplitIntoChunks(string text, int firstMax, int maxChars)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;
            if (maxChars < 50) maxChars = 50;
            if (firstMax < 50) firstMax = 50;

            var cur = new StringBuilder();
            int limit = firstMax;

            foreach (string sentence in SentenceSplit.Split(text))
            {
                string s = sentence.Trim();
                if (s.Length == 0) continue;
                // Đoạn đầu giữ ngắn (firstMax) để bắt đầu phát sớm, kể cả khi câu đầu rất dài.
                int pieceMax = (chunks.Count == 0 && cur.Length == 0) ? firstMax : maxChars;
                foreach (string piece in SplitLong(s, pieceMax))
                {
                    if (cur.Length == 0)
                    {
                        cur.Append(piece);
                    }
                    else if (cur.Length + 1 + piece.Length <= limit)
                    {
                        cur.Append(' ').Append(piece);
                    }
                    else
                    {
                        chunks.Add(cur.ToString());
                        cur.Clear();
                        limit = maxChars;
                        cur.Append(piece);
                    }
                }
            }
            if (cur.Length > 0) chunks.Add(cur.ToString());
            return chunks;
        }

        private static IEnumerable<string> SplitLong(string s, int max)
        {
            while (s.Length > max)
            {
                int idx = s.LastIndexOf(' ', max);
                if (idx <= 0) idx = max;
                yield return s.Substring(0, idx).Trim();
                s = s.Substring(idx).TrimStart();
            }
            if (s.Length > 0) yield return s;
        }
    }
}
