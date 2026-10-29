namespace VelyCleaner;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var client = new LicenseClient();

        // Si ya hay una key guardada se verifica sola; si no, se pide.
        LicenseResult? result = null;
        if (client.SavedKey is { } saved)
            result = client.CheckAsync(saved).GetAwaiter().GetResult();

        while (true)
        {
            if (result is not { Valid: true })
            {
                using var keyForm = new KeyForm(client, result?.Message);
                if (keyForm.ShowDialog() != DialogResult.OK) return;
                result = keyForm.Result!;
            }

            using var main = new MainForm(client, result);
            Application.Run(main);
            if (!main.ChangeKeyRequested) return;
            client.Clear();
            result = null;
        }
    }
}
