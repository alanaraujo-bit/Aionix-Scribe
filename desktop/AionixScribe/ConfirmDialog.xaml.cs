using System.Windows;
using System.Windows.Input;

namespace AionixScribe;

/// Substitui o MessageBox do Windows em confirmações destrutivas. O MessageBox não aceita nenhuma
/// personalização visual — abria uma caixa cinza do sistema no meio de uma interface escura.
///
/// Modal de verdade (ShowDialog com Owner), para manter o comportamento que o usuário espera de uma
/// confirmação: a janela de trás não aceita interação enquanto a decisão não é tomada.
public partial class ConfirmDialog : Window
{
    private bool _confirmed;

    private ConfirmDialog()
    {
        InitializeComponent();
    }

    /// Retorna true se o usuário confirmou. `owner` centraliza o diálogo sobre a janela correta —
    /// sem ele, uma confirmação pode aparecer atrás da janela principal e travar a interação sem o
    /// usuário entender por quê.
    public static bool Ask(Window? owner, string title, string message,
                           string confirmLabel = "Confirmar", string cancelLabel = "Cancelar")
    {
        var dialog = new ConfirmDialog
        {
            Owner = owner,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
        };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmLabel;
        dialog.CancelButton.Content = cancelLabel;

        // O foco começa em Cancelar: numa ação destrutiva, um Enter distraído não pode ser o que
        // apaga os dados do usuário.
        dialog.Loaded += (_, _) => dialog.CancelButton.Focus();

        dialog.ShowDialog();
        return dialog._confirmed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Sem barra de título não há botão de fechar — Esc precisa funcionar, senão a janela
        // vira uma armadilha sem saída visível.
        if (e.Key == Key.Escape) Close();
    }
}
