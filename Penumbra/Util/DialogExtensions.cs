using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Penumbra.Util
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
            var th         = new Thread( () => DialogThread( form, owner, taskSource ) );
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
                => Handle = handle;
        }

        public class HiddenForm : Form
        {
            private readonly CommonDialog                         _form;
            private readonly IWin32Window                         _owner;
            private readonly TaskCompletionSource< DialogResult > _taskSource;

            public HiddenForm( CommonDialog form, IWin32Window owner, TaskCompletionSource< DialogResult > taskSource )
            {
                _form       = form;
                _owner      = owner;
                _taskSource = taskSource;

                Opacity         = 0;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar   = false;
                Size            = new Size( 0, 0 );

                Shown += HiddenForm_Shown;
            }

            private void HiddenForm_Shown( object? sender, EventArgs _ )
            {
                Hide();
                try
                {
                    var result = _form.ShowDialog( _owner );
                    _taskSource.SetResult( result );
                }
                catch( Exception e )
                {
                    _taskSource.SetException( e );
                }

                Close();
            }
        }
    }
}