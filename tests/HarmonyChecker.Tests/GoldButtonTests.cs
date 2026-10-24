using System;
using System.IO;
using UnityEngine;
using Xunit;

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// The update button's gold picture and its pulsing halo.
    /// </summary>
    public class GoldButtonTests : IDisposable
    {
        private readonly string temp = Path.Combine(Path.GetTempPath(), "HarmonyCheckerTests_" + Guid.NewGuid().ToString("N"));

        public GoldButtonTests()
        {
            TestGame.Reset();
            Directory.CreateDirectory(temp);
        }

        public void Dispose() => Directory.Delete(temp, true);

        /// <summary>
        /// The gold picture is the size of the game's menu button picture, so its 9-slice edges are the
        /// game's, and the halo is that size plus its padding on every side, with the edges moved out by it.
        /// </summary>
        [Fact]
        public void Pictures_MatchGameButton()
        {
            (int width, int height) button = PngSize(GoldButton.Button.RelativePath);
            (int width, int height) glow = PngSize(GoldButton.Glow.RelativePath);
            int pad = (int)GoldButton.GLOW_PAD;

            Assert.Equal((343, 78), button);
            Assert.Equal((button.width + 2 * pad, button.height + 2 * pad), glow);
            Assert.Equal(GoldButton.Button.Border + new Vector4(pad, pad, pad, pad), GoldButton.Glow.Border);
            Assert.True(GoldButton.Button.Border.x + GoldButton.Button.Border.z < button.width);
            Assert.True(GoldButton.Button.Border.y + GoldButton.Button.Border.w < button.height);
        }

        [Fact]
        public void FindFile_PrefersFolderNextToDll()
        {
            string dll = MakeDir("dll", withPicture: true);
            string mod = MakeDir("mod", withPicture: true);

            Assert.Equal(Path.Combine(dll, GoldButton.Button.RelativePath), ModPicture.FindFile(GoldButton.Button.RelativePath, dll, new[] { mod }));
        }

        [Fact]
        public void FindFile_FallsBackToModFolder()
        {
            string dll = MakeDir("dll", withPicture: false);
            string mod = MakeDir("mod", withPicture: true);

            Assert.Equal(Path.Combine(mod, GoldButton.Button.RelativePath), ModPicture.FindFile(GoldButton.Button.RelativePath, dll, new[] { null, "", mod }));
        }

        [Fact]
        public void FindFile_NullWhenMissing()
        {
            string dll = MakeDir("dll", withPicture: false);

            Assert.Null(ModPicture.FindFile(GoldButton.Button.RelativePath, dll, new string[0]));
        }

        /// <summary>
        /// The halo starts dim, is brightest halfway through the period and dim again at its end.
        /// </summary>
        [Fact]
        public void Pulse_GoesDimBrightDim()
        {
            Assert.Equal(GlowPulse.MIN_ALPHA, GlowPulse.AlphaAt(0f), 3);
            Assert.Equal(GlowPulse.MAX_ALPHA, GlowPulse.AlphaAt(GlowPulse.PERIOD / 2f), 3);
            Assert.Equal(GlowPulse.MIN_ALPHA, GlowPulse.AlphaAt(GlowPulse.PERIOD), 3);
            Assert.InRange(GlowPulse.AlphaAt(GlowPulse.PERIOD / 4f), GlowPulse.MIN_ALPHA, GlowPulse.MAX_ALPHA);
        }

        /// <summary>
        /// With the mouse over the button the halo brightens smoothly, from its dimmest to full in FADE_IN
        /// seconds, then stays full while the pulse time runs on.
        /// </summary>
        [Fact]
        public void Hovered_FadesUpToFullAndHolds()
        {
            const float frame = 1f / 60f;
            float alpha = GlowPulse.MIN_ALPHA;
            float maxStep = (GlowPulse.MAX_ALPHA - GlowPulse.MIN_ALPHA) * frame / GlowPulse.FADE_IN;
            float time = 0f;
            for (; time < GlowPulse.FADE_IN / 2f - frame / 2f; time += frame)
            {
                float next = GlowPulse.Step(alpha, true, time, frame);
                Assert.InRange(next - alpha, 0f, maxStep + 1e-5f);
                alpha = next;
            }
            Assert.Equal((GlowPulse.MIN_ALPHA + GlowPulse.MAX_ALPHA) / 2f, alpha, 2);

            for (; time < 3f * GlowPulse.PERIOD; time += frame)
                alpha = GlowPulse.Step(alpha, true, time, frame);
            Assert.Equal(GlowPulse.MAX_ALPHA, alpha, 5);
        }

        /// <summary>
        /// After the mouse leaves, the pulse picks up at the halo's brightness and dims from there.
        /// </summary>
        [Theory]
        [InlineData(GlowPulse.MAX_ALPHA)]
        [InlineData(0.75f)]
        [InlineData(0.4f)]
        [InlineData(GlowPulse.MIN_ALPHA)]
        public void Leaving_ContinuesFromCurrentBrightnessAndDims(float alpha)
        {
            float time = GlowPulse.FadingTimeAt(alpha);

            Assert.InRange(time, GlowPulse.PERIOD / 2f, GlowPulse.PERIOD);
            Assert.Equal(alpha, GlowPulse.Step(alpha, false, time, 0f), 3);
            if (alpha > GlowPulse.MIN_ALPHA)
                Assert.True(GlowPulse.AlphaAt(time + 0.05f) < alpha);
        }

        /// <summary>
        /// The hover handler on the button switches the halo's hovered state, and does nothing without a halo.
        /// </summary>
        [Fact]
        public void HoverHandler_SwitchesHalo()
        {
            GlowPulse pulse = Seams.Fake<GlowPulse>();
            GlowHover hover = Seams.Fake<GlowHover>();
            hover.Pulse = pulse;

            hover.OnPointerEnter(null);
            Assert.True(pulse.Hovered);
            hover.OnPointerExit(null);
            Assert.False(pulse.Hovered);

            Seams.Fake<GlowHover>().OnPointerEnter(null);
        }

        private string MakeDir(string name, bool withPicture)
        {
            string dir = Path.Combine(temp, name);
            Directory.CreateDirectory(Path.Combine(dir, "Images"));
            if (withPicture)
                File.WriteAllBytes(Path.Combine(dir, GoldButton.Button.RelativePath), new byte[0]);
            return dir;
        }

        /// <summary>A PNG's size, from its header (bytes 16 to 23, big-endian).</summary>
        private static (int, int) PngSize(string relativePath)
        {
            byte[] bytes = File.ReadAllBytes(TestGame.ModAsset(relativePath));
            int Read(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
            return (Read(16), Read(20));
        }
    }
}
