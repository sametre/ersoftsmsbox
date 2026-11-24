using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SetUICulture("tr-TR")]
public class Form1Tests
{
    [Test]
    public void WhenMainFormIsCreatedThenShellTitleIsSet()
    {
        using Form1 form = new();

        Assert.That(form.Text, Is.EqualTo("ERSoft SMS Box"));
    }

    [Test]
    public void WhenCloseIsRequestedThenWindowIsHidden()
    {
        using Form1 form = new();
        form.Show();

        form.Close();

        Assert.That(form.Visible, Is.False);
    }

    [Test]
    public void WhenCloseIsRequestedThenFormRemainsAlive()
    {
        using Form1 form = new();
        form.Show();

        form.Close();

        Assert.That(form.IsDisposed, Is.False);
    }

    [Test]
    public void WhenWindowIsMinimizedThenWindowIsHidden()
    {
        using Form1 form = new();
        form.Show();

        form.WindowState = FormWindowState.Minimized;

        Assert.That(form.Visible, Is.False);
    }

    [Test]
    public void WhenHiddenWindowIsShownAgainThenWindowIsVisible()
    {
        using Form1 form = new();
        form.Show();
        form.Close();

        form.Show();

        Assert.That(form.Visible, Is.True);
    }
}
