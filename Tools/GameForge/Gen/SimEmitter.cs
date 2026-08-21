using System.Globalization;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>
/// 밸런스 시뮬레이터 프로젝트를 생성한다. Core 의 .cs 를 그대로 링크하므로
/// Unity 를 켜지 않고 dotnet 만으로 게임 로직을 수만 번 돌려볼 수 있다.
/// </summary>
public static class SimEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string simDir, string coreRelativeToSim)
    {
        var ns = spec.SafeName + ".Core";

        e.Add($"{simDir}/{spec.SafeName}.Sim.csproj", Csproj(coreRelativeToSim), header: false);
        e.Add($"{simDir}/Program.cs",
            ProgramFor(spec.Genre)
                .Replace("__NS__", ns)
                .Replace("__DAY1__", D(spec.Balance.Day1Progress))
                .Replace("__DAY7__", D(spec.Balance.Day7Progress))
                .Replace("__CLEAR__", D(spec.Balance.TargetClearRate))
                .Replace("__ADOPT__", D(spec.Balance.MaxAdoptionRate))
                .Replace("__FULLDAYS__", D(spec.Balance.FullUnlockDays)));
    }

    static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>장르별 시뮬레이터 본문. 장르를 추가하면 여기에 한 줄 등록한다.</summary>
    static string ProgramFor(string genre) => genre switch
    {
        "towerdefense" => TdProgram,
        "merge" => MergeProgram,
        "autobattler" => AutoBattlerProgram,
        "survivor" => SurvivorProgram,
        "match3" => Match3Program,
        _ => IdleProgram,
    };

    static string Csproj(string coreRelative) => $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <!-- Unity 프로젝트의 Core 소스를 그대로 링크한다. 사본이 아니라 같은 파일이다. -->
    <Compile Include="{coreRelative}/**/*.cs" />
    <Compile Include="Program.cs" />
  </ItemGroup>
</Project>

""";

    const string IdleProgram = """
using System;
using System.Collections.Generic;
using __NS__;

// 방치형 밸런스 시뮬레이터.
// 가상 플레이어가 합리적으로(회수 빠른 순) 구매한다고 가정하고 진행 속도를 측정한다.
static class Program
{
    const double Day1Target = __DAY1__;
    const double Day7Target = __DAY7__;
    const double FullUnlockDaysTarget = __FULLDAYS__;

    sealed class Run
    {
        public double[] UnlockAt;          // 시설별 최초 구매 시각(초). -1 이면 미도달
        public double LastUnlockSec;       // 마지막 시설이 열린 시각. 미도달이면 double.MaxValue
        public double WorstGapSec;
        public string WorstGapAt = "";
        public int Day1Count, Day7Count;
        public List<string> Checkpoints = new List<string>();
    }

    static int Main(string[] args)
    {
        int days = ArgInt(args, "--days", 30);

        if (Has(args, "--tune")) return Tune(days);

        var run = Simulate(days, growth: -1, tierFactor: -1);
        Report(run);

        int issues = Diagnose(run);
        Console.WriteLine();
        Console.WriteLine($"RESULT day1={run.Day1Count} day7={run.Day7Count} " +
                          $"fullUnlock={Fmt(run.LastUnlockSec)} issues={issues}");
        return issues == 0 ? 0 : 1;
    }

    /// <summary>한 판 시뮬. tierFactor 가 0 보다 크면 그 조합으로 비용 곡선을 갈아끼운다.</summary>
    static Run Simulate(int days, double growth, double tierFactor)
    {
        if (tierFactor > 0) GameData.ApplyTuning(growth, tierFactor); else GameData.ResetTuning();

        var world = GameFactory.Create(seed: 20260820);
        var player = new AutoPlayer(world);
        var gens = world.Get<GeneratorSystem>();

        var r = new Run { UnlockAt = new double[gens.Count] };
        for (int i = 0; i < r.UnlockAt.Length; i++) r.UnlockAt[i] = -1;

        var checkpoints = new (string Label, double Sec)[]
        {
            ("10분", 600), ("1시간", 3600), ("1일", 86400),
            ("3일", 259200), ("7일", 604800), ("30일", 2592000)
        };
        int nextCp = 0;

        // 체크 간격: 초반은 촘촘하게, 후반은 성글게 봐야 30일치가 빨리 돈다.
        double elapsed = 0, total = days * 86400.0;
        while (elapsed < total)
        {
            double step = elapsed < 3600 ? 10 : elapsed < 86400 ? 60 : 600;
            player.BuyGreedy();
            gens.Produce(world, step);       // 틱 루프를 건너뛰는 등가 경로
            elapsed += step;

            for (int i = 0; i < gens.Count; i++)
                if (r.UnlockAt[i] < 0 && gens.Owned[i] > 0) r.UnlockAt[i] = elapsed;

            while (nextCp < checkpoints.Length && elapsed >= checkpoints[nextCp].Sec)
            {
                player.BuyGreedy();
                var cp = checkpoints[nextCp++];
                var first = GameData.Resources[0].Id;
                r.Checkpoints.Add($"{cp.Label,6} | 시설 {CountOwnedKinds(gens),2}종 보유 {gens.TotalOwned,6}개 | " +
                                  $"초당 {gens.TotalRate(first),12} | 누적 {world.Resources.Lifetime(first)}");
            }
        }

        double prev = 0;
        r.LastUnlockSec = 0;
        for (int i = 0; i < gens.Count; i++)
        {
            if (r.UnlockAt[i] < 0) { r.LastUnlockSec = double.MaxValue; continue; }
            double gap = r.UnlockAt[i] - prev;
            if (gap > r.WorstGapSec) { r.WorstGapSec = gap; r.WorstGapAt = GameData.Generators[i].Name; }
            prev = r.UnlockAt[i];
            if (r.LastUnlockSec != double.MaxValue) r.LastUnlockSec = r.UnlockAt[i];
        }

        r.Day1Count = CountBy(r.UnlockAt, 86400);
        r.Day7Count = CountBy(r.UnlockAt, 604800);
        return r;
    }

    static void Report(Run r)
    {
        Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ===");
        Console.WriteLine();
        foreach (var line in r.Checkpoints) Console.WriteLine(line);
        Console.WriteLine();
        Console.WriteLine("-- 시설 해금 시각 --");
        double prev = 0;
        for (int i = 0; i < r.UnlockAt.Length; i++)
        {
            var def = GameData.Generators[i];
            if (r.UnlockAt[i] < 0) { Console.WriteLine($"  {def.Name,-14} 미도달"); continue; }
            Console.WriteLine($"  {def.Name,-14} {Human(r.UnlockAt[i]),10}   (직전 대비 +{Human(r.UnlockAt[i] - prev)})");
            prev = r.UnlockAt[i];
        }
        Console.WriteLine();
        Console.WriteLine("-- 진단 --");
    }

    static int Diagnose(Run r)
    {
        int issues = 0;
        double targetSec = FullUnlockDaysTarget * 86400;

        if (r.LastUnlockSec == double.MaxValue)
        {
            Console.WriteLine($"  [느림] 마지막 시설 미도달 — 비용 증가율(CostGrowth) 완화 또는 후반 산출 상향");
            issues++;
        }
        else if (r.LastUnlockSec < targetSec * 0.25)
        {
            Console.WriteLine($"  [빠름] 전 시설이 {Human(r.LastUnlockSec)} 만에 해금 (목표 {Human(targetSec)}) " +
                              $"— 콘텐츠 조기 소진. CostGrowth 상향 필요");
            issues++;
        }
        else if (r.LastUnlockSec > targetSec * 4)
        {
            Console.WriteLine($"  [느림] 전 시설 해금에 {Human(r.LastUnlockSec)} (목표 {Human(targetSec)}) — 이탈 위험");
            issues++;
        }
        else
        {
            Console.WriteLine($"  [정상] 전 시설 해금 {Human(r.LastUnlockSec)} (목표 {Human(targetSec)})");
        }

        issues += Check("1일차 시설수", r.Day1Count, Day1Target);
        issues += Check("7일차 시설수", r.Day7Count, Day7Target);

        if (r.WorstGapSec > targetSec * 0.6 && r.LastUnlockSec != double.MaxValue)
        {
            Console.WriteLine($"  [벽] {r.WorstGapAt} 앞에서 {Human(r.WorstGapSec)} 정체 — 해당 구간 비용/보상 재조정");
            issues++;
        }

        if (issues == 0) Console.WriteLine("  이상 없음. 목표 곡선 안에 들어옴.");
        return issues;
    }

    /// <summary>
    /// 두 축(단가 상승률 x 티어 간 배율)을 훑어 목표 해금 곡선에 가장 가까운 조합을 찾는다.
    /// 한 축만 훑으면 "아무 값이나 다 비슷" 한 결과가 나와 튜닝이 무의미해진다.
    /// </summary>
    static int Tune(int days)
    {
        double targetSec = FullUnlockDaysTarget * 86400;
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 전 시설 해금 {Human(targetSec)}, 1일차 {Day1Target}종, 7일차 {Day7Target}종");
        Console.WriteLine();

        var growths = new[] { 1.07, 1.10, 1.13, 1.16, 1.20 };
        // 방치형 경제는 보유수에 대해 지수로 커져서 티어 배율의 유효 구간이 좁다. 그 구간을 촘촘히 훑는다.
        var tiers = new[] { 8.0, 16.0, 24.0, 32.0, 40.0, 48.0, 64.0 };

        double bestG = 0, bestT = 0, bestScore = double.MaxValue;
        Run bestRun = null;

        Console.WriteLine("  단가상승률 / 티어배율   " + string.Join("  ", Array.ConvertAll(tiers, t => $"x{t,-6:0}")));
        foreach (var g in growths)
        {
            var cells = new List<string>();
            foreach (var t in tiers)
            {
                var run = Simulate(days, g, t);
                double last = run.LastUnlockSec;
                cells.Add($"{(last == double.MaxValue ? "미도달" : Human(last)),-8}");

                double score = last == double.MaxValue
                    ? 50 + Math.Abs(Day7Target - run.Day7Count)
                    : Math.Abs(Math.Log(last / targetSec)) + 0.4 * Math.Abs(Math.Log((run.Day1Count + 1.0) / (Day1Target + 1.0)));

                if (score < bestScore) { bestScore = score; bestG = g; bestT = t; bestRun = run; }
            }
            Console.WriteLine($"      {g:0.00}            " + string.Join("  ", cells));
        }

        Console.WriteLine();
        if (bestRun == null) { Console.WriteLine("적합한 조합 없음 — 시설 산출(BaseRate) 비율부터 다시 보세요."); return 1; }

        Console.WriteLine($"추천 조합: costGrowth = {bestG:0.00}, 티어 배율 = x{bestT:0}");
        Console.WriteLine($"  → 전 시설 해금 {Human(bestRun.LastUnlockSec)} (목표 {Human(targetSec)}), " +
                          $"1일차 {bestRun.Day1Count}종, 7일차 {bestRun.Day7Count}종");
        Console.WriteLine();
        Console.WriteLine("적용 방법: Specs/*.json 에서");
        Console.WriteLine($"  - generators[*].costGrowth 를 {bestG:0.00} 으로");
        Console.WriteLine($"  - generators[i].baseCost 에 x{bestT:0}^i 를 곱한 값으로");
        Console.WriteLine("고친 뒤 gameforge gen 으로 재생성하면 됩니다.");
        Console.WriteLine();
        Console.WriteLine($"TUNED costGrowth={bestG:0.00} tierFactor={bestT:0}");
        return 0;
    }

    static int Check(string label, int actual, double target)
    {
        double ratio = target <= 0 ? 1 : actual / target;
        if (ratio < 0.6) { Console.WriteLine($"  [느림] {label} {actual} < 목표 {target}"); return 1; }
        if (ratio > 1.6) { Console.WriteLine($"  [빠름] {label} {actual} > 목표 {target}"); return 1; }
        Console.WriteLine($"  [정상] {label} {actual} (목표 {target})");
        return 0;
    }

    static int CountOwnedKinds(GeneratorSystem gens)
    {
        int n = 0;
        for (int i = 0; i < gens.Count; i++) if (gens.Owned[i] > 0) n++;
        return n;
    }

    static int CountBy(double[] unlockAt, double sec)
    {
        int n = 0;
        for (int i = 0; i < unlockAt.Length; i++) if (unlockAt[i] >= 0 && unlockAt[i] <= sec) n++;
        return n;
    }

    static string Fmt(double sec) => sec == double.MaxValue ? "never" : $"{sec / 86400:0.##}d";

    static string Human(double sec)
    {
        if (sec == double.MaxValue) return "미도달";
        if (sec < 60) return $"{sec:0}초";
        if (sec < 3600) return $"{sec / 60:0.#}분";
        if (sec < 86400) return $"{sec / 3600:0.#}시간";
        return $"{sec / 86400:0.#}일";
    }

    static bool Has(string[] args, string key) => Array.IndexOf(args, key) >= 0;

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";

    const string TdProgram = """
using System;
using System.Collections.Generic;
using __NS__;

// 타워디펜스 밸런스 시뮬레이터.
// 전략(선호 타워 순서)을 바꿔가며 수백~수천 판을 돌려 웨이브별 클리어율과 타워 채용률을 낸다.
static class Program
{
    const double ClearTarget = __CLEAR__;
    const double MaxAdoption = __ADOPT__;

    static int Main(string[] args)
    {
        int runs = ArgInt(args, "--runs", 300);
        if (Array.IndexOf(args, "--tune") >= 0) return Tune(ArgInt(args, "--runs", 40));

        var r = Play(runs, hpGrowth: -1, rewardGrowth: -1, verbose: true);
        return r.Issues == 0 ? 0 : 1;
    }

    sealed class Result
    {
        public double WinRate;
        public double AvgWave;      // 아무도 클리어 못 할 때 기울기를 주는 지표
        public int FirstWall = -1;
        public int Issues;
        public double MaxAdoption;
    }

    /// <summary>
    /// 난이도(체력 증가율) x 경제(보상 배율) 두 축을 훑어 목표 클리어율에 가장 가까운 조합을 찾는다.
    /// 한 축만 만지면 "쉬운데 지루" 또는 "어려운데 돈만 남는" 쪽으로 치우친다.
    /// </summary>
    static int Tune(int runs)
    {
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 최종 클리어율 {ClearTarget * 100:0}%, 단일 타워 채용률 {MaxAdoption * 100:0}% 미만");
        Console.WriteLine();

        var hps = new[] { 1.04, 1.06, 1.08, 1.10, 1.12, 1.14 };
        var rewards = new[] { 1.14, 1.18, 1.22, 1.26, 1.30 };

        double bestHp = 0, bestRw = 0, bestScore = double.MaxValue;
        Result best = null;

        Console.WriteLine("  (평균 도달 웨이브 / 최종 클리어율)");
        Console.WriteLine("  체력증가율 / 보상증가율  " + string.Join("  ", Array.ConvertAll(rewards, r => $"{r,-8:0.00}")));
        foreach (var hp in hps)
        {
            var cells = new List<string>();
            foreach (var rw in rewards)
            {
                var res = Play(runs, hp, rw, verbose: false);
                cells.Add($"{res.AvgWave,4:0.0}w/{res.WinRate * 100,3:0}%");

                // 아무 조합도 클리어를 못 하면 클리어율만으로는 기울기가 없다.
                // 평균 도달 웨이브를 함께 봐야 "어느 방향이 덜 나쁜지"가 보인다.
                double reach = res.AvgWave / GameData.WaveCount;
                double score = Math.Abs(res.WinRate - ClearTarget) + (1 - reach)
                             + 0.5 * Math.Max(0, res.MaxAdoption - MaxAdoption);
                if (score < bestScore) { bestScore = score; bestHp = hp; bestRw = rw; best = res; }
            }
            Console.WriteLine($"      {hp:0.00}            " + string.Join(" ", cells));
        }

        Console.WriteLine();
        if (best == null) { Console.WriteLine("적합한 조합 없음 — 타워 성능/적 체력 기본값부터 다시 보세요."); return 1; }

        Console.WriteLine($"추천 조합: hpGrowth = {bestHp:0.00}, rewardGrowth = {bestRw:0.00}");
        Console.WriteLine($"  → 평균 도달 웨이브 {best.AvgWave:0.0}/{GameData.WaveCount}, " +
                          $"클리어율 {best.WinRate * 100:0.0}%, 최고 채용률 {best.MaxAdoption * 100:0.0}%");
        Console.WriteLine();
        Console.WriteLine("적용:");
        Console.WriteLine($"  gameforge apply <spec.json> --hp-growth {bestHp:0.00} --reward-growth {bestRw:0.00}");
        Console.WriteLine();
        Console.WriteLine($"TUNED hpGrowth={bestHp:0.00} rewardGrowth={bestRw:0.00}");
        return 0;
    }

    static Result Play(int runs, double hpGrowth, double rewardGrowth, bool verbose)
    {
        if (hpGrowth > 0 || rewardGrowth > 0) GameData.ApplyTuning(hpGrowth, rewardGrowth);
        else GameData.ResetTuning();

        int towerCount = GameData.Towers.Length;
        var reachedAt = new int[GameData.WaveCount + 2];   // 웨이브 n 에 도달한 판 수
        var buildCount = new int[towerCount];
        var waveSumByLead = new long[towerCount];         // 전략(1순위 타워)별 도달 웨이브 합
        var runsByLead = new int[towerCount];
        int victories = 0, totalBuilds = 0;
        long waveSum = 0;

        if (verbose)
        {
            Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ({runs}판) ===");
            Console.WriteLine();
        }

        for (int run = 0; run < runs; run++)
        {
            // 판마다 선호 타워를 돌려가며 전략 다양성을 만든다.
            int lead = towerCount == 0 ? 0 : run % towerCount;
            var pref = Preference(lead, towerCount);

            var world = GameFactory.Create(seed: (ulong)(1000 + run));
            var combat = world.Get<CombatSystem>();
            var waves = world.Get<WaveSystem>();
            var player = new AutoPlayer(world, pref);

            int reached = 1;
            world.OnEvent += ev =>
            {
                if (ev.Kind == SimEventKind.WaveStarted) reached = ev.IntValue;
                else if (ev.Kind == SimEventKind.TowerBuilt) { buildCount[IndexOf(ev.Id)]++; totalBuilds++; }
            };

            // 최대 2시간치 게임 시간이면 어떤 스펙이든 결판이 난다.
            int maxSteps = GameData.TickRate * 7200;
            for (int step = 0; step < maxSteps && !world.IsOver; step++)
            {
                if (step % GameData.TickRate == 0) player.BuildGreedy();
                world.Step();
            }

            if (waves.Cleared) victories++;
            waveSum += reached;
            for (int w = 1; w <= reached && w < reachedAt.Length; w++) reachedAt[w]++;
            waveSumByLead[lead] += reached;
            runsByLead[lead]++;
        }

        int firstWall = -1;
        bool tooEasy = reachedAt[GameData.WaveCount] == runs;
        int issues = 0;
        double maxShare = 0;

        if (verbose) Console.WriteLine("-- 웨이브별 도달률 --");
        for (int w = 1; w <= GameData.WaveCount; w++)
        {
            double rate = (double)reachedAt[w] / runs;
            if (firstWall < 0 && rate < ClearTarget) firstWall = w;
            if (verbose && (w % 5 == 0 || w == 1 || w == GameData.WaveCount))
                Console.WriteLine($"  웨이브 {w,3} : {rate * 100,5:0.0}%  {Bar(rate)}");
        }

        // 채용률(지어진 개수 비중)은 싼 타워가 무조건 이기므로 전략 성능을 뜻하지 않는다.
        // 각 타워를 주력으로 삼은 전략이 실제로 몇 웨이브까지 갔는지로 평가한다.
        if (verbose) { Console.WriteLine(); Console.WriteLine("-- 주력 타워별 전략 성능 --"); }

        double bestLeadWave = 0;
        for (int i = 0; i < towerCount; i++)
        {
            double avg = runsByLead[i] == 0 ? 0 : (double)waveSumByLead[i] / runsByLead[i];
            if (avg > bestLeadWave) bestLeadWave = avg;
        }

        for (int i = 0; i < towerCount; i++)
        {
            double avg = runsByLead[i] == 0 ? 0 : (double)waveSumByLead[i] / runsByLead[i];
            double rel = bestLeadWave <= 0 ? 0 : avg / bestLeadWave;
            if (rel > maxShare) maxShare = rel;

            string flag = "";
            if (rel < 0.5) { flag = "  <-- 약함: 버프 검토"; issues++; }
            if (verbose)
                Console.WriteLine($"  {GameData.Towers[i].Name,-14} 평균 {avg,5:0.0} 웨이브  " +
                                  $"(최고 전략 대비 {rel * 100,3:0}%)  건설 {buildCount[i],4}기{flag}");
        }

        // 한 전략만 압도적이면 메타가 고착된 것
        if (towerCount > 1)
        {
            int strong = 0;
            for (int i = 0; i < towerCount; i++)
            {
                double avg = runsByLead[i] == 0 ? 0 : (double)waveSumByLead[i] / runsByLead[i];
                if (bestLeadWave > 0 && avg / bestLeadWave >= 0.7) strong++;
            }
            if (strong <= 1)
            {
                if (verbose) Console.WriteLine("  [고착] 쓸 만한 전략이 하나뿐 — 타워 간 역할 분화 필요");
                issues++;
            }
        }

        double winRate = (double)victories / runs;

        if (verbose)
        {
            Console.WriteLine();
            Console.WriteLine("-- 진단 --");
            Console.WriteLine($"  최종 클리어율 {winRate * 100:0.0}%");
        }

        if (firstWall > 0)
        {
            if (verbose)
                Console.WriteLine($"  [벽] 웨이브 {firstWall} 부터 도달률이 목표({ClearTarget * 100:0}%) 밑 — 체력 증가율 완화 또는 보상 상향");
            issues++;
        }
        if (tooEasy)
        {
            if (verbose) Console.WriteLine("  [쉬움] 전 판이 끝까지 클리어 — 난이도 곡선 상향 여지");
            issues++;
        }

        if (verbose)
        {
            if (issues == 0) Console.WriteLine("  이상 없음. 난이도/채용률 모두 목표 범위.");
            Console.WriteLine();
            Console.WriteLine($"RESULT win={winRate:0.000} wall={firstWall} issues={issues}");
        }

        return new Result
        {
            WinRate = winRate,
            AvgWave = (double)waveSum / runs,
            FirstWall = firstWall,
            Issues = issues,
            MaxAdoption = maxShare,
        };
    }

    static int[] Preference(int lead, int count)
    {
        var pref = new int[count];
        for (int i = 0; i < count; i++) pref[i] = (lead + i) % count;
        return pref;
    }

    static int IndexOf(string towerId)
    {
        for (int i = 0; i < GameData.Towers.Length; i++)
            if (GameData.Towers[i].Id == towerId) return i;
        return 0;
    }

    static string Bar(double rate)
    {
        int n = (int)Math.Round(rate * 20);
        return new string('#', n) + new string('.', 20 - n);
    }

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";

    const string MergeProgram = """
using System;
using __NS__;

// 머지 밸런스 시뮬레이터.
// 가상 플레이어가 합칠 수 있으면 합치고 소환한다는 가정으로 성장 속도를 측정한다.
static class Program
{
    const double FullUnlockDaysTarget = __FULLDAYS__;

    static int Main(string[] args)
    {
        int days = ArgInt(args, "--days", 14);
        if (Array.IndexOf(args, "--tune") >= 0) return Tune(days);

        var r = Run(days, -1, -1, verbose: true);
        return r.Issues == 0 ? 0 : 1;
    }

    sealed class Result
    {
        public int Highest;
        public double SecondsToMax = double.MaxValue;
        public double EnergyIdleRatio;   // 에너지가 가득 차서 놀고 있던 시간 비율
        public int Issues;
    }

    static Result Run(int days, double regen, double incomeGrowth, bool verbose)
    {
        if (regen > 0 || incomeGrowth > 0) GameData.ApplyTuning(regen, incomeGrowth);
        else GameData.ResetTuning();

        var world = GameFactory.Create(seed: 20260820);
        var board = world.Get<MergeBoard>();
        var energy = world.Get<EnergySystem>();
        var player = new AutoPlayer(world);
        var r = new Result();

        if (verbose)
        {
            Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ({days}일) ===");
            Console.WriteLine();
        }

        var checkpoints = new (string Label, double Sec)[]
        {
            ("10분", 600), ("1시간", 3600), ("1일", 86400), ("3일", 259200), ("7일", 604800),
        };
        int nextCp = 0;

        double elapsed = 0, total = days * 86400.0, idleTime = 0;
        const double Step = 5;   // 에너지 회복 단위에 맞춘 간격

        while (elapsed < total)
        {
            if (energy.Current >= energy.Max) idleTime += Step;

            player.Play();
            energy.Regen(Step);
            board.Produce(world, Step);
            elapsed += Step;

            if (r.SecondsToMax == double.MaxValue && board.Highest >= GameData.MaxLevel)
                r.SecondsToMax = elapsed;

            while (nextCp < checkpoints.Length && elapsed >= checkpoints[nextCp].Sec)
            {
                var cp = checkpoints[nextCp++];
                if (verbose)
                    Console.WriteLine($"{cp.Label,6} | 최고 {GameData.LevelNamePrefix}.{board.Highest,-3} | " +
                                      $"초당 {board.TotalIncome(),10:0.##} | 합성 {board.MergeCount,6}회 | " +
                                      $"누적 {world.Resources.Lifetime(GameData.Resources[0].Id)}");
            }
        }

        r.Highest = board.Highest;
        r.EnergyIdleRatio = idleTime / total;

        if (!verbose) return r;

        Console.WriteLine();
        Console.WriteLine("-- 진단 --");
        double targetSec = FullUnlockDaysTarget * 86400;

        if (r.SecondsToMax == double.MaxValue)
        {
            Console.WriteLine($"  [느림] 최고 레벨 미도달 (현재 {GameData.LevelNamePrefix}.{r.Highest}/{GameData.MaxLevel})" +
                              " — 에너지 회복 상향 또는 최고 레벨 하향");
            r.Issues++;
        }
        else if (r.SecondsToMax < targetSec * 0.2)
        {
            Console.WriteLine($"  [빠름] {Human(r.SecondsToMax)} 만에 최고 레벨 도달 (목표 {Human(targetSec)}) — 콘텐츠 조기 소진");
            r.Issues++;
        }
        else
        {
            Console.WriteLine($"  [정상] 최고 레벨 도달 {Human(r.SecondsToMax)} (목표 {Human(targetSec)})");
        }

        if (r.EnergyIdleRatio > 0.5)
        {
            Console.WriteLine($"  [정체] 에너지가 가득 찬 채 방치된 시간 {r.EnergyIdleRatio * 100:0}% " +
                              "— 판이 꽉 차서 소환을 못 하고 있다. 판 크기 또는 판매 유도 재검토");
            r.Issues++;
        }

        if (r.Issues == 0) Console.WriteLine("  이상 없음.");
        Console.WriteLine();
        Console.WriteLine($"RESULT highest={r.Highest} toMax={Fmt(r.SecondsToMax)} energyIdle={r.EnergyIdleRatio:0.00} issues={r.Issues}");
        return r;
    }

    static int Tune(int days)
    {
        double targetSec = FullUnlockDaysTarget * 86400;
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 최고 레벨 도달 {Human(targetSec)}");
        Console.WriteLine();

        var regens = new[] { 0.1, 0.25, 0.5, 1.0, 2.0 };
        var growths = new[] { 1.8, 2.0, 2.2, 2.4 };

        double bestRegen = 0, bestGrowth = 0, bestScore = double.MaxValue;
        Result best = null;

        Console.WriteLine("  에너지회복/초 / 산출증가율  " + string.Join("   ", Array.ConvertAll(growths, g => $"{g,-8:0.0}")));
        foreach (var regen in regens)
        {
            var cells = new System.Collections.Generic.List<string>();
            foreach (var growth in growths)
            {
                var r = Run(days, regen, growth, verbose: false);
                cells.Add($"{(r.SecondsToMax == double.MaxValue ? "미도달" : Human(r.SecondsToMax)),-10}");

                double score = r.SecondsToMax == double.MaxValue
                    ? 50 + (GameData.MaxLevel - r.Highest)
                    : Math.Abs(Math.Log(r.SecondsToMax / targetSec)) + r.EnergyIdleRatio;

                if (score < bestScore) { bestScore = score; bestRegen = regen; bestGrowth = growth; best = r; }
            }
            Console.WriteLine($"      {regen,-6:0.00}              " + string.Join(" ", cells));
        }

        Console.WriteLine();
        Console.WriteLine($"추천 조합: energyRegenPerSec = {bestRegen:0.00}, itemIncomeGrowth = {bestGrowth:0.0}");
        Console.WriteLine($"  → 최고 레벨 도달 {Human(best.SecondsToMax)}, 에너지 유휴 {best.EnergyIdleRatio * 100:0}%");
        Console.WriteLine();
        Console.WriteLine($"TUNED energyRegen={bestRegen:0.00} incomeGrowth={bestGrowth:0.0}");
        return 0;
    }

    static string Fmt(double sec) => sec == double.MaxValue ? "never" : $"{sec / 86400:0.##}d";

    static string Human(double sec)
    {
        if (sec == double.MaxValue) return "미도달";
        if (sec < 60) return $"{sec:0}초";
        if (sec < 3600) return $"{sec / 60:0.#}분";
        if (sec < 86400) return $"{sec / 3600:0.#}시간";
        return $"{sec / 86400:0.#}일";
    }

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";

    const string AutoBattlerProgram = """
using System;
using __NS__;

// 오토배틀러 밸런스 시뮬레이터.
// 스테이지 진행 속도와 유닛별 전략 성능을 본다.
static class Program
{
    const double Day1Target = __DAY1__;
    const double Day7Target = __DAY7__;

    static int Main(string[] args)
    {
        int days = ArgInt(args, "--days", 7);
        if (Array.IndexOf(args, "--tune") >= 0) return Tune(days);

        var r = Run(days, -1, -1, -1, verbose: true);
        return r.Issues == 0 ? 0 : 1;
    }

    sealed class Result
    {
        public int Day1Stage, Day7Stage, BestStage;
        public double WinRate;
        public int Issues;
    }

    static Result Run(int days, double stageGrowth, double rewardGrowth, int leadUnit, bool verbose)
    {
        if (stageGrowth > 0 || rewardGrowth > 0) GameData.ApplyTuning(stageGrowth, rewardGrowth);
        else GameData.ResetTuning();

        var world = GameFactory.Create(seed: 20260820);
        var roster = world.Get<Roster>();
        var battle = world.Get<BattleSystem>();
        var player = new AutoPlayer(world, leadUnit < 0 ? null : Preference(leadUnit));
        var r = new Result();

        if (verbose)
        {
            Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ({days}일) ===");
            Console.WriteLine();
        }

        var checkpoints = new (string Label, double Sec)[]
        {
            ("10분", 600), ("1시간", 3600), ("1일", 86400), ("3일", 259200), ("7일", 604800),
        };
        int nextCp = 0;

        double elapsed = 0, total = days * 86400.0;
        int stepsPerCheck = GameData.TickRate;   // 1초 단위로 구매 판단

        while (elapsed < total && !world.IsOver)
        {
            player.Play();
            for (int i = 0; i < stepsPerCheck && !world.IsOver; i++) world.Step();
            elapsed += 1;

            while (nextCp < checkpoints.Length && elapsed >= checkpoints[nextCp].Sec)
            {
                var cp = checkpoints[nextCp++];
                if (cp.Sec <= 86400) r.Day1Stage = battle.BestStage;
                if (cp.Sec <= 604800) r.Day7Stage = battle.BestStage;
                if (verbose)
                    Console.WriteLine($"{cp.Label,6} | 스테이지 {battle.BestStage,4}/{GameData.StageCount} | " +
                                      $"팀 {roster.OwnedCount}/{GameData.TeamSize} | {battle.Wins}승 {battle.Losses}패 | " +
                                      $"골드 {world.Resources.Get(GameData.Resources[0].Id)}");
            }
        }

        r.BestStage = battle.BestStage;
        if (r.Day7Stage == 0) r.Day7Stage = battle.BestStage;
        int total2 = battle.Wins + battle.Losses;
        r.WinRate = total2 == 0 ? 0 : (double)battle.Wins / total2;

        if (!verbose) return r;

        Console.WriteLine();
        Console.WriteLine("-- 유닛별 최종 레벨 --");
        for (int i = 0; i < roster.Count; i++)
            Console.WriteLine($"  {GameData.Units[i].Name,-10} Lv.{roster.Levels[i],-3} " +
                              $"공격 {roster.AttackOf(i),10:0}  체력 {roster.HpOf(i),10:0}");

        Console.WriteLine();
        Console.WriteLine("-- 진단 --");
        r.Issues += Check("1일차 스테이지", r.Day1Stage, Day1Target);
        r.Issues += Check("7일차 스테이지", r.Day7Stage, Day7Target);

        if (r.WinRate < 0.35)
        {
            Console.WriteLine($"  [벽] 전투 승률 {r.WinRate * 100:0}% — 재도전만 반복 중. 스테이지 성장률 완화 필요");
            r.Issues++;
        }
        else if (r.WinRate > 0.95)
        {
            Console.WriteLine($"  [쉬움] 전투 승률 {r.WinRate * 100:0}% — 긴장이 없다. 성장률 상향 여지");
            r.Issues++;
        }
        else Console.WriteLine($"  [정상] 전투 승률 {r.WinRate * 100:0}%");

        if (r.Issues == 0) Console.WriteLine("  이상 없음.");
        Console.WriteLine();
        Console.WriteLine($"RESULT day1={r.Day1Stage} day7={r.Day7Stage} win={r.WinRate:0.00} issues={r.Issues}");
        return r;
    }

    static int Tune(int days)
    {
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 1일차 {Day1Target} / 7일차 {Day7Target} 스테이지, 승률 35~95%");
        Console.WriteLine();

        var stageGrowths = new[] { 1.06, 1.09, 1.12, 1.15, 1.18 };
        var rewardGrowths = new[] { 1.08, 1.11, 1.14, 1.17 };

        double bestS = 0, bestR = 0, bestScore = double.MaxValue;
        Result best = null;

        Console.WriteLine("  스테이지성장 / 보상성장   " + string.Join("   ", Array.ConvertAll(rewardGrowths, g => $"{g,-7:0.00}")));
        foreach (var sg in stageGrowths)
        {
            var cells = new System.Collections.Generic.List<string>();
            foreach (var rg in rewardGrowths)
            {
                var r = Run(days, sg, rg, -1, verbose: false);
                cells.Add($"{r.Day7Stage,4}스테이지 ");

                double score = Math.Abs(Math.Log((r.Day7Stage + 1.0) / (Day7Target + 1.0)))
                             + Math.Max(0, 0.35 - r.WinRate) * 2
                             + Math.Max(0, r.WinRate - 0.95) * 2;

                if (score < bestScore) { bestScore = score; bestS = sg; bestR = rg; best = r; }
            }
            Console.WriteLine($"      {sg:0.00}              " + string.Join(" ", cells));
        }

        Console.WriteLine();
        Console.WriteLine($"추천 조합: stageHpGrowth = {bestS:0.00}, rewardGrowth = {bestR:0.00}");
        Console.WriteLine($"  → 7일차 {best.Day7Stage} 스테이지, 승률 {best.WinRate * 100:0}%");
        Console.WriteLine();
        Console.WriteLine($"TUNED stageGrowth={bestS:0.00} rewardGrowth={bestR:0.00}");
        return 0;
    }

    static int[] Preference(int lead)
    {
        var pref = new int[GameData.Units.Length];
        for (int i = 0; i < pref.Length; i++) pref[i] = (lead + i) % pref.Length;
        return pref;
    }

    static int Check(string label, int actual, double target)
    {
        double ratio = target <= 0 ? 1 : actual / target;
        if (ratio < 0.6) { Console.WriteLine($"  [느림] {label} {actual} < 목표 {target}"); return 1; }
        if (ratio > 1.6) { Console.WriteLine($"  [빠름] {label} {actual} > 목표 {target}"); return 1; }
        Console.WriteLine($"  [정상] {label} {actual} (목표 {target})");
        return 0;
    }

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";

    const string SurvivorProgram = """
using System;
using __NS__;

// 서바이버 밸런스 시뮬레이터.
// 무기별로 한 판씩 돌려 생존 시간과 무기 성능 편차를 본다.
static class Program
{
    const double ClearTarget = __CLEAR__;

    static int Main(string[] args)
    {
        int runs = ArgInt(args, "--runs", 20);
        if (Array.IndexOf(args, "--tune") >= 0) return Tune(Math.Max(5, runs / 2));

        var r = Play(runs, -1, -1, verbose: true);
        return r.Issues == 0 ? 0 : 1;
    }

    sealed class Result
    {
        public double SurviveRate;
        public double AvgMinutes;
        public double WorstWeaponRatio = 1;
        public int Issues;
    }

    static Result Play(int runs, double hpGrowth, double spawnGrowth, bool verbose)
    {
        if (hpGrowth > 0 || spawnGrowth > 0) GameData.ApplyTuning(hpGrowth, spawnGrowth);
        else GameData.ResetTuning();

        int weaponCount = GameData.Weapons.Length;
        var minutesByWeapon = new double[weaponCount];
        var runsByWeapon = new int[weaponCount];
        int survived = 0;
        double totalMinutes = 0;

        if (verbose)
        {
            Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ({runs}판) ===");
            Console.WriteLine();
        }

        for (int run = 0; run < runs; run++)
        {
            int favorite = weaponCount == 0 ? -1 : run % weaponCount;

            var world = GameFactory.Create(seed: (ulong)(500 + run));
            var arena = world.Get<ArenaSystem>();
            var player = new AutoPlayer(world, favorite);

            int maxSteps = (int)(GameData.DurationMinutes * 60 * GameData.TickRate) + GameData.TickRate;
            for (int step = 0; step < maxSteps && !world.IsOver; step++)
            {
                if (step % GameData.TickRate == 0) player.Play();
                world.Step();
            }
            player.Play();

            if (arena.Survived) survived++;
            totalMinutes += arena.Minutes;
            if (favorite >= 0) { minutesByWeapon[favorite] += arena.Minutes; runsByWeapon[favorite]++; }
        }

        var r = new Result
        {
            SurviveRate = (double)survived / runs,
            AvgMinutes = totalMinutes / runs,
        };

        double bestAvg = 0;
        for (int i = 0; i < weaponCount; i++)
        {
            double avg = runsByWeapon[i] == 0 ? 0 : minutesByWeapon[i] / runsByWeapon[i];
            if (avg > bestAvg) bestAvg = avg;
        }

        if (verbose) { Console.WriteLine("-- 주력 무기별 생존 시간 --"); }
        for (int i = 0; i < weaponCount; i++)
        {
            double avg = runsByWeapon[i] == 0 ? 0 : minutesByWeapon[i] / runsByWeapon[i];
            double rel = bestAvg <= 0 ? 0 : avg / bestAvg;
            if (rel < r.WorstWeaponRatio) r.WorstWeaponRatio = rel;

            string flag = rel < 0.6 ? "  <-- 약함: 버프 검토" : "";
            if (rel < 0.6) r.Issues++;
            if (verbose)
                Console.WriteLine($"  {GameData.Weapons[i].Name,-12} 평균 {avg,5:0.0}분  (최고 대비 {rel * 100,3:0}%){flag}");
        }

        if (!verbose) return r;

        Console.WriteLine();
        Console.WriteLine("-- 진단 --");
        Console.WriteLine($"  평균 생존 {r.AvgMinutes:0.0}분 / {GameData.DurationMinutes:0}분,  완주율 {r.SurviveRate * 100:0}%");

        if (r.SurviveRate < ClearTarget * 0.5)
        {
            Console.WriteLine($"  [어려움] 완주율이 목표({ClearTarget * 100:0}%)의 절반 미만 — 적 체력/스폰 증가율 완화");
            r.Issues++;
        }
        else if (r.SurviveRate > 0.95)
        {
            Console.WriteLine("  [쉬움] 거의 전부 완주 — 난이도 상향 여지");
            r.Issues++;
        }

        if (r.Issues == 0) Console.WriteLine("  이상 없음.");
        Console.WriteLine();
        Console.WriteLine($"RESULT survive={r.SurviveRate:0.00} avgMin={r.AvgMinutes:0.0} issues={r.Issues}");
        return r;
    }

    static int Tune(int runs)
    {
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 완주율 {ClearTarget * 100:0}%");
        Console.WriteLine();

        var hps = new[] { 1.20, 1.25, 1.30, 1.35, 1.40 };
        var spawns = new[] { 1.05, 1.09, 1.13, 1.17 };

        double bestHp = 0, bestSpawn = 0, bestScore = double.MaxValue;
        Result best = null;

        Console.WriteLine("  적체력증가 / 스폰증가   " + string.Join("   ", Array.ConvertAll(spawns, s => $"{s,-7:0.00}")));
        foreach (var hp in hps)
        {
            var cells = new System.Collections.Generic.List<string>();
            foreach (var sp in spawns)
            {
                var r = Play(runs, hp, sp, verbose: false);
                cells.Add($"{r.AvgMinutes,4:0.0}분/{r.SurviveRate * 100,3:0}% ");

                double score = Math.Abs(r.SurviveRate - ClearTarget) + Math.Max(0, 0.6 - r.WorstWeaponRatio);
                if (score < bestScore) { bestScore = score; bestHp = hp; bestSpawn = sp; best = r; }
            }
            Console.WriteLine($"      {hp:0.00}             " + string.Join(" ", cells));
        }

        Console.WriteLine();
        Console.WriteLine($"추천 조합: enemyHpGrowth = {bestHp:0.00}, spawnPerSecGrowth = {bestSpawn:0.00}");
        Console.WriteLine($"  → 평균 생존 {best.AvgMinutes:0.0}분, 완주율 {best.SurviveRate * 100:0}%");
        Console.WriteLine();
        Console.WriteLine($"TUNED enemyHpGrowth={bestHp:0.00} spawnGrowth={bestSpawn:0.00}");
        return 0;
    }

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";

    const string Match3Program = """
using System;
using __NS__;

// 매치3 밸런스 시뮬레이터.
// 가상 플레이어(한 수 앞만 봄)로 스테이지별 클리어율을 낸다.
// 이 수준으로 깨지는 난이도면 사람은 여유 있게 깬다.
static class Program
{
    const double ClearTarget = __CLEAR__;

    static int Main(string[] args)
    {
        int runs = ArgInt(args, "--runs", 30);
        if (Array.IndexOf(args, "--tune") >= 0) return Tune(Math.Max(10, runs / 3));

        var r = Play(runs, -1, -1, verbose: true);
        return r.Issues == 0 ? 0 : 1;
    }

    sealed class Result
    {
        public double AvgStage;
        public double ClearRate;      // 스테이지 단위 클리어율
        public int FirstWall = -1;
        public int Issues;
    }

    static Result Play(int runs, int moves, double targetGrowth, bool verbose)
    {
        if (moves > 0 || targetGrowth > 0) GameData.ApplyTuning(moves, targetGrowth);
        else GameData.ResetTuning();

        var reachedAt = new int[GameData.StageCount + 2];
        long stageSum = 0;
        int totalCleared = 0, totalAttempts = 0;

        if (verbose)
        {
            Console.WriteLine($"=== {GameData.DisplayName} 밸런스 시뮬 ({runs}판) ===");
            Console.WriteLine();
        }

        for (int run = 0; run < runs; run++)
        {
            var world = GameFactory.Create(seed: (ulong)(9000 + run));
            var stage = world.Get<StageSystem>();
            var player = new AutoPlayer(world);

            // 실패 3회면 그 스테이지를 벽으로 보고 판을 끝낸다(무한 재도전 방지).
            int failsAllowed = 3;
            int lastFailed = stage.Failed;

            while (!world.IsOver)
            {
                if (!player.PlayOne()) break;
                if (stage.Failed > lastFailed)
                {
                    lastFailed = stage.Failed;
                    if (--failsAllowed <= 0) break;
                }
            }

            int reached = stage.Stage;
            stageSum += reached;
            totalCleared += stage.Cleared;
            totalAttempts += stage.Cleared + stage.Failed;
            for (int s = 1; s <= reached && s < reachedAt.Length; s++) reachedAt[s]++;
        }

        var r = new Result
        {
            AvgStage = (double)stageSum / runs,
            ClearRate = totalAttempts == 0 ? 0 : (double)totalCleared / totalAttempts,
        };

        if (verbose) Console.WriteLine("-- 스테이지별 도달률 --");
        for (int s = 1; s <= GameData.StageCount; s++)
        {
            double rate = (double)reachedAt[s] / runs;
            if (r.FirstWall < 0 && rate < 0.5) r.FirstWall = s;
            if (verbose && (s == 1 || s % 5 == 0 || s == GameData.StageCount))
                Console.WriteLine($"  스테이지 {s,3} : {rate * 100,5:0.0}%  {Bar(rate)}");
        }

        if (!verbose) return r;

        Console.WriteLine();
        Console.WriteLine("-- 진단 --");
        Console.WriteLine($"  평균 도달 {r.AvgStage:0.0}/{GameData.StageCount} 스테이지,  시도당 클리어율 {r.ClearRate * 100:0}%");

        if (r.ClearRate < ClearTarget * 0.7)
        {
            Console.WriteLine($"  [어려움] 클리어율이 목표({ClearTarget * 100:0}%)에 크게 못 미침 — 수 늘리기 또는 목표 점수 완화");
            r.Issues++;
        }
        else if (r.ClearRate > 0.97)
        {
            Console.WriteLine("  [쉬움] 거의 실패가 없다 — 목표 점수 상향 여지");
            r.Issues++;
        }

        if (r.FirstWall > 0)
        {
            Console.WriteLine($"  [벽] 스테이지 {r.FirstWall} 부터 절반 이상이 진행하지 못함");
            r.Issues++;
        }

        if (r.Issues == 0) Console.WriteLine("  이상 없음.");
        Console.WriteLine();
        Console.WriteLine($"RESULT avgStage={r.AvgStage:0.0} clear={r.ClearRate:0.00} wall={r.FirstWall} issues={r.Issues}");
        return r;
    }

    static int Tune(int runs)
    {
        Console.WriteLine($"=== {GameData.DisplayName} 자동 튜닝 ===");
        Console.WriteLine($"목표: 시도당 클리어율 {ClearTarget * 100:0}%");
        Console.WriteLine();

        var moveOptions = new[] { 18, 22, 26, 30, 35 };
        var growths = new[] { 1.04, 1.06, 1.08, 1.10 };

        int bestMoves = 0;
        double bestGrowth = 0, bestScore = double.MaxValue;
        Result best = null;

        Console.WriteLine("  수 제한 / 목표점수 증가율  " + string.Join("   ", Array.ConvertAll(growths, g => $"{g,-7:0.00}")));
        foreach (var mv in moveOptions)
        {
            var cells = new System.Collections.Generic.List<string>();
            foreach (var g in growths)
            {
                var r = Play(runs, mv, g, verbose: false);
                cells.Add($"{r.AvgStage,4:0.0}단/{r.ClearRate * 100,3:0}% ");

                double score = Math.Abs(r.ClearRate - ClearTarget);
                if (score < bestScore) { bestScore = score; bestMoves = mv; bestGrowth = g; best = r; }
            }
            Console.WriteLine($"      {mv,2}수                 " + string.Join(" ", cells));
        }

        Console.WriteLine();
        Console.WriteLine($"추천 조합: moves = {bestMoves}, targetGrowth = {bestGrowth:0.00}");
        Console.WriteLine($"  → 평균 {best.AvgStage:0.0} 스테이지, 클리어율 {best.ClearRate * 100:0}%");
        Console.WriteLine();
        Console.WriteLine($"TUNED moves={bestMoves} targetGrowth={bestGrowth:0.00}");
        return 0;
    }

    static string Bar(double rate)
    {
        int n = (int)Math.Round(rate * 20);
        return new string('#', n) + new string('.', 20 - n);
    }

    static int ArgInt(string[] args, string key, int def)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key && int.TryParse(args[i + 1], out var v)) return v;
        return def;
    }
}
""";
}
