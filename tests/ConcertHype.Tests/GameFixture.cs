using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using _concert = SEvent_Concerts._concert;

// The game tests share the game's static state (idols, variables, the live session) and patch the game.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace ConcertHype.Tests
{
    /// <summary>
    /// The concert popup as the session sees it, with the game's AddHype clamping and a log of the calls.
    /// </summary>
    public sealed class FakeStage : IConcertStage
    {
        public FakeStage(_concert concert) => Concert = concert;

        public _concert Concert { get; }

        public int SongCounter { get; set; }

        public readonly List<(float value, bool animate)> Calls = new();

        /// <summary>
        /// Actions for the next frame; the tests run them with <see cref="EndFrame"/>.
        /// </summary>
        public readonly List<Action> NextFrameActions = new();

        public void NextFrame(Action action) => NextFrameActions.Add(action);

        public void EndFrame()
        {
            List<Action> actions = new(NextFrameActions);
            NextFrameActions.Clear();
            actions.ForEach(a => a());
        }

        public void AddHype(float value, bool animate)
        {
            Calls.Add((value, animate));
            Concert.Hype = Mathf.Clamp(Concert.Hype + value, 0f, 200f);
        }

        /// <summary>
        /// What the popup does on Start: the talk break when anyone is on its row, else the song; it plays the item
        /// it is on, animates its hype and ends it.
        /// </summary>
        public void PlaySong(ShowSession session)
        {
            if (session.HasTalk)
                session.StartTalkBreak(this);
            else
                session.BeforeSong(this);
            float change = 0;
            _concert.ISetlistItem item = Concert.SetListItems[SongCounter];
            if (item is _concert._song song ? session.HypeFor(song, out change) : session.HypeForTalk((_concert._mc)item, out change))
                AddHype(change, false);
            session.AfterSong(this);
            EndFrame();
            SongCounter++;
        }
    }

    /// <summary>
    /// Idols, singles and concerts built from the game's own classes.
    /// </summary>
    public static class GameFixture
    {
        public static readonly data_girls._paramType[] Stats =
        {
            data_girls._paramType.cute, data_girls._paramType.cool, data_girls._paramType.sexy, data_girls._paramType.pretty,
            data_girls._paramType.vocal, data_girls._paramType.dance, data_girls._paramType.funny, data_girls._paramType.smart,
        };

        public static void Reset()
        {
            ShowSession.End();
            ShowSession.LogPath = null;
            data_girls.girl = new List<data_girls.girls>();
            Groups.Groups_ = new List<Groups._group>();
            variables.variable = new List<variables._variable>();
            Awards._Awards = new List<Awards._award>();
            Awards._Nominations = new List<Awards._award>();
            // Enough for the game's attendance: Normal difficulty, plenty of hardcore fans, no scandals
            staticVars.PlayerData.Difficulty = staticVars._playerData._difficulty.normal;
            staticVars.dateTime = new DateTime(2023, 6, 5);
            resources.resource = new List<long>(new long[32]);
            resources.Fans = new List<resources._fan> { new() { hardcoreness = resources.fanType.hardcore, people = 10_000_000 } };
            SEvent_Concerts.UnlockedVenue = SEvent_Concerts._venue.stadium;
        }

        /// <summary>
        /// A group with these idols, added to the agency's groups.
        /// </summary>
        public static Groups._group Group(int id, string title, bool disbanded, params data_girls.girls[] idols)
        {
            Groups._group group = new() { ID = id, Title = title, Girls = idols.ToList(), Status = disbanded ? Groups._group._status.disbanded : Groups._group._status.normal };
            Groups.Groups_.Add(group);
            return group;
        }

        public static void SetVariable(string name, string value)
        {
            variables.variable.RemoveAll(v => v.name == name);
            variables.variable.Add(new variables._variable { name = name, value = value });
        }

        /// <summary>
        /// An active idol with every stat at 50 unless given, and this many fans.
        /// </summary>
        public static data_girls.girls Idol(string name, float dance = 50, float vocal = 50, long fans = 1000, float funny = 50, float smart = 50)
        {
            data_girls.girls girl = new() { nickname = name, status = data_girls._status.normal };
            foreach (data_girls._paramType stat in Stats)
            {
                float value = stat == data_girls._paramType.dance ? dance : stat == data_girls._paramType.vocal ? vocal
                    : stat == data_girls._paramType.funny ? funny : stat == data_girls._paramType.smart ? smart : 50;
                girl.parameters.Add(new data_girls.girls.param { type = stat, _val = value, Parent = girl });
            }
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.casual, age = resources.fanType.teen, people = fans });
            data_girls.girl.Add(girl);
            return girl;
        }

        /// <summary>
        /// A single option whose appeal is the same at every level, except level 1 gets these values.
        /// </summary>
        public static singles._param Option(string title, params float[] levelOneAppeal)
        {
            singles._param option = new() { title = title };
            resources.fanType[] order = { resources.fanType.male, resources.fanType.female, resources.fanType.casual, resources.fanType.hardcore,
                resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult };
            for (int f = 0; f < order.Length; f++)
            {
                float value = f < levelOneAppeal.Length ? levelOneAppeal[f] : 0;
                option.appeal.Add(new singles._param._appeal { type = order[f], val = new List<float> { value }.Concat(Enumerable.Repeat(value + 5, 9)).ToList() });
            }
            return option;
        }

        /// <summary>
        /// A single whose senbatsu holds these idols at positions 0, 1, 2...; null leaves a position empty.
        /// </summary>
        public static singles._single Single(string title, params data_girls.girls[] senbatsu)
        {
            singles._single single = new()
            {
                title = title,
                genre = Option("Pop"),
                lyrics = Option("Love"),
                choreography = Option("Cute"),
                status = singles._single._status.released,
            };
            single.girls = Enumerable.Range(0, 15).Select(i => i < senbatsu.Length ? senbatsu[i] : null).ToList();
            return single;
        }

        public static _concert Concert(params singles._single[] singles)
        {
            _concert concert = new() { Title = "Test Concert", Venue = SEvent_Concerts._venue.concertHall };
            concert.ProjectedValues.Parent = concert;
            concert.ProjectedValues.Actual_Attendance = 1f;
            concert.parameters.Add(new _concert._progressable { type = _concert._progressable._type.production, val = 50 });
            foreach (singles._single single in singles)
                concert.SetListItems.Add(new _concert._song { Single = single, Center = single.GetCenter() });
            return concert;
        }

        public static ShowSession Begin(_concert concert, Config config = null)
        {
            config ??= new Config { Seed = 5 };
            return ShowSession.Begin(concert, config);
        }

        /// <summary>
        /// A popup object for the patch methods that only read its fields.
        /// </summary>
        public static Concert_Popup Popup(_concert concert)
        {
            Concert_Popup popup = (Concert_Popup)FormatterServices.GetUninitializedObject(typeof(Concert_Popup));
            popup.Concert = concert;
            popup.Accident_Top_Container = (GameObject)FormatterServices.GetUninitializedObject(typeof(GameObject));
            return popup;
        }
    }

    public sealed class LogRecorder : ILogHandler, IDisposable
    {
        private readonly ILogHandler gameLog = Debug.unityLogger.logHandler;
        public readonly List<string> Messages = new();

        public LogRecorder() => Debug.unityLogger.logHandler = this;

        public void Dispose() => Debug.unityLogger.logHandler = gameLog;

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) => Messages.Add(string.Format(format, args));

        public void LogException(Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
    }
}
