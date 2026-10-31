# Plan inicial de migración a Arconte

1. Auditar el código C# recuperado y compararlo funcionalmente con el ejecutable Python de agosto de 2026.
2. Verificar que compila y que las pruebas pasan sin modificar los originales.
3. Renombrar namespaces, ensamblado, solución e interfaz a Arconte.
4. Preparar compilación Windows x64 autocontenida y portátil, con dependencias locales y sin permisos de administrador.
5. Verificar que no modifica documentos originales sin confirmación; añadir vista previa, copias de seguridad y reversión segura donde falte.
6. Crear GitHub Actions para pruebas y entrega de ZIP portátil en Releases, sin incluir datos personales.

El número `v0.2.0` es provisional; la primera etiqueta se decidirá tras auditar el estado del código.
