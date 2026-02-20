using Masa.Blazor;

namespace PortKiller.Blazor.Services;

public class ThemeService
{
    private readonly MasaBlazor _masaBlazor;

    public event Action? OnThemeChanged;

    public bool IsDarkTheme => _masaBlazor.Theme.DefaultTheme == "dark";
    
    public string CurrentTheme => IsDarkTheme ? "dark" : "light";

    public ThemeService(MasaBlazor masaBlazor)
    {
        _masaBlazor = masaBlazor;
    }

    public void ToggleTheme()
    {
        var newTheme = IsDarkTheme ? "light" : "dark";
        _masaBlazor.SetTheme(newTheme);
        OnThemeChanged?.Invoke();
    }

    public void SetTheme(string theme)
    {
        _masaBlazor.SetTheme(theme);
        OnThemeChanged?.Invoke();
    }
}
