using System.ComponentModel;
using System.Windows;
namespace LingNote;

// Closing an activated window can synchronously raise Deactivated.
// All transient-menu close paths must share the same reentrancy guard.
public sealed class DismissibleWindow : Window
{
    private bool closingOrClosed;
    public void Dismiss()
    {
        if(closingOrClosed)return;
        Close();
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        closingOrClosed=true;
        base.OnClosing(e);
        if(e.Cancel)closingOrClosed=false;
    }
    protected override void OnClosed(EventArgs e)
    {
        closingOrClosed=true;
        base.OnClosed(e);
    }
}
