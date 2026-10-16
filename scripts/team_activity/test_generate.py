from collections import Counter
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import generate


def commit(login=None, *, kind="User", parents=1, name="Unlinked author", email="unlinked@example.invalid"):
    return {
        "parents": [{}] * parents,
        "author": {"login": login, "type": kind} if login else None,
        "commit": {"author": {"name": name, "email": email}},
    }


class ActivityTests(unittest.TestCase):
    def test_authors_are_case_insensitive_and_bots_and_merges_are_excluded(self):
        commits = [commit("unitybtw"), commit("UnityBtw"), commit("Ichnolith"),
                   commit("unitybtw", parents=2), commit("github-actions[bot]", kind="Bot"),
                   commit(name="github-actions[bot]"), commit()]
        with patch.object(generate, "api", return_value=(commits, "")):
            counts, unlinked = generate.commit_counts("abc123")
        self.assertEqual(counts, Counter({"unitybtw": 2, "ichnolith": 1}))
        self.assertEqual(unlinked, 1)

    def test_all_commit_pages_are_counted(self):
        next_page = 'repos/unitybtw/the-lighten-road/commits?sha=abc123&page=2'
        responses = [([commit("kemal-berke")], f'<https://api.github.com/{next_page}>; rel="next"'),
                     ([commit("kemal-berke")], "")]
        with patch.object(generate, "api", side_effect=responses) as api:
            counts, _ = generate.commit_counts("abc123")
        self.assertEqual(counts["kemal-berke"], 2)
        self.assertEqual(api.call_args_list[1].args[0], next_page)

    def test_unchanged_statistics_do_not_churn_the_snapshot_for_bot_commits(self):
        def profile(member):
            login, name, role = member
            return {"login": login, "name": name, "role": role,
                    "profile_url": "https://github.com/" + login, "avatar_sha256": "same"}, b"avatar fixture"
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(generate, "OUTPUT", Path(directory)), \
                 patch.object(generate, "commit_counts", return_value=(Counter({"unitybtw": 2}), 0)), \
                 patch.object(generate, "member_profile", side_effect=profile):
                first = generate.snapshot("human-commit", "main")
                second = generate.snapshot("bot-update-commit", "main")
                self.assertEqual(first, second)
                self.assertEqual(second["source_revision"], "human-commit")
                self.assertEqual(len(second["members"]), 5)
                self.assertEqual(second["members"][2]["commits"], 0)
                stored = json.loads((Path(directory) / "activity.json").read_text())
                self.assertEqual(stored, first)


if __name__ == "__main__":
    unittest.main()
