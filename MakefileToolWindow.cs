using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MakefileReader;

/// <summary>
/// Tool window for displaying and executing Makefile targets
/// </summary>
[Guid("4ad2b430-0651-4d2d-8c90-84b8c4b4b8c8")]
public class MakefileToolWindow : BaseToolWindow<MakefileToolWindow>
{
    public override string GetTitle(int toolWindowId) => "Makefile Reader";

    public override Type PaneType => typeof(Pane);

    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        return Task.FromResult<FrameworkElement>(new MakefileToolWindowControl());
    }

    [Guid("4ad2b430-0651-4d2d-8c90-84b8c4b4b8c8")]
    internal class Pane : ToolWindowPane
    {
        public Pane()
        {
            BitmapImageMoniker = KnownMonikers.DocumentOutline;
        }
    }
}