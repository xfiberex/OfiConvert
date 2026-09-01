using System.Diagnostics;
using System.IO.Compression;
using OfiConvert.Models;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El criterio de aceptación de <b>TJ-25</b>, ejecutado contra LibreOffice de verdad: <b>ocho documentos
/// con paralelismo 4</b>.
/// </summary>
/// <remarks>
/// <c>LibreOfficeCommandTests</c> comprueba la <b>forma</b> del comando sin LibreOffice instalado, y eso
/// es lo que se puede correr en cualquier máquina. Pero la forma correcta de un argumento no demuestra
/// que el lote converja: durante el Tier J la ficha se cerró con esa cobertura y el criterio de punta a
/// punta quedó <b>anotado como pendiente</b>, porque en la máquina donde se escribió no había LibreOffice.
/// Esta clase es esa deuda, saldada.
///
/// <b>Lo que se midió al escribirla</b> (LibreOffice 26.8.0.3, ocho <c>.docx</c>, paralelismo 4):
/// <list type="bullet">
///   <item><b>Perfil compartido: 4 de 8.</b> Cuatro procesos terminan con código 1 y —esto es lo
///   importante— <b>stdout y stderr VACÍOS</b>. La ficha decía «errores que no parecen de conversión»;
///   la realidad es peor, no hay ningún error que leer. Cuatro repeticiones, 4/8 en todas: no es
///   intermitente, es sistemático.</item>
///   <item><b>Perfil propio: 8 de 8</b>, código 0 en todos.</item>
/// </list>
///
/// 🔴 <b>Y la configuración ROTA parecía la rápida:</b> 12,8 s frente a 25,9 s. Precisamente porque la
/// mitad de los documentos moría al instante en vez de convertirse. Medir un lote por su duración habría
/// premiado el fallo.
///
/// ⚠️ <b>Nunca detectes LibreOffice con <c>soffice --version</c>:</b> en Windows abre una consola y espera
/// un «Press Enter to continue…». Capturado desde un script devuelve cadena vacía y deja la ventana
/// abierta esperando a un humano.
/// </remarks>
public sealed class LibreOfficeEndToEndTests : IDisposable
{
    /// <summary>El criterio de TJ-25, literal: ocho documentos, cuatro a la vez.</summary>
    private const int Documentos = 8;
    private const int Paralelismo = 4;

    /// <summary>
    /// Ruta CORTA a propósito. Un `.pdf` bajo un árbol profundo choca con MAX_PATH y el fallo no se
    /// parece a un fallo de conversión — la misma trampa que dio 177 rojos al medir la línea base de la
    /// v2.7.0 en un `git worktree`.
    /// </summary>
    private readonly string _raiz;

    public LibreOfficeEndToEndTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "ofc-lo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_raiz);
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch { /* limpieza best-effort */ }
    }

    [LibreOfficeFact]
    public async Task LoteDeOcho_ConParalelismo4_ConvierteLosOcho()
    {
        var servicio = new LibreOfficeConversionService();
        Assert.True(servicio.IsOfficeInstalled(),
            "LibreOffice no está instalado, pero la prueba se activó con OFICONVERT_LIBREOFFICE_TESTS=1.");

        var origenes = CrearDocumentos(Documentos);
        var destino = Path.Combine(_raiz, "salida");
        Directory.CreateDirectory(destino);

        // El mismo mecanismo que MainViewModel: un semáforo con MaxParallelConversions y Task.WhenAll.
        using var puerta = new SemaphoreSlim(Paralelismo, Paralelismo);
        var opciones = new ConversionOptions { OutputFormat = OutputFormat.PDF };

        var tareas = origenes.Select(async origen =>
        {
            await puerta.WaitAsync();
            try
            {
                var salida = Path.Combine(destino, Path.GetFileNameWithoutExtension(origen) + ".pdf");
                return (origen, resultado: await servicio.ConvertAsync(origen, salida, opciones));
            }
            finally { puerta.Release(); }
        });

        var resultados = await Task.WhenAll(tareas);

        var fallidos = resultados.Where(r => !r.resultado.Success).ToList();
        Assert.True(fallidos.Count == 0,
            "Con perfil propio por proceso los ocho tienen que convertirse. Fallaron "
                + $"{fallidos.Count}:\n  "
                + string.Join("\n  ", fallidos.Select(f => $"{Path.GetFileName(f.origen)} -> {f.resultado.Error?.Key}")));

        // Ocho archivos DISTINTOS y con contenido: que devuelva Success no basta si el PDF no está.
        var pdfs = resultados.Select(r => r.resultado.OutputPath).ToList();
        Assert.Equal(Documentos, pdfs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(pdfs, p =>
        {
            Assert.True(File.Exists(p), $"El servicio dijo que convirtió, pero no hay archivo: {p}");
            Assert.True(new FileInfo(p).Length > 0, $"PDF vacío: {p}");
        });
    }

    /// <summary>
    /// LA PREMISA, que es lo que justifica el arreglo: compartir perfil PIERDE documentos.
    /// </summary>
    /// <remarks>
    /// Esta prueba no ejercita la app — llama a <c>soffice</c> a mano, con un único perfil, igual que
    /// hacía OfiConvert antes de TJ-25. Está aquí porque el arreglo (un perfil por proceso) solo tiene
    /// sentido mientras esta premisa se cumpla: <b>si algún día LibreOffice admite perfiles compartidos
    /// y esta prueba se pone roja, lo que hay que revisar es si TJ-25 sigue haciendo falta</b>, no
    /// «arreglar» la prueba. Por eso el mensaje de fallo lo dice.
    /// </remarks>
    [LibreOfficeFact]
    public async Task Premisa_CompartirPerfil_PIERDE_Documentos()
    {
        var soffice = RutaDeSoffice();
        Assert.NotNull(soffice);

        var origenes = CrearDocumentos(Documentos);
        var perfilCompartido = Path.Combine(_raiz, "perfil-compartido");
        Directory.CreateDirectory(perfilCompartido);

        using var puerta = new SemaphoreSlim(Paralelismo, Paralelismo);

        var tareas = origenes.Select(async (origen, i) =>
        {
            await puerta.WaitAsync();
            try
            {
                var salida = Path.Combine(_raiz, "compartido-" + i);
                Directory.CreateDirectory(salida);

                var psi = new ProcessStartInfo
                {
                    FileName = soffice,
                    Arguments = OfiConvert.Core.LibreOfficeCommand.BuildArguments(
                        "pdf", salida, perfilCompartido, origen)
                };
                var run = await ProcessRunner.RunAsync(psi, CancellationToken.None);
                return Directory.GetFiles(salida, "*.pdf").Length > 0 && run.ExitCode == 0;
            }
            finally { puerta.Release(); }
        });

        var convertidos = (await Task.WhenAll(tareas)).Count(ok => ok);

        Assert.True(convertidos < Documentos,
            $"Con UN perfil compartido se convirtieron los {Documentos} documentos. Al escribir esta "
                + "prueba se perdían 4 de 8 en cuatro repeticiones seguidas (LibreOffice 26.8.0.3), y esa "
                + "pérdida es lo único que justifica dar un perfil por proceso (TJ-25). Si LibreOffice ha "
                + "cambiado, revisa si el arreglo sigue haciendo falta ANTES de tocar esta prueba.");
    }

    /// <summary>Ocho <c>.docx</c> mínimos pero de verdad: LibreOffice tiene que poder abrirlos.</summary>
    private string[] CrearDocumentos(int cuantos)
    {
        const string contentTypes =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""";
        const string rels =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""";

        var carpeta = Path.Combine(_raiz, "origen");
        Directory.CreateDirectory(carpeta);
        var rutas = new string[cuantos];

        for (int i = 0; i < cuantos; i++)
        {
            var parrafos = string.Concat(Enumerable.Range(1, 40).Select(j =>
                $"<w:p><w:r><w:t>Documento {i + 1}, parrafo {j}.</w:t></w:r></w:p>"));
            var documento =
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>"""
                + parrafos + "<w:sectPr/></w:body></w:document>";

            rutas[i] = Path.Combine(carpeta, $"informe-{i + 1:00}.docx");
            using var zip = ZipFile.Open(rutas[i], ZipArchiveMode.Create);
            Escribir(zip, "[Content_Types].xml", contentTypes);
            Escribir(zip, "_rels/.rels", rels);
            Escribir(zip, "word/document.xml", documento);
        }
        return rutas;

        static void Escribir(ZipArchive zip, string nombre, string contenido)
        {
            using var flujo = zip.CreateEntry(nombre).Open();
            using var escritor = new StreamWriter(flujo);
            escritor.Write(contenido);
        }
    }

    /// <summary>Por la ruta, NO por <c>--version</c> (ver el remark de la clase).</summary>
    private static string? RutaDeSoffice() => new[]
    {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    }.FirstOrDefault(File.Exists);
}
