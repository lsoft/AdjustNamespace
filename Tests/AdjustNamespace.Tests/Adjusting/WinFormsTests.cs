using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Windows Forms: the shape of the code the designer produces and the things which tie
    /// that code to a namespace.
    ///
    /// A form is a <c>partial class</c> split over <c>Form1.cs</c> and <c>Form1.Designer.cs</c>,
    /// the designer always writes the types of the solution by their FULL name
    /// (<c>new A.B.MyControl()</c>, <c>global::A.B.MyControl</c>), and the code around it
    /// (<c>Properties.Settings.Default</c>, <c>Application.Run(new Form1())</c>,
    /// <c>[Designer(typeof(...))]</c>) relies on the namespace as well.
    ///
    /// The test infrastructure references neither <c>System.Windows.Forms</c> nor
    /// <c>System.Drawing</c>, so the small stand-ins of <see cref="Stubs"/> are used: what matters
    /// here is the shape of the code, not the real controls.
    ///
    /// What <c>GeneratedCode</c> means for a designer file (<c>*.Designer.cs</c> is generated
    /// for it): the file is STILL a subject of the adjusting — it is planned, collected and
    /// moved like any other file, and the references to the moved types in it are fixed. A
    /// generated declaration only does not count as another declaration of a partial type
    /// (<c>NamespaceCenter</c>) and does not keep the old namespace of the type alive
    /// (<c>SymbolExtensions.IsNamespaceFilledOutside</c>), and a generated file is where a
    /// <c>global using</c> of a project file lives.
    ///
    /// Not covered here because a source rewrite cannot reach it: the manifest resource name
    /// of a <c>.resx</c> (<c>A.B.Form1.resources</c>) is derived by MSBuild at the next build
    /// from the namespace of the class of the code file the resx depends upon, so
    /// <c>new ComponentResourceManager(typeof(Form1))</c> follows the move by itself; a
    /// namespace written into a string literal (<c>new ResourceManager("A.Properties.Resources", ...)</c>,
    /// <c>[Designer("A.B.MyDesigner, MyApp")]</c>), the <c>type=""</c> of a <c>.resx</c> entry, a
    /// <c>.settings</c> file (<c>GeneratedClassNamespace</c>), a <c>.datasource</c> file and a
    /// <c>.licx</c> file are text of other files and are left as they are.
    /// </summary>
    public class WinFormsTests
    {
        /// <summary>
        /// The few types of Windows Forms the shapes below need, declared in their real namespaces
        /// (<c>System.*</c> is a special namespace, the file is never adjusted).
        /// </summary>
        private const string Stubs =
@"namespace System.ComponentModel
{
    public interface IContainer : IDisposable { }

    public class Container : IContainer
    {
        public void Dispose() { }
    }

    public class Component : IDisposable
    {
        public void Dispose() { Dispose(true); }

        protected virtual void Dispose(bool disposing) { }
    }

    public class ComponentResourceManager : System.Resources.ResourceManager
    {
        public ComponentResourceManager(Type type) : base(type) { }
    }

    public class TypeConverter { }

    public sealed class DesignerAttribute : Attribute
    {
        public DesignerAttribute(Type designerType) { }
    }

    public sealed class DefaultEventAttribute : Attribute
    {
        public DefaultEventAttribute(string name) { }
    }

    public sealed class TypeConverterAttribute : Attribute
    {
        public TypeConverterAttribute(Type type) { }
    }
}

namespace System.Drawing
{
    public sealed class ToolboxBitmapAttribute : Attribute
    {
        public ToolboxBitmapAttribute(Type t, string imageName) { }
    }
}

namespace System.Configuration
{
    public class ApplicationSettingsBase
    {
        public static ApplicationSettingsBase Synchronized(ApplicationSettingsBase settingsBase) { return settingsBase; }

        public object this[string propertyName]
        {
            get { return null; }
            set { }
        }
    }
}

namespace System.Windows.Forms
{
    public class Control : System.ComponentModel.Component
    {
        public class ControlCollection
        {
            public void Add(Control value) { }
        }

        public ControlCollection Controls { get; } = new ControlCollection();

        public string Name { get; set; }

        public string Text { get; set; }

        public void SuspendLayout() { }

        public void ResumeLayout(bool performLayout) { }
    }

    public class Button : Control { }

    public class Form : Control { }

    public class UserControl : Control { }

    public class BindingSource : System.ComponentModel.Component
    {
        public object DataSource { get; set; }
    }

    public static class Application
    {
        public static void Run(Form mainForm) { }
    }
}
";

        /// <summary>
        /// The form <c>MainForm</c> as the designer of Visual Studio produces it: the
        /// code file, the designer file and the starting <c>Program</c>. Everything is in the
        /// root namespace <c>WinApp</c> and has to move into <c>WinApp.Forms</c>.
        /// </summary>
        private static TestSolution FormSolution()
        {
            return NewSolution()
                .AddDocument("WinApp", @"Forms\MainForm.cs", MainFormBody)
                .AddDocument("WinApp", @"Forms\MainForm.Designer.cs", MainFormDesignerBody)
                .AddDocument("WinApp", "Program.cs",
@"using System;
using System.Windows.Forms;

namespace WinApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.Run(new MainForm());
        }
    }
}
")
                ;
        }

        private const string MainFormBody =
@"using System.Windows.Forms;

namespace WinApp
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
    }
}
";

        private const string MainFormDesignerBody =
@"namespace WinApp
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            this.button1.Name = ""button1"";
            this.Controls.Add(this.button1);
            this.Name = ""MainForm"";
            this.Text = ""MainForm"";
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button button1;
    }
}
";

        /// <summary>
        /// A form and its designer file chosen together (the wizard always offers both: the
        /// designer is a child of the form in the solution tree) are moved together, in any
        /// order, so the partial class ends up in a single namespace.
        /// <c>typeof(MainForm)</c> of the <c>ComponentResourceManager</c> keeps resolving
        /// and so does the <c>Application.Run(new MainForm())</c> of <c>Program</c>, which
        /// gets the new using clause. The name of the resource the manager looks for is
        /// derived by MSBuild from the new namespace on the next build.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task A_form_and_its_designer_chosen_together_move_together(bool designerFirst)
        {
            using var solution = FormSolution();

            Assert.Empty(await solution.CompilationErrorsAsync());

            var form = solution.PathOf("WinApp", @"Forms\MainForm.cs");
            var designer = solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs");

            var outcome = await RunAsync(
                solution,
                designerFirst
                    ? new[] { designer, form }
                    : new[] { form, designer }
                );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);
            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\MainForm.cs"));

            var designerText = solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs");
            Assert.Contains("namespace WinApp.Forms", designerText);
            Assert.Contains("new System.ComponentModel.ComponentResourceManager(typeof(MainForm))", designerText);

            Assert.Contains("using WinApp.Forms;", solution.TextOf("WinApp", "Program.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The templates of .NET 6+ use file scoped namespaces in both halves of a form
        /// (<c>namespace WinFormsApp1;</c>).
        /// </summary>
        [Fact]
        public async Task A_form_with_file_scoped_namespaces_moves_together_with_its_designer()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Forms\MainForm.cs",
@"namespace WinApp;

public partial class MainForm : System.Windows.Forms.Form
{
    public MainForm()
    {
        InitializeComponent();
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.Designer.cs",
@"namespace WinApp;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        this.Text = ""MainForm"";
    }
}
")
                ;

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Forms\MainForm.cs"),
                solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs")
                );

            Assert.Contains("namespace WinApp.Forms;", solution.TextOf("WinApp", @"Forms\MainForm.cs"));
            Assert.Contains("namespace WinApp.Forms;", solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A component (<c>Component1.Designer.cs</c>) is the same pair of files as a form.
        /// </summary>
        [Fact]
        public async Task A_component_and_its_designer_chosen_together_move_together()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Parts\Timer1.cs",
@"namespace WinApp
{
    public partial class Timer1 : System.ComponentModel.Component
    {
        public Timer1()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("WinApp", @"Parts\Timer1.Designer.cs",
@"namespace WinApp
{
    partial class Timer1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
        }
    }
}
")
                .AddDocument("WinApp", "Consumer.cs",
@"namespace WinApp
{
    public class Consumer
    {
        public Timer1 Create() => new Timer1();
    }
}
")
                ;

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Parts\Timer1.cs"),
                solution.PathOf("WinApp", @"Parts\Timer1.Designer.cs")
                );

            Assert.Contains("namespace WinApp.Parts", solution.TextOf("WinApp", @"Parts\Timer1.cs"));
            Assert.Contains("namespace WinApp.Parts", solution.TextOf("WinApp", @"Parts\Timer1.Designer.cs"));
            Assert.Contains("using WinApp.Parts;", solution.TextOf("WinApp", "Consumer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// "Generated" does not mean "skipped": the designer file is planned like any other
        /// file, with the namespace of its folder, and a designer which is in the right
        /// namespace already is dropped silently.
        /// </summary>
        [Fact]
        public async Task A_designer_file_is_planned_like_any_other_file()
        {
            using var solution = FormSolution()
                .AddDocument("WinApp", @"Done\Settled.Designer.cs",
@"namespace WinApp.Done
{
    partial class Settled { }
}
")
                .AddDocument("WinApp", @"Done\Settled.cs",
@"namespace WinApp.Done
{
    public partial class Settled { }
}
")
                ;

            var planner = new AdjustPlanner(solution.Context, NoRegex());

            var plan = await planner.TryPlanAsync(solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs"));
            Assert.NotNull(plan);
            Assert.Equal("WinApp.Forms", plan!.Value.TargetNamespace);
            Assert.Equal(new[] { "WinApp" }, plan.Value.Transitions.Transitions.Select(t => t.OriginalName));

            var settled = await planner.PlanAsync(solution.PathOf("WinApp", @"Done\Settled.Designer.cs"));
            Assert.False(settled.HasPlan);
            Assert.False(settled.HasBlock);
        }

        /// <summary>
        /// Was red: the scan of the second wizard step has to collect both halves of a form and to
        /// block neither. It blocks the second one (<see cref="AdjustBlockKind.TypeNameConflict"/>,
        /// "already contains a type 'MainForm'") — the very first thing every Windows Forms
        /// user does. <c>SubjectFileCollector</c> reserves the types of a collected file in the
        /// target namespace so that two subject files cannot land the same name there, and the
        /// other half of a <c>partial</c> type is the same type and not a second one.
        /// </summary>
        [Fact]
        public async Task The_form_and_its_designer_are_both_collected()
        {
            using var solution = FormSolution();

            var results = await CollectAsync(
                solution,
                solution.PathOf("WinApp", @"Forms\MainForm.cs"),
                solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs")
                );

            Assert.Empty(results.Blocked.Select(b => $"{b.Kind}: {b.FilePath}: {b.Message}"));
            Assert.Equal(
                new[]
                {
                    solution.PathOf("WinApp", @"Forms\MainForm.cs"),
                    solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs")
                }.OrderBy(p => p),
                results.CollectedFiles.Select(f => f.FilePath).OrderBy(p => p)
                );
        }

        /// <summary>
        /// Was red: the whole flow of the wizard for a form chosen with its designer — the scan, then
        /// the session over the collected files. The scan blocks the designer
        /// (see <see cref="The_form_and_its_designer_are_both_collected"/>), so only the code
        /// file is adjusted and the partial class is torn apart exactly as in
        /// <see cref="Only_the_code_file_of_a_form_does_not_tear_the_partial_class_apart"/>, although
        /// the user did choose both files.
        /// </summary>
        [Fact]
        public async Task A_form_chosen_with_its_designer_is_adjusted_by_the_flow_of_the_wizard()
        {
            using var solution = FormSolution();

            await AdjustCollectedAsync(
                solution,
                solution.PathOf("WinApp", @"Forms\MainForm.cs"),
                solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs")
                );

            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\MainForm.cs"));
            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: the user chose the code file of a form and not its designer (a file of the
        /// solution tree selected on its own, the command line of the console utility). The
        /// two halves of the partial class <c>MainForm</c> are moved apart: the code file is
        /// in <c>WinApp.Forms</c>, the designer — with <c>InitializeComponent</c> and the fields
        /// of the controls — stays in <c>WinApp</c>, and the solution does not compile anymore
        /// (CS0103). Nothing looks at the other declarations of a partial type:
        /// <c>AdjustPlanner</c> and <c>SubjectFileCollector</c> neither block the file nor add the
        /// missing half to the list, and <c>NamespaceCenter</c>/<c>IsNamespaceFilledOutside</c> even
        /// ignore a declaration in a <c>*.Designer.cs</c> as "generated".
        /// The wanted behaviour: the half is collected too, or the file is blocked.
        /// </summary>
        [Fact]
        public async Task Only_the_code_file_of_a_form_does_not_tear_the_partial_class_apart()
        {
            using var solution = FormSolution();

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustCollectedAsync(solution, solution.PathOf("WinApp", @"Forms\MainForm.cs"));

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: the same as <see cref="Only_the_code_file_of_a_form_does_not_tear_the_partial_class_apart"/>
        /// with the designer file chosen alone: the designer is moved into <c>WinApp.Forms</c>
        /// while <c>MainForm.cs</c>, which calls <c>InitializeComponent</c>, stays in <c>WinApp</c>.
        /// </summary>
        [Fact]
        public async Task Only_the_designer_of_a_form_does_not_tear_the_partial_class_apart()
        {
            using var solution = FormSolution();

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustCollectedAsync(solution, solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs"));

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The designer writes the controls of the solution by their full name, with and
        /// without <c>global::</c>, and the nested ones by the name of the outer class. The
        /// designer file is not chosen (it is where it has to be already), but the controls
        /// move, so the full names in it follow them.
        /// </summary>
        [Fact]
        public async Task The_full_names_of_the_controls_in_a_designer_follow_the_moved_controls()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Controls\MyControl.cs",
@"namespace A.B
{
    public class MyControl : System.Windows.Forms.UserControl { }
}
")
                .AddDocument("WinApp", @"Controls\Outer.cs",
@"namespace A.B
{
    public class Outer
    {
        public class Inner : System.Windows.Forms.Control { }
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.cs",
@"namespace WinApp.Forms
{
    public partial class MainForm : System.Windows.Forms.Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.Designer.cs",
@"namespace WinApp.Forms
{
    partial class MainForm
    {
        private void InitializeComponent()
        {
            this.myControl1 = new A.B.MyControl();
            this.inner1 = new global::A.B.Outer.Inner();
            this.inner2 = new A.B.Outer.Inner();
            this.myControl1.Name = ""myControl1"";
            this.Controls.Add(this.myControl1);
            this.Controls.Add(this.inner1);
            this.Controls.Add(this.inner2);
        }

        private A.B.MyControl myControl1;
        private global::A.B.Outer.Inner inner1;
        private A.B.Outer.Inner inner2;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Controls\MyControl.cs"),
                solution.PathOf("WinApp", @"Controls\Outer.cs")
                );

            var designer = solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs");

            Assert.Contains("this.myControl1 = new WinApp.Controls.MyControl();", designer);
            Assert.Contains("private WinApp.Controls.MyControl myControl1;", designer);
            Assert.Contains("this.inner1 = new global::WinApp.Controls.Outer.Inner();", designer);
            Assert.Contains("private global::WinApp.Controls.Outer.Inner inner1;", designer);
            Assert.Contains("this.inner2 = new WinApp.Controls.Outer.Inner();", designer);
            Assert.DoesNotContain("A.B.", designer);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A user control library: the control lives in one project, the form which hosts it —
        /// and the designer file with its full name — in another one.
        /// </summary>
        [Fact]
        public async Task A_control_of_another_project_is_followed_in_the_designer()
        {
            using var solution = new TestSolution()
                .AddProject("WinLib")
                .AddProject("WinApp")
                .AddProjectReference("WinApp", "WinLib")
                .AddDocument("WinLib", "Stubs.cs", Stubs)
                .AddDocument("WinLib", @"Controls\FancyButton.cs",
@"namespace Old.Lib
{
    public class FancyButton : System.Windows.Forms.Button { }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.cs",
@"namespace WinApp.Forms
{
    public partial class MainForm : System.Windows.Forms.Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.Designer.cs",
@"namespace WinApp.Forms
{
    partial class MainForm
    {
        private void InitializeComponent()
        {
            this.fancyButton1 = new Old.Lib.FancyButton();
            this.Controls.Add(this.fancyButton1);
        }

        private Old.Lib.FancyButton fancyButton1;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(solution, solution.PathOf("WinLib", @"Controls\FancyButton.cs"));

            var designer = solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs");

            Assert.Contains("this.fancyButton1 = new WinLib.Controls.FancyButton();", designer);
            Assert.Contains("private WinLib.Controls.FancyButton fancyButton1;", designer);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A visually inherited form: the base form moves, the code file of the derived form
        /// (and not only its designer) gets the new name.
        /// </summary>
        [Fact]
        public async Task The_base_form_of_a_visually_inherited_form_is_followed()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Base\BaseForm.cs",
@"namespace Old.Base
{
    public class BaseForm : System.Windows.Forms.Form { }
}
")
                .AddDocument("WinApp", @"Forms\Dialog.cs",
@"using Old.Base;

namespace WinApp.Forms
{
    public partial class Dialog : BaseForm
    {
        public Dialog()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\Dialog.Designer.cs",
@"namespace WinApp.Forms
{
    partial class Dialog
    {
        private void InitializeComponent()
        {
            this.Text = ""Dialog"";
        }
    }
}
")
                ;

            await RunAsync(solution, solution.PathOf("WinApp", @"Base\BaseForm.cs"));

            var dialog = solution.TextOf("WinApp", @"Forms\Dialog.cs");

            Assert.Contains("using WinApp.Base;", dialog);
            Assert.DoesNotContain("using Old.Base;", dialog);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The attributes which tie a control to the design time: the types they name go through
        /// <c>typeof</c> (<c>[Designer]</c>, <c>[TypeConverter]</c>, <c>[ToolboxBitmap]</c>) and are
        /// followed, the string argument (<c>[DefaultEvent("Click")]</c>) is no type at all. The
        /// resource of <c>[ToolboxBitmap(typeof(MyControl), "MyControl.bmp")]</c> is named by MSBuild
        /// out of the new namespace of <c>MyControl</c>.
        /// </summary>
        [Fact]
        public async Task The_types_named_by_the_design_time_attributes_are_followed()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Design\MyDesigner.cs",
@"namespace Old.Design
{
    public class MyDesigner { }
}
")
                .AddDocument("WinApp", @"Converters\MyConverter.cs",
@"namespace Old.Conv
{
    public class MyConverter : System.ComponentModel.TypeConverter { }
}
")
                .AddDocument("WinApp", @"Controls\MyControl.cs",
@"namespace Old.Ctl
{
    [System.ComponentModel.Designer(typeof(Old.Design.MyDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(MyControl), ""MyControl.bmp"")]
    [System.ComponentModel.DefaultEvent(""Click"")]
    [System.ComponentModel.TypeConverter(typeof(Old.Conv.MyConverter))]
    public class MyControl : System.Windows.Forms.UserControl
    {
        [System.ComponentModel.TypeConverter(typeof(global::Old.Conv.MyConverter))]
        public string Caption { get; set; }
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Design\MyDesigner.cs"),
                solution.PathOf("WinApp", @"Converters\MyConverter.cs"),
                solution.PathOf("WinApp", @"Controls\MyControl.cs")
                );

            var control = solution.TextOf("WinApp", @"Controls\MyControl.cs");

            Assert.Contains("namespace WinApp.Controls", control);
            Assert.Contains("typeof(WinApp.Design.MyDesigner)", control);
            Assert.Contains("typeof(WinApp.Converters.MyConverter)", control);
            Assert.Contains("typeof(global::WinApp.Converters.MyConverter)", control);
            Assert.Contains("typeof(MyControl), \"MyControl.bmp\"", control);
            Assert.DoesNotContain("Old.", control);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Data binding: the designer assigns the model type to a <c>BindingSource</c>
        /// (<c>DataSource = typeof(A.B.Customer)</c>), with and without <c>global::</c>.
        /// </summary>
        [Fact]
        public async Task The_data_source_type_of_a_binding_source_is_followed()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Models\Customer.cs",
@"namespace Old.Model
{
    public class Customer { }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.cs",
@"namespace WinApp.Forms
{
    public partial class MainForm : System.Windows.Forms.Form
    {
        public MainForm()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.Designer.cs",
@"namespace WinApp.Forms
{
    partial class MainForm
    {
        private void InitializeComponent()
        {
            this.customerBindingSource = new System.Windows.Forms.BindingSource();
            this.customerBindingSource.DataSource = typeof(Old.Model.Customer);
            this.otherBindingSource = new System.Windows.Forms.BindingSource();
            this.otherBindingSource.DataSource = typeof(global::Old.Model.Customer);
        }

        private System.Windows.Forms.BindingSource customerBindingSource;
        private System.Windows.Forms.BindingSource otherBindingSource;
    }
}
")
                ;

            await RunAsync(solution, solution.PathOf("WinApp", @"Models\Customer.cs"));

            var designer = solution.TextOf("WinApp", @"Forms\MainForm.Designer.cs");

            Assert.Contains("DataSource = typeof(WinApp.Models.Customer);", designer);
            Assert.Contains("DataSource = typeof(global::WinApp.Models.Customer);", designer);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>Properties\Resources.Designer.cs</c> (generated out of the resx): the class is
        /// moved into the namespace of its folder together with the code which uses it as
        /// <c>Properties.Resources</c>. The literal
        /// <c>new ResourceManager("Legacy.Properties.Resources", ...)</c> is a string and no
        /// reference; the custom tool writes it again out of the location of the resx on its
        /// next run, which is exactly the namespace this file has been moved into.
        /// </summary>
        [Fact]
        public async Task The_resources_class_moves_together_with_its_users()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"Properties\Resources.Designer.cs",
@"namespace Legacy.Properties
{
    internal class Resources
    {
        private static global::System.Resources.ResourceManager resourceMan;

        internal static global::System.Resources.ResourceManager ResourceManager
        {
            get
            {
                if (object.ReferenceEquals(resourceMan, null))
                {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager(""Legacy.Properties.Resources"", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        internal static string Greeting
        {
            get { return ResourceManager.GetString(""Greeting"", null); }
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\LoginForm.cs",
@"namespace Legacy.Forms
{
    public class LoginForm : System.Windows.Forms.Form
    {
        public LoginForm()
        {
            Text = Properties.Resources.Greeting;
        }
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Properties\Resources.Designer.cs"),
                solution.PathOf("WinApp", @"Forms\LoginForm.cs")
                );

            Assert.Contains("namespace WinApp.Properties", solution.TextOf("WinApp", @"Properties\Resources.Designer.cs"));
            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\LoginForm.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>Settings.cs</c> (the "View Code" half) and <c>Settings.Designer.cs</c> are the
        /// partial class <c>Settings</c>; both are moved, and <c>Properties.Settings.Default</c>
        /// of a form keeps resolving.
        /// </summary>
        [Fact]
        public async Task The_settings_class_moves_together_with_its_designer_and_its_users()
        {
            using var solution = NewSettingsSolution();

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Properties\Settings.cs"),
                solution.PathOf("WinApp", @"Properties\Settings.Designer.cs"),
                solution.PathOf("WinApp", @"Forms\LoginForm.cs")
                );

            Assert.Contains("namespace WinApp.Properties", solution.TextOf("WinApp", @"Properties\Settings.cs"));
            Assert.Contains("namespace WinApp.Properties", solution.TextOf("WinApp", @"Properties\Settings.Designer.cs"));
            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\LoginForm.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: <c>Properties.Settings.Default</c> / <c>Properties.Resources.X</c> is written
        /// in nearly every Windows Forms project, and it is a partially qualified name: it
        /// resolves because the form sits inside the namespace which contains the namespace
        /// <c>Properties</c>. A form which is moved out of it (here <c>Legacy.Forms</c> to
        /// <c>WinApp.Forms</c>, the project having been renamed), while <c>Settings</c> stays,
        /// loses the name: CS0234. <c>SelfReferenceFixer</c> considers a bare type name
        /// only, and <c>Properties</c> is a namespace, whereas <c>Settings</c> is the right part
        /// of a member access and is "already qualified". A <c>using Legacy;</c> would not help
        /// either (a using clause imports the types of a namespace, not its child namespaces), so
        /// the wanted fix is to write the name out (<c>Legacy.Properties.Settings.Default</c>).
        /// </summary>
        [Fact]
        public async Task A_form_which_leaves_the_namespace_of_Properties_Settings_still_finds_it()
        {
            using var solution = NewSettingsSolution();

            Assert.Empty(await solution.CompilationErrorsAsync());

            await RunAsync(solution, solution.PathOf("WinApp", @"Forms\LoginForm.cs"));

            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\LoginForm.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The entry point: <c>ApplicationConfiguration.Initialize()</c> is a type generated
        /// into the root namespace by a source generator (a file of the <c>obj</c> folder, which
        /// is a generated file). <c>Program</c> sees it because it is nested in that namespace;
        /// a <c>Program</c> which leaves the root namespace gets a using clause for it.
        /// </summary>
        [Fact]
        public async Task A_program_which_leaves_the_root_namespace_still_finds_the_generated_application_configuration()
        {
            using var solution = NewSolution()
                .AddDocument("WinApp", @"obj\Debug\net8.0-windows\ApplicationConfiguration.g.cs",
@"namespace WinApp
{
    internal static partial class ApplicationConfiguration
    {
        public static void Initialize() { }
    }
}
")
                .AddDocument("WinApp", @"Forms\MainForm.cs",
@"namespace WinApp
{
    public class MainForm : System.Windows.Forms.Form { }
}
")
                .AddDocument("WinApp", "Program.cs",
@"namespace WinApp.Startup
{
    internal static class Program
    {
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.Run(new WinApp.MainForm());
        }
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(solution, "WinApp", "Program.cs", "Other.Startup");

            var program = solution.TextOf("WinApp", "Program.cs");

            Assert.Contains("namespace Other.Startup", program);
            Assert.Contains("using WinApp;", program);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A <c>.resx</c> next to a form is no C# document: it is never touched, and it does not
        /// stop the form beside it from being moved. Its resource name is derived from the
        /// namespace of the form by MSBuild, so there is nothing to rewrite in it.
        /// </summary>
        [Fact]
        public async Task A_resx_file_chosen_with_a_form_is_left_alone()
        {
            using var solution = FormSolution();

            var resxPath = solution.PathOf("WinApp", @"Forms\MainForm.resx");
            Directory.CreateDirectory(Path.GetDirectoryName(resxPath)!);
            File.WriteAllText(resxPath, "<root><data name=\"$this.Text\"><value>MainForm</value></data></root>");

            var plan = await AdjustPlanner.PlanAsync(solution.Workspace, resxPath, "WinApp.Forms");
            Assert.True(plan.HasBlock);
            Assert.Equal(AdjustBlockKind.NotAProcessableDocument, plan.Block!.Value.Kind);

            var before = File.ReadAllBytes(resxPath);

            var outcome = await RunAsync(
                solution,
                solution.PathOf("WinApp", @"Forms\MainForm.cs"),
                solution.PathOf("WinApp", @"Forms\MainForm.Designer.cs"),
                resxPath
                );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);
            Assert.Equal(before, File.ReadAllBytes(resxPath));
            Assert.Contains("namespace WinApp.Forms", solution.TextOf("WinApp", @"Forms\MainForm.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The pair <c>Settings.cs</c> + <c>Settings.Designer.cs</c> and the form which uses
        /// <c>Properties.Settings.Default</c>, all of them in the namespace <c>Legacy</c> of a
        /// project whose root namespace is <c>WinApp</c>.
        /// </summary>
        private static TestSolution NewSettingsSolution()
        {
            return NewSolution()
                .AddDocument("WinApp", @"Properties\Settings.cs",
@"namespace Legacy.Properties
{
    internal sealed partial class Settings
    {
        public Settings() { }
    }
}
")
                .AddDocument("WinApp", @"Properties\Settings.Designer.cs",
@"namespace Legacy.Properties
{
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default
        {
            get { return defaultInstance; }
        }

        public string UserName
        {
            get { return ((string)(this[""UserName""])); }
            set { this[""UserName""] = value; }
        }
    }
}
")
                .AddDocument("WinApp", @"Forms\LoginForm.cs",
@"namespace Legacy.Forms
{
    public class LoginForm : System.Windows.Forms.Form
    {
        public LoginForm()
        {
            Text = Properties.Settings.Default.UserName;
        }
    }
}
")
                ;
        }

        private static TestSolution NewSolution()
        {
            return new TestSolution()
                .AddProject("WinApp")
                .AddDocument("WinApp", "Stubs.cs", Stubs)
                ;
        }

        private static async System.Threading.Tasks.Task<AdjustSessionOutcome> RunAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            return await new AdjustSession(solution.Context, NoRegex())
                .RunAsync(subjectFilePaths, null, CancellationToken.None);
        }

        /// <summary>
        /// What the wizard does: the scan of the chosen files first, the session over the
        /// files the scan has collected afterwards.
        /// </summary>
        private static async System.Threading.Tasks.Task AdjustCollectedAsync(
            TestSolution solution,
            params string[] chosenFilePaths
            )
        {
            var results = await CollectAsync(solution, chosenFilePaths);

            await RunAsync(solution, results.CollectedFiles.Select(f => f.FilePath).ToArray());
        }

        private static async System.Threading.Tasks.Task<SubjectFileCollector.SubjectCollectingResults> CollectAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            var collector = new SubjectFileCollector(
                solution.Context,
                new HashSet<string>(subjectFilePaths),
                NoRegex()
                );

            return await collector.AnalyzeAndCollectAsync((i, total, filePath) => { });
        }

        private static NamespaceReplaceRegex NoRegex()
        {
            return new NamespaceReplaceRegex(string.Empty, string.Empty);
        }
    }
}
