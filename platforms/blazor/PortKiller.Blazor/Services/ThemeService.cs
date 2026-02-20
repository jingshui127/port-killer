namespace PortKiller.Blazor.Services;

public class ThemeService
{
    private string _currentTheme = "dark";
    public event Action? OnThemeChanged;

    public string CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public void SetTheme(string theme)
    {
        CurrentTheme = theme;
    }

    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "dark" ? "light" : "dark";
    }

    public bool IsDarkTheme => CurrentTheme == "dark";
    public bool IsLightTheme => CurrentTheme == "light";
    
    public string GetMasaThemeClass()
    {
        return CurrentTheme == "dark" ? "theme--dark" : "theme--light";
    }
}
