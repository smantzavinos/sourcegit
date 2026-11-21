using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    internal static partial class GitHubPullRequests
    {
        internal record PullRequest(int Number, string Title, bool Draft, string TargetBranch,
            string HeadRepository, string HeadBranch, string HeadCommit);

        [GeneratedRegex(@"\A(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)(?<slug>[a-z0-9-]+/[a-z0-9_.-]+?)(?:\.git)?/?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex GitHubRemote();

        internal static Dictionary<string, string> GetRepositories(IEnumerable<Models.Remote> remotes)
        {
            var repositories = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var remote in remotes)
            {
                var match = GitHubRemote().Match(remote.URL ?? string.Empty);
                if (match.Success)
                    repositories[remote.Name] = match.Groups["slug"].Value;
            }
            return repositories;
        }

        // Null means refresh failed; an empty list is a successful result.
        internal static async Task<List<PullRequest>> ReadOpenAsync(string repository, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var ct = deadline.Token;
            try
            {
                ct.ThrowIfCancellationRequested();
                var start = new ProcessStartInfo("gh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var arg in new[] { "api", "--hostname", "github.com", "--paginate", "--slurp", $"repos/{repository}/pulls?state=open&per_page=100" })
                    start.ArgumentList.Add(arg);
                start.Environment["GH_PROMPT_DISABLED"] = "1";
                start.Environment["GH_NO_UPDATE_NOTIFIER"] = "1";

                using var process = Process.Start(start);
                if (process == null)
                    return null;

                try
                {
                    var output = process.StandardOutput.ReadToEndAsync(ct);
                    var error = process.StandardError.ReadToEndAsync(ct);
                    await Task.WhenAll(process.WaitForExitAsync(ct), output, error).ConfigureAwait(false);
                    if (process.ExitCode != 0)
                        return null;

                    using var payload = JsonDocument.Parse(await output.ConfigureAwait(false));
                    var result = new List<PullRequest>();
                    foreach (var page in payload.RootElement.EnumerateArray())
                    foreach (var pr in page.EnumerateArray())
                    {
                        var head = pr.GetProperty("head");
                        var source = head.GetProperty("repo");
                        if (source.ValueKind == JsonValueKind.Null)
                            continue;

                        var number = pr.GetProperty("number").GetInt32();
                        var slug = source.GetProperty("full_name").GetString();
                        var branch = head.GetProperty("ref").GetString();
                        var commit = head.GetProperty("sha").GetString();
                        if (number > 0 && slug != null && branch != null && commit != null)
                            result.Add(new PullRequest(number, pr.GetProperty("title").GetString(), pr.GetProperty("draft").GetBoolean(),
                                pr.GetProperty("base").GetProperty("ref").GetString(), slug, branch, commit));
                    }
                    return result;
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { }
                    throw;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
            catch (Win32Exception) { return null; }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
            catch (KeyNotFoundException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (FormatException) { return null; }
        }
    }
}
