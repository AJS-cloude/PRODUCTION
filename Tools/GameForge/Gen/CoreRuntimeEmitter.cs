using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>장르와 무관한 순수 C# 런타임. UnityEngine 참조가 없어 dotnet 으로 바로 컴파일/테스트된다.</summary>
public static class CoreRuntimeEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("BigNumber.cs", BigNumber);
        Add("Fx.cs", Fx);
        Add("DetRandom.cs", DetRandom);
        Add("SimTypes.cs", SimTypes);
        Add("ResourceLedger.cs", ResourceLedger);
        Add("SimWorld.cs", SimWorld);
        Add("UiModel.cs", UiModel);
    }

    const string UiModel = """
using System;
using System.Collections.Generic;

namespace __NS__
{
    /// <summary>화면에 그릴 버튼 하나. 라벨과 실행만 알고, 유니티는 모른다.</summary>
    public sealed class UiAction
    {
        public string Label = "";
        public string Sub = "";
        public int PaletteIndex;
        public string Icon = "";
        public bool Enabled = true;
        public bool Selected;
        public Action Execute;
    }

    /// <summary>머지/매치3 처럼 판이 있는 게임용. 셀 값은 색 인덱스, -1 이면 빈 칸.</summary>
    public sealed class UiGrid
    {
        public int Width, Height;
        public int[] Cells = Array.Empty<int>();
        public string[] Labels = Array.Empty<string>();
        public Action<int> OnCell;

        public int At(int x, int y) => Cells[y * Width + x];
    }

    /// <summary>타워디펜스처럼 1차원 경로 위에 뭔가 흐르는 게임용.</summary>
    public sealed class UiTrack
    {
        public double Length = 1;
        public List<(double Pos, int Palette)> Markers = new List<(double, int)>();
        public List<(double Pos, int Palette, bool Occupied)> Slots = new List<(double, int, bool)>();
        public Action<int> OnSlot;
    }

    /// <summary>
    /// 한 프레임에 그릴 화면 전체. 장르마다 HUD 를 새로 짜지 않으려고 둔 중간 표현이다.
    /// Core 가 이걸 만들고, 공용 HUD 가 받아서 그린다.
    /// </summary>
    public sealed class UiModel
    {
        public string Title = "";
        public List<string> Stats = new List<string>();
        public List<UiAction> Actions = new List<UiAction>();
        public string ActionHeader = "";
        public UiGrid Grid;
        public UiTrack Track;
        public string Banner = "";        // 게임오버/승리 등 큰 알림
    }

    /// <summary>장르 모듈이 구현한다. 화면 구성의 유일한 통로.</summary>
    public interface IUiProvider
    {
        /// <summary>매 프레임 호출된다. 버튼 개수/순서는 유지하고 내용만 갱신할 것.</summary>
        void BuildUi(UiModel ui);
    }
}
""";

    const string BigNumber = """
using System;

namespace __NS__
{
    /// <summary>방치형 재화용 큰 수. 가수(1~10)+지수로 들고 다녀 오버플로가 없다.</summary>
    public readonly struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
    {
        public readonly double M;   // 정규화된 가수: 0 이거나 1 <= |M| < 10
        public readonly int E;      // 10의 지수

        public static readonly BigNumber Zero = new BigNumber(0, 0);
        public static readonly BigNumber One = new BigNumber(1, 0);

        BigNumber(double m, int e) { M = m; E = e; }

        public static BigNumber Make(double m, int e) => Normalize(m, e);
        public static BigNumber From(double v) => Normalize(v, 0);

        static BigNumber Normalize(double m, int e)
        {
            if (m == 0 || double.IsNaN(m)) return new BigNumber(0, 0);
            if (double.IsInfinity(m)) return new BigNumber(m > 0 ? 1 : -1, int.MaxValue / 2);
            int sign = m < 0 ? -1 : 1;
            double a = Math.Abs(m);
            int shift = (int)Math.Floor(Math.Log10(a));
            a /= Math.Pow(10, shift);
            // 로그 오차 보정
            if (a >= 10) { a /= 10; shift++; }
            else if (a < 1) { a *= 10; shift--; }
            return new BigNumber(sign * a, e + shift);
        }

        public bool IsZero => M == 0;

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;
            if (a.E - b.E > 17) return a;
            if (b.E - a.E > 17) return b;
            int e = Math.Max(a.E, b.E);
            double m = a.M * Math.Pow(10, a.E - e) + b.M * Math.Pow(10, b.E - e);
            return Normalize(m, e);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b) => a + new BigNumber(-b.M, b.E);
        public static BigNumber operator -(BigNumber a) => new BigNumber(-a.M, a.E);
        public static BigNumber operator *(BigNumber a, BigNumber b)
            => a.IsZero || b.IsZero ? Zero : Normalize(a.M * b.M, a.E + b.E);
        public static BigNumber operator *(BigNumber a, double k) => a * From(k);
        public static BigNumber operator /(BigNumber a, BigNumber b)
            => b.IsZero ? Zero : Normalize(a.M / b.M, a.E - b.E);
        public static BigNumber operator /(BigNumber a, double k) => a / From(k);

        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator ==(BigNumber a, BigNumber b) => a.Equals(b);
        public static bool operator !=(BigNumber a, BigNumber b) => !a.Equals(b);

        public int CompareTo(BigNumber o)
        {
            if (IsZero && o.IsZero) return 0;
            bool ns = M < 0, os = o.M < 0;
            if (ns != os) return ns ? -1 : 1;
            int sign = ns ? -1 : 1;
            if (E != o.E) return E > o.E ? sign : -sign;
            return M.CompareTo(o.M);
        }

        public bool Equals(BigNumber o) => E == o.E && Math.Abs(M - o.M) < 1e-12;
        public override bool Equals(object obj) => obj is BigNumber b && Equals(b);
        public override int GetHashCode() => M.GetHashCode() ^ E;

        /// <summary>지수가 작을 때만 double 로. 큰 수는 포화된다.</summary>
        public double ToDouble() => E > 300 ? double.MaxValue : M * Math.Pow(10, E);

        /// <summary>성장 곡선용: b^exp 를 큰 수로.</summary>
        public static BigNumber Pow(double b, double exp)
        {
            if (b <= 0) return Zero;
            double log = exp * Math.Log10(b);
            int e = (int)Math.Floor(log);
            return Normalize(Math.Pow(10, log - e), e);
        }

        static readonly string[] Short = { "", "K", "M", "B", "T" };

        public override string ToString()
        {
            if (IsZero) return "0";
            if (E < 3) { double v = ToDouble(); return Math.Abs(v) < 10 ? v.ToString("0.#") : v.ToString("0"); }
            int tier = E / 3;
            double mant = M * Math.Pow(10, E - tier * 3);
            string suffix = tier < Short.Length ? Short[tier] : Letters(tier - Short.Length);
            return mant.ToString("0.##") + suffix;
        }

        // 5번째 자리부터 aa, ab, ... az, ba ...
        static string Letters(int i)
        {
            const int N = 26;
            char hi = (char)('a' + i / N % N);
            char lo = (char)('a' + i % N);
            return string.Concat(hi, lo);
        }
    }
}
""";

    const string Fx = """
using System;

namespace __NS__
{
    /// <summary>결정론 전투용 고정소수점(Q16.16). 플랫폼이 달라도 결과가 같다.</summary>
    public readonly struct Fx : IComparable<Fx>
    {
        public const int Shift = 16;
        const long OneRaw = 1L << Shift;
        public readonly long Raw;

        Fx(long raw) { Raw = raw; }

        public static readonly Fx Zero = new Fx(0);
        public static readonly Fx One = new Fx(OneRaw);

        public static Fx FromRaw(long raw) => new Fx(raw);
        public static Fx FromInt(int v) => new Fx((long)v << Shift);
        /// <summary>스펙(JSON)에서 읽은 값을 고정소수점으로 굳힌다. double 은 여기서만 쓴다.</summary>
        public static Fx FromDouble(double v) => new Fx((long)Math.Round(v * OneRaw));

        public double ToDouble() => (double)Raw / OneRaw;
        public int ToInt() => (int)(Raw >> Shift);

        public static Fx operator +(Fx a, Fx b) => new Fx(a.Raw + b.Raw);
        public static Fx operator -(Fx a, Fx b) => new Fx(a.Raw - b.Raw);
        public static Fx operator -(Fx a) => new Fx(-a.Raw);
        public static Fx operator *(Fx a, Fx b) => new Fx((a.Raw * b.Raw) >> Shift);
        public static Fx operator /(Fx a, Fx b) => b.Raw == 0 ? Zero : new Fx((a.Raw << Shift) / b.Raw);
        public static Fx operator *(Fx a, int k) => new Fx(a.Raw * k);

        public static bool operator >(Fx a, Fx b) => a.Raw > b.Raw;
        public static bool operator <(Fx a, Fx b) => a.Raw < b.Raw;
        public static bool operator >=(Fx a, Fx b) => a.Raw >= b.Raw;
        public static bool operator <=(Fx a, Fx b) => a.Raw <= b.Raw;

        public static Fx Min(Fx a, Fx b) => a.Raw < b.Raw ? a : b;
        public static Fx Max(Fx a, Fx b) => a.Raw > b.Raw ? a : b;
        public static Fx Abs(Fx a) => new Fx(Math.Abs(a.Raw));

        public static Fx Sqrt(Fx a)
        {
            if (a.Raw <= 0) return Zero;
            long x = a.Raw << Shift;
            long r = 0, bit = 1L << 62;
            while (bit > x) bit >>= 2;
            while (bit != 0)
            {
                if (x >= r + bit) { x -= r + bit; r = (r >> 1) + bit; }
                else r >>= 1;
                bit >>= 2;
            }
            return new Fx(r);
        }

        public int CompareTo(Fx o) => Raw.CompareTo(o.Raw);
        public override string ToString() => ToDouble().ToString("0.###");
    }
}
""";

    const string DetRandom = """
namespace __NS__
{
    /// <summary>시드가 같으면 항상 같은 수열. 리플레이/시뮬 재현에 쓴다.</summary>
    public sealed class DetRandom
    {
        ulong _s;
        public DetRandom(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextULong()
        {
            _s ^= _s >> 12; _s ^= _s << 25; _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>[0, max) 범위 정수.</summary>
        public int Next(int max) => max <= 0 ? 0 : (int)(NextULong() % (ulong)max);
        public int Range(int min, int max) => min + Next(max - min);
        /// <summary>[0,1) 고정소수점. double 을 거치지 않는다.</summary>
        public Fx Unit() => Fx.FromRaw((long)(NextULong() >> 48));
        public bool Chance(Fx p) => Unit() < p;
    }
}
""";

    const string SimTypes = """
namespace __NS__
{
    /// <summary>고정 스텝으로 도는 시뮬 시스템. Unity 의 Update 와 분리되어 있다.</summary>
    public interface ISimSystem
    {
        void Init(SimWorld world);
        void Tick(SimWorld world, Fx dt);
    }

    /// <summary>Unity 뷰가 구독하는 이벤트. Core 는 뷰를 모른다.</summary>
    public enum SimEventKind
    {
        ResourceChanged, GeneratorBought, UpgradeBought,
        WaveStarted, WaveCleared, EnemySpawned, EnemyKilled, EnemyLeaked,
        TowerBuilt, GameOver, Victory
    }

    public readonly struct SimEvent
    {
        public readonly SimEventKind Kind;
        public readonly string Id;
        public readonly int IntValue;
        public SimEvent(SimEventKind kind, string id = "", int intValue = 0)
        { Kind = kind; Id = id; IntValue = intValue; }
    }
}
""";

    const string ResourceLedger = """
using System.Collections.Generic;

namespace __NS__
{
    /// <summary>모든 재화의 보유량. 소비는 반드시 TrySpend 를 거친다.</summary>
    public sealed class ResourceLedger
    {
        readonly Dictionary<string, BigNumber> _amounts = new Dictionary<string, BigNumber>();
        readonly Dictionary<string, BigNumber> _lifetime = new Dictionary<string, BigNumber>();

        public IReadOnlyDictionary<string, BigNumber> All => _amounts;

        public void Define(string id, double start)
        {
            _amounts[id] = BigNumber.From(start);
            _lifetime[id] = BigNumber.From(start);
        }

        public BigNumber Get(string id) => _amounts.TryGetValue(id, out var v) ? v : BigNumber.Zero;
        public BigNumber Lifetime(string id) => _lifetime.TryGetValue(id, out var v) ? v : BigNumber.Zero;

        public void Add(string id, BigNumber amount)
        {
            if (amount.IsZero) return;
            _amounts[id] = Get(id) + amount;
            if (amount > BigNumber.Zero) _lifetime[id] = Lifetime(id) + amount;
        }

        public bool CanAfford(string id, BigNumber cost) => Get(id) >= cost;

        public bool TrySpend(string id, BigNumber cost)
        {
            if (!CanAfford(id, cost)) return false;
            _amounts[id] = Get(id) - cost;
            return true;
        }
    }
}
""";

    const string SimWorld = """
using System;
using System.Collections.Generic;

namespace __NS__
{
    /// <summary>
    /// 게임 로직 전체를 담는 컨테이너. Unity 없이 단독으로 돌기 때문에
    /// 밸런스 시뮬레이터가 이걸 그대로 수만 번 돌릴 수 있다.
    /// </summary>
    public sealed class SimWorld
    {
        public readonly ResourceLedger Resources = new ResourceLedger();
        public readonly DetRandom Random;
        public readonly Fx TickDelta;
        public readonly int TickRate;

        readonly List<ISimSystem> _systems = new List<ISimSystem>();
        readonly List<SimEvent> _events = new List<SimEvent>();

        public long Tick { get; private set; }
        public double ElapsedSeconds => (double)Tick / TickRate;
        public bool IsOver { get; private set; }

        public event Action<SimEvent> OnEvent;

        public SimWorld(int tickRate, ulong seed)
        {
            TickRate = tickRate <= 0 ? 20 : tickRate;
            TickDelta = Fx.One / Fx.FromInt(TickRate);
            Random = new DetRandom(seed);
        }

        public T Add<T>(T system) where T : ISimSystem { _systems.Add(system); return system; }

        public T Get<T>() where T : class, ISimSystem
        {
            for (int i = 0; i < _systems.Count; i++) if (_systems[i] is T t) return t;
            return null;
        }

        public void Init() { for (int i = 0; i < _systems.Count; i++) _systems[i].Init(this); }

        public void Step()
        {
            if (IsOver) return;
            Tick++;
            for (int i = 0; i < _systems.Count; i++) _systems[i].Tick(this, TickDelta);
            FlushEvents();
        }

        /// <summary>초 단위로 진행. 오프라인 보상 계산도 같은 경로를 쓴다.</summary>
        public void Advance(double seconds)
        {
            int steps = (int)Math.Round(seconds * TickRate);
            for (int i = 0; i < steps && !IsOver; i++) Step();
        }

        public void Raise(SimEvent e) => _events.Add(e);
        public void End() => IsOver = true;

        void FlushEvents()
        {
            if (_events.Count == 0 || OnEvent == null) { _events.Clear(); return; }
            for (int i = 0; i < _events.Count; i++) OnEvent(_events[i]);
            _events.Clear();
        }
    }
}
""";
}
