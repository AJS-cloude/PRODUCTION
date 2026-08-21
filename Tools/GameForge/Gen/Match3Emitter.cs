using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>매치3 퍼즐(A급). 실제 판을 굴린다 — 이 장르만은 추상화하면 난이도가 안 나온다.</summary>
public static class Match3Emitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("Board.cs", Board);
        Add("StageSystem.cs", Stage);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
        var m = spec.Match3;
        var sb = new StringBuilder();
        sb.AppendLine("namespace __NS__");
        sb.AppendLine("{");
        sb.AppendLine("    public readonly struct ResourceDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Icon; public readonly double Start; public readonly bool Premium;");
        sb.AppendLine("        public ResourceDef(string id, string name, double start, bool premium, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Start = start; Premium = premium; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine();
        sb.AppendLine($"        public const int Width = {m.BoardWidth};");
        sb.AppendLine($"        public const int Height = {m.BoardHeight};");
        sb.AppendLine("        public const int CellCount = Width * Height;");
        sb.AppendLine($"        public const int StageCount = {m.StageCount};");
        sb.AppendLine($"        public const int ScorePerTile = {m.ScorePerTile};");
        sb.AppendLine($"        public const double ComboBonus = {N(m.ComboBonus)};");
        sb.AppendLine($"        public const int BaseTargetScore = {m.TargetScore};");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>튜너가 갈아끼우는 세 축.</summary>");
        sb.AppendLine($"        public static int ColorCount = {m.ColorCount};");
        sb.AppendLine($"        public static int Moves = {m.Moves};");
        sb.AppendLine($"        public static double TargetGrowth = {N(m.TargetGrowth)};");
        sb.AppendLine($"        const int OriginalColorCount = {m.ColorCount};");
        sb.AppendLine($"        const int OriginalMoves = {m.Moves};");
        sb.AppendLine($"        const double OriginalTargetGrowth = {N(m.TargetGrowth)};");
        sb.AppendLine();
        sb.AppendLine("        public static void ApplyTuning(int moves, double targetGrowth)");
        sb.AppendLine("        {");
        sb.AppendLine("            Moves = moves > 0 ? moves : OriginalMoves;");
        sb.AppendLine("            TargetGrowth = targetGrowth > 0 ? targetGrowth : OriginalTargetGrowth;");
        sb.AppendLine("            ColorCount = OriginalColorCount;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("        { Moves = OriginalMoves; TargetGrowth = OriginalTargetGrowth; ColorCount = OriginalColorCount; }");
        sb.AppendLine();
        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static int TargetScoreAt(int stage)");
        sb.AppendLine("            => (int)(BaseTargetScore * System.Math.Pow(TargetGrowth, stage - 1));");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string Board = """
using System;
using System.Collections.Generic;

namespace __NS__
{
    /// <summary>
    /// 매치3 판. 셀 값은 색 인덱스.
    /// 스왑 -> 매치 판정 -> 제거 -> 낙하 -> 보충 -> 연쇄 판정이 한 사이클이다.
    /// 보충에 쓰는 난수가 결정론적이라, 같은 시드면 판이 똑같이 재현된다.
    /// </summary>
    public sealed class Board
    {
        public readonly int[] Cells = new int[GameData.CellCount];
        readonly DetRandom _random;
        readonly bool[] _marked = new bool[GameData.CellCount];

        public int LastCombo { get; private set; }
        public int LastCleared { get; private set; }

        public Board(DetRandom random)
        {
            _random = random;
            Fill();
        }

        public int At(int x, int y) => Cells[y * GameData.Width + x];
        static int Index(int x, int y) => y * GameData.Width + x;
        static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < GameData.Width && y < GameData.Height;

        /// <summary>시작 판에는 이미 완성된 매치가 없어야 한다. 있으면 그 칸만 다시 뽑는다.</summary>
        void Fill()
        {
            for (int i = 0; i < Cells.Length; i++) Cells[i] = _random.Next(GameData.ColorCount);

            for (int guard = 0; guard < 100; guard++)
            {
                if (!FindMatches()) break;
                for (int i = 0; i < Cells.Length; i++)
                    if (_marked[i]) Cells[i] = _random.Next(GameData.ColorCount);
            }
        }

        /// <summary>가로/세로 3연속 이상을 _marked 에 표시한다.</summary>
        bool FindMatches()
        {
            Array.Clear(_marked, 0, _marked.Length);
            bool any = false;

            for (int y = 0; y < GameData.Height; y++)
            {
                int run = 1;
                for (int x = 1; x <= GameData.Width; x++)
                {
                    bool same = x < GameData.Width && At(x, y) == At(x - 1, y) && At(x, y) >= 0;
                    if (same) { run++; continue; }
                    if (run >= 3) { for (int k = 1; k <= run; k++) _marked[Index(x - k, y)] = true; any = true; }
                    run = 1;
                }
            }

            for (int x = 0; x < GameData.Width; x++)
            {
                int run = 1;
                for (int y = 1; y <= GameData.Height; y++)
                {
                    bool same = y < GameData.Height && At(x, y) == At(x, y - 1) && At(x, y) >= 0;
                    if (same) { run++; continue; }
                    if (run >= 3) { for (int k = 1; k <= run; k++) _marked[Index(x, y - k)] = true; any = true; }
                    run = 1;
                }
            }

            return any;
        }

        public bool IsAdjacent(int a, int b)
        {
            int ax = a % GameData.Width, ay = a / GameData.Width;
            int bx = b % GameData.Width, by = b / GameData.Width;
            return Math.Abs(ax - bx) + Math.Abs(ay - by) == 1;
        }

        void Swap(int a, int b) { (Cells[a], Cells[b]) = (Cells[b], Cells[a]); }

        /// <summary>실제로 바꾸지 않고 매치가 생기는지만 본다.</summary>
        public bool WouldMatch(int a, int b)
        {
            if (!IsAdjacent(a, b)) return false;
            Swap(a, b);
            bool ok = FindMatches();
            Swap(a, b);
            return ok;
        }

        /// <summary>스왑을 확정하고 연쇄가 끝날 때까지 처리한다. 얻은 점수를 돌려준다.</summary>
        public int ApplySwap(int a, int b)
        {
            if (!WouldMatch(a, b)) return 0;
            Swap(a, b);
            return Resolve();
        }

        int Resolve()
        {
            int score = 0;
            LastCombo = 0;
            LastCleared = 0;

            while (FindMatches())
            {
                int cleared = 0;
                for (int i = 0; i < Cells.Length; i++)
                    if (_marked[i]) { Cells[i] = -1; cleared++; }

                // 연쇄가 깊어질수록 점수 배율이 올라간다 — 이 장르의 쾌감 원천.
                double multiplier = 1 + GameData.ComboBonus * LastCombo;
                score += (int)(cleared * GameData.ScorePerTile * multiplier);

                LastCleared += cleared;
                LastCombo++;
                Collapse();
            }

            return score;
        }

        /// <summary>빈 칸을 아래로 메우고 위쪽을 새 색으로 채운다.</summary>
        void Collapse()
        {
            for (int x = 0; x < GameData.Width; x++)
            {
                int write = GameData.Height - 1;
                for (int y = GameData.Height - 1; y >= 0; y--)
                {
                    int v = At(x, y);
                    if (v < 0) continue;
                    Cells[Index(x, write)] = v;
                    write--;
                }
                for (int y = write; y >= 0; y--)
                    Cells[Index(x, y)] = _random.Next(GameData.ColorCount);
            }
        }

        /// <summary>가능한 수를 전부 찾는다. 하나도 없으면 판을 다시 깔아야 한다.</summary>
        public List<(int A, int B)> LegalMoves()
        {
            var moves = new List<(int, int)>();
            for (int y = 0; y < GameData.Height; y++)
            for (int x = 0; x < GameData.Width; x++)
            {
                int a = Index(x, y);
                if (InBounds(x + 1, y) && WouldMatch(a, Index(x + 1, y))) moves.Add((a, Index(x + 1, y)));
                if (InBounds(x, y + 1) && WouldMatch(a, Index(x, y + 1))) moves.Add((a, Index(x, y + 1)));
            }
            return moves;
        }

        public void Reshuffle() => Fill();
    }
}
""";

    const string Stage = """
namespace __NS__
{
    /// <summary>스테이지 진행: 제한된 수 안에 목표 점수를 넘기면 클리어.</summary>
    public sealed class StageSystem : ISimSystem
    {
        public Board Board { get; private set; }

        public int Stage { get; private set; } = 1;
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        public int Cleared { get; private set; }
        public int Failed { get; private set; }
        public bool Finished { get; private set; }

        public int Target => GameData.TargetScoreAt(Stage);

        public void Init(SimWorld world)
        {
            Board = new Board(world.Random);
            StartStage(world);
        }

        public void Tick(SimWorld world, Fx dt) { }

        void StartStage(SimWorld world)
        {
            Score = 0;
            MovesLeft = GameData.Moves;
            world.Raise(new SimEvent(SimEventKind.WaveStarted, "", Stage));
        }

        public bool CanPlay => !Finished && MovesLeft > 0;

        /// <summary>한 수 둔다. 매치가 안 생기는 스왑은 수를 소모하지 않는다.</summary>
        public bool Play(SimWorld world, int a, int b)
        {
            if (!CanPlay) return false;

            int gained = Board.ApplySwap(a, b);
            if (gained <= 0) return false;

            Score += gained;
            MovesLeft--;

            // 둘 수가 사라지면 판을 다시 깐다(정지 상태 방지).
            if (Board.LegalMoves().Count == 0) Board.Reshuffle();

            if (Score >= Target) { Win(world); return true; }
            if (MovesLeft <= 0) Fail(world);
            return true;
        }

        void Win(SimWorld world)
        {
            Cleared++;
            world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(Score));
            world.Raise(new SimEvent(SimEventKind.WaveCleared, "", Stage));

            if (Stage >= GameData.StageCount)
            {
                Finished = true;
                world.Raise(new SimEvent(SimEventKind.Victory, "", Stage));
                world.End();
                return;
            }

            Stage++;
            StartStage(world);
        }

        /// <summary>실패하면 같은 스테이지를 다시 시작한다. 매치3 의 표준 흐름이다.</summary>
        void Fail(SimWorld world)
        {
            Failed++;
            world.Raise(new SimEvent(SimEventKind.GameOver, "", Stage));
            StartStage(world);
        }
    }
}
""";

    const string AutoPlayer = """
namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어. 한 수 앞만 보고 가장 점수가 큰 수를 둔다.
    /// 사람보다 약간 못한 수준이라, 여기서 클리어되는 난이도면 사람도 클리어한다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly StageSystem _stage;

        public AutoPlayer(SimWorld world)
        {
            _world = world;
            _stage = world.Get<StageSystem>();
        }

        /// <summary>한 수 둔다. 둘 수 없으면 false.</summary>
        public bool PlayOne()
        {
            if (!_stage.CanPlay) return false;

            var moves = _stage.Board.LegalMoves();
            if (moves.Count == 0) { _stage.Board.Reshuffle(); return true; }

            // 제거되는 타일 수가 많은 수를 고른다. 연쇄까지 보려면 판 복제가 필요해 여기선 1수만 본다.
            var best = moves[0];
            int bestTiles = 0;

            for (int i = 0; i < moves.Count; i++)
            {
                int tiles = CountCleared(moves[i].A, moves[i].B);
                if (tiles > bestTiles) { bestTiles = tiles; best = moves[i]; }
            }

            return _stage.Play(_world, best.A, best.B);
        }

        /// <summary>해당 스왑이 즉시 제거할 타일 수(연쇄 제외).</summary>
        int CountCleared(int a, int b)
        {
            int ax = a % GameData.Width, ay = a / GameData.Width;
            int bx = b % GameData.Width, by = b / GameData.Width;

            var cells = _stage.Board.Cells;
            (cells[a], cells[b]) = (cells[b], cells[a]);
            int tiles = RunLength(ax, ay) + RunLength(bx, by);
            (cells[a], cells[b]) = (cells[b], cells[a]);
            return tiles;
        }

        int RunLength(int x, int y)
        {
            var board = _stage.Board;
            int color = board.At(x, y);
            int h = 1, v = 1;

            for (int i = x - 1; i >= 0 && board.At(i, y) == color; i--) h++;
            for (int i = x + 1; i < GameData.Width && board.At(i, y) == color; i++) h++;
            for (int i = y - 1; i >= 0 && board.At(x, i) == color; i--) v++;
            for (int i = y + 1; i < GameData.Height && board.At(x, i) == color; i++) v++;

            int total = 0;
            if (h >= 3) total += h;
            if (v >= 3) total += v;
            return total;
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>매치3 화면: 판 + 진행 상황. 인접한 두 칸을 눌러 바꾼다.</summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly StageSystem _stage;
        int _selected = -1;

        public GameUi(SimWorld world)
        {
            _world = world;
            _stage = world.Get<StageSystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;
            ui.ActionHeader = "인접한 두 칸을 눌러 바꾸세요";

            ui.Stats.Add($"스테이지 {_stage.Stage}/{GameData.StageCount}");
            ui.Stats.Add($"점수 {_stage.Score} / {_stage.Target}");
            ui.Stats.Add($"남은 수 {_stage.MovesLeft}    클리어 {_stage.Cleared}   실패 {_stage.Failed}");

            if (_stage.Finished) ui.Banner = "전 스테이지 클리어!";

            var grid = new UiGrid
            {
                Width = GameData.Width,
                Height = GameData.Height,
                Cells = new int[GameData.CellCount],
                Labels = new string[GameData.CellCount],
                OnCell = TapCell,
            };

            for (int i = 0; i < GameData.CellCount; i++)
            {
                grid.Cells[i] = _stage.Board.Cells[i];
                grid.Labels[i] = i == _selected ? "O" : "";
            }
            ui.Grid = grid;

            ui.Actions.Add(new UiAction
            {
                Label = "힌트",
                Sub = "가능한 수를 하나 표시합니다",
                PaletteIndex = 7,
                Execute = ShowHint,
            });
        }

        void TapCell(int index)
        {
            if (_selected < 0) { _selected = index; return; }
            if (_selected == index) { _selected = -1; return; }

            if (_stage.Board.IsAdjacent(_selected, index)) _stage.Play(_world, _selected, index);
            _selected = -1;
        }

        void ShowHint()
        {
            var moves = _stage.Board.LegalMoves();
            _selected = moves.Count > 0 ? moves[0].A : -1;
        }
    }
}
""";

    const string Factory = """
namespace __NS__
{
    /// <summary>게임 한 판을 조립한다. Unity 도 시뮬레이터도 이 함수를 통해서만 시작한다.</summary>
    public static class GameFactory
    {
        public static SimWorld Create(ulong seed = 12345)
        {
            var world = new SimWorld(GameData.TickRate, seed);
            foreach (var r in GameData.Resources) world.Resources.Define(r.Id, r.Start);
            world.Add(new StageSystem());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
