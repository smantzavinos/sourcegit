# Prototype: GitHub PR badges in SourceGit

**Branch:** `prototype/github-pr-badges` · **Verdict:** awaiting user feedback · **Tracker:** not yet filed

## What was prototyped

- **C1 — Discover open PRs.** With `--github-pr-badges`, SourceGit runs `gh api` in the background when a repository opens and once per minute afterward. It queries the GitHub repositories configured as remotes and matches the PR head repository, case-sensitive branch name, and exact fetched commit. See `src/Commands/GitHubPullRequests.cs` and `src/ViewModels/Repository.cs`.
- **C2 — Show links in the graph.** The graph shows `#123` beside the matching branch labels on the head commit row, with the theme's link color, a small merge icon, and space before the commit subject. The tooltip names the source branch and the destination repository; clicking opens that PR in the browser. See `src/Views/Histories.axaml` and `src/Views/PullRequestBadges.cs`.

## Demo scenarios

1. An open PR from the configured fork gets a badge at its fetched tip (C1, C2).
2. A same-named branch from another fork and a stale tip don't get the badge (C1).
3. Clicking the badge opens the PR on the destination repository (C2).
4. When the PR disappears from the open list, the next refresh removes the badge without reopening the repository (C1, C2).

All four were demonstrated in an isolated X11 session using a fixture `gh` response and a recording browser launcher. The .NET 10 Release build completed without warnings or errors. The corresponding read-only `gh api` query shape was also checked separately against GitHub.

## Try it

```bash
git clone --branch prototype/github-pr-badges --recurse-submodules \
  https://github.com/smantzavinos/sourcegit.git
cd sourcegit
gh auth status --hostname github.com
dotnet run --project src/SourceGit.csproj -- --github-pr-badges /path/to/checkout
```

Use a checkout with the PR's head and destination repositories configured as GitHub remotes. Fetch the head branch so its remote-tracking ref matches the PR's current tip. The flag is optional and does not persist. On NixOS, the development build needs the native X11, OpenGL, ICU, and Fontconfig library paths that conventional Linux systems provide outside the application.

Evidence: [badge present](prototype-evidence/open-pr.png) · [badge removed after refresh](prototype-evidence/after-refresh.png).

## Prototype limits and open questions

- The UI demo used fake PR data and a recorded browser URL; it did not create, close, or modify a live PR.
- Only `github.com` remotes are queried. There is no discovery of a fork's parent unless it is configured as a remote. Stale refs deliberately get no badge until the branch is fetched.
- `gh` failure currently looks like an empty result. Polling is per open checkout. Decide how to show failures and share/cache refreshes before a production build.
- When several refs share a commit, badges appear after the group of branch labels rather than inside an individual label. Decide whether this is sufficient or badges should attach to individual labels.
- If the behavior is accepted, branch from upstream afresh and add a persistent opt-in, clear refresh/error behavior, and focused regression tests. Do not merge the prototype branch directly.
