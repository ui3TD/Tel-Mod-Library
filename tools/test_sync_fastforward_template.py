"""Unit tests for sync_fastforward_template.py on temporary folders: python -m unittest discover tools"""

import os
import shutil
import tempfile
import unittest

import sync_fastforward_template as sync

MOD_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
\t<PropertyGroup>
\t\t<Version>1.4.1</Version>
{workshop}\t\t<Tags>["gameplay"]</Tags>
\t</PropertyGroup>
</Project>
"""
TESTS_CSPROJ = '<Project><ItemGroup><Compile Include="..\\..\\{mod}\\**\\*.cs" /></ItemGroup></Project>\n'
PROPS = "<Project>\n\t<!--{comment}-->\n\t<PropertyGroup><LangVersion>{lang}</LangVersion></PropertyGroup>\n</Project>\n"


def write(root, rel, text):
    path = os.path.join(root, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


class SyncTests(unittest.TestCase):
    def setUp(self):
        self.repo = tempfile.mkdtemp()
        self.template = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.repo)
        self.addCleanup(shutil.rmtree, self.template)
        write(self.repo, "mods/FastForward/FastForward.cs", "class FastForward {}\n")
        write(self.repo, "mods/FastForward/assets/JSON/Mod Menu/modmenu.json", "[]\n")
        write(self.repo, "mods/FastForward/FastForward.csproj",
              MOD_CSPROJ.format(workshop="\t\t<WorkshopID>2976136928</WorkshopID>\n"))
        write(self.repo, "tests/FastForward.Tests/TimeControlTests.cs", "class TimeControlTests {}\n")
        write(self.repo, "tests/FastForward.Tests/FastForward.Tests.csproj", TESTS_CSPROJ.format(mod="mods\\FastForward"))
        for rel in sync.MIRRORED_FILES:
            write(self.repo, rel, rel + "\n")
        for rel in sync.SAME_BUILD:
            write(self.repo, rel, PROPS.format(comment="Every mod", lang="9"))

        write(self.template, "source/FastForward.cs", "class FastForward {}\n")
        write(self.template, "source/assets/JSON/Mod Menu/modmenu.json", "[]\n")
        write(self.template, "source/FastForward.csproj", MOD_CSPROJ.format(workshop=""))
        write(self.template, "tests/FastForward.Tests/TimeControlTests.cs", "class TimeControlTests {}\n")
        write(self.template, "tests/FastForward.Tests/FastForward.Tests.csproj", TESTS_CSPROJ.format(mod="source"))
        for rel in sync.MIRRORED_FILES:
            write(self.template, rel, rel + "\n")
        for rel in sync.SAME_BUILD:
            write(self.template, rel, PROPS.format(comment="The mod, explained for beginners", lang="9"))

    def differences(self):
        return sync.differences(self.repo, self.template)

    def test_matching_trees_pass(self):
        self.assertEqual(self.differences(), [])

    def test_workshop_id_in_the_template_differs(self):
        write(self.template, "source/FastForward.csproj", MOD_CSPROJ.format(workshop="\t\t<WorkshopID>1</WorkshopID>\n"))
        self.assertEqual(self.differences(), ["differs: source/FastForward.csproj"])

    def test_changed_source_differs(self):
        write(self.template, "source/FastForward.cs", "class FastForward { }\n")
        self.assertEqual(self.differences(), ["differs: source/FastForward.cs"])

    def test_crlf_counts_as_lf(self):
        with open(os.path.join(self.template, "source/FastForward.cs"), "wb") as f:
            f.write(b"class FastForward {}\r\n")
        self.assertEqual(self.differences(), [])

    def test_file_on_one_side_only_differs(self):
        write(self.repo, "tests/FastForward.Tests/NewTests.cs", "class NewTests {}\n")
        write(self.template, "source/assets/extra.txt", "old\n")
        self.assertEqual(sorted(self.differences()), [
            "missing from the template: tests/FastForward.Tests/NewTests.cs",
            "only in the template: source/assets/extra.txt",
        ])

    def test_bin_and_obj_are_left_out(self):
        write(self.repo, "mods/FastForward/bin/Release/x.dll", "dll")
        write(self.template, "source/obj/project.assets.json", "{}")
        self.assertEqual(self.differences(), [])

    def test_props_build_change_differs_but_comments_dont(self):
        write(self.template, "tests/Directory.Build.props", PROPS.format(comment="Other words", lang="10"))
        self.assertEqual(self.differences(), ["build differs (comments ignored; fix by hand): tests/Directory.Build.props"])

    def test_apply_makes_the_template_match(self):
        write(self.repo, "mods/FastForward/FastForward.cs", "class FastForward { int x; }\n")
        write(self.repo, "tests/FastForward.Tests/NewTests.cs", "class NewTests {}\n")
        write(self.template, "source/assets/extra.txt", "old\n")
        write(self.template, "source/FastForward.csproj", "stale\n")

        changed = sync.apply(self.repo, self.template)

        self.assertEqual(self.differences(), [])
        self.assertIn("deleted source/assets/extra.txt", changed)
        with open(os.path.join(self.template, "source/FastForward.csproj"), encoding="utf-8") as f:
            self.assertNotIn("WorkshopID", f.read())

    def test_apply_leaves_props_comments_alone(self):
        sync.apply(self.repo, self.template)
        with open(os.path.join(self.template, "Directory.Build.props"), encoding="utf-8") as f:
            self.assertIn("explained for beginners", f.read())


if __name__ == "__main__":
    unittest.main()
