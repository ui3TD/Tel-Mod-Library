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

            Assert.Equal(Path.Combine(dll, GoldButton.Button.RelativePath), GoldButton.FindFile(GoldButton.Button.RelativePath, dll, new[] { mod }));
        }

        [Fact]
        public void FindFile_FallsBackToModFolder()
        {
            string dll = MakeDir("dll", withPicture: false);
            string mod = MakeDir("mod", withPicture: true);

            Assert.Equal(Path.Combine(mod, GoldButton.Button.RelativePath), GoldButton.FindFile(GoldButton.Button.RelativePath, dll, new[] { null, "", mod }));
        }

        [Fact]
        public void FindFile_NullWhenMissing()
        {
            string dll = MakeDir("dll", withPicture: false);

            Assert.Null(GoldButton.FindFile(GoldButton.Button.RelativePath, dll, new string[0]));
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
