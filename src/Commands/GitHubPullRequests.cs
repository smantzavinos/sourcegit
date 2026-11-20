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
    public static partial class GitHubPullRequests
    {
        public record Badge(string Repository, int Number, string Branch)
        {
            public string Url => $"https://github.com/{Repository}/pull/{Number}";
        }

        public static bool Enabled { get; internal set; }

        [GeneratedRegex(@"\A(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)(?<slug>[a-z0-9-]+/[a-z0-9_.-]+?)(?:\.git)?/?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex GitHubRemote();

        public static async Task<Dictionary<string, List<Badge>>> QueryAsync(
            List<Models.Remote> remotes, List<Models.Branch> branches, CancellationToken token)
        {
            var repositories = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var remote in remotes)
            {
                var match = GitHubRemote().Match(remote.URL ?? string.Empty);
                if (match.Success)
                    repositories[remote.Name] = match.Groups["slug"].Value;
            }

            var heads = new Dictionary<(string Repo, string Branch), string>();
            foreach (var branch in branches)
            {
                if (!branch.IsLocal && repositories.TryGetValue(branch.Remote, out var repo))
                    heads[(repo.ToLowerInvariant(), branch.Name)] = branch.Head;
            }

            var result = new Dictionary<string, List<Badge>>(StringComparer.Ordinal);
            if (heads.Count == 0)
                return result;

            foreach (var repo in new HashSet<string>(repositories.Values, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(15));
                    using var payload = await ReadOpenAsync(repo, deadline.Token).ConfigureAwait(false);
                    if (payload == null)
                        continue;

                    foreach (var page in payload.RootElement.EnumerateArray())
                    foreach (var pr in page.EnumerateArray())
                    {
                        var head = pr.GetProperty("head");
                        var source = head.GetProperty("repo");
                        if (source.ValueKind == JsonValueKind.Null)
                            continue;

                        var slug = source.GetProperty("full_name").GetString();
                        var name = head.GetProperty("ref").GetString();
                        if (slug == null || name == null ||
                            !heads.TryGetValue((slug.ToLowerInvariant(), name), out var commit) ||
                            !commit.Equals(head.GetProperty("sha").GetString(), StringComparison.Ordinal))
                            continue;

                        var number = pr.GetProperty("number").GetInt32();
                        if (number <= 0)
                            continue;

                        if (!result.TryGetValue(commit, out var badges))
                            result.Add(commit, badges = []);
                        badges.Add(new Badge(repo, number, name));
                    }
                }
                catch (Win32Exception) { break; } // gh unavailable
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (IOException) { }
                catch (JsonException) { }
                catch (KeyNotFoundException) { }
                catch (InvalidOperationException) { }
            }

            return result;
        }

        private static async Task<JsonDocument> ReadOpenAsync(string repo, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo("gh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in new[] { "api", "--hostname", "github.com", "--paginate", "--slurp", $"repos/{repo}/pulls?state=open&per_page=100" })
                start.ArgumentList.Add(arg);
            start.Environment["GH_PROMPT_DISABLED"] = "1";
            start.Environment["GH_NO_UPDATE_NOTIFIER"] = "1";

            using var process = Process.Start(start);
            if (process == null)
                return null;

            try
            {
                var output = process.StandardOutput.ReadToEndAsync(token);
                var error = process.StandardError.ReadToEndAsync(token);
                await Task.WhenAll(process.WaitForExitAsync(token), output, error).ConfigureAwait(false);
                return process.ExitCode == 0 ? JsonDocument.Parse(await output.ConfigureAwait(false)) : null;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); }
                catch (InvalidOperationException) { } // already exited
                catch (Win32Exception) { }
                throw;
            }
        }
    }
}
