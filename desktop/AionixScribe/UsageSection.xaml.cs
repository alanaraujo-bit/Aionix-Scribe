using System.Globalization;
using System.Windows;

namespace AionixScribe;

public sealed class GeminiCallViewModel
{
    public required string TimestampDisplay { get; init; }
    public required string TokensDisplay { get; init; }
    public required string CostDisplay { get; init; }
}

/// Painel interno de custo real da Gemini (D027) — "quanto ainda temos" e "quanto gastamos, chamada
/// por chamada". Decisão explícita do proprietário: fica visível para qualquer instalação do app
/// (não só builds de desenvolvedor) e sem senha própria, atrás do mesmo stopgap do /api/transcribe
/// (D013). Ver a ressalva completa em DECISIONS.md D027.
public partial class UsageSection : System.Windows.Controls.UserControl
{
    private readonly BackendClient _backend = new();

    public UsageSection()
    {
        InitializeComponent();
        Refresh();
    }

    /// Dispara a busca sem bloquear quem chamou — o shell reaproveita a mesma instância a cada
    /// navegação (D020), então isso roda de novo toda vez que a pessoa entra nesta seção.
    public void Refresh() => _ = LoadAsync();

    private async Task LoadAsync()
    {
        CallsList.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Carregando…";

        try
        {
            var usage = await _backend.GetGeminiUsageAsync();
            Render(usage);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Não consegui carregar o custo agora.\n{ex.Message}";
        }
    }

    private void Render(GeminiUsageSummary usage)
    {
        TodayValueText.Text = FormatUsd(usage.SpentTodayUsd);
        TodayCallsText.Text = FormatCalls(usage.CallsToday);

        MonthValueText.Text = FormatUsd(usage.SpentThisMonthUsd);
        MonthCallsText.Text = FormatCalls(usage.CallsThisMonth);

        AllTimeValueText.Text = FormatUsd(usage.SpentAllTimeUsd);
        AllTimeCallsText.Text = FormatCalls(usage.CallsAllTime);

        if (usage.BudgetUsd is double budget)
        {
            BudgetCard.Visibility = Visibility.Visible;
            RemainingValueText.Text = FormatUsd(usage.RemainingThisMonthUsd ?? 0);
            BudgetFootnoteText.Text = $"de {FormatUsd(budget)}/mês";
        }
        else
        {
            BudgetCard.Visibility = Visibility.Collapsed;
        }

        StatusText.Visibility = Visibility.Collapsed;

        if (usage.Recent.Count == 0)
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Nenhuma chamada à Gemini registrada ainda.";
            CallsList.Visibility = Visibility.Collapsed;
            return;
        }

        CallsList.ItemsSource = usage.Recent.Select(c => new GeminiCallViewModel
        {
            TimestampDisplay = c.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            TokensDisplay = $"{c.PromptTokens} → {c.CandidateTokens} tokens ({c.ModelVersion})"
                + (c.EmptyResult ? " · sem fala detectada" : ""),
            CostDisplay = FormatUsd(c.CostUsd),
        }).ToList();
        CallsList.Visibility = Visibility.Visible;
    }

    private static string FormatUsd(double value) => $"US$ {value.ToString("0.0000", CultureInfo.InvariantCulture)}";

    private static string FormatCalls(int count) => count == 1 ? "1 chamada" : $"{count} chamadas";
}
