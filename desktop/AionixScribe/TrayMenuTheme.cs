using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AionixScribe;

/// Aplica a identidade visual do app ao menu da bandeja.
///
/// O menu da bandeja é WinForms (NotifyIcon exige ContextMenuStrip), então ele NÃO enxerga os
/// dicionários de tema do WPF: sem isto ele nasce branco padrão do Windows, destoando de todo o
/// resto do app — foi exatamente essa a reclamação do proprietário.
///
/// As cores são LIDAS dos brushes do tema em tempo de execução, não copiadas como literais, para
/// que o menu acompanhe a troca claro/escuro. Como WinForms não reage a DynamicResource, App.ApplyTheme
/// precisa chamar Apply() de novo a cada troca de tema — senão quem mudar de escuro para claro
/// ficaria com o menu escuro para sempre.
public static class TrayMenuTheme
{
    private static readonly PrivateFontCollection FontCollection = new();
    private static Drawing.FontFamily? _soraFamily;

    public static void Apply(Forms.ContextMenuStrip menu)
    {
        var colors = ThemeColors.Current();

        menu.RenderMode = Forms.ToolStripRenderMode.Professional;
        menu.Renderer = new AionixRenderer(colors);
        menu.BackColor = colors.Surface;
        menu.ForeColor = colors.PrimaryText;
        // A coluna de margem de ícone é a parte que teima em ficar clara mesmo com o color table
        // customizado — e nenhum item deste menu tem ícone, então ela só ocupa espaço.
        menu.ShowImageMargin = false;
        menu.Padding = new Forms.Padding(0, 6, 0, 6);

        var font = ResolveFont();
        foreach (Forms.ToolStripItem item in menu.Items)
        {
            item.ForeColor = colors.PrimaryText;
            item.BackColor = colors.Surface;
            if (font != null) item.Font = font;
            if (item is Forms.ToolStripMenuItem) item.Padding = new Forms.Padding(0, 4, 0, 4);
        }
    }

    /// Carrega a Sora embutida no assembly. As fontes do app são recursos WPF (não estão instaladas
    /// no Windows), então o GDI+ só as enxerga via PrivateFontCollection alimentada em memória.
    /// Qualquer falha cai na fonte padrão do sistema — menu com fonte errada é um detalhe, menu que
    /// não abre é um defeito.
    private static Drawing.Font? ResolveFont()
    {
        try
        {
            if (_soraFamily == null)
            {
                var stream = System.Windows.Application.GetResourceStream(
                    new Uri("Fonts/Sora-VariableFont.ttf", UriKind.Relative))?.Stream;
                if (stream == null) return null;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();

                var handle = Marshal.AllocCoTaskMem(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, handle, bytes.Length);
                    FontCollection.AddMemoryFont(handle, bytes.Length);
                    _soraFamily = FontCollection.Families.FirstOrDefault();
                }
                finally
                {
                    // Não liberar aqui: o GDI+ mantém referência à memória da fonte enquanto a
                    // coleção viver. Liberar causaria texto corrompido ou crash na renderização.
                }
            }

            return _soraFamily == null ? null : new Drawing.Font(_soraFamily, 9.5f);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"TrayMenuTheme: falha ao carregar fonte embutida: {ex.Message}");
            return null;
        }
    }

    private sealed record ThemeColors(
        Drawing.Color Surface,
        Drawing.Color Hover,
        Drawing.Color Border,
        Drawing.Color PrimaryText,
        Drawing.Color DisabledText,
        Drawing.Color Separator)
    {
        public static ThemeColors Current() => new(
            Surface: Resource("CardBackgroundBrush", Drawing.Color.FromArgb(0x21, 0x1F, 0x27)),
            Hover: Resource("SurfaceRaisedBrush", Drawing.Color.FromArgb(0x2A, 0x28, 0x30)),
            Border: Blend(Resource("BorderStrongBrush", Drawing.Color.Gray),
                          Resource("CardBackgroundBrush", Drawing.Color.FromArgb(0x21, 0x1F, 0x27))),
            PrimaryText: Resource("PrimaryTextBrush", Drawing.Color.WhiteSmoke),
            DisabledText: Resource("FootnoteTextBrush", Drawing.Color.Gray),
            Separator: Blend(Resource("BorderBrush", Drawing.Color.Gray),
                             Resource("CardBackgroundBrush", Drawing.Color.FromArgb(0x21, 0x1F, 0x27))));

        private static Drawing.Color Resource(string key, Drawing.Color fallback)
        {
            if (System.Windows.Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            {
                var c = brush.Color;
                return Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            return fallback;
        }

        /// Os brushes de borda do tema são alfa sobre a superfície (ex.: #20FFFFFF). GDI+ desenha
        /// borda de menu sem composição alfa confiável, então achatamos contra o fundo do menu.
        private static Drawing.Color Blend(Drawing.Color over, Drawing.Color under)
        {
            var a = over.A / 255.0;
            return Drawing.Color.FromArgb(255,
                (int)(over.R * a + under.R * (1 - a)),
                (int)(over.G * a + under.G * (1 - a)),
                (int)(over.B * a + under.B * (1 - a)));
        }
    }

    private sealed class AionixColorTable : Forms.ProfessionalColorTable
    {
        private readonly ThemeColors _c;
        public AionixColorTable(ThemeColors c) { _c = c; UseSystemColors = false; }

        public override Drawing.Color ToolStripDropDownBackground => _c.Surface;
        public override Drawing.Color MenuBorder => _c.Border;
        public override Drawing.Color MenuItemBorder => _c.Hover;
        public override Drawing.Color MenuItemSelected => _c.Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => _c.Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => _c.Hover;
        public override Drawing.Color MenuItemPressedGradientBegin => _c.Hover;
        public override Drawing.Color MenuItemPressedGradientMiddle => _c.Hover;
        public override Drawing.Color MenuItemPressedGradientEnd => _c.Hover;
        public override Drawing.Color ImageMarginGradientBegin => _c.Surface;
        public override Drawing.Color ImageMarginGradientMiddle => _c.Surface;
        public override Drawing.Color ImageMarginGradientEnd => _c.Surface;
        public override Drawing.Color SeparatorDark => _c.Separator;
        public override Drawing.Color SeparatorLight => _c.Separator;
    }

    private sealed class AionixRenderer : Forms.ToolStripProfessionalRenderer
    {
        private readonly ThemeColors _c;
        public AionixRenderer(ThemeColors c) : base(new AionixColorTable(c)) { _c = c; }

        /// O renderer padrão pinta item desabilitado com a cor cinza do SISTEMA, que sobre fundo
        /// escuro fica praticamente invisível — "Reprocessar pendências (0)" sumiria em vez de
        /// apenas parecer apagado.
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item!.Enabled ? _c.PrimaryText : _c.DisabledText;
            base.OnRenderItemText(e);
        }
    }
}
