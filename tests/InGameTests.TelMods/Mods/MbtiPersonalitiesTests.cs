using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// MBTI Personalities on the real profile popup, the fixture's idols and a freshly generated
    /// idol, with every other mod's patches on the same code. The unit tests don't cover the
    /// profile text or the real GenerateGirl.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class MbtiPersonalitiesTests
    {
        private const string HarmonyId = "com.tel.mbtipersonalities";
        private const string Utility = "MBTIPersonalities.MBTIPersonalities";
        private static readonly int[] ProfileIdols = { 1, 167, 222 };

        /// <summary>The Extras tab of an idol's profile names her MBTI type.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator ProfileExtrasShowType(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            foreach (int id in ProfileIdols)
            {
                data_girls.girls girl = Game.Girl(id);
                string type = AccessTools.Method(utility, "GetGirlMBTI").Invoke(null, new object[] { girl }).ToString();
                ctx.Record("idol" + id, type);
                if (type == "None")
                {
                    ctx.Fail("Idol " + id + " has no MBTI type");
                    continue;
                }
                yield return Game.OpenProfile(girl, Profile_Popup._tabs.extras);
                string text = ExtrasText();
                string title = Language.Data["MBTI__TITLE_" + type] + ": ";
                ctx.Assert(text != null && text.Contains(title), "Idol " + id + "'s Extras tab doesn't show \"" + title + "\": " + text);
            }
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>
        /// No idol in the save carries two MBTI types, and the one she carries is the one the
        /// mod uses. A second type would be saved and win on the next load.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator EveryIdolHasAtMostOneType(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            IDictionary assigned = Traverse.Create(utility).Field("MBTIReferenceDict").GetValue<IDictionary>();
            int typed = 0;
            foreach (data_girls.girls girl in data_girls.girl)
            {
                List<string> types = Types(utility, girl);
                if (types.Count == 0)
                    continue;
                typed++;
                ctx.Assert(types.Count == 1, "Idol " + girl.id + " carries " + types.Count + " MBTI types: " + string.Join(", ", types.ToArray()));
                if (assigned.Contains(girl.id))
                {
                    string inUse = assigned[girl.id].ToString();
                    ctx.Assert(types.Contains(inUse, StringComparer.OrdinalIgnoreCase),
                        "Idol " + girl.id + " is treated as " + inUse + " but carries " + string.Join(", ", types.ToArray()));
                }
            }
            ctx.Record("idolsWithType", typed);
            ctx.Record("idols", data_girls.girl.Count);
        }

        /// <summary>
        /// A newly generated idol whose idol pack sets an MBTI type ends up with exactly that one
        /// type, with every other mod's patches on GenerateGirl. If no installed pack sets one,
        /// a game body is given a type for the duration. Uses up one idol ID; the session isn't saved.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator GeneratedIdolHasThePacksType(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType(Utility, true);
            IList packs = Traverse.Create(utility).Field("MBTITextureReferenceList").GetValue<IList>();
            data_girls_textures._textureAsset body = null;
            string packType = null;
            foreach (object data in packs)
            {
                packType = Traverse.Create(data).Field("mbti").GetValue<object>().ToString();
                body = data_girls_textures.GetTextureAssets(data_girls_textures._spriteType.body,
                    Traverse.Create(data).Field("body_id").GetValue<int>(), Traverse.Create(data).Field("ModName").GetValue<string>()).FirstOrDefault();
                if (body != null && packType != "None")
                    break;
                body = null;
            }

            object added = null;
            if (body == null)
            {
                body = data_girls_textures.GetTextureAssets(data_girls_textures._spriteType.body).First();
                packType = "ESTJ";
                added = Activator.CreateInstance(utility.GetNestedType("MBTITextureData"));
                Traverse.Create(added).Field("ModName").SetValue(body.ModName);
                Traverse.Create(added).Field("body_id").SetValue(body.body_id);
                Traverse.Create(added).Field("mbti").SetValue(Enum.Parse(utility.GetNestedType("MBTI"), packType));
                packs.Insert(0, added);
                ctx.Note("No installed idol pack sets an MBTI type; gave body " + body.body_id + " " + packType + " for the check");
            }
            ctx.Record("body", (body.ModName == "" ? "game" : body.ModName) + " #" + body.body_id + " = " + packType);

            data_girls.girls girl;
            using (TestTools.Restore(() => { if (added != null) packs.Remove(added); }))
                girl = Game.Main.Data.GetComponent<data_girls>().GenerateGirl(true, Auditions.data._girl._type.normal, body);

            List<string> types = Types(utility, girl);
            ctx.Record("types", string.Join(", ", types.ToArray()));
            ctx.Assert(types.Count == 1 && string.Equals(types[0], packType, StringComparison.OrdinalIgnoreCase),
                "The idol pack sets " + packType + ", but the generated idol carries " + (types.Count == 0 ? "none" : string.Join(", ", types.ToArray())));
        }

        /// <summary>The idol's saved variables that name an MBTI type (None excluded).</summary>
        private static List<string> Types(Type utility, data_girls.girls girl)
        {
            Type mbti = utility.GetNestedType("MBTI");
            var names = new HashSet<string>(Enum.GetNames(mbti).Where(n => n != "None"), StringComparer.OrdinalIgnoreCase);
            return girl.Variables.Where(names.Contains).ToList();
        }

        /// <summary>The first text of the open profile's Extras tab: the trait line that the Extras patches append to.</summary>
        private static string ExtrasText()
        {
            Transform text = Game.ProfilePopup.Extras_Container.transform.Find("Text(Clone)");
            return text == null ? null : text.GetComponent<TMPro.TextMeshProUGUI>().text;
        }
    }
}
