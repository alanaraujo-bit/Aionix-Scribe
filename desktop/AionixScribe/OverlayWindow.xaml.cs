using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
// O projeto referencia WPF e WinForms ao mesmo tempo, e ambos definem Color/ColorConverter.
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace AionixScribe;

public enum OverlayState { Listening, Processing, Done, Error, Cancelled }

/// Overlay always-on-top que nunca rouba foco da janela ativa — validado no spike via
/// WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED (ver DECISIONS.md D003).
///
/// O indicador é um orbe animado (D025). Enquanto ouve, ele reage ao NÍVEL REAL do microfone:
/// `AudioRecorder.CurrentLevel` é lido a cada quadro e suavizado aqui, nunca no callback de áudio —
/// o RMS bruto salta de bloco em bloco e a animação ficaria trêmula.
public partial class OverlayWindow : Window
{
    private OverlayState _state = OverlayState.Listening;
    private bool _rendering;

    // Valor exibido, perseguindo o alvo vindo do microfone. A defasagem é o que dá fluidez.
    private double _level;
    private double _phase;

    /// Preenchido pelo App com o gravador ativo. Nulo fora do estado de escuta.
    public Func<float>? LevelSource { get; set; }

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionBottomCenter();
        IsVisibleChanged += OnVisibleChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
    }

    /// O laço de render só existe enquanto o overlay está visível. Um callback de
    /// CompositionTarget.Rendering esquecido ligado é CPU queimando para sempre num app que passa
    /// o dia inteiro ocioso na bandeja (a linha de base medida é 0% de CPU parado).
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue) StartRendering();
        else StopRendering();
    }

    private void StartRendering()
    {
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRender;
    }

    private void StopRendering()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnRender;
    }

    private void OnRender(object? sender, EventArgs e)
    {
        _phase += 0.03;

        // Alvo: nível do microfone enquanto ouve. Nos demais estados não há captura em andamento —
        // um orbe ligado ao microfone congelaria e pareceria travado, então ele passa a respirar
        // sozinho, com um pulso mais evidente em "processando" para comunicar trabalho acontecendo.
        double target = _state switch
        {
            OverlayState.Listening => LevelSource?.Invoke() ?? 0,
            OverlayState.Processing => 0.32 + Math.Sin(_phase * 2.1) * 0.28,
            _ => 0.12,
        };

        // Sobe rápido, desce devagar: acompanhar o ataque da voz e deixar a cauda cair suave é o que
        // faz o movimento parecer orgânico em vez de mecânico.
        var rate = target > _level ? 0.35 : 0.08;
        _level += (target - _level) * rate;

        var breath = Math.Sin(_phase * 1.15) * 0.02;

        CoreScale.ScaleX = CoreScale.ScaleY = 1 + _level * 0.20 + breath;
        RingInnerScale.ScaleX = RingInnerScale.ScaleY = 1 + _level * 0.30 + breath * 1.5;
        RingOuterScale.ScaleX = RingOuterScale.ScaleY = 1 + _level * 0.42 + breath * 2;
        HaloScale.ScaleX = HaloScale.ScaleY = 1 + _level * 0.34;

        Halo.Opacity = 0.34 + _level * 0.42;
        RingInner.Opacity = 0.20 + _level * 0.34;
        RingOuter.Opacity = 0.10 + _level * 0.24;
    }

    private void PositionBottomCenter()
    {
        Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
        Top = SystemParameters.PrimaryScreenHeight - ActualHeight - 70;
    }

    /// `detail` continua no contrato porque o App passa mensagens específicas ("Nenhuma fala
    /// detectada", "Erro — gravação preservada") e elas seguem valendo: quem as exibe agora são os
    /// avisos do canto da tela (D023). Aqui o texto seria ilegível num indicador de 30 pixels.
    public void SetState(OverlayState state, string? detail = null)
    {
        _state = state;

        // Sem rótulo, a COR é o único canal de estado do overlay — laranja ouvindo/processando,
        // verde confirma, vermelho falha, cinza "nada aconteceu".
        var (light, mid, dark) = state switch
        {
            OverlayState.Done => ("#8FF0B4", "#4ADE80", "#1E7A44"),
            OverlayState.Error => ("#FFA5A5", "#F87171", "#8E2727"),
            OverlayState.Cancelled => ("#C9C6D0", "#8E8B96", "#4A4852"),
            _ => ("#FFB183", "#E8763F", "#A8431A"),
        };

        CoreLight.Color = (Color)ColorConverter.ConvertFromString(light);
        CoreMid.Color = (Color)ColorConverter.ConvertFromString(mid);
        CoreDark.Color = (Color)ColorConverter.ConvertFromString(dark);

        var haloBrush = new RadialGradientBrush();
        haloBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(mid), 0));
        haloBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        Halo.Fill = haloBrush;

        var ringColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mid));
        RingInner.Stroke = ringColor;
        RingOuter.Stroke = ringColor;

        Dispatcher.InvokeAsync(PositionBottomCenter);
    }
}
