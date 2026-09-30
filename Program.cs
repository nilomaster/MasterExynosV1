namespace ZeroKnoxRemoval
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
            {
                MessageBox.Show($"Ocorreu um aviso: {e.Exception.Message}", "ZeroKnox Removal", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    MessageBox.Show($"Erro critico: {ex.Message}", "ZeroKnox Removal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
