"""Unit tests for workshop_tags.py and the tag hint in publish_workshop.py, with no Steam and no game:
python -m unittest discover tools"""

import contextlib
import io
import unittest

import publish_workshop
import workshop_tags as wt


def mod(name, tags, workshop_id="1"):
    return wt.Mod(name, name + ".csproj", workshop_id, tags)


class CompareTests(unittest.TestCase):
    def test_same_tags_match(self):
        self.assertEqual(wt.compare(["gameplay"], ["gameplay"]), wt.MATCHES)

    def test_different_tags_differ(self):
        self.assertEqual(wt.compare(["modding"], ["gameplay"]), wt.DIFFERS)

    def test_no_tags_on_steam_differs(self):
        self.assertEqual(wt.compare(["gameplay"], []), wt.DIFFERS)

    def test_extra_tag_on_steam_differs(self):
        self.assertEqual(wt.compare(["gameplay"], ["gameplay", "modding"]), wt.DIFFERS)

    def test_order_and_case_are_ignored(self):
        self.assertEqual(wt.compare(["example", "patches"], ["Patches", "example"]), wt.MATCHES)

    def test_item_the_api_does_not_return_is_unreadable(self):
        self.assertEqual(wt.compare(["gameplay"], None), wt.UNREADABLE)


class SteamTagsTests(unittest.TestCase):
    def test_public_item(self):
        self.assertEqual(wt.steam_tags({"result": 1, "tags": [{"tag": "gameplay"}]}), ["gameplay"])

    def test_public_item_without_tags(self):
        self.assertEqual(wt.steam_tags({"result": 1}), [])

    def test_unlisted_item(self):
        self.assertIsNone(wt.steam_tags({"result": 9}))

    def test_missing_item(self):
        self.assertIsNone(wt.steam_tags(None))


class ChooseTests(unittest.TestCase):
    mods = [mod("A", ["gameplay"]), mod("B", ["gameplay"]), mod("C", ["gameplay"])]
    statuses = {"A": wt.MATCHES, "B": wt.DIFFERS, "C": wt.UNREADABLE}

    def choose(self, names, apply_all=False):
        with contextlib.redirect_stdout(io.StringIO()):
            return [m.name for m in wt.choose(self.mods, self.statuses, names, apply_all)]

    def test_apply_all_takes_only_differing_mods(self):
        self.assertEqual(self.choose([], apply_all=True), ["B"])

    def test_named_unreadable_mod_is_sent(self):
        self.assertEqual(self.choose(["C"]), ["C"])

    def test_named_matching_mod_is_skipped(self):
        self.assertEqual(self.choose(["A", "B"]), ["B"])

    def test_unknown_name_exits(self):
        with self.assertRaises(SystemExit):
            self.choose(["Nope"])


class ProbeTests(unittest.TestCase):
    def test_source_lists_each_item_and_its_tags(self):
        source = wt.probe_source([mod("A", ["example", "patches"], "3102983863"), mod("B", [], "42")])
        self.assertIn("Ids = { 3102983863UL, 42UL }", source)
        self.assertIn('Tags = { new[] { "example", "patches" }, new string[0] }', source)

    def test_source_sets_only_tags(self):
        source = wt.probe_source([mod("A", ["gameplay"])])
        for call in ("SetItemTitle", "SetItemDescription", "SetItemContent", "SetItemPreview", "SetItemVisibility"):
            self.assertNotIn(call, source)
        self.assertIn("SubmitItemUpdate(handle, null)", source)

    def test_project_adds_steamworks_reference(self):
        project = wt.probe_project("<Project>\n  <ItemGroup />\n</Project>\n")
        self.assertIn('<Reference Include="Steamworks.NET">', project)
        self.assertTrue(project.rstrip().endswith("</Project>"))

    def test_answers_are_read_per_item_id(self):
        reply = "note: something\n3821906838 = OK\n42 = AccessDenied\nother = 1\n"
        self.assertEqual(wt.parse_answers(reply), {"3821906838": "OK", "42": "AccessDenied"})


class PublishHintTests(unittest.TestCase):
    def test_hint_when_tags_differ(self):
        hint = publish_workshop.tag_hint("AI Fan Chatter", ["gameplay"], {"result": 1, "tags": []})
        self.assertIn('workshop_tags.py --apply "AI Fan Chatter"', hint)

    def test_no_hint_when_tags_match(self):
        self.assertIsNone(publish_workshop.tag_hint("A", ["gameplay"], {"result": 1, "tags": [{"tag": "gameplay"}]}))

    def test_no_hint_when_item_is_unreadable(self):
        self.assertIsNone(publish_workshop.tag_hint("A", ["gameplay"], {"result": 9}))


if __name__ == "__main__":
    unittest.main()
