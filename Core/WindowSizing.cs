namespace OfiConvert.Core;

/// <summary>
/// El tamaño de la ventana, en píxeles <b>físicos</b>, para el DPI de la pantalla donde nace.
/// </summary>
/// <remarks>
/// <c>AppWindow.Resize</c> habla en píxeles físicos, no en unidades escaladas: pedirle 1050×800 a pelo
/// da esa ventana solo al 100 %. Al 150 % —lo normal en un portátil moderno— el contenido se dibuja un
/// 50 % más grande dentro de la misma caja, así que la ventana nace **un tercio más pequeña** de lo
/// pensado y el layout aparece apretado sin que nadie haya tocado nada. (TJ-16, 2026-09-01.)
///
/// El mínimo no es decorativo: los desplegables tienen ancho fijo (110/140/160 px) y las etiquetas
/// alemanas son las más largas de los ocho idiomas; por debajo de ese tamaño el layout se rompe. Vive
/// aquí, en <c>Core/</c>, porque es aritmética pura y se puede probar sin abrir una ventana.
/// </remarks>
public static class WindowSizing
{
    /// <summary>DPI de referencia de Windows: el 100 %.</summary>
    public const uint BaselineDpi = 96;

    /// <summary>Tamaño de apertura, en unidades de 96 ppp.</summary>
    public const int DefaultWidth = 1050;
    public const int DefaultHeight = 800;

    /// <summary>Tamaño mínimo usable, en unidades de 96 ppp.</summary>
    public const int MinimumWidth = 880;
    public const int MinimumHeight = 620;

    /// <summary>Convierte unidades de 96 ppp a píxeles físicos para <paramref name="dpi"/>.</summary>
    /// <remarks>
    /// Un <paramref name="dpi"/> de 0 significa «no se pudo averiguar» (<c>GetDpiForWindow</c> devuelve 0
    /// con un handle inválido): se cae al 100 %, que es peor que acertar pero mejor que una ventana de
    /// tamaño cero.
    /// </remarks>
    public static (int Width, int Height) Scale(int width, int height, uint dpi)
    {
        double factor = dpi == 0 ? 1.0 : dpi / (double)BaselineDpi;

        return ((int)Math.Round(width * factor), (int)Math.Round(height * factor));
    }

    /// <summary>Tamaño de apertura para ese DPI.</summary>
    public static (int Width, int Height) Default(uint dpi) => Scale(DefaultWidth, DefaultHeight, dpi);

    /// <summary>Tamaño mínimo para ese DPI.</summary>
    public static (int Width, int Height) Minimum(uint dpi) => Scale(MinimumWidth, MinimumHeight, dpi);
}
