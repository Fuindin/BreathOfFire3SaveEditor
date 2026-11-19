namespace BreathOfFire3SaveEditor;

/// <summary>
/// Remembers the last save state that was opened, so the editor comes back up
/// pointed at the file you were working on.
/// </summary>
public static class Settings
{
    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BreathOfFire3SaveEditor");

    private static string PathFile => Path.Combine(Folder, "last-state.txt");

    public static string? LastFile
    {
        get
        {
            try
            {
                return File.Exists(PathFile) ? File.ReadAllText(PathFile).Trim() : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathFile, value ?? string.Empty);
            }
            catch (IOException)
            {
                // Remembering the path is a convenience; never fail a save over it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
