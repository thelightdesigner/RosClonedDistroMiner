using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static RosClonedDistroMiner.Commit;

namespace RosClonedDistroMiner
{
    class MainlineBackportCommitPair(string mainlineSHA, string backportedSHA, Repository repo, bool extractContents)
    {
        public Commit Mainline { get; set; } = new Commit(mainlineSHA, repo, extractContents);
        public Commit Backported { get; set; } = new Commit(backportedSHA, repo, extractContents);
        public override string ToString()
        {
            string str = "";

            str += Extensions.HorizontalLine(Program.COLUMN_WIDTH * 2);
            str += Extensions.SideBySide("Mainline", "Backported", Program.COLUMN_WIDTH);
            str += Extensions.HorizontalLine(Program.COLUMN_WIDTH * 2);
            str += Extensions.SideBySide(Mainline.ToString(), Backported.ToString(), Program.COLUMN_WIDTH);
            str += Extensions.HorizontalLine(Program.COLUMN_WIDTH * 2);
            return str;
        }
    }

    public class Commit
    {
        public string RawMessage { get; set; }
        public string SHA { get; set; }


        [JsonIgnore]
        public List<Modified<File>> ModifiedFiles { get; set; }

        public List<Modified<FileSegment>> ModifiedFilesSegmented { get; set; }
        public Commit(string SHA, Repository repo, bool extractFileContents)
        {
            this.SHA = SHA;
            this.RawMessage = "";
            ModifiedFiles = new();
            ModifiedFilesSegmented = new();
            if (!extractFileContents) return;

            RawMessage = repo.ExecuteGitCommand($"log --format=%B -n 1 {SHA}");

            string[] modifiedFiles = repo.ExecuteGitCommand($"diff --name-only {SHA}^ {SHA}")
            .Split("\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !new string[] { ".github" }.Any(str => path.Contains(str)))
            .Select(path => Path.Combine(repo.Path, path))
            .ToArray();

            var modified = new Modified<List<File>>()
            {
                Before = new List<File>(),
                After = new List<File>()
            };

            //get the contents of all modified files on the commit BEFORE this one
            repo.ExecuteGitCommand($"checkout {SHA}^1");
            foreach (string path in modifiedFiles) modified.Before.Add(new File(path, extractFileContents));

            //switch to this SHA, then get contents
            repo.ExecuteGitCommand($"checkout {SHA}");
            foreach (string path in modifiedFiles) modified.After.Add(new File(path, extractFileContents));


            //convert Modified<List<FileSegment>> to List<Modified<FileSegment>>
            foreach (string path in modifiedFiles)
            {
                ModifiedFiles.Add(new Modified<File>()
                {
                    Before = modified.Before.Find(file => file.Path.Equals(path)) ?? throw new Exception("darn 1"),
                    After = modified.After.Find(file => file.Path.Equals(path)) ?? throw new Exception("darn 2")
                });
            }

            foreach (var wholeFile in ModifiedFiles)
            {
                ModifiedFilesSegmented.AddRange(FindDifferences(wholeFile.Before.Contents, wholeFile.After.Contents, 2)
                    .Select(diff => new Modified<FileSegment>()
                    {
                        Before = new FileSegment(wholeFile.Before.Path, diff.Before, wholeFile.Before.Exists),
                        After = new FileSegment(wholeFile.After.Path, diff.After, wholeFile.After.Exists)
                    }));
            }
        }

        public override string ToString()
        {
            StringBuilder str = new();
            str.Append($"Message: {RawMessage.Split("\n").FirstOrDefault()} \nSHA: {SHA}\n");
            foreach (var modified in ModifiedFilesSegmented)
            {
                str.Append(modified.ToString());
                str.Append(Extensions.HorizontalLine(Program.COLUMN_WIDTH));
            }
            return str.ToString();
        }

        public class Modified<T>
        {
            public T Before { get; set; }
            public T After { get; set; }
            public override string ToString()
            {
                string hz = Extensions.HorizontalLine(Program.COLUMN_WIDTH);
                return $"{hz}****BEFORE****\n{Before}\n{hz}****AFTER****\n{After}\n";
            }
        }

        public class File
        {
            public string Path { get; set; }
            public string Contents { get; set; }
            public bool Exists { get; set; }
            public File(string path, bool readContents)
            {
                Path = path;
                Exists = System.IO.File.Exists(Path);
                Contents = Exists && readContents ? System.IO.File.ReadAllText(Path) : "";
            }
            public override string ToString()
            {
                return $"Name: {new FileInfo(Path).Name}\n\n{Contents}";
            }
        }
        public class FileSegment
        {
            public string Path { get; set; }
            public Line[] Lines { get; set; }
            public bool Exists { get; set; }
            public FileSegment(string path, Line[] contents, bool exists)
            {
                Path = path;
                Lines = contents;
                Exists = exists;
            }
            public override string ToString()
            {
                StringBuilder str = new();
                for (int i = 0; i < Lines.Length; i++)
                {
                    Line currentLine = Lines[i];
                    char prefix = currentLine.Status switch
                    {
                        LineStatus.Unchanged => ' ',
                        LineStatus.Modified => '~',
                        LineStatus.New => '+',
                        LineStatus.Deleted => '-',
                        _ => throw new NotImplementedException()
                    };

                    str.Append($"{currentLine.LineNumber}: ");
                    str.Append(prefix);
                    str.Append("  ");
                    str.AppendLine(currentLine.Content);
                }
                return str.ToString();
                //  return string.Join("", Lines.Select(line => line.Content));
            }
        }
        public enum LineStatus
        {
            Unchanged,
            Modified,
            New,
            Deleted
        }

        public class Line
        {
            public int LineNumber { get; set; }
            public string Content { get; set; }
            public LineStatus Status { get; set; }
        }

        public static List<Modified<Line[]>> FindDifferences(string beforeContent, string afterContent, int contextLines)
        {
            var beforeLines = beforeContent.Split('\n');
            var afterLines = afterContent.Split('\n');
            var modifications = new List<Modified<Line[]>>();

            int beforeIndex = 0, afterIndex = 0;
            while (beforeIndex < beforeLines.Length || afterIndex < afterLines.Length)
            {
                if (beforeIndex < beforeLines.Length && afterIndex < afterLines.Length && beforeLines[beforeIndex] == afterLines[afterIndex])
                {
                    beforeIndex++;
                    afterIndex++;
                    continue;
                }

                var beforeSnippet = new List<Line>();
                var afterSnippet = new List<Line>();

                int startBefore = Math.Max(0, beforeIndex - contextLines);
                int startAfter = Math.Max(0, afterIndex - contextLines);

                for (int i = startBefore; i < beforeIndex; i++)
                    beforeSnippet.Add(new Line { LineNumber = i + 1, Content = beforeLines[i], Status = LineStatus.Unchanged });
                for (int i = startAfter; i < afterIndex; i++)
                    afterSnippet.Add(new Line { LineNumber = i + 1, Content = afterLines[i], Status = LineStatus.Unchanged });

                while (beforeIndex < beforeLines.Length && afterIndex < afterLines.Length && beforeLines[beforeIndex] != afterLines[afterIndex])
                {
                    beforeSnippet.Add(new Line { LineNumber = beforeIndex + 1, Content = beforeLines[beforeIndex], Status = LineStatus.Deleted });
                    afterSnippet.Add(new Line { LineNumber = afterIndex + 1, Content = afterLines[afterIndex], Status = LineStatus.New });
                    beforeIndex++;
                    afterIndex++;
                }

                while (beforeIndex < beforeLines.Length && (afterIndex >= afterLines.Length || beforeLines[beforeIndex] != afterLines[Math.Min(afterIndex, afterLines.Length - 1)]))
                {
                    beforeSnippet.Add(new Line { LineNumber = beforeIndex + 1, Content = beforeLines[beforeIndex], Status = LineStatus.Deleted });
                    beforeIndex++;
                }

                while (afterIndex < afterLines.Length && (beforeIndex >= beforeLines.Length || afterLines[afterIndex] != beforeLines[Math.Min(beforeIndex, beforeLines.Length - 1)]))
                {
                    afterSnippet.Add(new Line { LineNumber = afterIndex + 1, Content = afterLines[afterIndex], Status = LineStatus.New });
                    afterIndex++;
                }

                int endBefore = Math.Min(beforeLines.Length, beforeIndex + contextLines);
                int endAfter = Math.Min(afterLines.Length, afterIndex + contextLines);

                for (int i = beforeIndex; i < endBefore; i++)
                    beforeSnippet.Add(new Line { LineNumber = i + 1, Content = beforeLines[i], Status = LineStatus.Unchanged });
                for (int i = afterIndex; i < endAfter; i++)
                    afterSnippet.Add(new Line { LineNumber = i + 1, Content = afterLines[i], Status = LineStatus.Unchanged });

                if (beforeSnippet.Count > 0 || afterSnippet.Count > 0)
                {
                    modifications.Add(new Modified<Line[]> { Before = beforeSnippet.ToArray(), After = afterSnippet.ToArray() });
                }
            }

            return modifications;
        }
    }
    public class LineArrayJsonConverter : JsonConverter<Line[]>
    {
        public override Line[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string content = reader.GetString();
            return content.Split('\n').Select(line => new Line { Content = line, Status = LineStatus.Unchanged }).ToArray();
        }

        public override void Write(Utf8JsonWriter writer, Line[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(string.Join("\n", value.Select(line => line.Content)));
        }
    }

}
