using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace SourceGit.Views
{
    public class PullRequestBadges : StackPanel
    {
        public static readonly StyledProperty<Dictionary<string, List<Commands.GitHubPullRequests.Badge>>> BadgesProperty =
            AvaloniaProperty.Register<PullRequestBadges, Dictionary<string, List<Commands.GitHubPullRequests.Badge>>>(nameof(Badges));

        public Dictionary<string, List<Commands.GitHubPullRequests.Badge>> Badges
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
                    FontSize = 11,
                    Padding = new Thickness(4, 0),
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTip.SetTip(button, $"{badge.Repository} #{badge.Number} — {badge.Branch}");
                button.Click += (_, _) => Native.OS.OpenBrowser(badge.Url);
                Children.Add(button);
            }
        }
    }
}
