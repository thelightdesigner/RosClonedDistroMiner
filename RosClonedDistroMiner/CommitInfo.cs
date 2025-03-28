using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RosClonedDistroMiner
{
    class CommitInfo
    {
        public Repository Repo;
        public string Message;
        public string Hash;
        public CommitInfo(string formatted, Repository repo)
        {
            var parts = formatted.Split(' ', 2);
            Hash = parts[0].Trim();
            Message = parts.Length > 1 ? parts[1].Trim() : "";
            Repo = repo;
        }

        public bool IsProbablyNotCode()
        {
            return Message.Contains("Changelog") || Regex.IsMatch(Message, @"\d+\.\d+\.\d+");
        }

        /// <summary>
        /// Gets a dictionary representation of the changes that this commit made to the whole project.
        /// This method SUCKS! Don't use it!
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> Changes()
        {
            //Begin AI code
            var changesDict = new Dictionary<string, string>();
            //string output = Repo.ExecuteGitCommand($"show --pretty=format: --name-only {Hash}");
            string diffOutput = Repo.ExecuteGitCommand($"show {Hash}");

            var fileChanges = diffOutput.Split("diff --git");
            foreach (var fileChange in fileChanges.Skip(1))
            {
                var lines = fileChange.Split('\n');
                if (lines.Length > 1)
                {
                    string filePath = lines[0].Split(' ')[2].TrimStart('b', '/');
                    changesDict[filePath] = string.Join("\n", lines.Skip(1));
                }
            }
            //End AI code
            return changesDict;
        }

        public string CleanedMessage()
        {
            return Regex.Replace(Message.Trim(), @"\([^()]*#\d+[^()]*\)", "");
        }

        public override string ToString()
        {
            return $"[{Hash.Substring(0, 5)}..] {Message}";
        }
    }
}
