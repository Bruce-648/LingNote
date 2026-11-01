using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;
using System.Text;
using System.Text.RegularExpressions;

namespace LingNote;
public static class MarkdownView
{
    private static readonly MarkdownPipeline Pipeline=new MarkdownPipelineBuilder().UsePipeTables().Build();
    public static FlowDocument Render(string markdown)
    {
        var result=new FlowDocument { FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),FontSize=15,PagePadding=new Thickness(8),LineHeight=25,Foreground=new SolidColorBrush(Color.FromRgb(35,46,63)) };
        foreach(var block in Markdown.Parse(ReadableMath(markdown),Pipeline)) Add(result.Blocks,block);
        return result;
    }
    private static void Add(BlockCollection target,Markdig.Syntax.Block block)
    {
        if(block is Markdig.Extensions.Tables.Table table) {
            var view=new System.Windows.Documents.Table {CellSpacing=0,Margin=new Thickness(0,8,0,16)};
            var rows=new TableRowGroup();view.RowGroups.Add(rows);
            foreach(var row in table.OfType<Markdig.Extensions.Tables.TableRow>()) {
                var rendered=new System.Windows.Documents.TableRow();rows.Rows.Add(rendered);
                foreach(var cell in row.OfType<Markdig.Extensions.Tables.TableCell>()) {
                    var renderedCell=new System.Windows.Documents.TableCell {Padding=new Thickness(9,6,9,6),BorderBrush=(Brush)Application.Current.FindResource("Line"),BorderThickness=new Thickness(.5),ColumnSpan=Math.Max(1,cell.ColumnSpan)};
                    foreach(var child in cell)Add(renderedCell.Blocks,child);
                    foreach(var paragraph in renderedCell.Blocks.OfType<Paragraph>())paragraph.Margin=new Thickness(0);
                    if(row.IsHeader){renderedCell.FontWeight=FontWeights.SemiBold;renderedCell.Background=new SolidColorBrush(Color.FromRgb(245,247,251));}
                    rendered.Cells.Add(renderedCell);
                }
            }
            target.Add(view);
        }
        else if(block is ListBlock list) { var view=new System.Windows.Documents.List { MarkerStyle=list.IsOrdered?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,StartIndex=list.IsOrdered&&int.TryParse(list.OrderedStart,out var start)?Math.Max(1,start):1,Margin=new Thickness(0,4,0,12) }; foreach(var item in list.OfType<ListItemBlock>()) { var li=new ListItem(); foreach(var child in item) Add(li.Blocks,child);foreach(var paragraph in li.Blocks.OfType<Paragraph>())paragraph.Margin=new Thickness(0,1,0,2); view.ListItems.Add(li); } target.Add(view); }
        else if(block is FencedCodeBlock code) { target.Add(new Paragraph(new Run(code.Lines.ToString())) { Tag="code:"+code.Info,FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),FontSize=15,Background=new SolidColorBrush(Color.FromRgb(245,247,251)),Padding=new Thickness(14),Margin=new Thickness(0,8,0,14) }); }
        else if(block is ThematicBreakBlock) target.Add(new Paragraph(new Run("────────────────────────")) { Tag="rule",Foreground=Brushes.LightGray });
        else if(block is LeafBlock leaf) { var p=new Paragraph {Margin=new Thickness(0,6,0,12)}; if(leaf.Inline!=null) Inlines(p.Inlines,leaf.Inline); else p.Inlines.Add(new Run(leaf.Lines.ToString())); if(block is HeadingBlock heading) { p.Tag="heading:"+heading.Level;p.FontSize=heading.Level switch {1=>23,2=>20,3=>17,_=>15}; p.FontWeight=FontWeights.SemiBold; p.Margin=new Thickness(0,18,0,10); } target.Add(p); }
        else if(block is ContainerBlock container) { var section=new Section {Tag=block is QuoteBlock?"quote":null}; foreach(var child in container) Add(section.Blocks,child);
            if(block is QuoteBlock){section.Background=(Brush)Application.Current.FindResource("Supplement");section.BorderBrush=(Brush)Application.Current.FindResource("Accent");section.BorderThickness=new Thickness(5,0,0,0);section.Padding=new Thickness(18,8,14,8);section.Margin=new Thickness(0,8,0,14);
                if(section.Blocks.FirstBlock is Paragraph title&&new TextRange(title.ContentStart,title.ContentEnd).Text.TrimStart().StartsWith("补充")){title.Foreground=(Brush)Application.Current.FindResource("Accent");title.FontWeight=FontWeights.SemiBold;}}
            target.Add(section); }
    }
    private static void Inlines(InlineCollection target,ContainerInline container)
    {
        foreach(var inline in container) { switch(inline) { case LiteralInline text: target.Add(new Run(text.Content.ToString())); break; case CodeInline code: target.Add(new Run(code.Content) {Tag="code",FontFamily=(FontFamily)Application.Current.FindResource("AppFont"),Background=new SolidColorBrush(Color.FromRgb(240,244,250))}); break; case LineBreakInline: target.Add(new LineBreak()); break; case EmphasisInline emphasis: var span=new Span(); if(emphasis.DelimiterCount>=2) span.FontWeight=FontWeights.Bold; else span.FontStyle=FontStyles.Italic; Inlines(span.Inlines,emphasis); target.Add(span); break; case LinkInline link: var textSpan=new Span {Tag="link:"+link.Url,Foreground=new SolidColorBrush(Color.FromRgb(37,99,235))}; Inlines(textSpan.Inlines,link); target.Add(textSpan); break; case ContainerInline nested: Inlines(target,nested); break; } }
    }

    // Keep existing notes readable without sending their contents back to AI.
    // Code spans and fenced code are intentionally left verbatim.
    public static string ReadableMath(string text)
    {
        return Regex.Replace(text,@"(```[\s\S]*?```|`[^`\n]*`)|\\\(([\s\S]*?)\\\)|\\\[([\s\S]*?)\\\]|\$\$([\s\S]*?)\$\$|(?<!\$)\$([^$\n]+)\$(?!\$)",m=> {
            if(m.Groups[1].Success)return m.Value;
            var math=m.Groups.Cast<Group>().Skip(2).First(g=>g.Success).Value;
            math=Regex.Replace(math,@"\\(?:text|mathrm|mathbf|operatorname)\s*\{([^{}]*)\}","$1");
            foreach(var (command,symbol) in new[]{("times","×"),("cdot","·"),("leq","≤"),("geq","≥"),("neq","≠"),("approx","≈"),("alpha","α"),("beta","β"),("Delta","Δ"),("pi","π")})
                math=Regex.Replace(math,@"\\"+command+@"(?![A-Za-z])",symbol);
            math=Regex.Replace(math,@"\\frac\{([^{}]+)\}\{([^{}]+)\}","($1)/($2)");
            math=Regex.Replace(math,@"\^\{([^{}]+)\}|\^([0-9]+)",p=> {
                var exponent=p.Groups[1].Success?p.Groups[1].Value:p.Groups[2].Value;
                const string normal="0123456789+-=()ni",superscript="⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿⁱ";
                return exponent.All(c=>normal.Contains(c))?string.Concat(exponent.Select(c=>superscript[normal.IndexOf(c)])):"^("+exponent+")";
            });
            return math.Replace(@"\,"," ").Replace(@"\;"," ").Replace(@"\%","%").Replace(@"\left","").Replace(@"\right","").Trim();
        });
    }

    public static string ToMarkdown(FlowDocument document)=>WriteBlocks(document.Blocks).TrimEnd();
    private static string WriteBlocks(BlockCollection blocks)
    {
        var text=new StringBuilder();
        foreach(var block in blocks) {
            switch(block) {
                case Paragraph p:
                    var tag=p.Tag as string??"";
                    if(tag=="rule"){text.Append("---\n\n");break;}
                    if(tag.StartsWith("code:")){text.Append("```"+tag[5..]+"\n"+new TextRange(p.ContentStart,p.ContentEnd).Text.TrimEnd('\r','\n')+"\n```\n\n");break;}
                    if(tag.StartsWith("heading:"))text.Append(new string('#',int.Parse(tag[8..]))+" ");
                    text.Append(WriteInlines(p.Inlines)).Append("\n\n");break;
                case System.Windows.Documents.List list:
                    int index=list.StartIndex;
                    foreach(var item in list.ListItems){var body=WriteBlocks(item.Blocks).TrimEnd();var marker=list.MarkerStyle==TextMarkerStyle.Decimal?$"{index++}. ":"- ";var lines=body.Split('\n');text.Append(marker).Append(lines[0]).Append('\n');foreach(var line in lines.Skip(1))text.Append(new string(' ',marker.Length)).Append(line).Append('\n');}
                    text.Append('\n');break;
                case System.Windows.Documents.Table table:
                    bool header=true;
                    foreach(var group in table.RowGroups)foreach(var row in group.Rows){text.Append("| ").Append(string.Join(" | ",row.Cells.Select(c=>WriteBlocks(c.Blocks).Trim().Replace("\n"," ").Replace("|",@"\|")))).Append(" |\n");if(header){text.Append("| ").Append(string.Join(" | ",row.Cells.Select(_=>"---"))).Append(" |\n");header=false;}}
                    text.Append('\n');break;
                case Section section:
                    var content=WriteBlocks(section.Blocks);
                    text.Append(section.Tag as string=="quote"?string.Join("\n",content.TrimEnd().Split('\n').Select(line=>"> "+line))+"\n\n":content);break;
            }
        }
        return text.ToString();
    }
    private static string WriteInlines(InlineCollection inlines)
    {
        var text=new StringBuilder();
        foreach(var inline in inlines) {
            var value=inline switch {Run run=>run.Text,Span span=>WriteInlines(span.Inlines),LineBreak=>"  \n",_=>""};
            var tag=inline.Tag as string??"";
            if(tag=="code")value="`"+value+"`";
            else if(inline is Run)value=Regex.Replace(value,@"([\\`*\[\]<>_])",@"\$1");
            if(tag.StartsWith("link:"))value="["+value+"]("+tag[5..]+")";
            if(inline.ReadLocalValue(TextElement.FontWeightProperty)!=DependencyProperty.UnsetValue&&inline.FontWeight==FontWeights.Bold)value="**"+value+"**";
            if(inline.ReadLocalValue(TextElement.FontStyleProperty)!=DependencyProperty.UnsetValue&&inline.FontStyle==FontStyles.Italic)value="*"+value+"*";
            text.Append(value);
        }
        return text.ToString();
    }
}
