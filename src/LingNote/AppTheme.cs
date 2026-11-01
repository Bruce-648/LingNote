using System.Windows;
using System.Windows.Media;
namespace LingNote;
public static class AppTheme
{
    public static void Apply(Preferences prefs)
    {
        var accent=Parse(prefs.ThemeAccent,"#1263F1");var background=Parse(prefs.ThemeBackground,"#FFFFFF");
        Put("Accent",accent);Put("AccentSelection",Blend(accent,Colors.White,0.12));Put("Selection",Blend(accent,Colors.White,0.08));Put("Supplement",Blend(accent,Colors.White,0.06));Put("Surface",background);Put("SidebarSurface",Blend(background,Color.FromRgb(240,243,248),0.42));
    }
    private static Color Parse(string? hex,string fallback){try{return (Color)ColorConverter.ConvertFromString(hex??fallback);}catch{return (Color)ColorConverter.ConvertFromString(fallback);}}
    private static Color Blend(Color a,Color b,double amount)=>Color.FromRgb((byte)(a.R*amount+b.R*(1-amount)),(byte)(a.G*amount+b.G*(1-amount)),(byte)(a.B*amount+b.B*(1-amount)));
    private static void Put(string key,Color color)
    {
        var resources=Application.Current.Resources;
        if(resources[key] is SolidColorBrush existing&&!existing.IsFrozen)existing.Color=color;
        else resources[key]=new SolidColorBrush(color);
    }
}
