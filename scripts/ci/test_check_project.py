import json
from pathlib import Path
import tempfile
import unittest

from check_project import inspect, REQUIRED


class ProjectIntegrityTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.files = {
            REQUIRED[0]: json.dumps({"dependencies": {"com.unity.test-framework": "1.6.0"}}),
            REQUIRED[1]: '{"dependencies": {}}',
            REQUIRED[2]: 'm_EditorVersion: 6000.3.2f1\n',
            REQUIRED[3]: 'EditorBuildSettings:\n  m_Scenes: []\n',
            'Game/.vscode/settings.json': '{"files.exclude": {"**/.git": true,},}',
            'Game/Assets/Player.cs': 'public class Player {}\n',
            'Game/Assets/Player.cs.meta': 'guid: ' + 'a' * 32 + '\n',
        }

    def check(self):
        for path, text in self.files.items():
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(text)
        return inspect(self.root, self.files)

    def test_valid_clean_checkout(self):
        self.assertEqual(self.check()['failures'], [])

    def test_missing_metadata_is_reported(self):
        del self.files['Game/Assets/Player.cs.meta']
        self.assertTrue(any('metadata is not tracked' in f['message'] for f in self.check()['failures']))

    def test_duplicate_guids_are_reported(self):
        self.files['Game/Assets/Other.cs'] = 'class Other {}'
        self.files['Game/Assets/Other.cs.meta'] = 'guid: ' + 'a' * 32
        self.assertTrue(any('Duplicate Unity GUID' in f['message'] for f in self.check()['failures']))

    def test_conflicts_invalid_json_and_case_collisions_are_reported(self):
        self.files['Game/Assets/player.cs'] = '<<<<<<< HEAD\nclass Broken {}\n=======\n>>>>>>> branch\n'
        self.files['Game/Packages/manifest.json'] = '{"dependencies": '
        failures = self.check()['failures']
        for expected in ('merge conflict', 'Invalid JSON', 'differ only by case'):
            self.assertTrue(any(expected in f['message'] for f in failures), expected)

    def test_enabled_build_scene_must_be_tracked_and_match_its_guid(self):
        scene = 'Game/Assets/Scene.unity'
        self.files[REQUIRED[3]] = '  - enabled: 1\n    path: Assets/Scene.unity\n    guid: ' + 'b' * 32 + '\n'
        self.assertTrue(any('Enabled build scene' in f['message'] for f in self.check()['failures']))
        self.files[scene] = '%YAML 1.1\n'
        self.files[scene + '.meta'] = 'guid: ' + 'a' * 32
        self.assertTrue(any('does not match' in f['message'] for f in self.check()['failures']))
        self.files[scene + '.meta'] = 'guid: ' + 'b' * 32
        self.assertEqual(self.check()['failures'], [])


if __name__ == '__main__':
    unittest.main()
