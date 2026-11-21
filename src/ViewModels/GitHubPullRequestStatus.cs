using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class GitHubPullRequestStatus : ObservableObject
    {
        public record Badge(string Repository, int Number, string Title, bool Draft, string HeadBranch, string TargetBranch, bool Stale)
        {
            public string Url => $"https://github.com/{Repository}/pull/{Number}";
        }

        public bool Enabled => _settings.ShowGitHubPullRequestBadges;

        public bool CanRefresh => Enabled && _refresh == null && _repositories.Count > 0;

        public bool RefreshFailed
        {
            get => _refreshFailed;
            private set => SetProperty(ref _refreshFailed, value);
        }

        public Dictionary<string, List<Badge>> Badges
        {
            get => _badges;
            private set => SetProperty(ref _badges, value);
        }

        internal static GitHubPullRequestStatus For(Models.RepositorySettings settings) =>
            s_shared.GetValue(settings, static key => new GitHubPullRequestStatus(key));

        private GitHubPullRequestStatus(Models.RepositorySettings settings)
        {
            _settings = settings;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _timer.Tick += (_, _) => _ = RefreshAsync();
        }

        internal void Attach()
        {
            if (++_users == 1)
                _settings.GitHubPullRequestBadgesChanged += OnEnabledChanged;
        }

        internal void Detach()
        {
            if (--_users == 0)
            {
                _settings.GitHubPullRequestBadgesChanged -= OnEnabledChanged;
                Stop();
                _repositories.Clear();
                _branches = [];
            }
        }

        internal void UpdateRefs(List<Models.Remote> remotes, List<Models.Branch> branches)
        {
            var repositories = Commands.GitHubPullRequests.GetRepositories(remotes);
            var changed = !_repositories.Values.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(repositories.Values);
            _repositories = repositories;
            _branches = branches;
            if (!Enabled)
                return;

            if (changed)
            {
                CancelRefresh();
                foreach (var repository in _cache.Keys.Except(repositories.Values, StringComparer.OrdinalIgnoreCase).ToArray())
                    _cache.Remove(repository);
                UpdateTimer();
            }
            Publish();
            if (changed)
                _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (!CanRefresh)
                return;

            var repositories = _repositories.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using var refresh = new CancellationTokenSource();
            _refresh = refresh;
            OnPropertyChanged(nameof(CanRefresh));
            try
            {
                var results = await Task.Run(async () =>
                {
                    var results = new Dictionary<string, List<Commands.GitHubPullRequests.PullRequest>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var repository in repositories)
                        results[repository] = await Commands.GitHubPullRequests.ReadOpenAsync(repository, refresh.Token).ConfigureAwait(false);
                    return results;
                });

                if (!refresh.IsCancellationRequested)
                {
                    foreach (var (repository, prs) in results)
                    {
                        if (prs != null)
                            _cache[repository] = new(prs, false);
                        else
                            _cache[repository] = new(_cache.TryGetValue(repository, out var last) ? last.PullRequests : [], true);
                    }
                    Publish();
                }
            }
            catch (OperationCanceledException) when (refresh.IsCancellationRequested) { }
            finally
            {
                if (_refresh == refresh)
                {
                    _refresh = null;
                    OnPropertyChanged(nameof(CanRefresh));
                }
            }
        }

        private void OnEnabledChanged()
        {
            OnPropertyChanged(nameof(Enabled));
            if (Enabled)
            {
                UpdateTimer();
                _ = RefreshAsync();
            }
            else
            {
                Stop();
            }
            OnPropertyChanged(nameof(CanRefresh));
        }

        private void UpdateTimer()
        {
            if (_repositories.Count > 0)
                _timer.Start();
            else
                _timer.Stop();
            OnPropertyChanged(nameof(CanRefresh));
        }

        private void Stop()
        {
            _timer.Stop();
            CancelRefresh();
            _cache.Clear();
            RefreshFailed = false;
            Badges = new(StringComparer.Ordinal);
        }

        private void CancelRefresh()
        {
            _refresh?.Cancel();
            _refresh = null;
        }

        private void Publish()
        {
            var heads = new HashSet<(string Repository, string Branch, string Commit)>();
            foreach (var branch in _branches)
            {
                if (!branch.IsLocal && branch.Remote != null && _repositories.TryGetValue(branch.Remote, out var repo))
                    heads.Add((repo.ToLowerInvariant(), branch.Name, branch.Head));
            }

            var badges = new Dictionary<string, List<Badge>>(StringComparer.Ordinal);
            foreach (var (repository, entry) in _cache)
            foreach (var pr in entry.PullRequests)
            {
                if (!heads.Contains((pr.HeadRepository.ToLowerInvariant(), pr.HeadBranch, pr.HeadCommit)))
                    continue;

                if (!badges.TryGetValue(pr.HeadCommit, out var matches))
                    badges.Add(pr.HeadCommit, matches = []);
                matches.Add(new(repository, pr.Number, pr.Title, pr.Draft, pr.HeadBranch, pr.TargetBranch, entry.Stale));
            }

            RefreshFailed = _cache.Values.Any(static entry => entry.Stale);
            Badges = badges;
        }

        private record CacheEntry(List<Commands.GitHubPullRequests.PullRequest> PullRequests, bool Stale);

        private static readonly ConditionalWeakTable<Models.RepositorySettings, GitHubPullRequestStatus> s_shared = new();
        private readonly Models.RepositorySettings _settings;
        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _repositories = new(StringComparer.Ordinal);
        private List<Models.Branch> _branches = [];
        private Dictionary<string, List<Badge>> _badges = new(StringComparer.Ordinal);
        private CancellationTokenSource _refresh;
        private bool _refreshFailed;
        private int _users;
    }
}
