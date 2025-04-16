// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Extensions;
using osu.Game.IO.Legacy;
using osu.Game.IO.Serialization;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using SharpCompress.Compressors.LZMA;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace PerformanceCalculatorGUI.Components
{
    public class ExtendedScoreEncoder
    {
        public const int LATEST_VERSION = 30000013;

        /// <summary>
        /// The first stable-compatible YYYYMMDD format version given to lazer usage of replays.
        /// </summary>
        public const int FIRST_LAZER_VERSION = 30000000;

        private readonly Score score;
        private readonly IBeatmap? beatmap;

        /// <summary>
        /// Create a new score encoder for a specific score.
        /// </summary>
        /// <param name="score">The score to be encoded.</param>
        /// <param name="beatmap">The beatmap used to convert frames for the score. May be null if the frames are already <see cref="LegacyReplayFrame"/>s.</param>
        /// <exception cref="ArgumentException"></exception>
        public ExtendedScoreEncoder(Score score, IBeatmap? beatmap)
        {
            this.score = score;
            this.beatmap = beatmap;

            if (beatmap == null && score.ScoreInfo.Ruleset.ShortName == "mania" && !score.Replay.Frames.All(f => f is LegacyReplayFrame))
                throw new ArgumentException(@"Beatmap must be provided if it's mania and frames are not already legacy frames.", nameof(beatmap));

            if (!score.ScoreInfo.Ruleset.IsLegacyRuleset())
                throw new ArgumentException(@"Only scores in the osu, taiko, catch, or mania rulesets can be encoded to the legacy score format.", nameof(score));
        }

        public void Export(string filepath, bool leaveOpen = false, bool useDefaulVersion = true, bool addNameMark = true)
        {
            using (var stream = File.OpenWrite(filepath))
                Encode(stream, leaveOpen, useDefaulVersion, addNameMark);
        }
        public void Encode(Stream stream, bool leaveOpen = false, bool useDefaulVersion = true, bool addNameMark = true)
        {
            using (SerializationWriter sw = new SerializationWriter(stream, leaveOpen))
            {
                sw.Write((byte)(score.ScoreInfo.Ruleset.OnlineID));
                sw.Write(useDefaulVersion ? LATEST_VERSION : score.ScoreInfo.TotalScoreVersion);
                sw.Write(score.ScoreInfo.BeatmapHash);
                sw.Write(addNameMark ? score.ScoreInfo.User.Username + " (edited)" : score.ScoreInfo.User.Username);
                sw.Write(FormattableString.Invariant($"lazer-{score.ScoreInfo.User.Username}-{score.ScoreInfo.Date}").ComputeMD5Hash());
                sw.Write((ushort)(score.ScoreInfo.GetCount300() ?? 0));
                sw.Write((ushort)(score.ScoreInfo.GetCount100() ?? 0));
                sw.Write((ushort)(score.ScoreInfo.GetCount50() ?? 0));
                sw.Write((ushort)(score.ScoreInfo.GetCountGeki() ?? 0));
                sw.Write((ushort)(score.ScoreInfo.GetCountKatu() ?? 0));
                sw.Write((ushort)(score.ScoreInfo.GetCountMiss() ?? 0));
                sw.Write((int)(score.ScoreInfo.TotalScore));
                sw.Write((ushort)score.ScoreInfo.MaxCombo);
                sw.Write(score.ScoreInfo.Combo == score.ScoreInfo.MaxCombo);
                sw.Write((int)score.ScoreInfo.Ruleset.CreateInstance().ConvertToLegacyMods(score.ScoreInfo.Mods));

                sw.Write(getHpGraphFormatted());
                sw.Write(score.ScoreInfo.Date.DateTime);
                sw.WriteByteArray(createReplayData());
                sw.Write(score.ScoreInfo.LegacyOnlineID);
                writeModSpecificData(score.ScoreInfo, sw);
                sw.WriteByteArray(createScoreInfoData());
            }
        }

        private void writeModSpecificData(ScoreInfo score, SerializationWriter sw)
        {
        }

        private byte[] createReplayData() => compress(replayStringContent);

        private byte[] createScoreInfoData() => compress(LegacyReplaySoloScoreInfo.FromScore(score.ScoreInfo).Serialize());

        private byte[] compress(string data)
        {
            byte[] content = new ASCIIEncoding().GetBytes(data);

            using (var outStream = new MemoryStream())
            {
                using (var lzma = new LzmaStream(new LzmaEncoderProperties(false, 1 << 21, 255), false, outStream))
                {
                    outStream.Write(lzma.Properties);

                    long fileSize = content.Length;
                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte)(fileSize >> (8 * i)));

                    lzma.Write(content);
                }

                return outStream.ToArray();
            }
        }

        private string replayStringContent
        {
            get
            {
                StringBuilder replayData = new StringBuilder();

                // As this is baked into hitobject timing (see `LegacyBeatmapDecoder`) we also need to apply this to replay frame timing.
                double offset = beatmap?.BeatmapVersion < 5 ? -LegacyBeatmapDecoder.EARLY_VERSION_TIMING_OFFSET : 0;

                int lastTime = 0;

                if (score.Replay != null)
                {
                    foreach (var f in score.Replay.Frames)
                    {
                        var legacyFrame = getLegacyFrame(f);

                        // Rounding because stable could only parse integral values
                        int time = (int)Math.Round(legacyFrame.Time + offset);
                        replayData.Append(FormattableString.Invariant($"{time - lastTime}|{legacyFrame.MouseX ?? 0}|{legacyFrame.MouseY ?? 0}|{(int)legacyFrame.ButtonState},"));
                        lastTime = time;
                    }
                }

                // Warning: this is purposefully hardcoded as a string rather than interpolating, as in some cultures the minus sign is not encoded as the standard ASCII U+00C2 codepoint,
                // which then would break decoding.
                replayData.Append(@"-12345|0|0|0");
                return replayData.ToString();
            }
        }

        private LegacyReplayFrame getLegacyFrame(ReplayFrame replayFrame)
        {
            switch (replayFrame)
            {
                case LegacyReplayFrame legacyFrame:
                    return legacyFrame;

                case IConvertibleReplayFrame convertibleFrame:
                    return convertibleFrame.ToLegacy(beatmap);

                default:
                    throw new ArgumentException(@"Frame could not be converted to legacy frames", nameof(replayFrame));
            }
        }

        private string getHpGraphFormatted()
        {
            // todo: implement, maybe?
            return string.Empty;
        }
    }
}
