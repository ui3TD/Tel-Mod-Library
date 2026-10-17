using System;
using System.IO;
using System.Linq;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A promoted male producer's small portrait uses the mod's picture, found next to the DLL or in the mod folder.
    /// </summary>
    public class ThumbnailTests
    {
        private static readonly string AssetPath = Path.Combine(TestGame.ModDir(), "assets", PlayerThumbnail.RelativePath);

        [Fact]
        public void OnlyThePromotedMaleProducerThumbnailIsFilledIn()
        {
            Assert.True(PlayerThumbnail.IsMissingThumbnail(staff._type.player, false, true, staff._staff._unique_type.NONE));

            // The atlas has these
            Assert.False(PlayerThumbnail.IsMissingThumbnail(staff._type.player, false, false, staff._staff._unique_type.NONE));
            Assert.False(PlayerThumbnail.IsMissingThumbnail(staff._type.player_female, false, true, staff._staff._unique_type.NONE));
            Assert.False(PlayerThumbnail.IsMissingThumbnail(staff._type.physician, false, true, staff._staff._unique_type.NONE));
            // Full portraits and unique staff come from elsewhere
            Assert.False(PlayerThumbnail.IsMissingThumbnail(staff._type.player, true, true, staff._staff._unique_type.NONE));
            Assert.False(PlayerThumbnail.IsMissingThumbnail(staff._type.player, false, true, Enum.GetValues(typeof(staff._staff._unique_type)).Cast<staff._staff._unique_type>().First(t => t != staff._staff._unique_type.NONE)));
        }

        [Fact]
        public void PictureIsAnRgbaThumbnailWithTransparency()
        {
            byte[] png = File.ReadAllBytes(AssetPath);

            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8));
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
            Assert.Equal(128, BigEndian(png, 16));
            Assert.Equal(128, BigEndian(png, 20));
            Assert.Equal(8, png[24]); // bit depth
            Assert.Equal(6, png[25]); // colour type: RGBA, like the game's thumbnails (transparent around the hair)
        }

        [Fact]
        public void PictureIsFoundNextToTheDllFirst()
        {
            string dllDir = TempModDir(withPicture: true), modDir = TempModDir(withPicture: true);

            Assert.Equal(Path.Combine(dllDir, PlayerThumbnail.RelativePath), PlayerThumbnail.FindFile(dllDir, new[] { modDir }));
        }

        [Fact]
        public void PictureFallsBackToTheModFolder()
        {
            string dllDir = TempModDir(withPicture: false), modDir = TempModDir(withPicture: true);

            Assert.Equal(Path.Combine(modDir, PlayerThumbnail.RelativePath), PlayerThumbnail.FindFile(dllDir, new[] { null, "", modDir }));
        }

        [Fact]
        public void NoPictureGivesNull()
        {
            Assert.Null(PlayerThumbnail.FindFile(TempModDir(withPicture: false), new[] { TempModDir(withPicture: false) }));
            Assert.Null(PlayerThumbnail.FindFile(null, Array.Empty<string>()));
        }

        private static int BigEndian(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static string TempModDir(bool withPicture)
        {
            string dir = Path.Combine(Path.GetTempPath(), "UnofficialPatchTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "Images"));
            if (withPicture)
                File.Copy(AssetPath, Path.Combine(dir, PlayerThumbnail.RelativePath));
            return dir;
        }
    }
}
