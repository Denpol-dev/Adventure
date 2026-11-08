using Foxnet.Session;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MarkdownBlock = Markdig.Syntax.Block;
using MarkdownInline = Markdig.Syntax.Inlines.Inline;
using WpfList = System.Windows.Documents.List;
using WpfInline = System.Windows.Documents.Inline;

namespace Foxnet.Desktop.Services;

internal static class DocumentPreview
{
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");

    public static (FlowDocument? Content, string? Notice) Create(LibraryDocument document)
    {
        if (document.Info.Format is not (DocumentFormat.Text or DocumentFormat.Markdown or DocumentFormat.Json))
            return (null, "Встроенный просмотр этого формата недоступен.");

        using var bytes = new MemoryStream(document.Content, writable: false);
        using var reader = new StreamReader(bytes, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var flow = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            PagePadding = new Thickness(24, 20, 24, 24)
        };
        if (document.Info.Format == DocumentFormat.Markdown)
        {
            foreach (var block in Markdown.Parse(text))
                RenderBlock(flow.Blocks, block);
            return (flow, null);
        }

        if (document.Info.Format == DocumentFormat.Json)
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                flow.Blocks.Add(Code(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })));
                return (flow, null);
            }
            catch (JsonException)
            {
                flow.Blocks.Add(Code(text));
                return (flow, "Некорректный JSON. Показан исходный текст файла.");
            }
        }

        flow.Blocks.Add(new Paragraph(new Run(text)));
        return (flow, null);
    }

    private static void RenderBlock(BlockCollection output, MarkdownBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var title = new Paragraph { FontWeight = FontWeights.SemiBold, FontSize = Math.Max(16, 30 - heading.Level * 3) };
                RenderInlines(title.Inlines, heading.Inline);
                output.Add(title);
                break;
            case CodeBlock code:
                output.Add(Code(code.Lines.ToString()));
                break;
            case ListBlock list:
                var formatted = new WpfList
                {
                    MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    StartIndex = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? Math.Max(1, start) : 1,
                    Padding = new Thickness(24, 0, 0, 0)
                };
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var entry = new ListItem();
                    foreach (var child in item)
                        RenderBlock(entry.Blocks, child);
                    formatted.ListItems.Add(entry);
                }
                output.Add(formatted);
                break;
            case QuoteBlock quote:
                var section = new Section { Margin = new Thickness(18, 0, 0, 0), FontStyle = FontStyles.Italic };
                foreach (var child in quote)
                    RenderBlock(section.Blocks, child);
                output.Add(section);
                break;
            case ThematicBreakBlock:
                output.Add(new Paragraph(new Run("────────────")));
                break;
            case LeafBlock leaf:
                var paragraph = new Paragraph();
                if (leaf.Inline == null)
                    paragraph.Inlines.Add(new Run(leaf.Lines.ToString()));
                else
                    RenderInlines(paragraph.Inlines, leaf.Inline);
                output.Add(paragraph);
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    RenderBlock(output, child);
                break;
        }
    }

    private static Paragraph Code(string text) =>
        new(new Run(text)) { FontFamily = CodeFont, FontSize = 13, Padding = new Thickness(10) };

    private static void RenderInlines(InlineCollection output, ContainerInline? source)
    {
        if (source == null)
            return;
        foreach (var inline in source)
        {
            WpfInline? rendered = inline switch
            {
                LiteralInline literal => new Run(literal.Content.ToString()),
                CodeInline code => new Run(code.Content) { FontFamily = CodeFont },
                LineBreakInline { IsHard: true } => new LineBreak(),
                LineBreakInline => new Run(" "),
                HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
                AutolinkInline link => new Run(link.Url),
                HtmlInline html => new Run(html.Tag),
                ContainerInline container => RenderSpan(container),
                _ => null
            };
            if (rendered != null)
                output.Add(rendered);
        }
    }

    private static Span RenderSpan(ContainerInline inline)
    {
        Span span = inline switch
        {
            EmphasisInline { DelimiterCount: >= 2 } => new Bold(),
            EmphasisInline => new Italic(),
            LinkInline => new Underline { Foreground = (Brush)Application.Current.FindResource("AccentBrush") },
            _ => new Span()
        };
        RenderInlines(span.Inlines, inline);
        return span;
    }
}
