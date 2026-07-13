// WinUI 3 does not use ThemeInfo assembly attribute.
// Assembly attributes are defined in the .csproj (Product, AssemblyTitle, etc.).

// GitHubUpdateService y sus ayudantes son `internal`: no son API pública de la app, pero SÍ tienen que
// ser comprobables. Es el código que decide si se ejecuta o no un instalador descargado de internet.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OfiConvert.Tests")]
