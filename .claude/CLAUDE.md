# OfiConvert — instrucciones del proyecto

## Lo primero, antes de tocar nada

**Lee [`CONTEXT.md`](../CONTEXT.md).** No es documentación de cortesía: es el registro de las trampas ya
pagadas por este proyecto y por sus dos hermanos ([FormatDiskPro](https://github.com/xfiberex/FormatDiskPro)
y [WingetUSoft](https://github.com/xfiberex/WingetUSoft)). Casi todo lo que parece un error absurdo aquí
ya le costó horas a alguien, y está explicado allí.

Reparto de los documentos vivos:

- **`CONTEXT.md`** — qué hay hecho, cómo y **por qué**. Arquitectura, decisiones, registro de cambios.
- **`ROADMAP.md`** — qué falta, por tiers.

**Mantenerlos es parte del trabajo, no un extra.** Tras un cambio relevante: entrada nueva en el registro
de cambios (con fecha absoluta), actualizar §3 *Estado actual*, y commitearlos **junto** al código.

## Invariantes que no se rompen

Están todos razonados en `CONTEXT.md` §4. En corto:

1. **El updater no ejecuta nada que no haya verificado** (Authenticode → SHA-256 → borrar y abortar). Todo
   release debe subir su `.sha256`, o los clientes rechazan la actualización. La descarga vive en su
   propio método a propósito: su `FileStream` debe cerrarse **antes** de verificar.
2. **El idioma es estado ESTÁTICO en `LocalizationService`.** Hay dos instancias vivas (el singleton y la
   que construye el XAML) y no se puede evitar. Con estado de instancia, la UI se queda en español en los
   ocho idiomas. Ya pasó.
3. **`[ObservableProperty]` va sobre propiedades parciales, nunca sobre campos**, y
   `CommunityToolkit.Mvvm` no baja de 8.4.2.
4. **La versión tiene una única fuente (`.csproj`) y sube en las TRES etiquetas** (`Version`,
   `AssemblyVersion`, `FileVersion`). El updater compara contra `AssemblyVersion`.
5. **`Core/` es lógica pura**: sin UI, sin `Process`, sin `HttpClient`, sin COM. Lo que no cumpla eso, no
   entra ahí.
6. **Los textos legales van embebidos en el `.exe`** (`Core/LegalText`). Las licencias de Serilog
   (Apache-2.0), WebView2 (BSD-3) y el Windows App SDK (términos de Microsoft) **obligan** a mostrar su
   atribución. No todo es MIT: verificar contra el `.nuspec`, nunca de memoria.

## Cómo se trabaja aquí

- **Compilar:** `dotnet build OfiConvert.slnx -c Release` — se mantiene en **0 errores / 0 advertencias**.
- **Probar:** `dotnet test` sobre los dos proyectos de `tests\`. Los de UI **arrancan la app real**; no
  necesitan Office ni elevación.
- **Publicar:** `.\release.ps1 -Version X.Y.Z` (con `-DryRun` primero). Corre las pruebas y aborta si
  fallan. Solo hace `git add -u`: **los archivos nuevos hay que `git add`earlos antes**.
- **Un test que nunca ha fallado no prueba nada.** Al añadir uno, comprobar que se pone rojo si se rompe
  lo que dice cubrir.
- **No hay CI** (decisión cerrada, ver `ROADMAP.md`): los UI tests necesitan un escritorio interactivo.

<!-- CODEGRAPH_START -->
## CodeGraph

This project has a CodeGraph MCP server (`codegraph_*` tools) configured. CodeGraph is a tree-sitter-parsed knowledge graph of every symbol, edge, and file. Reads are sub-millisecond and return structural information grep cannot.

### When to prefer codegraph over native search

Use codegraph for **structural** questions — what calls what, what would break, where is X defined, what is X's signature. Use native grep/read only for **literal text** queries (string contents, comments, log messages) or after you already have a specific file open.

| Question | Tool |
|---|---|
| "Where is X defined?" / "Find symbol named X" | `codegraph_search` |
| "What calls function Y?" | `codegraph_callers` |
| "What does Y call?" | `codegraph_callees` |
| "How does X reach/become Y? / trace the flow from X to Y" | `codegraph_trace` (one call = the whole path, incl. callback/React/JSX dynamic hops) |
| "What would break if I changed Z?" | `codegraph_impact` |
| "Show me Y's signature / source / docstring" | `codegraph_node` |
| "Give me focused context for a task/area" | `codegraph_context` |
| "See several related symbols' source at once" | `codegraph_explore` |
| "What files exist under path/" | `codegraph_files` |
| "Is the index healthy?" | `codegraph_status` |

### Rules of thumb

- **Answer directly — don't delegate exploration.** For "how does X work" / architecture questions, answer with 2-3 codegraph calls: `codegraph_context` first, then ONE `codegraph_explore` for the source of the symbols it surfaces. For a specific **flow** ("how does X reach Y") start with `codegraph_trace` from→to — one call returns the whole path with dynamic hops bridged — then ONE `codegraph_explore` for the bodies; don't rebuild the path with `codegraph_search` + `codegraph_callers`. Codegraph IS the pre-built index, so spawning a separate file-reading sub-task/agent — or running a grep + read loop — repeats work codegraph already did and costs more for the same answer.
- **Trust codegraph results.** They come from a full AST parse. Do NOT re-verify them with grep — that's slower, less accurate, and wastes context.
- **Don't grep first** when looking up a symbol by name. `codegraph_search` is faster and returns kind + location + signature in one call.
- **Don't chain `codegraph_search` + `codegraph_node`** when you just want context — `codegraph_context` is one call.
- **Don't loop `codegraph_node` over many symbols** — one `codegraph_explore` call returns several symbols' source grouped in a single capped call, while each separate node/Read call re-reads the whole context and costs far more.
- **Index lag**: the file watcher debounces ~500ms behind writes; don't re-query immediately after editing a file in the same turn.

### If `.codegraph/` doesn't exist

The MCP server returns "not initialized." Ask the user: *"I notice this project doesn't have CodeGraph initialized. Want me to run `codegraph init -i` to build the index?"*
<!-- CODEGRAPH_END -->
