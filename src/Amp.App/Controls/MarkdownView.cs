using System.Text;
using System.Text.RegularExpressions;
using Amp.App.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Amp.App.Controls;

/// <summary>
/// Renders Markdown with native XAML text elements: headings, paragraphs, bullet and numbered lists, block quotes,
/// fenced code, horizontal rules, and inline bold / italic / code / links. Tables and raw HTML fall back to monospace.
/// </summary>
public sealed class MarkdownView : UserControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownView), new PropertyMetadata("", (d, _) => ((MarkdownView)d).Rebuild()));

    private readonly StackPanel _panel = new() { Spacing = 8 };

    public MarkdownView()
    {
        IsTabStop = false;
        Content = _panel;
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static FontFamily CodeFont => (FontFamily)Application.Current.Resources["CodeFontFamily"];

    private void Rebuild()
    {
        _panel.Children.Clear();
        foreach (var block in MarkdownBlocks.Split(Markdown ?? ""))
            _panel.Children.Add(Render(block));
    }

    private UIElement Render(MarkdownBlocks.Block block) => block.Kind switch
    {
        MarkdownBlocks.Kind.Code => CodeBlock(block.Text, block.Info),
        MarkdownBlocks.Kind.Rule => new Border { Height = 1, Margin = new Thickness(0, 4, 0, 4), Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] },
        MarkdownBlocks.Kind.Heading => Paragraphs(block.Text, p =>
        {
            p.FontWeight = FontWeights.SemiBold;
            p.FontSize = block.Level switch { 1 => 22, 2 => 18, 3 => 16, _ => 14 };
            p.Margin = new Thickness(0, 8, 0, 0);
        }),
        MarkdownBlocks.Kind.Quote => new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Padding = new Thickness(12, 2, 0, 2),
            Child = Paragraphs(block.Text, p => p.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]),
        },
        MarkdownBlocks.Kind.List => ListBlock(block),
        _ => Paragraphs(block.Text, null),
    };

    private static RichTextBlock Paragraphs(string text, Action<Paragraph>? style)
    {
        var rtb = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        var paragraph = new Paragraph();
        MarkdownInlines.AppendTo(paragraph.Inlines, text, CodeFont);
        style?.Invoke(paragraph);
        rtb.Blocks.Add(paragraph);
        return rtb;
    }

    private static UIElement ListBlock(MarkdownBlocks.Block block)
    {
        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var row = 0;
        foreach (var item in block.Items)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var marker = new TextBlock { Text = item.Marker, Margin = new Thickness(item.Indent * 16, 0, 0, 0) };
            var body = Paragraphs(item.Text, null);
            Grid.SetRow(marker, row); Grid.SetColumn(marker, 0);
            Grid.SetRow(body, row); Grid.SetColumn(body, 1);
            grid.Children.Add(marker);
            grid.Children.Add(body);
            row++;
        }
        return grid;
    }

    private static UIElement CodeBlock(string code, string? info)
    {
        var text = new TextBlock
        {
            Text = code,
            FontFamily = CodeFont,
            FontSize = 13,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
        };
        var scroller = new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            Padding = new Thickness(12, 8, 12, 8),
        };
        var copy = new Button
        {
            Content = new FontIcon { Glyph = "\uE8C8", FontSize = 12 },
            Padding = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(copy, "Copy");
        copy.Click += (_, _) =>
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(code);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        };
        var grid = new Grid();
        grid.Children.Add(scroller);
        grid.Children.Add(copy);
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = grid,
        };
        return border;
    }
}

/// <summary>Block-level Markdown splitter.</summary>
internal static class MarkdownBlocks
{
    public enum Kind { Paragraph, Heading, Code, Quote, List, Rule }

    public sealed record ListItem(string Marker, string Text, int Indent);

    public sealed class Block
    {
        public Kind Kind { get; init; }
        public string Text { get; init; } = "";
        public string? Info { get; init; }
        public int Level { get; init; }
        public List<ListItem> Items { get; } = [];
    }

    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*?)\s*#*$", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex Rule = new(@"^\s*([-*_])(\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex Fence = new(@"^\s*(```+|~~~+)\s*(\S*)\s*$", RegexOptions.Compiled);

    public static List<Block> Split(string markdown)
    {
        var blocks = new List<Block>();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            var fence = Fence.Match(line);
            if (fence.Success)
            {
                var marker = fence.Groups[1].Value;
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !(lines[i].TrimStart().StartsWith(marker[0]) && Fence.Match(lines[i]) is { Success: true } close && close.Groups[1].Value[0] == marker[0] && close.Groups[2].Value.Length == 0))
                    code.Append(lines[i++]).Append('\n');
                i++; // closing fence (or EOF)
                blocks.Add(new Block { Kind = Kind.Code, Text = code.ToString().TrimEnd('\n'), Info = fence.Groups[2].Value });
                continue;
            }

            if (Rule.IsMatch(line)) { blocks.Add(new Block { Kind = Kind.Rule }); i++; continue; }

            var heading = Heading.Match(line);
            if (heading.Success)
            {
                blocks.Add(new Block { Kind = Kind.Heading, Level = heading.Groups[1].Length, Text = heading.Groups[2].Value });
                i++;
                continue;
            }

            if (line.TrimStart().StartsWith('>'))
            {
                var quote = new StringBuilder();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                    quote.Append(lines[i++].TrimStart().TrimStart('>').TrimStart()).Append('\n');
                blocks.Add(new Block { Kind = Kind.Quote, Text = quote.ToString().Trim() });
                continue;
            }

            if (Bullet.IsMatch(line))
            {
                var list = new Block { Kind = Kind.List };
                while (i < lines.Length && Bullet.Match(lines[i]) is { Success: true } m)
                {
                    var indent = m.Groups[1].Value.Replace("\t", "    ").Length / 2;
                    var marker = char.IsDigit(m.Groups[2].Value[0]) ? m.Groups[2].Value : "•";
                    var text = new StringBuilder(m.Groups[3].Value);
                    i++;
                    // Continuation lines: indented, non-blank, not a new bullet.
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !Bullet.IsMatch(lines[i]) && lines[i].StartsWith(' '))
                        text.Append(' ').Append(lines[i++].Trim());
                    list.Items.Add(new ListItem(marker, text.ToString(), Math.Min(indent, 4)));
                }
                blocks.Add(list);
                continue;
            }

            var paragraph = new StringBuilder();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !Fence.IsMatch(lines[i]) && !Heading.IsMatch(lines[i]) && !Bullet.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith('>'))
                paragraph.Append(lines[i++].Trim()).Append('\n');
            var paragraphText = paragraph.ToString().TrimEnd('\n');
            // Pipe tables are not laid out natively; keep them aligned in monospace instead.
            if (paragraphText.StartsWith('|'))
                blocks.Add(new Block { Kind = Kind.Code, Text = paragraphText });
            else
                blocks.Add(new Block { Kind = Kind.Paragraph, Text = paragraphText.Replace('\n', ' ') });
        }
        return blocks;
    }
}

/// <summary>Inline Markdown: <c>**bold**</c>, <c>*italic*</c>, <c>`code`</c>, <c>[text](url)</c>, bare URLs.</summary>
internal static class MarkdownInlines
{
    private static readonly Regex Token = new(
        @"(?<code>`+)(?<codeText>.+?)\k<code>" +
        @"|\*\*(?<bold>.+?)\*\*" +
        @"|__(?<bold2>.+?)__" +
        @"|(?<![\w*])\*(?<italic>[^*\n]+?)\*(?![\w*])" +
        @"|(?<![\w_])_(?<italic2>[^_\n]+?)_(?![\w_])" +
        @"|~~(?<strike>.+?)~~" +
        @"|\[(?<linkText>[^\]]+)\]\((?<linkUrl>[^)\s]+)\)" +
        @"|(?<url>https?://[^\s<>)\]]+)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static void AppendTo(InlineCollection inlines, string text, FontFamily codeFont)
    {
        var last = 0;
        foreach (Match m in Token.Matches(text))
        {
            if (m.Index > last) inlines.Add(new Run { Text = text[last..m.Index] });
            last = m.Index + m.Length;

            if (m.Groups["codeText"].Success)
                inlines.Add(new Run { Text = m.Groups["codeText"].Value, FontFamily = codeFont });
            else if (m.Groups["bold"].Success || m.Groups["bold2"].Success)
                inlines.Add(Styled(new Bold(), (m.Groups["bold"].Success ? m.Groups["bold"] : m.Groups["bold2"]).Value, codeFont));
            else if (m.Groups["italic"].Success || m.Groups["italic2"].Success)
                inlines.Add(Styled(new Italic(), (m.Groups["italic"].Success ? m.Groups["italic"] : m.Groups["italic2"]).Value, codeFont));
            else if (m.Groups["strike"].Success)
                inlines.Add(new Run { Text = m.Groups["strike"].Value, TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough });
            else if (m.Groups["linkText"].Success)
                inlines.Add(Link(m.Groups["linkText"].Value, m.Groups["linkUrl"].Value));
            else if (m.Groups["url"].Success)
                inlines.Add(Link(m.Groups["url"].Value, m.Groups["url"].Value));
        }
        if (last < text.Length) inlines.Add(new Run { Text = text[last..] });
    }

    private static Span Styled(Span span, string inner, FontFamily codeFont)
    {
        AppendTo(span.Inlines, inner, codeFont);
        return span;
    }

    private static Hyperlink Link(string text, string url)
    {
        var link = new Hyperlink();
        link.Inlines.Add(new Run { Text = text });
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            link.Click += (_, _) => CliSetup.OpenUrl(uri.ToString());
        return link;
    }
}
