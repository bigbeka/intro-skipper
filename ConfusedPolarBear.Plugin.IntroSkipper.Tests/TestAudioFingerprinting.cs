/* These tests require that the host system has a version of FFmpeg installed
 * which supports both chromaprint and the "-fp_format raw" flag.
 */

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Tests;

public class TestAudioFingerprinting
{
    [Fact]
    public void TestInstallationCheck()
    {
        Assert.True(Chromaprint.CheckFFmpegVersion());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 213)]
    [InlineData(10, 56_021)]
    [InlineData(16, 16_112_341)]
    [InlineData(19, 2_465_585_877)]
    public void TestBitCounting(int expectedBits, uint number)
    {
        Assert.Equal(expectedBits, FingerprinterTask.CountBits(number));
    }

    [Fact]
    public void TestFingerprinting()
    {
        // Generated with `fpcalc -raw audio/big_buck_bunny_intro.mp3`
        var expected = new uint[]{
            3269995649, 3261610160, 3257403872, 1109989680, 1109993760, 1110010656, 1110142768, 1110175504,
            1110109952, 1126874880, 2788611, 2787586, 6981634, 15304754, 28891170, 43579426, 43542561,
            47737888, 41608640, 40559296, 36352644, 53117572, 2851460, 1076465548, 1080662428, 1080662492,
            1089182044, 1148041501, 1148037422, 3291343918, 3290980398, 3429367854, 3437756714, 3433698090,
            3433706282, 3366600490, 3366464314, 2296916250, 3362269210, 3362265115, 3362266441, 3370784472,
            3366605480, 1218990776, 1223217816, 1231602328, 1260950200, 1245491640, 169845176, 1510908120,
            1510911000, 2114365528, 2114370008, 1996929688, 1996921480, 1897171592, 1884588680, 1347470984,
            1343427226, 1345467054, 1349657318, 1348673570, 1356869666, 1356865570, 295837698, 60957698,
            44194818, 48416770, 40011778, 36944210, 303147954, 369146786, 1463847842, 1434488738, 1417709474,
            1417713570, 3699441634, 3712167202, 3741460534, 2585144342, 2597725238, 2596200487, 2595926077,
            2595984141, 2594734600, 2594736648, 2598931176, 2586348264, 2586348264, 2586561257, 2586451659,
            2603225802, 2603225930, 2573860970, 2561151018, 3634901034, 3634896954, 3651674122, 3416793162,
            3416816715, 3404331257, 3395844345, 3395836155, 3408464089, 3374975369, 1282036360, 1290457736,
            1290400440, 1290314408, 1281925800, 1277727404, 1277792932, 1278785460, 1561962388, 1426698196,
            3607924711, 4131892839, 4140215815, 4292259591, 3218515717, 3209938229, 3171964197, 3171956013,
            4229117295, 4229312879, 4242407935, 4240114111, 4239987133, 4239990013, 3703060732, 1547188252,
            1278748677, 1278748935, 1144662786, 1148854786, 1090388802, 1090388962, 1086260130, 1085940098,
            1102709122, 45811586, 44634002, 44596656, 44592544, 1122527648, 1109944736, 1109977504, 1111030243,
            1111017762, 1109969186, 1126721826, 1101556002, 1084844322, 1084979506, 1084914450, 1084914449,
            1084873520, 3228093296, 3224996817, 3225062275, 3241840002, 3346701698, 3349843394, 3349782306,
            3349719842, 3353914146, 3328748322, 3328747810, 3328809266, 3471476754, 3472530451, 3472473123,
            3472417825, 3395841056, 3458735136, 3341420624, 1076496560, 1076501168, 1076501136, 1076497024
        };

        var actual = Chromaprint.Fingerprint(queueEpisode("audio/big_buck_bunny_intro.mp3"));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TestIndexGeneration()
    {
        //                                    0  1  2  3  4  5   6   7
        var fpr = new List<uint>(new uint[] { 1, 2, 3, 1, 5, 77, 42, 2 }).AsReadOnly();
        var expected = new Dictionary<uint, int>()
        {
            {1, 3},
            {2, 7},
            {3, 2},
            {5, 4},
            {42, 6},
            {77, 5},
        };

        var actual = Chromaprint.CreateInvertedIndex(fpr);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TestIntroDetection()
    {
        var task = new FingerprinterTask(new LoggerFactory());

        var lhsEpisode = queueEpisode("audio/big_buck_bunny_intro.mp3");
        var rhsEpisode = queueEpisode("audio/big_buck_bunny_clip.mp3");

        var (lhs, rhs) = task.FingerprintEpisodes(lhsEpisode, rhsEpisode);

        Assert.True(lhs.Valid);
        Assert.Equal(0, lhs.IntroStart);
        Assert.Equal(17.792, lhs.IntroEnd);

        Assert.True(rhs.Valid);
        Assert.Equal(5.12, rhs.IntroStart);
        Assert.Equal(22.912, rhs.IntroEnd);
    }

    private QueuedEpisode queueEpisode(string path)
    {
        return new QueuedEpisode()
        {
            Path = "../../../" + path,
            FingerprintDuration = 60
        };
    }
}
