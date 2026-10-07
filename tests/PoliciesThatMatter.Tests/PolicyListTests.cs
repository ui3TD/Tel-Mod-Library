using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;

namespace PoliciesThatMatter.Tests
{
    /// <summary>
    /// The mod replaces the game's policies.json, so its copy must define every policy the game uses.
    /// </summary>
    public class PolicyFileTests
    {
        private static readonly List<policies.value> Values = TestPolicies.Parse(TestPolicies.PolicyFile);

        [Fact]
        public void EveryPolicyHasOneHeaderAndOneDefault()
        {
            foreach (policies._type type in Enum.GetValues(typeof(policies._type)))
            {
                List<policies.value> entries = Values.Where(v => v.Type == type).ToList();
                policies.value header = Assert.Single(entries, v => v.Value == policies._value.NONE);
                Assert.False(string.IsNullOrEmpty(header.Title), $"{type} has no title");
                Assert.Single(entries, v => v.Selected);
            }
        }

        [Fact]
        public void EveryOptionInTheGameIsDefinedOnce()
        {
            foreach (policies._value option in Enum.GetValues(typeof(policies._value)))
            {
                if (option == policies._value.NONE)
                    continue;

                policies.value value = Assert.Single(Values, v => v.Value == option);
                Assert.StartsWith(value.Type + "_", option.ToString());
                Assert.False(string.IsNullOrEmpty(value.Title), $"{option} has no title");
            }
        }

        /// <summary>
        /// The game's own file must stay hidden in every language, with either slash style.
        /// </summary>
        [Theory]
        [InlineData("en")]
        [InlineData("jp")]
        [InlineData("cn")]
        [InlineData("ru")]
        [InlineData("ptbr")]
        public void IgnoreFile_HidesTheGamesPolicies(string language)
        {
            JSONNode ignore = JSON.Parse(File.ReadAllText(TestPolicies.ModAsset("ignore.json")));
            List<string> patterns = ignore["Ignore_Files"].AsArray.Cast<JSONNode>().Select(n => (string)n).ToList();

            // Mods.GetDefaultPath: Path.Combine(streamingAssetsPath, "Languages", language, "JSON/Policies/policies.json")
            string windows = Path.Combine("C:/Game/IM_Data/StreamingAssets", "Languages", language, @"JSON\Policies\policies.json");
            string unix = $"/game/IM_Data/StreamingAssets/Languages/{language}/JSON/Policies/policies.json";

            Assert.Contains(patterns, p => windows.Contains(p));
            Assert.Contains(patterns, p => unix.Contains(p));
        }

        [Fact]
        public void DatingNotificationLabel_IsDefined()
        {
            JSONNode constants = JSON.Parse(File.ReadAllText(TestPolicies.ModAsset(Path.Combine("JSON", "Constants", "constants.json"))));
            Assert.Contains(constants.AsArray.Cast<JSONNode>(), c => (string)c["id"] == PoliciesThatMatter.DATING_NOTIF_LABEL && !string.IsNullOrEmpty(c["text"]));
        }
    }

    /// <summary>
    /// Runs the game's policies.Load against chosen files.
    /// </summary>
    public sealed class PolicyLoader : IDisposable
    {
        public static List<string> Files = new();

        private readonly Harmony harmony = new("tests.PoliciesThatMatter.PolicyLoader");

        public PolicyLoader()
        {
            harmony.Patch(
                AccessTools.Method(typeof(Mods), nameof(Mods.GetFilePaths)),
                prefix: new HarmonyMethod(typeof(PolicyLoader), nameof(Prefix)));
        }

        private static bool Prefix(ref List<string> __result)
        {
            __result = new List<string>(Files);
            return false;
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
        }
    }

    public class DuplicatePolicyTests : IClassFixture<PolicyLoader>, IDisposable
    {
        private readonly List<string> tempFiles = new();

        private static List<policies.value> Load(bool withFix, params string[] files)
        {
            PolicyLoader.Files = files.ToList();
            Harmony harmony = new("tests.PoliciesThatMatter.RemoveDuplicates");
            try
            {
                if (withFix)
                    harmony.CreateClassProcessor(typeof(policies_Load_RemoveDuplicateValues)).Patch();

                // policies is a MonoBehaviour; Load only touches static state.
                policies loader = (policies)FormatterServices.GetUninitializedObject(typeof(policies));
                loader.Load();
                return policies.Values.ToList();
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        private string WriteFile(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (string path in tempFiles)
                File.Delete(path);
        }

        private static List<(policies._type, policies._value)> Keys(IEnumerable<policies.value> values) =>
            values.Select(v => (v.Type, v.Value)).ToList();

        /// <summary>
        /// The problem the fix addresses: the game appends every copy it finds.
        /// </summary>
        [Fact]
        public void WithoutFix_TwoCopiesListEveryOptionTwice()
        {
            string file = TestPolicies.PolicyFile;
            int once = Load(false, file).Count;
            Assert.Equal(2 * once, Load(false, file, file).Count);
        }

        [Fact]
        public void TwoCopies_ListEveryOptionOnce_InFileOrder()
        {
            string file = TestPolicies.PolicyFile;
            Assert.Equal(Keys(Load(false, file)), Keys(Load(true, file, file)));
        }

        [Fact]
        public void OneCopy_IsUnchanged()
        {
            string file = TestPolicies.PolicyFile;
            Assert.Equal(Keys(Load(false, file)), Keys(Load(true, file)));
        }

        [Fact]
        public void LaterFile_WinsADuplicate()
        {
            string first = WriteFile("[{Type: \"image\", Title: \"First\"}, {Type: \"image\", Value: \"image_neutral\", Title: \"First neutral\", Selected: true}]");
            string second = WriteFile("[{Type: \"image\", Title: \"Second\"}, {Type: \"image\", Value: \"image_neutral\", Title: \"Second neutral\"}]");

            List<policies.value> values = Load(true, first, second);

            Assert.Equal(new[] { "Second", "Second neutral" }, values.Select(v => v.Title));
            // The default comes from the surviving definition.
            Assert.DoesNotContain(values, v => v.Selected);
        }

        [Fact]
        public void DifferentOptions_AreAllKept()
        {
            string first = WriteFile("[{Type: \"image\"}, {Type: \"image\", Value: \"image_neutral\"}]");
            string second = WriteFile("[{Type: \"dating\"}, {Type: \"image\", Value: \"image_orthodox\"}]");

            List<policies.value> values = Load(true, first, second);

            Assert.Equal(
                new[] { (policies._type.image, policies._value.NONE), (policies._type.image, policies._value.image_neutral), (policies._type.dating, policies._value.NONE), (policies._type.image, policies._value.image_orthodox) },
                Keys(values));
        }

        [Fact]
        public void NullEntries_AreSkipped()
        {
            policies.Values = new List<policies.value> { null, new() { Type = policies._type.image }, null, new() { Type = policies._type.image } };
            policies_Load_RemoveDuplicateValues.Postfix();
            Assert.Equal(3, policies.Values.Count);
            Assert.Single(policies.Values, v => v != null);
        }
    }
}
