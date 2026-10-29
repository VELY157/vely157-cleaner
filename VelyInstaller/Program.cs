namespace VelyInstaller;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var form = new InstallerForm();
        // Solo para capturas: VELY_PREVIEW_STEP=0..3 muestra ese paso sin instalar nada.
        if (int.TryParse(Environment.GetEnvironmentVariable("VELY_PREVIEW_STEP"), out var step)) form.Preview(step);
        Application.Run(form);
    }
}
