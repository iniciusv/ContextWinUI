using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace ContextWinUI.Services;
public class TagColorService : ObservableObject
{
    public static TagColorService Instance { get; } = new TagColorService();

    private readonly Dictionary<string, Color> _tagColors = new();
    public event EventHandler<string>? ColorChanged;
    private TagColorService()
    {
    // Você pode pré-definir algumas se quiser, ou deixar vazio
    }

    public Color GetColorForTag(string tag)
    {
        if (_tagColors.TryGetValue(tag, out var color))
        {
            return color;
        }

        // COR PADRÃO: Azul do Sistema (ou um azul fixo como DodgerBlue)
        // Isso atende ao requisito: "As tags terão a cor padrão azul anterior"
        return Color.FromArgb(255, 0, 120, 215); // #0078D7 (Windows Blue)
    }

    public void SetColorForTag(string tag, Color color)
    {
        _tagColors[tag] = color;
        ColorChanged?.Invoke(this, tag);
    // TODO: Salvar persistência aqui
    }
	public Dictionary<string, Color> GetAllColors()
	{
		// Retorna uma cópia para evitar modificação externa direta
		return new Dictionary<string, Color>(_tagColors);
	}
}