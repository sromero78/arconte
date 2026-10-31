# Auditoría inicial v0.2.0

Base: e5fa6ca9d0adffb9f4b9961ab9285a4efdf636b7, main de sromero78/arconte. Se revisan todos los fuentes C#, XAML, configuración y pruebas recuperados. No se dispone del ejecutable Python antiguo para comparar. No se utilizan documentos personales.

## Inventario funcional

| Funcionalidad | Implementada y cobertura existente | Limitación / pendiente |
|---|---|---|
| Escaneo | Recursivo, 13 extensiones; exclusiones ocultos/sistema/temporales y subdirectorios reparse; DocumentScannerTests | Errores de acceso omitidos sin diagnóstico; enlaces a archivos no excluidos; síncrono |
| TXT | UTF-8, BOM UTF-16, fallback Windows-1252 | Lee archivo completo, sin cancelación durante lectura |
| PDF | PdfPig; OCR si menos de 40 caracteres | Corpus sintético, cifrados y corruptos sin verificar |
| DOCX/XLSX | OpenXML en lectura; shared strings XLSX | XLSX inline strings ignoradas; fechas sin formato; DOCX pierde estructura |
| DOC/XLS | DOC heurístico binario y XLS con NPOI | DOC no es parser completo; fidelidad sin verificar |
| ODT/ODS | content.xml de ZIP | Sin límites de expansión/tamaño; fidelidad pendiente |
| OCR imágenes/PDF | Tesseract español, Poppler para PDF, temporales únicos | Herramientas y spa.traineddata no incluidos; sin timeout propio; diez páginas PDF |
| IA local | Ollama loopback, qwen2.5:3b, hasta 12000 caracteres; OllamaAnalyzerTests | Modelo debe provisionarse aparte; sin alternativa automática; calidad/prompt injection requieren revisión humana |
| Propuestas | Fecha/código/materia/identificador, orden/separador y edición manual; NameBuilder/MainViewModelTests | Fecha ausente usa modificación del archivo sin explicar procedencia; analizar puede sobrescribir edición manual |
| Perfiles | Dos iniciales, validación/guardado, editor visual y vista JSON; ProfileServiceTests | Sin alta/baja de perfiles UI; regla_caja admite valores no implementados; cambio de perfil durante análisis inconsistente |
| Renombrar | Selección, extensión, validación, sufijos y sin sobrescritura; RenameServiceTests | Rutas de la vista quedan antiguas; volver a seleccionar carpeta tras operar |
| Deshacer | Orden inverso, conserva omitidos y no sobrescribe; RenameServiceTests | Sin hashes/identidad ni coordinación multiinstancia; no sustituye backup |
| CSV | Historial de sesión y escape | Tras deshacer sigue indicando Renombrado; fecha de exportación, no operación; contiene rutas privadas |
| WPF/legal/temas | Progreso, cancelar, claro/oscuro/sistema y aceptación local | Sin prueba visual/DPI/accesibilidad; lecturas síncronas bloquean UI |

## Correcciones y alcance

Se conserva WPF/MVVM, formatos, servicios y funcionalidad existente. Identidad completa Arconte y versión 0.2.0; aceptación legal en LOCALAPPDATA/Arconte, sin borrar marcador antiguo. Perfiles e historial siguen junto al ejecutable: extraer en carpeta del usuario escribible, nunca Program Files ni ejecutar dentro del ZIP.

Correcciones: fechas de calendario, nombres Windows independientes del host, sufijos después de 99, JSON IA no objeto, cliente Ollama propio sin proxy/redirecciones, cancelación de subprocesos OCR y límite PDF desde conversión. Confirmación UI antes de renombrar; selección de carpeta/editor bloqueados durante análisis. Registro único guardado tras cada movimiento; cancelación devuelve lote parcial. Si falla guardar tras mover, se detiene y muestra error. Persiste ventana entre movimiento y escritura: hace falta diario transaccional para tolerar caída eléctrica. Reversión tampoco detecta reemplazo del archivo en destino; usar copias de seguridad. No se introduce cambio grande de arquitectura.

## Dependencias, seguridad y portabilidad

Producción: PdfPig 0.1.16, DocumentFormat.OpenXml 3.0.2, NPOI 2.7.1; pruebas: Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2, runner 2.8.2, coverlet.collector 6.0.2. Versiones iniciales conservadas hasta disponer de evidencia del restore/advisories. CI consulta vulnerabilidades con transitivas y adjunta informe: éxito del comando no significa ausencia de vulnerabilidades. Revisar advisories antes de considerar estable.

Licencias declaradas por proyectos: PdfPig y NPOI Apache-2.0, OpenXML MIT. Pendiente inventario exhaustivo de licencias/notices transitivos antes de redistribución estable. No consta licencia del código propio. No se redistribuyen OCR ni modelos: revisar sus licencias/datos si se incorporan.

No se añade telemetría, nube ni actualización automática. El análisis solo conecta a Ollama loopback; uso manual disponible sin red. El HttpClient inyectable conserva configuración del llamador para pruebas. Restauración de paquetes y provisión inicial del modelo requieren red de desarrollo/provisión. Procesos usan ArgumentList, sin shell; PATH puede seleccionar ejecutables no confiables. Temporales se borran en finally, sin borrado seguro tras caída. Historial/CSV contienen nombres y rutas: tratarlos como privados.

WPF exige Windows. Publicación autocontenida win-x64, sin single-file ni trimming para preservar recursos/reflexión. Sin instalador, servicio, registro de máquina ni elevación. Requiere carpeta escribible. CI empaqueta únicamente salida limpia de publish y documentación, sin documentos ni logs privados. .NET 8 llega al final de soporte en noviembre de 2026: planificar actualización separada.

## Evidencia y aceptación

Se intentaron dotnet build Arconte.sln y dotnet test Arconte.sln en Linux: ambos fallan con dotnet: command not found. La descarga de SDK falla por conexión al proxy. Esto no constituye compilación ni pruebas superadas. CI Windows ejecuta restore, build Release, pruebas con TRX, consulta de vulnerabilidades y publish. El ZIP/SHA-256 solo se publica tras superar pruebas. El resultado real de Actions y el TRX determinan qué ha sido verificado en ejecución; inspección estática no equivale a prueba funcional.

Pendiente manual con documentos exclusivamente sintéticos, en Windows 10/11 x64 con cuenta estándar y sin runtime: primer arranque/legal, temas/DPI, perfiles, escaneo, edición y confirmación, contenido intacto, colisiones/deshacer con destino ocupado, ausencia de Ollama/OCR, análisis de cada formato con modelo local, cancelación, diez páginas OCR, CSV, Unicode y rutas largas. No declarar estable hasta completar la matriz. No se verifica equivalencia con la versión Python.
