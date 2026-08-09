using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AionixScribe;

public enum ToastKind { Info, Success, Warning, Error }

/// Aviso flutuante próprio, no lugar das notificações do Windows (NotifyIcon.ShowBalloonTip).
///
/// Decisão registrada em D023: ou a interface é nossa e a do Windows sai, ou fica só a do Windows —
/// nunca as duas. Escolhemos a nossa. O custo real é não ter histórico na Central de Ações; isso é
/// aceitável porque o estado DURÁVEL já é representado em lugares permanentes do app (contador de
/// pendências no menu da bandeja, selo de atualização na janela). Estes avisos são anúncios
/// passageiros, não a única pista de que algo aconteceu.
public partial class ToastWindow : Window
{
    private const int EdgeMargin = 16;
    private const int StackGap = 4;

    // Avisos empilham de baixo para cima; a lista mantém a ordem para reposicionar quando um sai.
    private static readonly List<ToastWindow> Live = new();

    private DispatcherTimer? _timer;

    private ToastWindow()
    {
        InitializeComponent();
    }

    public static void Show(string message, ToastKind kind = ToastKind.Info, string title = "Aionix Scribe", int durationMs = 6000)
    {
        // Sempre no thread da UI: vários chamadores vêm de continuações async ou de eventos do
        // sistema, e criar Window fora do Dispatcher quebra.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => Show(message, kind, title, durationMs));
            return;
        }

        var toast = new ToastWindow();
        toast.TitleText.Text = title;
        toast.MessageText.Text = message;
        toast.KindDot.Fill = toast.BrushFor(kind);

        Live.Add(toast);
        toast.Show();

        // Posicionar logo após Show() usa ActualHeight ainda ZERO (o layout não rodou), e o aviso
        // nasce colado na borda de baixo, com o rodapé cortado — visto na captura de tela, não
        // deduzido. UpdateLayout força a medição antes de calcular a posição; ContentRendered
        // reposiciona de novo caso o texto quebre em mais linhas do que a primeira medida previa.
        toast.UpdateLayout();
        Reflow();
        toast.ContentRendered += (_, _) => Reflow();
        toast.SizeChanged += (_, _) => Reflow();

        toast.Opacity = 0;
        toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));

        toast._timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        toast._timer.Tick += (_, _) => toast.Dismiss();
        toast._timer.Start();
    }

    private System.Windows.Media.Brush BrushFor(ToastKind kind)
    {
        var key = kind switch
        {
            ToastKind.Success => "SuccessBrush",
            ToastKind.Warning => "WarningBrush",
            ToastKind.Error => "ErrorBrush",
            _ => "AccentBrush",
        };
        return (System.Windows.Media.Brush)FindResource(key);
    }

    /// Empilha os avisos no canto inferior direito da ÁREA DE TRABALHO (WorkArea, não a resolução
    /// da tela) — assim eles nunca ficam atrás da barra de tarefas.
    private static void Reflow()
    {
        var area = SystemParameters.WorkArea;
        var offset = 0.0;
        // Do mais novo para o mais antigo: o recém-chegado fica embaixo, junto do cursor.
        for (var i = Live.Count - 1; i >= 0; i--)
        {
            var t = Live[i];
            t.Left = area.Right - t.Width - EdgeMargin;
            t.Top = area.Bottom - t.ActualHeight - EdgeMargin - offset;
            offset += t.ActualHeight + StackGap;
        }
    }

    private void OnClicked(object sender, System.Windows.Input.MouseButtonEventArgs e) => Dismiss();

    private void Dismiss()
    {
        _timer?.Stop();
        _timer = null;

        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160));
        fade.Completed += (_, _) =>
        {
            Live.Remove(this);
            Close();
            Reflow();
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
