using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace SourceGit.Views
{
    public class PullRequestBadges : StackPanel
    {
        public static readonly StyledProperty<Dictionary<string, List<ViewModels.GitHubPullRequestStatus.Badge>>> BadgesProperty =
            AvaloniaProperty.Register<PullRequestBadges, Dictionary<string, List<ViewModels.GitHubPullRequestStatus.Badge>>>(nameof(Badges));

        public Dictionary<string, List<ViewModels.GitHubPullRequestStatus.Badge>> Badges
        {
            get => GetValue(BadgesProperty);
            set => SetValue(BadgesProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BadgesProperty)
                Rebuild();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Rebuild();
        }

        private void Rebuild()
        {
            Children.Clear();
            if (DataContext is not Models.Commit commit || Badges == null || !Badges.TryGetValue(commit.SHA, out var badges))
                return;

            foreach (var badge in badges)
            {
                var button = new Button
                {
                    Content = $"#{badge.Number}",
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                button.Classes.Add("graph_pr_badge");
                var title = badge.Draft ? $"[{App.Text("Histories.PullRequest.Draft")}] {badge.Title}" : badge.Title;
                var tooltip = $"{badge.Repository} #{badge.Number} — {title}\n{badge.HeadBranch} → {badge.TargetBranch}";
                if (badge.Stale)
                {
                    button.Opacity = 0.65;
                    tooltip += $"\n{App.Text("Histories.PullRequest.Stale")}";
                }
                ToolTip.SetTip(button, new TextBlock { Text = tooltip, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440 });
                button.Click += (_, _) => Native.OS.OpenBrowser(badge.Url);
                Children.Add(button);
            }
        }
    }
}
