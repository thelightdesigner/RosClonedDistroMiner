using System.Text;

namespace RosClonedDistroMiner
{
    class MainlineBackportCommitPair(string mainlineSHA, string backportedSHA, Repository repo)
    {
        public Commit Mainline { get; set; }  = new Commit(mainlineSHA, repo);
        public Commit Backported { get; set; } = new Commit(backportedSHA, repo);
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

    class Commit
    {
        public string RawMessage { get; set; }
        public string SHA { get; set; }
        public List<Modified<File>> ModifiedFiles { get; set; }
        public Commit(string SHA, Repository repo)
        {
            this.SHA = SHA;
            RawMessage = repo.ExecuteGitCommand($"log --format=%B -n 1 {SHA}");
            ModifiedFiles = new();

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
            foreach (string path in modifiedFiles) modified.Before.Add(File.From(path));
            
            //switch to this SHA, then get contents
            repo.ExecuteGitCommand($"checkout {SHA}");
            foreach (string path in modifiedFiles) modified.After.Add(File.From(path));

            //convert Modified<List<File>> to List<Modified<File>>
            foreach(string path in modifiedFiles)
            {
                ModifiedFiles.Add(new Modified<File>()
                {
                    Before = modified.Before.Find(file => file.Path.Equals(path)) ?? throw new Exception("darn 1"),
                    After = modified.After.Find(file => file.Path.Equals(path)) ?? throw new Exception("darn 2")
                });
            }

        }

        public override string ToString()
        {
            StringBuilder str = new();
            str.Append($"Message: {RawMessage.Split("\n").FirstOrDefault()} \nSHA: {SHA}\n");
            foreach (var modified in ModifiedFiles)
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
        public class File(string path, string contents, bool exists)
        {
            public string Path { get; set; } = path;
            public string Contents { get; set; } = contents;
            public bool Exists { get; set; } = exists;
            public static File From(string path)
            {
                if (System.IO.File.Exists(path)) return new File(path, System.IO.File.ReadAllText(path), true);
                else return new File(path, "", false);
            }
            public override string ToString()
            {
                return $"Name: {new FileInfo(Path).Name}\n\n{Contents}";
            }
        }
    }


}
