using System.Globalization;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>
/// Unity 뷰 레이어. 인스펙터 드래그 배선이 없도록 화면을 런타임에 전부 만든다.
///
/// 기본은 2D(sprite2d): 판과 경로는 월드에 스프라이트로 그리고, 상단 정보와 하단 버튼만 UI 로 올린다.
/// 실제 2D 모바일 게임이 쓰는 구성이다. view = "ui" 로 두면 전부 UI 로만 그린다.
/// </summary>
public static class UnityEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir, string unityDir, string editorDir)
    {
        var core = spec.SafeName + ".Core";
        var runtime = spec.SafeName + ".Runtime";
        bool world = spec.View != "ui";

        e.Add($"{coreDir}/{core}.asmdef", Asmdef(core, noEngine: true, refs: ""), header: false);
        e.Add($"{unityDir}/{runtime}.asmdef", Asmdef(runtime, noEngine: false, refs: $"\"{core}\""), header: false);
        e.Add($"{editorDir}/{spec.SafeName}.Editor.asmdef",
            AsmdefEditor($"{spec.SafeName}.Editor", $"\"{core}\", \"{runtime}\""), header: false);

        void Add(string file, string body)
            => e.Add($"{unityDir}/{file}",
                WithNamespace(
                    body.Replace("__NS__", core)
                        .Replace("__GAME__", spec.SafeName)
                        .Replace("__DISPLAY__", spec.DisplayName)
                        .Replace("__WORLD__", world ? "true" : "false")
                        .Replace("__PPU__", spec.Art.PixelsPerUnit.ToString(CultureInfo.InvariantCulture))
                        .Replace("__CAMSIZE__", spec.Art.CameraSize.ToString("R", CultureInfo.InvariantCulture))
                        // view = "ui" 면 WorldView 자체를 만들지 않으므로 참조도 남기면 안 된다.
                        .Replace("__ADDWORLD__", world ? "        gameObject.AddComponent<WorldView>();" : ""),
                    spec.SafeName + ".Unity"));

        Add("ViewConfig.cs", ViewConfig);
        Add("GameRunner.cs", Runner);
        Add("PlaceholderArt.cs", PlaceholderArt);
        Add("UiKit.cs", UiKit);
        Add("TitleScreen.cs", TitleScreen);
        Add("Hud.cs", Hud);
        if (world) Add("WorldView.cs", WorldView);
    }

    /// <summary>
    /// 생성된 뷰 타입(GameRunner, Hud ...)을 게임별 네임스페이스에 넣는다.
    /// 한 유니티 프로젝트에서 여러 게임을 생성하면 전역 이름이 충돌하기 때문이다.
    /// </summary>
    /// <remarks>
    /// 유니티는 C# 9 까지만 지원한다. 파일 범위 네임스페이스(C# 10)를 쓰면
    /// CS8773 로 컴파일이 깨지므로 반드시 중괄호 블록으로 감싼다.
    /// </remarks>
    internal static string WithNamespace(string body, string ns)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');

        // using 들 바로 다음부터 네임스페이스 블록을 연다.
        int openAt = 0;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("using ")) openAt = i + 1;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == openAt) sb.Append("\nnamespace ").Append(ns).Append("\n{\n");
            if (i < openAt) sb.Append(lines[i]).Append('\n');
            else sb.Append(Indent(lines[i])).Append('\n');
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    static string Indent(string line) => line.Length == 0 ? line : "    " + line;

    static string Asmdef(string name, bool noEngine, string refs) => $$"""
{
    "name": "{{name}}",
    "rootNamespace": "",
    "references": [{{refs}}],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": {{(noEngine ? "true" : "false")}}
}
""";

    static string AsmdefEditor(string name, string refs) => $$"""
{
    "name": "{{name}}",
    "rootNamespace": "",
    "references": [{{refs}}],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
""";

    const string ViewConfig = """
using UnityEngine;

/// <summary>
/// 화면 배치 상수. HUD 와 월드 뷰가 같은 값을 봐야 겹치지 않으므로 한곳에 모아둔다.
/// 화면 세로를 0(아래)~1(위) 로 보고 나눈다.
/// </summary>
public static class ViewConfig
{
    /// <summary>판/경로를 월드 스프라이트로 그릴지. false 면 전부 UI 로 그린다.</summary>
    public const bool WorldRendering = __WORLD__;

    /// <summary>씬 이름. SceneBuilder 가 만드는 씬 파일명과 반드시 일치해야 한다.</summary>
    public const string TitleSceneName = "__GAME__Title";
    public const string GameSceneName = "__GAME__Game";
    public const string DisplayName = "__DISPLAY__";

    public const float PixelsPerUnit = __PPU__f;
    public const float CameraSize = __CAMSIZE__f;

    public const float TopBarBottom = 0.86f;    // 상단 정보 영역 아래 경계
    public const float ActionTop = 0.45f;       // 하단 버튼 영역 위 경계

    public const float WorldBottom = ActionTop;
    public const float WorldTop = TopBarBottom;

    /// <summary>월드 영역의 화면 중심(0~1)과 높이 비율.</summary>
    public const float WorldCenterY = (WorldBottom + WorldTop) * 0.5f;
    public const float WorldHeightRatio = WorldTop - WorldBottom;

    /// <summary>좌우 여백을 뺀 사용 가능 폭 비율.</summary>
    public const float WorldWidthRatio = 0.94f;

    /// <summary>월드 영역을 카메라 기준 사각형(중심, 크기)으로 환산한다.</summary>
    public static void WorldArea(Camera cam, out Vector2 center, out Vector2 size)
    {
        float half = cam != null && cam.orthographic ? cam.orthographicSize : CameraSize;
        float aspect = cam != null ? cam.aspect : 9f / 16f;

        size = new Vector2(half * 2f * aspect * WorldWidthRatio, half * 2f * WorldHeightRatio);
        center = new Vector2(0f, (WorldCenterY - 0.5f) * half * 2f);
    }
}
""";

    const string TitleScreen = """
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬. 게임 이름과 시작 안내를 그리고, 화면 어디든 누르면 게임 씬으로 넘어간다.
/// 씬에 이 컴포넌트 하나만 있으면 성립한다(UI 는 코드가 만든다).
/// </summary>
public sealed class TitleScreen : MonoBehaviour
{
    Text _prompt;
    float _time;
    bool _loading;

    void Start()
    {
        var canvas = UiKit.CreateCanvas("Title");
        UiKit.Panel(canvas.transform, "BG", Vector2.zero, Vector2.one, new Color(0.06f, 0.07f, 0.1f));
        var root = UiKit.SafeArea(canvas);

        var title = UiKit.Label(root, "Title", ViewConfig.DisplayName, 88, TextAnchor.MiddleCenter);
        Anchor(title.transform, new Vector2(0f, 0.55f), new Vector2(1f, 0.8f));

        _prompt = UiKit.Label(root, "Prompt", "화면을 누르면 시작", 40, TextAnchor.MiddleCenter);
        Anchor(_prompt.transform, new Vector2(0f, 0.18f), new Vector2(1f, 0.28f));

        var version = UiKit.Label(root, "Version", "v" + Application.version, 22, TextAnchor.MiddleRight);
        Anchor(version.transform, new Vector2(0f, 0f), new Vector2(0.98f, 0.05f));
        version.color = new Color(1f, 1f, 1f, 0.4f);

        // 화면 전체를 덮는 투명 버튼 — 입력 설정(레거시/신형)과 무관하게 EventSystem 으로 받는다.
        // UiKit.Panel 은 투명이면 레이캐스트를 꺼 버리므로 직접 만든다.
        var tap = new GameObject("TapToStart", typeof(Image), typeof(Button));
        tap.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)tap.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = tap.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        tap.GetComponent<Button>().onClick.AddListener(StartGame);
    }

    void Update()
    {
        if (_prompt == null) return;
        _time += Time.unscaledDeltaTime;
        var c = _prompt.color;
        c.a = 0.45f + 0.4f * Mathf.Sin(_time * 4f);
        _prompt.color = c;
    }

    void StartGame()
    {
        if (_loading) return;
        _loading = true;
        SceneManager.LoadScene(ViewConfig.GameSceneName);
    }

    static void Anchor(Transform t, Vector2 min, Vector2 max)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
""";

    const string Runner = """
using UnityEngine;
using __NS__;

/// <summary>
/// Core 시뮬을 고정 스텝으로 돌리고, 매 프레임 화면 모델을 한 번 만든다.
/// HUD 와 월드 뷰는 LateUpdate 에서 그 결과를 읽기만 한다(빌드 순서를 확정하기 위함).
/// 씬에 이 컴포넌트 하나만 있으면 게임이 성립한다.
/// </summary>
public sealed class GameRunner : MonoBehaviour
{
    public static GameRunner Instance { get; private set; }

    public SimWorld World { get; private set; }
    public IUiProvider UiProvider { get; private set; }

    /// <summary>이번 프레임의 화면 모델. 뷰들이 공유한다.</summary>
    public readonly UiModel Ui = new UiModel();

    [SerializeField] ulong seed = 20260820;
    [SerializeField, Range(1, 20)] int maxCatchUpSteps = 8;

    float _accumulator;
    float _stepSeconds;

    void Awake()
    {
        Instance = this;
        // 모바일 기본값(30fps)을 60 으로. 세로 모바일 타겟 공통 설정.
        Application.targetFrameRate = 60;
        World = GameFactory.Create(seed);
        UiProvider = GameFactory.CreateUi(World);
        _stepSeconds = 1f / GameData.TickRate;

        SetupCamera();
        gameObject.AddComponent<Hud>();
__ADDWORLD__
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = ViewConfig.CameraSize;
    }

    void Update()
    {
        if (World == null) return;

        if (!World.IsOver)
        {
            _accumulator += Time.deltaTime;
            int steps = 0;
            while (_accumulator >= _stepSeconds && steps < maxCatchUpSteps)
            {
                World.Step();
                _accumulator -= _stepSeconds;
                steps++;
            }
            // 너무 밀렸으면 따라잡기를 포기한다(스파이럴 방지).
            if (steps >= maxCatchUpSteps) _accumulator = 0f;
        }

        BuildUi();
    }

    void BuildUi()
    {
        if (UiProvider == null) return;
        Ui.Stats.Clear();
        Ui.Actions.Clear();
        Ui.Banner = "";
        Ui.Grid = null;
        Ui.Track = null;
        UiProvider.BuildUi(Ui);
    }

    /// <summary>앱이 꺼져 있던 시간만큼 한 번에 진행 — 오프라인 보상.</summary>
    public void ApplyOfflineProgress(double seconds)
    {
        if (World == null || seconds <= 0) return;
        World.Advance(System.Math.Min(seconds, 86400));
    }
}
""";

    const string PlaceholderArt = """
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아트가 없어도 게임이 굴러가도록 스프라이트를 코드로 만든다.
/// Resources 아래에 같은 이름의 이미지를 넣으면 그쪽이 자동으로 우선한다.
/// 하위 폴더(Icons/, Sprites/, Backgrounds/)를 순서대로 뒤지므로
/// 기획서에는 폴더 없이 파일명만 적으면 된다.
/// </summary>
public static class PlaceholderArt
{
    static readonly string[] SearchFolders = { "Icons/", "Sprites/", "Backgrounds/", "" };
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string iconName, int paletteIndex)
    {
        string key = string.IsNullOrEmpty(iconName) ? "auto_" + paletteIndex : iconName;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var sprite = Load(iconName) ?? Generate(paletteIndex);
        Cache[key] = sprite;
        return sprite;
    }

    static Sprite Load(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;

        for (int i = 0; i < SearchFolders.Length; i++)
        {
            string path = SearchFolders[i] + iconName;

            // 스프라이트로 임포트된 이미지가 우선. (Texture Type = Sprite)
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            // Default 로 임포트됐어도 굴러가게 텍스처에서 만들어 준다.
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), ViewConfig.PixelsPerUnit);
        }
        return null;
    }

    /// <summary>팔레트를 황금비로 돌려 인접 항목끼리 색이 겹치지 않게 한다.</summary>
    public static Color Color(int index)
        => UnityEngine.Color.HSVToRGB((index * 0.618034f) % 1f, 0.55f, 0.85f);

    static Sprite Generate(int paletteIndex)
    {
        const int Size = 64;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var fill = Color(paletteIndex);
        var edge = fill * 0.6f; edge.a = 1f;

        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool border = x < 3 || y < 3 || x >= Size - 3 || y >= Size - 3;
            pixels[y * Size + x] = border ? (Color32)edge : (Color32)fill;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), ViewConfig.PixelsPerUnit);
    }
}
""";

    const string UiKit = """
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>UI 를 코드로 짓기 위한 최소 도구. 프리팹도 인스펙터 배선도 필요 없다.</summary>
public static class UiKit
{
    static Font _font;
    public static Font Font => _font != null
        ? _font
        : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Canvas CreateCanvas(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        go.AddComponent<AdaptiveScaler>();
        return canvas;
    }

    /// <summary>노치/펀치홀을 피하는 콘텐츠 루트. BG 처럼 화면 전체를 덮을 것만 캔버스에 직접 붙이고,
    /// 나머지 UI 는 전부 이 아래에 둔다.</summary>
    public static RectTransform SafeArea(Canvas canvas)
    {
        var go = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = color;
        // 투명 패널이 월드 클릭을 가로채지 않도록 한다.
        image.raycastTarget = color.a > 0.01f;
        return rt;
    }

    public static VerticalLayoutGroup VerticalList(Transform parent, string name, float spacing = 8f)
    {
        var go = new GameObject(name, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(16, 0); rt.offsetMax = new Vector2(-16, 0);

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return layout;
    }

    public static Text Label(Transform parent, string name, string text, int size = 32,
        TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.text = text;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static Image Icon(Transform parent, string name, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = size; le.preferredHeight = size;
        le.minWidth = size; le.minHeight = size;
        return img;
    }

    /// <summary>아이콘 + 제목/부제가 한 줄인 표준 버튼.</summary>
    public static Button Row(Transform parent, string name, Sprite sprite, int paletteIndex,
        out Text title, out Text subtitle, UnityAction onClick, float height = 132f)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.08f);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16;
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childForceExpandWidth = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        Icon(go.transform, "Icon", sprite, height - 32f);

        var textCol = new GameObject("Text", typeof(VerticalLayoutGroup), typeof(LayoutElement));
        textCol.transform.SetParent(go.transform, false);
        textCol.GetComponent<LayoutElement>().flexibleWidth = 1;
        var col = textCol.GetComponent<VerticalLayoutGroup>();
        col.childControlHeight = true; col.childControlWidth = true;
        col.childForceExpandHeight = false;

        title = Label(textCol.transform, "Title", "", 34);
        subtitle = Label(textCol.transform, "Subtitle", "", 26);
        subtitle.color = new Color(1, 1, 1, 0.65f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }
}

/// <summary>화면비에 따라 CanvasScaler 기준 축을 고른다.
/// 기준(9:16)보다 길쭉한 폰은 가로 1080 을 유지해 세로 여유만 늘어나고,
/// 태블릿(4:3)으로 갈수록 세로 기준으로 옮겨 UI 가 화면 밖으로 밀리지 않게 한다.</summary>
public sealed class AdaptiveScaler : MonoBehaviour
{
    const float PhoneAspect = 16f / 9f;   // referenceResolution 1080x1920 과 같은 비율
    const float TabletAspect = 4f / 3f;

    CanvasScaler _scaler;
    int _w, _h;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Update()
    {
        if (Screen.width != _w || Screen.height != _h) Apply();
    }

    void Apply()
    {
        _w = Screen.width; _h = Screen.height;
        if (_w <= 0 || _h <= 0 || _scaler == null) return;
        float aspect = _h / (float)_w;   // 세로 화면에서 1보다 크다
        _scaler.matchWidthOrHeight = aspect >= PhoneAspect
            ? 0f
            : Mathf.InverseLerp(PhoneAspect, TabletAspect, aspect);
    }
}

/// <summary>안드로이드 노치/펀치홀을 피해 자기 RectTransform 을 Screen.safeArea 에 맞춘다.
/// 회전/폴더블 접힘 등으로 safeArea 가 바뀌면 즉시 따라간다.</summary>
public sealed class SafeAreaFitter : MonoBehaviour
{
    Rect _applied = new Rect(-1f, -1f, -1f, -1f);

    void OnEnable() { Apply(); }

    void Update()
    {
        if (Screen.safeArea != _applied) Apply();
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        _applied = Screen.safeArea;
        var rt = (RectTransform)transform;
        Vector2 min = _applied.position;
        Vector2 max = _applied.position + _applied.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
""";

    const string Hud = """
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using __NS__;

/// <summary>
/// 장르와 무관한 공용 HUD. GameRunner 가 만든 UiModel 만 보고 그린다.
/// 2D 모드에서는 판/경로를 WorldView 가 맡고, 여기서는 정보 줄과 버튼만 그린다.
/// </summary>
public sealed class Hud : MonoBehaviour
{
    Text _title, _banner, _actionHeader;
    Transform _statRoot, _actionRoot, _gridRoot, _trackRoot;

    readonly List<Text> _statLabels = new List<Text>();
    readonly List<Button> _actionButtons = new List<Button>();
    readonly List<Text> _actionTitles = new List<Text>();
    readonly List<Text> _actionSubs = new List<Text>();
    readonly List<Image> _actionIcons = new List<Image>();
    readonly List<Image> _cells = new List<Image>();
    readonly List<Text> _cellLabels = new List<Text>();
    readonly List<Image> _markers = new List<Image>();
    readonly List<Image> _slots = new List<Image>();

    static UiModel Ui => GameRunner.Instance != null ? GameRunner.Instance.Ui : null;

    void Start()
    {
        var canvas = UiKit.CreateCanvas("HUD");

        // 2D 모드에서는 배경을 카메라가 칠하므로 전체 배경 패널을 깔지 않는다.
        // BG 는 노치 뒤까지 화면 전체를 덮어야 하므로 SafeArea 밖(캔버스 직속)에 둔다.
        if (!ViewConfig.WorldRendering)
            UiKit.Panel(canvas.transform, "BG", Vector2.zero, Vector2.one, new Color(0.07f, 0.08f, 0.11f));

        // 나머지 UI 는 전부 SafeArea 아래 — 안드로이드 노치/펀치홀을 피한다.
        var root = UiKit.SafeArea(canvas);

        var top = UiKit.Panel(root, "Top",
            new Vector2(0, ViewConfig.TopBarBottom), new Vector2(1, 1), new Color(0, 0, 0, 0.35f));
        var topList = UiKit.VerticalList(top, "Info", 2).transform;
        _title = UiKit.Label(topList, "Title", "", 40, TextAnchor.MiddleCenter);
        _statRoot = topList;

        if (!ViewConfig.WorldRendering)
        {
            _gridRoot = UiKit.Panel(root, "Grid",
                new Vector2(0.04f, ViewConfig.WorldBottom + 0.01f),
                new Vector2(0.96f, ViewConfig.WorldTop - 0.01f), Color.clear);
            _trackRoot = UiKit.Panel(root, "Track",
                new Vector2(0.03f, ViewConfig.WorldCenterY - 0.04f),
                new Vector2(0.97f, ViewConfig.WorldCenterY + 0.04f), Color.clear);
        }

        var bottom = UiKit.Panel(root, "Actions",
            Vector2.zero, new Vector2(1, ViewConfig.ActionTop), new Color(0, 0, 0, 0.55f));
        var bottomList = UiKit.VerticalList(bottom, "List").transform;
        _actionHeader = UiKit.Label(bottomList, "Header", "", 28, TextAnchor.MiddleCenter);
        _actionRoot = bottomList;

        _banner = UiKit.Label(root, "Banner", "", 56, TextAnchor.MiddleCenter);
        var brt = (RectTransform)_banner.transform;
        brt.anchorMin = new Vector2(0, ViewConfig.WorldCenterY - 0.07f);
        brt.anchorMax = new Vector2(1, ViewConfig.WorldCenterY + 0.07f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
    }

    // GameRunner.Update 가 UiModel 을 만든 뒤에 읽는다.
    void LateUpdate()
    {
        var ui = Ui;
        if (ui == null) return;

        _title.text = ui.Title;
        _banner.text = ui.Banner;
        _actionHeader.text = ui.ActionHeader;

        SyncStats(ui);
        SyncActions(ui);

        if (ViewConfig.WorldRendering) return;
        SyncGrid(ui);
        SyncTrack(ui);
    }

    void SyncStats(UiModel ui)
    {
        while (_statLabels.Count < ui.Stats.Count)
            _statLabels.Add(UiKit.Label(_statRoot, "Stat" + _statLabels.Count, "", 32, TextAnchor.MiddleCenter));

        for (int i = 0; i < _statLabels.Count; i++)
        {
            bool on = i < ui.Stats.Count;
            _statLabels[i].gameObject.SetActive(on);
            if (on) _statLabels[i].text = ui.Stats[i];
        }
    }

    void SyncActions(UiModel ui)
    {
        while (_actionButtons.Count < ui.Actions.Count)
        {
            int index = _actionButtons.Count;
            var btn = UiKit.Row(_actionRoot, "Action" + index, null, index,
                out var title, out var sub, () => Invoke(index), 112f);
            _actionButtons.Add(btn);
            _actionTitles.Add(title);
            _actionSubs.Add(sub);
            _actionIcons.Add(btn.transform.GetChild(0).GetComponent<Image>());
        }

        for (int i = 0; i < _actionButtons.Count; i++)
        {
            bool on = i < ui.Actions.Count;
            _actionButtons[i].gameObject.SetActive(on);
            if (!on) continue;

            var a = ui.Actions[i];
            _actionTitles[i].text = (a.Selected ? "> " : "") + a.Label;
            _actionSubs[i].text = a.Sub;
            _actionButtons[i].interactable = a.Enabled;
            _actionIcons[i].sprite = PlaceholderArt.Get(a.Icon, a.PaletteIndex);
        }
    }

    /// <summary>버튼 클릭은 항상 그 프레임의 최신 UiModel 을 통해 실행한다.</summary>
    void Invoke(int index)
    {
        var ui = Ui;
        if (ui == null || index < 0 || index >= ui.Actions.Count) return;
        var execute = ui.Actions[index].Execute;
        if (execute != null) execute();
    }

    void SyncGrid(UiModel ui)
    {
        var g = ui.Grid;
        int need = g == null ? 0 : g.Width * g.Height;

        while (_cells.Count < need)
        {
            int index = _cells.Count;
            var go = new GameObject("Cell" + index, typeof(Image), typeof(Button));
            go.transform.SetParent(_gridRoot, false);
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                var model = Ui;
                if (model != null && model.Grid != null && model.Grid.OnCell != null) model.Grid.OnCell(index);
            });
            _cells.Add(go.GetComponent<Image>());

            var label = UiKit.Label(go.transform, "L", "", 26, TextAnchor.MiddleCenter);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _cellLabels.Add(label);
        }

        for (int i = 0; i < _cells.Count; i++)
        {
            bool on = i < need;
            _cells[i].gameObject.SetActive(on);
            if (!on) continue;

            int x = i % g.Width, y = i / g.Width;
            var rt = (RectTransform)_cells[i].transform;
            float w = 1f / g.Width, h = 1f / g.Height;
            // y 는 위에서부터 세는 게 판을 읽기 편하다.
            rt.anchorMin = new Vector2(x * w, 1f - (y + 1) * h);
            rt.anchorMax = new Vector2((x + 1) * w, 1f - y * h);
            rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);

            int v = g.Cells[i];
            _cells[i].color = v < 0 ? new Color(1, 1, 1, 0.08f) : PlaceholderArt.Color(v);
            _cellLabels[i].text = g.Labels != null && i < g.Labels.Length ? g.Labels[i] : "";
        }
    }

    void SyncTrack(UiModel ui)
    {
        var t = ui.Track;
        _trackRoot.gameObject.SetActive(t != null);
        if (t == null) return;

        while (_slots.Count < t.Slots.Count)
        {
            int index = _slots.Count;
            var go = new GameObject("Slot" + index, typeof(Image), typeof(Button));
            go.transform.SetParent(_trackRoot, false);
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                var model = Ui;
                if (model != null && model.Track != null && model.Track.OnSlot != null) model.Track.OnSlot(index);
            });
            _slots.Add(go.GetComponent<Image>());
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool on = i < t.Slots.Count;
            _slots[i].gameObject.SetActive(on);
            if (!on) continue;

            var slot = t.Slots[i];
            Place(_slots[i], slot.Pos / t.Length, 26f, -18f);
            _slots[i].color = slot.Occupied ? PlaceholderArt.Color(slot.Palette) : new Color(1, 1, 1, 0.15f);
        }

        while (_markers.Count < t.Markers.Count)
        {
            var go = new GameObject("Marker" + _markers.Count, typeof(Image));
            go.transform.SetParent(_trackRoot, false);
            _markers.Add(go.GetComponent<Image>());
        }

        for (int i = 0; i < _markers.Count; i++)
        {
            bool on = i < t.Markers.Count;
            _markers[i].gameObject.SetActive(on);
            if (!on) continue;

            var marker = t.Markers[i];
            Place(_markers[i], marker.Pos / t.Length, 24f, 14f);
            _markers[i].color = PlaceholderArt.Color(marker.Palette);
        }
    }

    static void Place(Image img, double t01, float size, float yOffset)
    {
        var rt = (RectTransform)img.transform;
        float x = Mathf.Clamp01((float)t01);
        rt.anchorMin = new Vector2(x, 0.5f);
        rt.anchorMax = new Vector2(x, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(size, size);
    }
}
""";

    const string WorldView = """
using System.Collections.Generic;
using UnityEngine;
using __NS__;

/// <summary>
/// 2D 월드 뷰. 판(Grid)과 경로(Track)를 SpriteRenderer 로 그린다.
/// 화면 좌표가 아니라 월드 좌표에 놓이므로 카메라 연출·파티클·정렬을 정상적으로 쓸 수 있다.
/// 상단 정보와 하단 버튼은 Hud 가 UI 로 그린다 — 실제 2D 모바일 게임의 구성이다.
/// </summary>
public sealed class WorldView : MonoBehaviour
{
    const int GridOrder = 0;
    const int TrackOrder = 5;
    const int MarkerOrder = 10;

    Camera _cam;
    Transform _root;
    TextMesh[] _cellLabels = new TextMesh[0];

    readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
    SpriteRenderer _trackLine;

    // 이번 프레임의 판 배치. 클릭 판정에 그대로 쓴다.
    Vector2 _gridOrigin;
    float _cellSize;
    int _gridWidth, _gridHeight;

    Vector2 _trackStart;
    float _trackWidth;
    int _slotCount;

    static UiModel Ui => GameRunner.Instance != null ? GameRunner.Instance.Ui : null;

    void Start()
    {
        _cam = Camera.main;
        _root = new GameObject("World").transform;
        _root.SetParent(transform, false);
    }

    void LateUpdate()
    {
        var ui = Ui;
        if (ui == null) return;

        DrawGrid(ui);
        DrawTrack(ui);
        HandleTap(ui);
    }

    // ── 판 ─────────────────────────────────────────────

    void DrawGrid(UiModel ui)
    {
        var g = ui.Grid;
        _gridWidth = g == null ? 0 : g.Width;
        _gridHeight = g == null ? 0 : g.Height;
        int need = _gridWidth * _gridHeight;

        while (_cells.Count < need) _cells.Add(NewSprite("Cell" + _cells.Count, GridOrder));
        while (_cellLabels.Length < need) GrowLabels(need);

        if (need == 0)
        {
            for (int i = 0; i < _cells.Count; i++) _cells[i].gameObject.SetActive(false);
            for (int i = 0; i < _cellLabels.Length; i++) _cellLabels[i].gameObject.SetActive(false);
            return;
        }

        ViewConfig.WorldArea(_cam, out var center, out var size);

        // 칸이 정사각형을 유지하도록 가로/세로 중 빡빡한 쪽에 맞춘다.
        _cellSize = Mathf.Min(size.x / _gridWidth, size.y / _gridHeight);
        float boardW = _cellSize * _gridWidth;
        float boardH = _cellSize * _gridHeight;
        _gridOrigin = new Vector2(center.x - boardW * 0.5f, center.y + boardH * 0.5f);

        for (int i = 0; i < _cells.Count; i++)
        {
            bool on = i < need;
            _cells[i].gameObject.SetActive(on);
            _cellLabels[i].gameObject.SetActive(on);
            if (!on) continue;

            int x = i % _gridWidth, y = i / _gridWidth;
            var pos = CellCenter(x, y);
            _cells[i].transform.position = pos;

            int v = g.Cells[i];
            var sprite = PlaceholderArt.Get("", v < 0 ? 99 : v);
            _cells[i].sprite = sprite;
            _cells[i].color = v < 0 ? new Color(1, 1, 1, 0.10f) : Color.white;
            FitSprite(_cells[i], _cellSize * 0.92f);

            var label = _cellLabels[i];
            label.transform.position = new Vector3(pos.x, pos.y, -1f);
            label.text = g.Labels != null && i < g.Labels.Length ? g.Labels[i] : "";
            label.characterSize = _cellSize * 0.18f;
        }
    }

    Vector2 CellCenter(int x, int y)
        => new Vector2(_gridOrigin.x + (x + 0.5f) * _cellSize,
                       _gridOrigin.y - (y + 0.5f) * _cellSize);

    // ── 경로 ────────────────────────────────────────────

    void DrawTrack(UiModel ui)
    {
        var t = ui.Track;
        _slotCount = t == null ? 0 : t.Slots.Count;

        if (t == null)
        {
            if (_trackLine != null) _trackLine.gameObject.SetActive(false);
            for (int i = 0; i < _slots.Count; i++) _slots[i].gameObject.SetActive(false);
            for (int i = 0; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            return;
        }

        ViewConfig.WorldArea(_cam, out var center, out var size);
        _trackWidth = size.x;
        _trackStart = new Vector2(center.x - size.x * 0.5f, center.y);

        if (_trackLine == null) _trackLine = NewSprite("TrackLine", GridOrder);
        _trackLine.gameObject.SetActive(true);
        _trackLine.sprite = PlaceholderArt.Get("", 98);
        _trackLine.color = new Color(1, 1, 1, 0.12f);
        _trackLine.transform.position = center;
        StretchSprite(_trackLine, _trackWidth, size.y * 0.12f);

        float unit = Mathf.Min(_trackWidth / Mathf.Max(4, _slotCount), size.y * 0.25f);

        while (_slots.Count < t.Slots.Count) _slots.Add(NewSprite("Slot" + _slots.Count, TrackOrder));
        for (int i = 0; i < _slots.Count; i++)
        {
            bool on = i < t.Slots.Count;
            _slots[i].gameObject.SetActive(on);
            if (!on) continue;

            var slot = t.Slots[i];
            _slots[i].transform.position = TrackPoint(slot.Pos / t.Length, -unit * 0.7f);
            _slots[i].sprite = PlaceholderArt.Get("", slot.Occupied ? slot.Palette : 97);
            _slots[i].color = slot.Occupied ? Color.white : new Color(1, 1, 1, 0.25f);
            FitSprite(_slots[i], unit * 0.9f);
        }

        while (_markers.Count < t.Markers.Count) _markers.Add(NewSprite("Marker" + _markers.Count, MarkerOrder));
        for (int i = 0; i < _markers.Count; i++)
        {
            bool on = i < t.Markers.Count;
            _markers[i].gameObject.SetActive(on);
            if (!on) continue;

            var marker = t.Markers[i];
            _markers[i].transform.position = TrackPoint(marker.Pos / t.Length, unit * 0.55f);
            _markers[i].sprite = PlaceholderArt.Get("", marker.Palette);
            _markers[i].color = Color.white;
            FitSprite(_markers[i], unit * 0.7f);
        }
    }

    Vector3 TrackPoint(double t01, float yOffset)
    {
        float x = _trackStart.x + Mathf.Clamp01((float)t01) * _trackWidth;
        return new Vector3(x, _trackStart.y + yOffset, 0f);
    }

    // ── 입력 ────────────────────────────────────────────

    void HandleTap(UiModel ui)
    {
        if (!TryGetTap(out var screenPos) || _cam == null) return;

        // 하단 버튼/상단 정보 영역을 누른 것은 UI 가 처리한다.
        float y01 = screenPos.y / Mathf.Max(1, Screen.height);
        if (y01 < ViewConfig.WorldBottom || y01 > ViewConfig.WorldTop) return;

        var world = (Vector2)_cam.ScreenToWorldPoint(screenPos);

        if (ui.Grid != null && _gridWidth > 0 && TryHitCell(world, out int cell))
        {
            if (ui.Grid.OnCell != null) ui.Grid.OnCell(cell);
            return;
        }

        if (ui.Track != null && _slotCount > 0 && TryHitSlot(world, out int slot))
        {
            if (ui.Track.OnSlot != null) ui.Track.OnSlot(slot);
        }
    }

    bool TryHitCell(Vector2 world, out int index)
    {
        index = -1;
        float dx = world.x - _gridOrigin.x;
        float dy = _gridOrigin.y - world.y;
        if (dx < 0 || dy < 0) return false;

        int x = (int)(dx / _cellSize);
        int y = (int)(dy / _cellSize);
        if (x < 0 || y < 0 || x >= _gridWidth || y >= _gridHeight) return false;

        index = y * _gridWidth + x;
        return true;
    }

    bool TryHitSlot(Vector2 world, out int index)
    {
        index = -1;
        float best = float.MaxValue;
        for (int i = 0; i < _slotCount && i < _slots.Count; i++)
        {
            float d = Vector2.Distance(world, _slots[i].transform.position);
            if (d < best) { best = d; index = i; }
        }
        // 너무 멀리 찍은 것은 무시한다.
        return index >= 0 && best <= _trackWidth / Mathf.Max(4, _slotCount);
    }

    /// <summary>
    /// 입력 처리 방식은 프로젝트 설정에 따라 달라진다.
    /// Active Input Handling 이 "Input System Package (New)" 전용이면 레거시 Input 이 예외를 던지므로,
    /// 컴파일 심볼로 갈라서 안전한 쪽만 쓴다.
    /// </summary>
    static bool TryGetTap(out Vector2 screenPos)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0)) { screenPos = Input.mousePosition; return true; }
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPos = Input.GetTouch(0).position;
            return true;
        }
#else
        // 새 Input System 전용 설정에서는 패키지 참조가 필요하다.
        // 지금은 의존을 만들지 않기 위해 비워 둔다 —
        // Project Settings > Player > Active Input Handling 을 "Both" 로 두면 위쪽 경로가 동작한다.
        WarnOnce();
#endif
        screenPos = default;
        return false;
    }

#if !ENABLE_LEGACY_INPUT_MANAGER
    static bool _warned;

    static void WarnOnce()
    {
        if (_warned) return;
        _warned = true;
        Debug.LogWarning(
            "[GameForge] 월드 입력이 비활성 상태입니다. " +
            "Project Settings > Player > Active Input Handling 을 'Both' 로 바꾸고 에디터를 재시작하세요.");
    }
#endif

    // ── 스프라이트 도우미 ─────────────────────────────────

    SpriteRenderer NewSprite(string name, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(_root, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }

    void GrowLabels(int need)
    {
        var grown = new TextMesh[need];
        System.Array.Copy(_cellLabels, grown, _cellLabels.Length);
        for (int i = _cellLabels.Length; i < need; i++)
        {
            var go = new GameObject("CellLabel" + i, typeof(TextMesh));
            go.transform.SetParent(_root, false);
            var tm = go.GetComponent<TextMesh>();
            tm.font = UiKit.Font;
            tm.GetComponent<MeshRenderer>().sharedMaterial = UiKit.Font.material;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = Color.white;
            tm.GetComponent<MeshRenderer>().sortingOrder = MarkerOrder;
            grown[i] = tm;
        }
        _cellLabels = grown;
    }

    /// <summary>스프라이트 실제 크기와 무관하게 원하는 월드 크기로 맞춘다.</summary>
    static void FitSprite(SpriteRenderer sr, float worldSize)
    {
        if (sr.sprite == null) return;
        var bounds = sr.sprite.bounds.size;
        float longest = Mathf.Max(bounds.x, bounds.y);
        if (longest <= 0.0001f) return;
        float scale = worldSize / longest;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }

    static void StretchSprite(SpriteRenderer sr, float worldWidth, float worldHeight)
    {
        if (sr.sprite == null) return;
        var bounds = sr.sprite.bounds.size;
        if (bounds.x <= 0.0001f || bounds.y <= 0.0001f) return;
        sr.transform.localScale = new Vector3(worldWidth / bounds.x, worldHeight / bounds.y, 1f);
    }
}
""";
}
