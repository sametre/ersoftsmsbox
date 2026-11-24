using System.Threading;
using System.Windows.Forms;

namespace ERSoftSMSBox.Tests;

internal static class UiTestPump
{
    internal static void DoEvents()
    {
        // Application.Run keeps this context installed in production. A standalone DoEvents
        // loop can uninstall it when it returns, so keep async continuations on this STA thread.
        SynchronizationContext context = SynchronizationContext.Current is WindowsFormsSynchronizationContext current
            ? current : new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try { Application.DoEvents(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
}
