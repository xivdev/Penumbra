using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Penumbra
{
    public static class DialogExtensions
    {
        public static Task< DialogResult > ShowDialogAsync( this CommonDialog form )
        {
            using var process = Process.GetCurrentProcess();
            return form.ShowDialogAsync( new DialogHandle( process.MainWindowHandle ) );
        }

        public static Task< DialogResult > ShowDialogAsync( this CommonDialog form, IWin32Window owner )
        {
            var taskSource = new TaskCompletionSource< DialogResult >();
            var th = new Thread( () => DialogThread( form, owner, taskSource ) );
            th.Start();
            return taskSource.Task;
        }

        [STAThread]
        private static void DialogThread( CommonDialog form, IWin32Window owner,
            TaskCompletionSource< DialogResult > taskSource )
        {
            Application.SetCompatibleTextRenderingDefault( false );
            Application.EnableVisualStyles();
            using var hiddenForm = new HiddenForm( form, owner, taskSource );
            Application.Run( hiddenForm );
            Application.ExitThread();
        }

        public class DialogHandle : IWin32Window
        {
            public IntPtr Handle { get; set; }

            public DialogHandle( IntPtr handle )
            {
                Handle = handle;
            }
        }

        public class HiddenForm : Form
        {
            private readonly CommonDialog form;
            private readonly IWin32Window owner;
            private readonly TaskCompletionSource< DialogResult > taskSource;

            public HiddenForm( CommonDialog form, IWin32Window owner, TaskCompletionSource< DialogResult > taskSource )
            {
                this.form = form;
                this.owner = owner;
                this.taskSource = taskSource;

                Opacity = 0;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                Size = new Size( 0, 0 );

                Shown += HiddenForm_Shown;
            }

            private void HiddenForm_Shown( object sender, EventArgs _ )
            {
                Hide();
                try
                {
                    var result = form.ShowDialog( owner );
                    taskSource.SetResult( result );
                }
                catch( Exception e )
                {
                    taskSource.SetException( e );
                }

                Close();
            }
        }
    }
}