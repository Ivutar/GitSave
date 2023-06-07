using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitSave.Models;

namespace GitSave.Tools;

public static class Git
{
    #region [ get commits ]

    const char GitLogCommitSeparator = '¶';
    const string GitLogLineSeparator = "\r\n";
    const string GitHead = "HEAD -> master";
    const string GitLog = "git log --date-order --pretty=format:\"%h¶%an¶%ad¶%s¶%D\" -n {0}";
    const string GitLogAllCommits = "git log --reflog --date-order --pretty=format:\"%h¶%an¶%ad¶%s¶%D\" -n {0}";

    public static async Task<IEnumerable<Commit>> GetCommits(int limit, bool showAllCommits, string root)
    {
        try
        {
            limit = limit <= 0 ? 1 : limit;
            limit = limit > 100 ? 100 : limit;

            var cmd = string.Format(showAllCommits ? GitLogAllCommits : GitLog, limit);
            var commits = await Cmd.Run(cmd, root);

            if (commits.StartsWith("Error"))
                return Enumerable.Empty<Commit>();

            return commits
                .Split(GitLogLineSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => {
                    var parts = line.Split(GitLogCommitSeparator);
                    return new Commit() {
                        UUID = parts[0],
                        Login = parts[1],
                        Created = parts[2],
                        Comment = parts[3],
                        Head = parts[4].Contains(GitHead)
                    };
                });
        }
        catch
        {
            return Enumerable.Empty<Commit>();
        }        
    }

    #endregion

    #region [ return to last commit ]

    const string GitReset = "git reset --hard";

    public static async Task Reset(string root)
    {
        await Cmd.Run(GitReset, root);
    }

    #endregion

    #region [ new commit ]

    const string GitAddFiles = "git add .";
    const string GitCommit = "git commit -F {0}";

    public static async Task New(string comment, string root)
    {
        await Cmd.Run(GitAddFiles, root);

        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, comment.Trim());

        var cmd = string.Format(GitCommit, file);
        await Cmd.Run(cmd, root);

        File.Delete(file);
    }

    #endregion

    #region [ update the last commit ]    

    const string GitAddFilesAmend = "git add .";
    const string GitCommitAmend = "git commit --amend -F {0}";

    public static async Task Update(string comment, string root)
    {
        await Cmd.Run(GitAddFilesAmend, root);

        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, comment.Trim());

        var cmd = string.Format(GitCommitAmend, file);
        await Cmd.Run(cmd, root);

        File.Delete(file);
    }

    #endregion

    #region [ return last full comment ]

    const string GitLastFullComment = "git log --pretty=\"%B\" -1";

    public static async Task<string> LastComment(string root)
    {
        return (await Cmd.Run(GitLastFullComment, root)).Trim();
    }

    #endregion

    #region [ return to last commit ]

    const string GitResetToCommit = "git reset --hard {0}";

    public static async Task ResetToCommit(string commitUUID, string root)
    {
        string cmd = string.Format(GitResetToCommit, commitUUID);
        await Cmd.Run(cmd, root);
    }

    #endregion

    #region [ status ]

    //const string GitLastFullComment = "git status";

    public static async Task<bool> HasUpates(string root)
    {
        //...
        return false;
    }

    #endregion
}