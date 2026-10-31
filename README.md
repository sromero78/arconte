# Arconte v0.2.0 — candidata de pruebas

Gestor documental local para Windows 10/11 x64, recuperado de RenombradorDocumental. Explora documentos, genera propuestas con IA local, permite editarlas, renombrar, deshacer y exportar historial. Sin telemetría ni servicios en la nube.

## Descargar y ejecutar

GitHub → Actions → Windows portable → ejecución correcta de esta rama/PR → artifact Arconte-v0.2.0-win-x64. Descomprimir el artifact y el ZIP portátil incluido en una carpeta del usuario escribible. Ejecutar Arconte.exe conservando todos los archivos contiguos. No requiere instalar .NET ni permisos de administrador. La compilación no está firmada.

Revisión y renombrado manual disponibles sin IA. Propuestas automáticas: Ollama local con qwen2.5:3b previamente disponible en http://127.0.0.1:11434. OCR opcional: Tesseract español en PATH o ruta detectada; PDF escaneado necesita además Poppler/pdftoppm. Herramientas/modelos no incluidos. Los formatos de extracción directa usan bibliotecas incluidas.

Revisar propuestas y confirmar antes de aplicar. Guardar copia de seguridad de documentos importantes: deshacer no sustituye backup. Conservar logs junto al ejecutable. Tras aplicar/deshacer, volver a seleccionar carpeta para refrescar rutas. No compartir documentos, perfiles personalizados, logs ni CSV privados.

## Desarrollo

SDK .NET 8 en Windows:

```powershell
dotnet restore Arconte.sln
dotnet build Arconte.sln -c Release --no-restore
dotnet test Arconte.sln -c Release --no-build
./scripts/Publish-Portable.ps1
```

Pruebas con datos sintéticos temporales. CI adjunta TRX y auditoría NuGet. Véanse [auditoría y pendientes](docs/AUDITORIA-v0.2.0.md) y [cambios](CHANGELOG.md). El ZIP no certifica funcionamiento en ambos sistemas: faltan las pruebas manuales indicadas. Sin release estable ni fusión automática en main.
