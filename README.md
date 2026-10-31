# Arconte

Gestor documental para Windows, basado inicialmente en el proyecto `RenombradorDocumental` (C# / .NET 8 / WPF).

## Estado

Importación inicial del código fuente recuperado del equipo local (septiembre de 2026). **Versión candidata `v0.2.0-dev`**, todavía sin validar ni publicar como versión estable.

## Objetivo

Aplicación portátil para Windows 10/11, sin instalación ni privilegios de administrador, con revisión previa de operaciones de renombrado, extracción documental y perfiles configurables.

## Seguridad

No procesar documentos de terceros sin autorización. No subir documentos reales, credenciales, registros privados ni resultados de procesamiento. Los directorios de compilación y ejecutables quedan excluidos del control de versiones.

## Desarrollo

Abrir `RenombradorDocumental.sln` con Visual Studio o .NET SDK 8. Codex deberá verificar compilación, pruebas y dependencias antes de rebautizar ensamblados y generar la versión portátil.
