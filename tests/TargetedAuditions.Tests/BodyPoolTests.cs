using CustomAuditions;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CustomAuditions.CustomAuditions;
using Asset = data_girls_textures._textureAsset;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// The game picks each candidate's body from those not yet used in the audition. The mod lets
    /// bodies repeat only once every eligible body has been used.
    /// </summary>
    public class BodyPoolTests
    {
        public BodyPoolTests()
        {
            Seams.Reset();
        }

        private static Asset Body(int id, bool unique = false) => new()
        {
            type = data_girls_textures._spriteType.body,
            body_id = id,
            Unique = unique,
        };

        private static void AddBodies(params Asset[] assets) => Seams.TextureAssets.AddRange(assets);

        private static void BeforeCandidate(bool genTextures = true, Asset bodyAsset = null) =>
            data_girls_GenerateGirl.Prefix(genTextures, bodyAsset);

        /// <summary>
        /// Mirrors data_girls_textures.getRandomBodyAsset's filter, taking the lowest ID.
        /// </summary>
        private static int PickBody()
        {
            Asset picked = Seams.TextureAssets
                .Where(a => !a.Add_To_Default && a.type == data_girls_textures._spriteType.body)
                .Where(a => !Auditions.UsedBodyIDs.Contains(a.body_id) && a.CanBeHired())
                .OrderBy(a => a.body_id)
                .First();
            Auditions.UsedBodyIDs.Add(picked.body_id);
            return picked.body_id;
        }

        [Fact]
        public void LargeAudition_UsesEveryBodyBeforeRepeating()
        {
            AddBodies(Enumerable.Range(1, 10).Select(id => Body(id)).ToArray());
            BeginAuditionGeneration();

            List<int> bodies = new();
            for (int i = 0; i < 25; i++)
            {
                BeforeCandidate();
                bodies.Add(PickBody());
            }

            Assert.Equal(Enumerable.Range(1, 10), bodies.Take(10).OrderBy(b => b));
            Assert.Equal(Enumerable.Range(1, 10), bodies.Skip(10).Take(10).OrderBy(b => b));
            Assert.Equal(5, bodies.Skip(20).Distinct().Count());
        }

        [Fact]
        public void UnusedBodyLeft_KeepsTheUsedList()
        {
            AddBodies(Body(1), Body(2));
            Auditions.UsedBodyIDs.Add(1);
            BeginAuditionGeneration();

            BeforeCandidate();

            Assert.Equal(new[] { 1 }, Auditions.UsedBodyIDs);
        }

        [Fact]
        public void AllBodiesUsed_ClearsTheUsedList()
        {
            AddBodies(Body(1), Body(2));
            Auditions.UsedBodyIDs.AddRange(new[] { 1, 2 });
            BeginAuditionGeneration();

            BeforeCandidate();

            Assert.Empty(Auditions.UsedBodyIDs);
        }

        /// <summary>
        /// Bodies the game would never pick don't count as unused.
        /// </summary>
        [Fact]
        public void IneligibleBodies_DontHoldOffTheClear()
        {
            Asset hidden = Body(2);
            hidden.appears_in_auditions = false;
            Asset addOn = Body(3);
            addOn.Add_To_Default = true;
            Asset hair = Body(4);
            hair.type = data_girls_textures._spriteType.hair;
            Asset hiredUnique = Body(5, unique: true);
            data_girls.girls hired = Seams.NewGirl();
            hired.status = data_girls._status.normal;
            hired.textureAssets.Add(new data_girls.girls._textureAsset { asset = hiredUnique });
            data_girls.girl.Add(hired);

            AddBodies(Body(1), hidden, addOn, hair, hiredUnique);
            Auditions.UsedBodyIDs.Add(1);
            BeginAuditionGeneration();

            BeforeCandidate();

            Assert.Empty(Auditions.UsedBodyIDs);
        }

        [Fact]
        public void UnhiredUniqueBody_CountsAsUnused()
        {
            AddBodies(Body(1), Body(2, unique: true));
            Auditions.UsedBodyIDs.Add(1);
            BeginAuditionGeneration();

            BeforeCandidate();

            Assert.Equal(new[] { 1 }, Auditions.UsedBodyIDs);
        }

        [Fact]
        public void OutsideAudition_LeavesTheUsedList()
        {
            AddBodies(Body(1));
            Auditions.UsedBodyIDs.Add(1);

            BeforeCandidate();

            Assert.Equal(new[] { 1 }, Auditions.UsedBodyIDs);
        }

        /// <summary>
        /// Girls generated without textures, or with a body chosen by the caller, don't draw from the pool.
        /// </summary>
        [Fact]
        public void NoRandomBody_LeavesTheUsedList()
        {
            AddBodies(Body(1));
            Auditions.UsedBodyIDs.Add(1);
            BeginAuditionGeneration();

            BeforeCandidate(genTextures: false);
            BeforeCandidate(bodyAsset: Body(1));

            Assert.Equal(new[] { 1 }, Auditions.UsedBodyIDs);
        }

        [Fact]
        public void NoAssetsLoaded_ClearsWithoutError()
        {
            Auditions.UsedBodyIDs.Add(1);
            BeginAuditionGeneration();

            BeforeCandidate();

            Assert.Empty(Auditions.UsedBodyIDs);
        }
    }
}
