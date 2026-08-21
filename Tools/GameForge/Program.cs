using System.Text.Json;
using GameForge.Gen;
using GameForge.Spec;

namespace GameForge;

static class Program
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    static int Main(string[] args)
    {
        if (args.Length == 0) { Usage(); return 1; }

        return args[0] switch
        {
            "gen" => Generate(args),
            "scaffold" => Scaffold(args),
            "apply" => Apply(args),
            "validate" => Validate(args),
            "schema" => Schema(),
            "genres" => Genres(),
            _ => Usage(),
        };
    }

    static int Usage()
    {
        Console.WriteLine("""
GameForge — 기획서(spec)에서 게임 프로젝트를 생성한다.

  gameforge scaffold --genre <genre> --name <Name> [--display "표시 이름"]
      해당 장르의 기본 기획서를 Specs/<name>.json 으로 만든다.

  gameforge gen <spec.json> [--project <유니티 프로젝트 루트>]
      기획서에서 Core/Unity/Editor 코드와 밸런스 시뮬레이터를 생성한다.

  gameforge apply <spec.json> <장르별 옵션>
      밸런스 시뮬(--tune)이 찾은 수치를 기획서에 그대로 반영한다.
        idle          --cost-growth      --tier-factor
        towerdefense  --hp-growth        --reward-growth
        merge         --energy-regen     --income-growth
        autobattler   --stage-growth     --reward-growth
        survivor      --enemy-hp-growth  --spawn-growth
        match3        --moves            --target-growth

  gameforge validate <spec.json>
      코드를 만들기 전에 기획서를 점검한다. 빠진 필드와 말이 안 되는 수치를 잡는다.

  gameforge schema
      기획서에 쓸 수 있는 필드 전체 안내. 문서를 스펙으로 옮길 때 참고한다.

  gameforge genres
      지원 장르 목록.
""");
        return 1;
    }

    static int Genres()
    {
        Console.WriteLine("지원 장르 6종 (모바일 다운로드 상위 + 자동화 적합):");
        Console.WriteLine("  match3        매치3 퍼즐      판 시뮬 + 수 제한 + 스테이지 목표 점수");
        Console.WriteLine("  merge         머지            합성 판 + 에너지 + 아이템 생산");
        Console.WriteLine("  idle          방치형/타이쿤   생산 시설 + 업그레이드 + 오프라인 보상");
        Console.WriteLine("  survivor      서바이버        자동 공격 + 몰림 압력 + 레벨업 강화");
        Console.WriteLine("  towerdefense  타워디펜스      웨이브 + 타워 배치/강화 + 난이도 곡선");
        Console.WriteLine("  autobattler   오토배틀러/RPG  팀 편성 + 자동 전투 + 스테이지 진행");
        return 0;
    }

    static string Arg(string[] args, string key, string def = null)
    {
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return def;
    }

    /// <summary>ProjectSettings 폴더를 가진 가장 가까운 상위 디렉터리를 유니티 프로젝트 루트로 본다.</summary>
    static string FindProjectRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    static int Schema()
    {
        Console.WriteLine(Validator.Schema());
        return 0;
    }

    static int Validate(string[] args)
    {
        var spec = LoadSpec(args, out int code);
        if (spec == null) return code;

        var report = Validator.Check(spec);

        foreach (var w in report.Warnings) Console.WriteLine("  [경고] " + w);
        foreach (var e in report.Errors) Console.Error.WriteLine("  [오류] " + e);

        Console.WriteLine();
        Console.WriteLine(report.Ok
            ? $"[GameForge] {spec.DisplayName} ({spec.Genre}) 점검 통과 — 경고 {report.Warnings.Count}건"
            : $"[GameForge] 점검 실패 — 오류 {report.Errors.Count}건, 경고 {report.Warnings.Count}건");
        return report.Ok ? 0 : 1;
    }

    /// <summary>스펙 로딩은 gen/apply/validate 가 똑같이 쓴다.</summary>
    static GameSpec LoadSpec(string[] args, out int code)
    {
        code = 0;
        if (args.Length < 2) { Console.Error.WriteLine("스펙 파일 경로가 필요합니다."); code = 1; return null; }

        var path = Path.GetFullPath(args[1]);
        if (!File.Exists(path)) { Console.Error.WriteLine($"스펙을 찾을 수 없음: {path}"); code = 1; return null; }

        try
        {
            var spec = JsonSerializer.Deserialize<GameSpec>(File.ReadAllText(path), Json);
            if (spec != null) return spec;
            Console.Error.WriteLine("스펙 파싱 실패");
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine("스펙 JSON 형식 오류: " + ex.Message);
        }

        code = 1;
        return null;
    }

    static int Generate(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("스펙 파일 경로가 필요합니다."); return 1; }

        var specPath = Path.GetFullPath(args[1]);
        if (!File.Exists(specPath)) { Console.Error.WriteLine($"스펙을 찾을 수 없음: {specPath}"); return 1; }

        var spec = JsonSerializer.Deserialize<GameSpec>(File.ReadAllText(specPath), Json);
        if (spec == null) { Console.Error.WriteLine("스펙 파싱 실패"); return 1; }

        var report = Validator.Check(spec);
        foreach (var w in report.Warnings) Console.WriteLine("  [경고] " + w);
        if (!report.Ok)
        {
            foreach (var err in report.Errors) Console.Error.WriteLine("  [오류] " + err);
            Console.Error.WriteLine();
            Console.Error.WriteLine("기획서에 문제가 있어 생성을 중단했습니다. gameforge schema 로 필드를 확인하세요.");
            return 1;
        }

        var root = Arg(args, "--project") ?? FindProjectRoot(Path.GetDirectoryName(specPath));
        if (root == null) { Console.Error.WriteLine("유니티 프로젝트 루트를 찾지 못했습니다. --project 로 지정하세요."); return 1; }

        var name = spec.SafeName;
        var coreDir = $"Assets/Games/{name}/Core";
        var unityDir = $"Assets/Games/{name}/Unity";
        var editorDir = $"Assets/Games/{name}/Editor";
        var simDir = $"Sim/{name}";
        var titleScenePath = $"Assets/Games/{name}/{name}Title.unity";
        var gameScenePath = $"Assets/Games/{name}/{name}Game.unity";

        var e = new Emitter(root);

        CoreRuntimeEmitter.Emit(e, spec, coreDir);
        switch (spec.Genre)
        {
            case "idle": IdleEmitter.Emit(e, spec, coreDir); break;
            case "towerdefense": TowerDefenseEmitter.Emit(e, spec, coreDir); break;
            case "merge": MergeEmitter.Emit(e, spec, coreDir); break;
            case "autobattler": AutoBattlerEmitter.Emit(e, spec, coreDir); break;
            case "survivor": SurvivorEmitter.Emit(e, spec, coreDir); break;
            case "match3": Match3Emitter.Emit(e, spec, coreDir); break;
            default:
                Console.Error.WriteLine($"지원하지 않는 장르: {spec.Genre} (gameforge genres 참고)");
                return 1;
        }

        UnityEmitter.Emit(e, spec, coreDir, unityDir, editorDir);
        FolderEmitter.Emit(e, spec, $"Assets/Games/{name}");
        // 방어코드용 기획서 경로 — 유니티(프로젝트 루트 기준)에서 File.Exists 로 확인한다.
        var specRel = Path.GetRelativePath(root, Path.GetFullPath(specPath)).Replace('\\', '/');
        EditorEmitter.Emit(e, spec, editorDir, titleScenePath, gameScenePath, specRel);
        SimEmitter.Emit(e, spec, simDir, coreRelativeToSim: $"../../{coreDir}");

        int count = e.Flush();

        Console.WriteLine($"[GameForge] {spec.DisplayName} ({spec.Genre}) — {count}개 파일 생성");
        foreach (var p in e.Paths) Console.WriteLine("  " + p);
        Console.WriteLine();
        Console.WriteLine("리소스는 여기에 넣으세요:");
        Console.WriteLine($"  Assets/Games/{name}/Resources/Icons/        버튼 아이콘");
        Console.WriteLine($"  Assets/Games/{name}/Resources/Sprites/      월드 스프라이트");
        Console.WriteLine($"  Assets/Games/{name}/Resources/Backgrounds/  배경");
        Console.WriteLine($"  Assets/Games/{name}/Resources/Audio/        사운드");
        Console.WriteLine($"  자세한 규칙: Assets/Games/{name}/리소스-넣는-법.md");
        Console.WriteLine();
        Console.WriteLine("다음 단계:");
        Console.WriteLine($"  dotnet run --project {simDir}/{name}.Sim.csproj      # 밸런스 시뮬 실행");
        Console.WriteLine($"  유니티에서 메뉴 > GameForge > {spec.DisplayName} > 씬 생성 및 열기 (타이틀부터)");
        return 0;
    }

    /// <summary>
    /// 튜너가 찾은 수치를 기획서에 되먹인다. 손으로 JSON 을 고칠 필요가 없도록.
    /// 축 이름은 장르마다 다르므로 해당 장르가 쓰는 옵션만 반영한다.
    /// </summary>
    static int Apply(string[] args)
    {
        var spec = LoadSpec(args, out int code);
        if (spec == null) return code;
        var specPath = Path.GetFullPath(args[1]);

        var applied = new List<string>();

        switch (spec.Genre)
        {
            case "idle":
            {
                double growth = Num(args, "--cost-growth");
                double tier = Num(args, "--tier-factor");
                for (int i = 0; i < spec.Generators.Count; i++)
                {
                    if (growth > 0) spec.Generators[i].CostGrowth = growth;
                    if (tier > 0) spec.Generators[i].BaseCost = Round(spec.Generators[i].BaseCost * Math.Pow(tier, i));
                }
                if (growth > 0) applied.Add($"costGrowth = {growth}");
                if (tier > 0) applied.Add($"baseCost x {tier}^i");
                break;
            }

            case "towerdefense":
            {
                double hp = Num(args, "--hp-growth");
                double reward = Num(args, "--reward-growth");
                if (hp > 0) { spec.Waves.HpGrowth = hp; applied.Add($"waves.hpGrowth = {hp}"); }
                if (reward > 0) { spec.Waves.RewardGrowth = reward; applied.Add($"waves.rewardGrowth = {reward}"); }
                break;
            }

            case "merge":
            {
                double regen = Num(args, "--energy-regen");
                double income = Num(args, "--income-growth");
                if (regen > 0) { spec.Merge.EnergyRegenPerSec = regen; applied.Add($"merge.energyRegenPerSec = {regen}"); }
                if (income > 0) { spec.Merge.ItemIncomeGrowth = income; applied.Add($"merge.itemIncomeGrowth = {income}"); }
                break;
            }

            case "autobattler":
            {
                double stage = Num(args, "--stage-growth");
                double reward = Num(args, "--reward-growth");
                if (stage > 0)
                {
                    spec.Stages.HpGrowth = stage;
                    // 공격 성장은 체력 성장보다 완만해야 전투가 순식간에 끝나지 않는다.
                    spec.Stages.AttackGrowth = Round3(1 + (stage - 1) * 0.8);
                    applied.Add($"stages.hpGrowth = {stage}, attackGrowth = {spec.Stages.AttackGrowth}");
                }
                if (reward > 0) { spec.Stages.RewardGrowth = reward; applied.Add($"stages.rewardGrowth = {reward}"); }
                break;
            }

            case "survivor":
            {
                double hp = Num(args, "--enemy-hp-growth");
                double spawn = Num(args, "--spawn-growth");
                if (hp > 0) { spec.Survivor.EnemyHpGrowth = hp; applied.Add($"survivor.enemyHpGrowth = {hp}"); }
                if (spawn > 0) { spec.Survivor.SpawnPerSecGrowth = spawn; applied.Add($"survivor.spawnPerSecGrowth = {spawn}"); }
                break;
            }

            case "match3":
            {
                double moves = Num(args, "--moves");
                double target = Num(args, "--target-growth");
                if (moves > 0) { spec.Match3.Moves = (int)moves; applied.Add($"match3.moves = {(int)moves}"); }
                if (target > 0) { spec.Match3.TargetGrowth = target; applied.Add($"match3.targetGrowth = {target}"); }
                break;
            }
        }

        if (applied.Count == 0)
        {
            Console.Error.WriteLine($"'{spec.Genre}' 장르에 적용할 옵션이 없습니다. 사용 가능한 옵션:");
            Console.Error.WriteLine("  " + OptionsFor(spec.Genre));
            return 1;
        }

        File.WriteAllText(specPath, JsonSerializer.Serialize(spec, Json));
        Console.WriteLine($"[GameForge] 기획서 갱신: {specPath}");
        foreach (var line in applied) Console.WriteLine("  " + line);
        Console.WriteLine("이제 gameforge gen 으로 재생성하세요.");
        return 0;
    }

    static string OptionsFor(string genre) => genre switch
    {
        "idle" => "--cost-growth <값>  --tier-factor <값>",
        "towerdefense" => "--hp-growth <값>  --reward-growth <값>",
        "merge" => "--energy-regen <값>  --income-growth <값>",
        "autobattler" => "--stage-growth <값>  --reward-growth <값>",
        "survivor" => "--enemy-hp-growth <값>  --spawn-growth <값>",
        "match3" => "--moves <정수>  --target-growth <값>",
        _ => "(지원하지 않는 장르)",
    };

    static double Num(string[] args, string key)
    {
        var text = Arg(args, key);
        if (text == null) return 0;
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value);
        return value;
    }

    static double Round3(double v) => Math.Round(v, 3);

    /// <summary>기획서에 남는 숫자라 유효자리 3자리로 다듬는다. 읽기 쉬운 값이 수정도 쉽다.</summary>
    static double Round(double v)
    {
        if (v <= 0) return v;
        int digits = (int)Math.Floor(Math.Log10(v));
        double scale = Math.Pow(10, Math.Max(0, digits - 2));
        return Math.Round(v / scale) * scale;
    }

    static int Scaffold(string[] args)
    {
        var genre = Arg(args, "--genre", "idle");
        var name = Arg(args, "--name", "NewGame");
        var display = Arg(args, "--display", name);

        var spec = genre switch
        {
            "idle" => Presets.Idle(name, display),
            "towerdefense" => Presets.TowerDefense(name, display),
            "merge" => Presets.Merge(name, display),
            "autobattler" => Presets.AutoBattler(name, display),
            "survivor" => Presets.Survivor(name, display),
            "match3" => Presets.Match3(name, display),
            _ => null,
        };
        if (spec == null) { Console.Error.WriteLine($"지원하지 않는 장르: {genre}"); return 1; }

        var root = Arg(args, "--project") ?? FindProjectRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
        var path = Path.Combine(root, "Specs", name.ToLowerInvariant() + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(spec, Json));

        Console.WriteLine($"[GameForge] 기획서 생성: {path}");
        Console.WriteLine("이 파일을 고친 뒤 gameforge gen 으로 재생성하면 코드가 따라옵니다.");
        return 0;
    }
}
