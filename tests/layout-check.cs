using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;
using DeepSeekHarness;

class LayoutCheck
{
    static List<string> failures = new List<string>();
    static void Validate(Control parent, string name)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            var scrolling = parent as ScrollableControl;
            if (!(parent is Form) && !(parent is TabControl) && !(parent is NumericUpDown) && !(parent is ListView) && !(scrolling != null && scrolling.AutoScroll))
            {
                if (child.Left < -1 || child.Top < -1 || child.Right > parent.ClientSize.Width + 1 || child.Bottom > parent.ClientSize.Height + 1)
                    failures.Add(name + ": clipped " + child.GetType().Name + " " + child.Text + " " + child.Bounds + " in " + parent.GetType().Name + " " + parent.ClientSize);
            }
            if (child is Label || child is Button || child is CheckBox)
            {
                Size required = child.GetPreferredSize(new Size(child.Width, 0));
                if (child.Height + 1 < required.Height || (child is CheckBox && child.Width + 1 < required.Width))
                    failures.Add(name + ": insufficient text space " + child.GetType().Name + " " + child.Text + " " + child.Size + " required " + required);
            }
            Validate(child, name);
        }
    }
    static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls) foreach (Control node in Descendants(child)) yield return node;
    }
    static void Dump(Control c, string indent, StreamWriter writer)
    {
        if (!c.Visible) return;
        writer.WriteLine(indent + c.GetType().Name + " " + c.Text.Replace("\r", "").Replace("\n", " ") + " bounds=" + c.Bounds + " preferred=" + c.GetPreferredSize(new Size(c.Width, 0)) + " margin=" + c.Margin);
        foreach (Control child in c.Controls) Dump(child, indent + "  ", writer);
    }
    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string output = Path.GetDirectoryName(args[0]);
        foreach (float scale in new[] { 1F, 1.25F, 1.5F })
        foreach (Size logical in new[] { new Size(1140, 780), new Size(980, 680) })
        using (var form = new DshManagerForm(false))
        {
            form.Show(); Application.DoEvents();
            // Simulate scaled geometry AND scaled fonts without changing Windows display settings.
            form.AutoScaleMode = AutoScaleMode.None;
            var fonts = new Dictionary<Control, Font>();
            foreach (Control c in Descendants(form)) fonts[c] = new Font(c.Font.FontFamily, c.Font.Size * scale, c.Font.Style);
            form.SuspendLayout();
            form.Scale(new SizeF(scale, scale));
            foreach (var entry in fonts) entry.Key.Font = entry.Value;
            form.Size = new Size((int)(logical.Width * scale), (int)(logical.Height * scale));
            form.ResumeLayout(true);
            TabControl tabs = (TabControl)typeof(DshManagerForm).GetField("tabControl", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            for (int page = 0; page < 4; page++)
            {
                tabs.SelectedIndex = page;
                Application.DoEvents(); form.PerformLayout(); Application.DoEvents();
                string name = "scale-" + (int)(scale * 100) + "-width-" + logical.Width + "-page-" + page;
                Validate(form, name);
                using (var writer = new StreamWriter(Path.Combine(output, name + ".txt"))) Dump(form, "", writer);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(Path.Combine(output, name + ".png")); }
            }
            tabs.SelectedIndex = 0; Application.DoEvents();
            var toggle = typeof(DshManagerForm).GetMethod("ToggleLog", BindingFlags.Instance | BindingFlags.NonPublic);
            var log = (Panel)typeof(DshManagerForm).GetField("logBody", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            int collapsed = log.Parent.Height;
            toggle.Invoke(form, null); Application.DoEvents();
            Validate(form, "expanded-log-" + scale + "-" + logical.Width);
            if (!log.Visible || log.Parent.Height < collapsed + log.Height) failures.Add("Log failed to expand");
            toggle.Invoke(form, null); Application.DoEvents();
            if (log.Visible || log.Parent.Height != collapsed) failures.Add("Log failed to restore collapsed height");
            form.Close();
            foreach (var font in fonts.Values) font.Dispose();
        }
        File.WriteAllLines(args[0], failures);
        Console.WriteLine("Layout cases: 24 pages + 6 log expand/collapse cases. Issues: " + failures.Count);
        Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    }
}
