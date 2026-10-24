using DG.Tweening;
using UnityEngine;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// A national tour's results show the National Tour picture; a world tour's show the game's own background.
    /// </summary>
    public class ResultsPictureTests
    {
        private readonly Scene scene;

        public ResultsPictureTests()
        {
            TestGame.Reset();
            TestGame.LoadImages();
            TestGame.LoadCountries();
            scene = new Scene();
        }

        private static void Launch(bool national)
        {
            SEvent_Tour tour = Seams.Fake<SEvent_Tour>();
            tour.Tour = TestGame.Tour(national ? Prefectures.All[0] : SEvent_Tour.Countries[0]);
            SEvent_Tour_StartTour.Prefix(tour);
            Seams.TourResultsReset();
        }

        [Fact]
        public void NationalTour_FadesInTheNationalTourPicture()
        {
            Launch(national: true);

            Assert.Same(Pictures.World_tour_def_tex, scene.BGRaw.texture);
            Assert.Equal(0f, Seams.Alphas[scene.BGFade]);
            Assert.Equal(new Vector2(1024, 0), Seams.Sizes[scene.BGRect]);
            Assert.Collection(Seams.Tweens,
                fade =>
                {
                    Assert.Equal(nameof(DOTweenModuleUI.DOFade), fade.Method);
                    Assert.Same(scene.BGFade, fade.Target);
                    Assert.Equal(1f, fade.EndValue);
                    Assert.Equal(0.5f, fade.Duration);
                    Assert.Equal(Ease.OutQuint, fade.Ease);
                },
                zoom =>
                {
                    Assert.Equal(nameof(DOTweenModuleUI.DOSizeDelta), zoom.Method);
                    Assert.Same(scene.BGRect, zoom.Target);
                    Assert.Equal(new Vector2(1074, 20), zoom.EndValue);
                    Assert.Equal(4.5f, zoom.Duration);
                    Assert.Equal(Ease.OutQuint, zoom.Ease);
                });
        }

        [Fact]
        public void NationalTour_ShowsTheBlurredPictureBehindTheResults()
        {
            Launch(national: true);

            Assert.Same(Pictures.World_tour_def_BG, scene.ResultsBG.sprite);
            Assert.Equal(Color.white, scene.ResultsBG.color);
        }

        [Fact]
        public void WorldTour_KeepsTheGamesBackground()
        {
            Launch(national: false);

            Assert.Same(scene.GameBackground, scene.BGRaw.texture);
            Assert.True(scene.ResultsBG.sprite is null);
            Assert.Equal(Scene.BlurColor, scene.ResultsBG.color);
            Assert.Empty(Seams.Tweens);
        }

        [Fact]
        public void WorldTour_AfterANationalTour_GetsTheGamesBlurBack()
        {
            Launch(national: true);
            Seams.TourResultsFinish();

            Launch(national: false);

            Assert.True(scene.ResultsBG.sprite is null);
            Assert.Equal(Scene.BlurColor, scene.ResultsBG.color);
            Assert.Equal(Scene.BackgroundSize, Seams.Sizes[scene.BGRect]);
        }

        /// <summary>
        /// The background is shared by every popup, so its zoom ends with the results: the zoom tween is
        /// stopped and the game's size put back.
        /// </summary>
        [Fact]
        public void ClosingNationalResults_PutsBackTheBackgroundSize()
        {
            Launch(national: true);
            Seams.Sizes[scene.BGRect] = new Vector2(1060, 14);

            Seams.TourResultsFinish();

            Assert.Equal(Scene.BackgroundSize, Seams.Sizes[scene.BGRect]);
            Assert.Same(scene.BGRect, Assert.Single(Seams.Killed));
        }

        [Fact]
        public void ClosingWorldResults_LeavesTheBackgroundAlone()
        {
            Launch(national: false);
            Seams.Sizes[scene.BGRect] = new Vector2(500, 5);

            Seams.TourResultsFinish();

            Assert.Equal(new Vector2(500, 5), Seams.Sizes[scene.BGRect]);
            Assert.Empty(Seams.Killed);
        }

        [Fact]
        public void NationalTour_WithoutThePictures_KeepsTheGamesBackground()
        {
            Pictures.World_tour_def_tex = null;

            Launch(national: true);

            Assert.Same(scene.GameBackground, scene.BGRaw.texture);
            Assert.Empty(Seams.Tweens);
        }
    }
}
