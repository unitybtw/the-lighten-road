# Team activity dashboard

The main README displays a self-contained animated GIF with circular GitHub avatars, authored commit counts, and each member's share of team commits. A static PNG is provided for reduced motion and a readable table provides the same numbers in text.

## Data

`activity.json` records a specific default-branch revision, the UTC update time, and the counting method. The renderer walks the complete GitHub commit history at that immutable revision, using the API's linked author accounts. It excludes merge commits and bot accounts. Co-author trailers, pushes, lines changed, issues, reviews, offline work, and uncommitted artwork are not counted. Zero commits means no qualifying commits under that account; it does not mean no work was done.

All five team members are always shown. Equal counts preserve roster order. The animation changes only the highlights; the numbers and bar lengths are correct from the first frame.

## Updates

[Update team activity](../../.github/workflows/team-activity.yml) refreshes the dashboard after human pushes to `main`, on a daily schedule, or through **Actions → Update team activity → Run workflow**. It commits only the README's generated section and `docs/team-activity/`. If the data and images do not change, no update commit is created. Its bot commits are excluded from the statistics. A concurrent push may reject the bot's normal push; rerun the workflow after it finishes rather than force-pushing.

The workflow uses the repository's automatic `GITHUB_TOKEN`; no personal access token or external badge service is required. GitHub avatars are fetched publicly without forwarding that token. Only public profile details, counts, avatar PNGs, and aggregate metadata are published.

To reproduce locally from the repository root:

```sh
python -m pip install -r scripts/team_activity/requirements.txt
python scripts/team_activity/generate.py
```

To preview rendering changes without querying GitHub:

```sh
python scripts/team_activity/generate.py --from-snapshot
```

The renderer uses the existing OFL-licensed Liberation Sans font at `Game/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`; the original license remains alongside that font. It does not alter Unity scenes, assets, or teammate workspaces.
