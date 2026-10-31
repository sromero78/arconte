# Cambios en 0.2.0 (candidata de pruebas)

- Renombrados solución, proyectos, namespaces, ensamblado e interfaz a Arconte.
- Publicación autocontenida win-x64 sin instalador, recorte ni dependencia del runtime instalado.
- CI Windows para compilación, pruebas, auditoría NuGet y ZIP con SHA-256.
- Confirmación explícita antes de renombrar; bloqueo de selección de carpeta y editor de perfiles durante análisis.
- Fechas de calendario válidas, nombres Windows independientes del sistema de compilación y sufijos superiores a 99.
- Ollama sin redirecciones ni proxy para mantener el procesamiento en loopback.
- Respuestas JSON no objeto tratadas como propuestas vacías.
- OCR PDF limitado a diez páginas desde la conversión; cancelación termina los subprocesos.
- Historial con identificador único, guardado tras cada movimiento y conservación de operaciones previas al cancelar.
- CSV protege celdas que empiezan por caracteres de fórmula.

Limitaciones, alcance y evidencia: docs/AUDITORIA-v0.2.0.md.

- Prueba de propiedad ausente en perfiles corregida para no depender de saltos de línea CRLF/LF.
- SDK 8 seleccionado explícitamente y acciones fijadas por SHA.

- Parches transitivos ImageSharp 2.1.13, Asn1 8.0.1 y Packaging 8.0.1; persisten avisos ImageSharp documentados como bloqueo para estable.
- Extracción XLS sintética con comprobación de bytes originales.
