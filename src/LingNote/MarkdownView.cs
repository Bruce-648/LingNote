using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;

namespace LingNote;
public static class MarkdownView
{
    public static FlowDocument Render(string markdown)
    {
        var result=new FlowDocument { FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),FontSize=14,PagePadding=new Thickness(12),LineHeight=26,Foreground=new SolidColorBrush(Color.FromRgb(35,46,63)) };
        foreach(var block in Markdown.Parse(markdown)) Add(result.Blocks,block);
        return result;
    }
    private static void Add(BlockCollection target,Markdig.Syntax.Block block)
    {
        if(block is ListBlock list) { var view=new System.Windows.Documents.List { MarkerStyle=list.IsOrdered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,Margin=new Thickness(0,4,0,12) }; foreach(var item in list.OfType<ListItemBlock>()) { var li=new ListItem(); foreach(var child in item) Add(li.Blocks,child); view.ListItems.Add(li); } target.Add(view); }
        else if(block is FencedCodeBlock code) { target.Add(new Paragraph(new Run(code.Lines.ToString())) { FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),FontSize=13,Background=new SolidColorBrush(Color.FromRgb(245,247,251)),Padding=new Thickness(14),Margin=new Thickness(0,8,0,14) }); }
        else if(block is ThematicBreakBlock) target.Add(new Paragraph(new Run("────────────────────────")) { Foreground=Brushes.LightGray });
        else if(block is LeafBlock leaf) { var p=new Paragraph {Margin=new Thickness(0,6,0,12)}; if(leaf.Inline!=null) Inlines(p.Inlines,leaf.Inline); else p.Inlines.Add(new Run(leaf.Lines.ToString())); if(block is HeadingBlock heading) { p.FontSize=heading.Level switch {1=>26,2=>21,3=>17,_=>15}; p.FontWeight=FontWeights.SemiBold; p.Margin=new Thickness(0,18,0,10); } target.Add(p); }
        else if(block is ContainerBlock container) { var section=new Section(); foreach(var child in container) Add(section.Blocks,child); target.Add(section); }
    }
    private static void Inlines(InlineCollection target,ContainerInline container)
    {
        foreach(var inline in container) { switch(inline) { case LiteralInline text: target.Add(new Run(text.Content.ToString())); break; case CodeInline code: target.Add(new Run(code.Content) {FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),Background=new SolidColorBrush(Color.FromRgb(240,244,250))}); break; case LineBreakInline: target.Add(new LineBreak()); break; case EmphasisInline emphasis: var span=new Span(); if(emphasis.DelimiterCount>=2) span.FontWeight=FontWeights.Bold; else span.FontStyle=FontStyles.Italic; Inlines(span.Inlines,emphasis); target.Add(span); break; case LinkInline link: var textSpan=new Span {Foreground=new SolidColorBrush(Color.FromRgb(37,99,235))}; Inlines(textSpan.Inlines,link); target.Add(textSpan); break; case ContainerInline nested: Inlines(target,nested); break; } }
    }
}
